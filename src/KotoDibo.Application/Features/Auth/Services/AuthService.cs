using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FluentValidation;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Auth.DTOs;
using KotoDibo.Application.Features.Auth.Interfaces;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;

namespace KotoDibo.Application.Features.Auth.Services;

public class AuthService : IAuthService
{
    private const int MaxFailedLoginAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    // Cached (via the IPasswordHasher abstraction, computed once) hash with no corresponding real
    // password. Verifying against this when a user/credential isn't found keeps login response
    // timing consistent, so the endpoint can't be used to enumerate which emails are registered.
    private static string? _dummyPasswordHash;
    private static readonly object DummyPasswordHashLock = new();

    private readonly IRepository<User> _userRepository;
    private readonly IRepository<UserCredential> _credentialRepository;
    private readonly IRepository<RefreshToken> _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IRefreshTokenSettings _refreshTokenSettings;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<RefreshTokenRequest> _refreshTokenValidator;
    private readonly IValidator<LogoutRequest> _logoutValidator;

    public AuthService(
        IRepository<User> userRepository,
        IRepository<UserCredential> credentialRepository,
        IRepository<RefreshToken> refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IDateTimeProvider dateTimeProvider,
        IRefreshTokenSettings refreshTokenSettings,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<RefreshTokenRequest> refreshTokenValidator,
        IValidator<LogoutRequest> logoutValidator)
    {
        _userRepository = userRepository;
        _credentialRepository = credentialRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _dateTimeProvider = dateTimeProvider;
        _refreshTokenSettings = refreshTokenSettings;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _refreshTokenValidator = refreshTokenValidator;
        _logoutValidator = logoutValidator;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        await _registerValidator.ValidateAndThrowAsync(request, cancellationToken);

        var normalizedEmail = NormalizeEmail(request.Email);

        var existingUser = await _userRepository.FindOneAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
        if (existingUser is not null)
        {
            throw DuplicateEmailException();
        }

        var now = _dateTimeProvider.UtcNow;
        var user = new User
        {
            Name = SanitizeName(request.Name),
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            Status = AccountStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };

        try
        {
            await _userRepository.AddAsync(user, cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            // Closes the race between the pre-check above and this insert under concurrent signups.
            throw DuplicateEmailException();
        }

        var credential = new UserCredential
        {
            UserId = user.Id,
            Provider = AuthProvider.Password,
            PasswordHash = _passwordHasher.Hash(request.Password),
            PasswordChangedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        try
        {
            await _credentialRepository.AddAsync(credential, cancellationToken);
        }
        catch
        {
            // Compensating action: don't leave a user with no way to log in. No cross-collection
            // Mongo transaction here — see the Phase 1 design notes for why.
            await _userRepository.DeleteAsync(user.Id, CancellationToken.None);
            throw;
        }

        return await IssueTokensAsync(user, request.DeviceId, request.DeviceName, ipAddress, userAgent, Guid.NewGuid().ToString("N"), tokenToRevoke: null, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        await _loginValidator.ValidateAndThrowAsync(request, cancellationToken);

        var normalizedEmail = NormalizeEmail(request.Email);
        var now = _dateTimeProvider.UtcNow;

        var user = await _userRepository.FindOneAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
        var credential = user is null
            ? null
            : await _credentialRepository.FindOneAsync(c => c.UserId == user.Id && c.Provider == AuthProvider.Password, cancellationToken);

        // Always run Verify, even against a dummy hash when no user/credential was found, so the
        // response time doesn't leak whether the email is registered.
        var passwordHash = credential?.PasswordHash ?? GetDummyPasswordHash();
        var isPasswordValid = _passwordHasher.Verify(request.Password, passwordHash);
        var isLocked = credential?.LockedUntil is { } lockedUntil && lockedUntil > now;

        if (user is null || credential is null || isLocked || !isPasswordValid)
        {
            if (credential is not null && !isLocked && !isPasswordValid)
            {
                await RecordFailedLoginAsync(credential, now, cancellationToken);
            }

            throw new UnauthorizedException("Invalid email or password.");
        }

        if (credential.FailedLoginAttempts > 0 || credential.LockedUntil is not null)
        {
            credential.FailedLoginAttempts = 0;
            credential.LockedUntil = null;
            credential.UpdatedAt = now;
            await _credentialRepository.UpdateAsync(credential, cancellationToken);
        }

        // Only disclosed after the password has already been verified: at this point the caller
        // has proven ownership of the credentials, so there's no enumeration risk left to protect.
        if (user.Status != AccountStatus.Active)
        {
            throw new ForbiddenException(DescribeInactiveStatus(user.Status));
        }

        user.LastLoginAt = now;
        await _userRepository.UpdateAsync(user, cancellationToken);

        return await IssueTokensAsync(user, request.DeviceId, request.DeviceName, ipAddress, userAgent, Guid.NewGuid().ToString("N"), tokenToRevoke: null, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        await _refreshTokenValidator.ValidateAndThrowAsync(request, cancellationToken);

        var tokenHash = HashToken(request.RefreshToken);
        var existingToken = await _refreshTokenRepository.FindOneAsync(t => t.TokenHash == tokenHash, cancellationToken);
        var now = _dateTimeProvider.UtcNow;

        if (existingToken is null)
        {
            throw new UnauthorizedException("Invalid refresh token.");
        }

        if (existingToken.RevokedAt is not null)
        {
            // A revoked token being presented again means it was copied/stolen: assume the whole
            // rotation chain is compromised and kill every session descended from it.
            await RevokeFamilyAsync(existingToken.FamilyId, now, cancellationToken);
            throw new UnauthorizedException("Invalid refresh token.");
        }

        if (existingToken.ExpiresAt <= now)
        {
            throw new UnauthorizedException("Refresh token expired.");
        }

        var user = await _userRepository.GetByIdAsync(existingToken.UserId, cancellationToken);
        if (user is null || user.Status != AccountStatus.Active)
        {
            existingToken.RevokedAt = now;
            await _refreshTokenRepository.UpdateAsync(existingToken, cancellationToken);
            throw new UnauthorizedException("Invalid refresh token.");
        }

        return await IssueTokensAsync(
            user,
            existingToken.DeviceId,
            existingToken.DeviceName,
            ipAddress ?? existingToken.CreatedByIp,
            userAgent ?? existingToken.UserAgent,
            existingToken.FamilyId,
            tokenToRevoke: existingToken,
            cancellationToken);
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default)
    {
        await _logoutValidator.ValidateAndThrowAsync(request, cancellationToken);

        var tokenHash = HashToken(request.RefreshToken);
        var token = await _refreshTokenRepository.FindOneAsync(t => t.TokenHash == tokenHash, cancellationToken);

        // Idempotent: an unknown or already-revoked token is treated as already logged out.
        if (token is not null && token.RevokedAt is null)
        {
            token.RevokedAt = _dateTimeProvider.UtcNow;
            await _refreshTokenRepository.UpdateAsync(token, cancellationToken);
        }
    }

    public async Task LogoutAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.UtcNow;
        var tokens = await _refreshTokenRepository.FindAsync(t => t.UserId == userId && t.RevokedAt == null, cancellationToken);

        foreach (var token in tokens)
        {
            token.RevokedAt = now;
            await _refreshTokenRepository.UpdateAsync(token, cancellationToken);
        }
    }

    private async Task<AuthResponse> IssueTokensAsync(
        User user,
        string? deviceId,
        string? deviceName,
        string? ipAddress,
        string? userAgent,
        string familyId,
        RefreshToken? tokenToRevoke,
        CancellationToken cancellationToken)
    {
        var (accessToken, expiresAt) = _jwtTokenGenerator.GenerateToken(user);
        var rawRefreshToken = GenerateRawToken();
        var tokenHash = HashToken(rawRefreshToken);
        var now = _dateTimeProvider.UtcNow;

        if (tokenToRevoke is not null)
        {
            tokenToRevoke.RevokedAt = now;
            tokenToRevoke.ReplacedByTokenHash = tokenHash;
            await _refreshTokenRepository.UpdateAsync(tokenToRevoke, cancellationToken);
        }

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            FamilyId = familyId,
            DeviceId = deviceId,
            DeviceName = deviceName,
            UserAgent = userAgent,
            CreatedByIp = ipAddress,
            CreatedAt = now,
            ExpiresAt = now.Add(_refreshTokenSettings.RefreshTokenLifetime),
        };

        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);

        return new AuthResponse
        {
            AccessToken = accessToken,
            ExpiresAt = expiresAt,
            RefreshToken = rawRefreshToken,
            UserId = user.Id,
            Name = user.Name,
            Email = user.Email,
        };
    }

    private async Task RevokeFamilyAsync(string familyId, DateTime now, CancellationToken cancellationToken)
    {
        var tokens = await _refreshTokenRepository.FindAsync(t => t.FamilyId == familyId && t.RevokedAt == null, cancellationToken);
        foreach (var token in tokens)
        {
            token.RevokedAt = now;
            await _refreshTokenRepository.UpdateAsync(token, cancellationToken);
        }
    }

    private async Task RecordFailedLoginAsync(UserCredential credential, DateTime now, CancellationToken cancellationToken)
    {
        credential.FailedLoginAttempts++;
        if (credential.FailedLoginAttempts >= MaxFailedLoginAttempts)
        {
            credential.LockedUntil = now.Add(LockoutDuration);
        }

        credential.UpdatedAt = now;
        await _credentialRepository.UpdateAsync(credential, cancellationToken);
    }

    private string GetDummyPasswordHash()
    {
        if (_dummyPasswordHash is not null)
        {
            return _dummyPasswordHash;
        }

        lock (DummyPasswordHashLock)
        {
            _dummyPasswordHash ??= _passwordHasher.Hash(Guid.NewGuid().ToString("N"));
            return _dummyPasswordHash;
        }
    }

    private static KotoDibo.Application.Common.Exceptions.ValidationException DuplicateEmailException() => new(new Dictionary<string, string[]>
    {
        [nameof(RegisterRequest.Email)] = ["An account with this email already exists."],
    });

    private static string DescribeInactiveStatus(AccountStatus status) => status switch
    {
        AccountStatus.Suspended => "This account has been suspended.",
        AccountStatus.Deactivated => "This account has been deactivated.",
        AccountStatus.DeletionRequested => "This account is scheduled for deletion.",
        AccountStatus.Deleted => "This account no longer exists.",
        AccountStatus.PendingVerification => "Please verify your account before logging in.",
        _ => "This account cannot log in right now.",
    };

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string SanitizeName(string name) => Regex.Replace(name.Trim(), @"\s+", " ");

    private static string GenerateRawToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static string HashToken(string rawToken) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
