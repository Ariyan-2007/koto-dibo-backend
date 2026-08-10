using System.Linq.Expressions;
using FluentAssertions;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Auth.DTOs;
using KotoDibo.Application.Features.Auth.Services;
using KotoDibo.Application.Features.Auth.Validators;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using Moq;

namespace KotoDibo.UnitTests.Features.Auth;

public class AuthServiceTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IRepository<User>> _users = new();
    private readonly Mock<IRepository<UserCredential>> _credentials = new();
    private readonly Mock<IRepository<RefreshToken>> _refreshTokens = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGenerator = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();
    private readonly Mock<IRefreshTokenSettings> _refreshTokenSettings = new();

    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _dateTimeProvider.Setup(x => x.UtcNow).Returns(Now);
        _refreshTokenSettings.Setup(x => x.RefreshTokenLifetime).Returns(TimeSpan.FromDays(30));
        _jwtTokenGenerator.Setup(x => x.GenerateToken(It.IsAny<User>())).Returns(("access-token", Now.AddMinutes(15)));
        _passwordHasher.Setup(x => x.Hash(It.IsAny<string>())).Returns("hashed");

        _users.Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => u.Id = "user-1")
            .ReturnsAsync((User u, CancellationToken _) => u);
        _credentials.Setup(x => x.AddAsync(It.IsAny<UserCredential>(), It.IsAny<CancellationToken>()))
            .Callback<UserCredential, CancellationToken>((c, _) => c.Id = "cred-1")
            .ReturnsAsync((UserCredential c, CancellationToken _) => c);
        _refreshTokens.Setup(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshToken, CancellationToken>((t, _) => t.Id = "token-1")
            .ReturnsAsync((RefreshToken t, CancellationToken _) => t);

        _sut = new AuthService(
            _users.Object,
            _credentials.Object,
            _refreshTokens.Object,
            _passwordHasher.Object,
            _jwtTokenGenerator.Object,
            _dateTimeProvider.Object,
            _refreshTokenSettings.Object,
            new RegisterRequestValidator(),
            new LoginRequestValidator(),
            new RefreshTokenRequestValidator(),
            new LogoutRequestValidator());
    }

    private static User ActiveUser(string id = "user-1") => new()
    {
        Id = id,
        Email = "jane@example.com",
        NormalizedEmail = "jane@example.com",
        Name = "Jane Doe",
        Status = AccountStatus.Active,
        CreatedAt = Now,
        UpdatedAt = Now,
    };

    private static UserCredential PasswordCredential(string userId = "user-1", string hash = "hashed", int failedAttempts = 0, DateTime? lockedUntil = null) => new()
    {
        Id = "cred-1",
        UserId = userId,
        Provider = AuthProvider.Password,
        PasswordHash = hash,
        FailedLoginAttempts = failedAttempts,
        LockedUntil = lockedUntil,
        CreatedAt = Now,
        UpdatedAt = Now,
    };

    [Fact]
    public async Task RegisterAsync_NewEmail_CreatesUserAndCredentialAndReturnsTokens()
    {
        _users.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var request = new RegisterRequest { Name = "Jane Doe", Email = "Jane@Example.com", Password = "GoodPass123" };

        var result = await _sut.RegisterAsync(request, "127.0.0.1", "test-agent");

        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.Email.Should().Be("Jane@Example.com");

        _users.Verify(x => x.AddAsync(It.Is<User>(u => u.NormalizedEmail == "jane@example.com"), It.IsAny<CancellationToken>()), Times.Once);
        _credentials.Verify(x => x.AddAsync(It.Is<UserCredential>(c => c.Provider == AuthProvider.Password && c.PasswordHash == "hashed"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsValidationException()
    {
        _users.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveUser());

        var request = new RegisterRequest { Name = "Jane Doe", Email = "jane@example.com", Password = "GoodPass123" };

        var act = () => _sut.RegisterAsync(request, null, null);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainKey(nameof(RegisterRequest.Email));
        _users.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_CorrectPassword_ReturnsTokensAndUpdatesLastLogin()
    {
        var user = ActiveUser();
        var credential = PasswordCredential();

        _users.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _credentials.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<UserCredential, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(credential);
        _passwordHasher.Setup(x => x.Verify("correct-password", "hashed")).Returns(true);

        var request = new LoginRequest { Email = "jane@example.com", Password = "correct-password" };

        var result = await _sut.LoginAsync(request, "127.0.0.1", "test-agent");

        result.AccessToken.Should().Be("access-token");
        _users.Verify(x => x.UpdateAsync(It.Is<User>(u => u.LastLoginAt == Now), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsUnauthorizedAndRecordsFailedAttempt()
    {
        var user = ActiveUser();
        var credential = PasswordCredential();

        _users.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _credentials.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<UserCredential, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(credential);
        _passwordHasher.Setup(x => x.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var request = new LoginRequest { Email = "jane@example.com", Password = "wrong-password" };

        var act = () => _sut.LoginAsync(request, null, null);

        await act.Should().ThrowAsync<UnauthorizedException>();
        _credentials.Verify(x => x.UpdateAsync(It.Is<UserCredential>(c => c.FailedLoginAttempts == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ThrowsUnauthorizedWithoutTouchingAnyCredential()
    {
        _users.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        _passwordHasher.Setup(x => x.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var request = new LoginRequest { Email = "nobody@example.com", Password = "whatever123" };

        var act = () => _sut.LoginAsync(request, null, null);

        await act.Should().ThrowAsync<UnauthorizedException>();
        _credentials.Verify(x => x.UpdateAsync(It.IsAny<UserCredential>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_FifthFailedAttempt_LocksAccount()
    {
        var user = ActiveUser();
        var credential = PasswordCredential(failedAttempts: 4);

        _users.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _credentials.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<UserCredential, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(credential);
        _passwordHasher.Setup(x => x.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var request = new LoginRequest { Email = "jane@example.com", Password = "wrong-password" };

        await FluentActions.Awaiting(() => _sut.LoginAsync(request, null, null)).Should().ThrowAsync<UnauthorizedException>();

        _credentials.Verify(x => x.UpdateAsync(
            It.Is<UserCredential>(c => c.FailedLoginAttempts == 5 && c.LockedUntil == Now.Add(TimeSpan.FromMinutes(15))),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_LockedAccount_ThrowsGenericUnauthorizedEvenWithCorrectPassword()
    {
        var user = ActiveUser();
        var credential = PasswordCredential(failedAttempts: 5, lockedUntil: Now.AddMinutes(10));

        _users.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _credentials.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<UserCredential, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(credential);
        _passwordHasher.Setup(x => x.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var request = new LoginRequest { Email = "jane@example.com", Password = "correct-password" };

        await FluentActions.Awaiting(() => _sut.LoginAsync(request, null, null)).Should().ThrowAsync<UnauthorizedException>();
        _credentials.Verify(x => x.UpdateAsync(It.IsAny<UserCredential>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_SuspendedAccount_ThrowsForbiddenOnlyAfterPasswordVerified()
    {
        var user = ActiveUser();
        user.Status = AccountStatus.Suspended;
        var credential = PasswordCredential();

        _users.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _credentials.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<UserCredential, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(credential);
        _passwordHasher.Setup(x => x.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var request = new LoginRequest { Email = "jane@example.com", Password = "correct-password" };

        await FluentActions.Awaiting(() => _sut.LoginAsync(request, null, null)).Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task RefreshAsync_ValidToken_RotatesTokenAndKeepsFamily()
    {
        var user = ActiveUser();
        var existingToken = new RefreshToken
        {
            Id = "token-old",
            UserId = user.Id,
            TokenHash = "irrelevant-in-test-because-we-return-it-directly",
            FamilyId = "family-1",
            CreatedAt = Now.AddDays(-1),
            ExpiresAt = Now.AddDays(29),
        };

        _refreshTokens.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<RefreshToken, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(existingToken);
        _users.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await _sut.RefreshAsync(new RefreshTokenRequest { RefreshToken = "raw-token" }, "127.0.0.1", "test-agent");

        result.AccessToken.Should().Be("access-token");
        existingToken.RevokedAt.Should().Be(Now);
        existingToken.ReplacedByTokenHash.Should().NotBeNullOrEmpty();

        _refreshTokens.Verify(x => x.UpdateAsync(existingToken, It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokens.Verify(x => x.AddAsync(It.Is<RefreshToken>(t => t.FamilyId == "family-1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_AlreadyRevokedTokenReused_RevokesWholeFamilyAndThrows()
    {
        var reusedToken = new RefreshToken
        {
            Id = "token-old",
            UserId = "user-1",
            TokenHash = "hash",
            FamilyId = "family-1",
            CreatedAt = Now.AddDays(-2),
            ExpiresAt = Now.AddDays(28),
            RevokedAt = Now.AddMinutes(-5),
        };
        var siblingToken = new RefreshToken
        {
            Id = "token-sibling",
            UserId = "user-1",
            TokenHash = "hash-2",
            FamilyId = "family-1",
            CreatedAt = Now.AddMinutes(-5),
            ExpiresAt = Now.AddDays(30),
        };

        _refreshTokens.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<RefreshToken, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(reusedToken);
        _refreshTokens.Setup(x => x.FindAsync(It.IsAny<Expression<Func<RefreshToken, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([siblingToken]);

        var act = () => _sut.RefreshAsync(new RefreshTokenRequest { RefreshToken = "stolen-token" }, null, null);

        await act.Should().ThrowAsync<UnauthorizedException>();
        _refreshTokens.Verify(x => x.UpdateAsync(It.Is<RefreshToken>(t => t.Id == "token-sibling" && t.RevokedAt == Now), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_KnownActiveToken_RevokesIt()
    {
        var token = new RefreshToken { Id = "token-1", UserId = "user-1", TokenHash = "hash", FamilyId = "family-1", CreatedAt = Now, ExpiresAt = Now.AddDays(30) };
        _refreshTokens.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<RefreshToken, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(token);

        await _sut.LogoutAsync(new LogoutRequest { RefreshToken = "raw" });

        _refreshTokens.Verify(x => x.UpdateAsync(It.Is<RefreshToken>(t => t.RevokedAt == Now), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_AlreadyRevokedToken_IsIdempotentAndDoesNotThrow()
    {
        var token = new RefreshToken { Id = "token-1", UserId = "user-1", TokenHash = "hash", FamilyId = "family-1", CreatedAt = Now, ExpiresAt = Now.AddDays(30), RevokedAt = Now.AddMinutes(-1) };
        _refreshTokens.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<RefreshToken, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(token);

        var act = () => _sut.LogoutAsync(new LogoutRequest { RefreshToken = "raw" });

        await act.Should().NotThrowAsync();
        _refreshTokens.Verify(x => x.UpdateAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
