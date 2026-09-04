using FluentValidation;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Households.DTOs;
using KotoDibo.Application.Features.Invites.DTOs;
using KotoDibo.Application.Features.Invites.Interfaces;
using KotoDibo.Common.Helpers;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using KotoDibo.Domain.Exceptions;

namespace KotoDibo.Application.Features.Invites.Services;

public class HouseholdInviteService : IHouseholdInviteService
{
    private const int MaxCodeGenerationAttempts = 5;

    private readonly IRepository<Household> _households;
    private readonly IRepository<HouseholdMembership> _memberships;
    private readonly IRepository<HouseholdInvite> _invites;
    private readonly IRepository<User> _users;
    private readonly IHouseholdAccessService _access;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IInviteSettings _inviteSettings;
    private readonly IFileStorageService _fileStorage;
    private readonly IQrCodeService _qrCodeService;
    private readonly IEmailSender _emailSender;
    private readonly IValidator<CreateHouseholdInviteRequest> _createValidator;

    public HouseholdInviteService(
        IRepository<Household> households,
        IRepository<HouseholdMembership> memberships,
        IRepository<HouseholdInvite> invites,
        IRepository<User> users,
        IHouseholdAccessService access,
        IDateTimeProvider dateTimeProvider,
        IInviteSettings inviteSettings,
        IFileStorageService fileStorage,
        IQrCodeService qrCodeService,
        IEmailSender emailSender,
        IValidator<CreateHouseholdInviteRequest> createValidator)
    {
        _households = households;
        _memberships = memberships;
        _invites = invites;
        _users = users;
        _access = access;
        _dateTimeProvider = dateTimeProvider;
        _inviteSettings = inviteSettings;
        _fileStorage = fileStorage;
        _qrCodeService = qrCodeService;
        _emailSender = emailSender;
        _createValidator = createValidator;
    }

    public async Task<HouseholdInviteDto> CreateAsync(string householdId, string callerUserId, CreateHouseholdInviteRequest request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.AddMember, cancellationToken);

        var household = await _households.GetByIdAsync(householdId, cancellationToken)
            ?? throw new NotFoundException("Household", householdId);
        RequireActive(household);

        var role = Enum.Parse<HouseholdRole>(request.Role, ignoreCase: true);
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();

        var requestedExpiry = request.ExpiresInHours.HasValue
            ? TimeSpan.FromHours(request.ExpiresInHours.Value)
            : _inviteSettings.DefaultExpiry;
        var expiry = requestedExpiry > _inviteSettings.MaxExpiry ? _inviteSettings.MaxExpiry : requestedExpiry;

        var now = _dateTimeProvider.UtcNow;
        var invite = new HouseholdInvite
        {
            HouseholdId = householdId,
            InvitedByUserId = callerUserId,
            Role = role,
            Email = email,
            Status = HouseholdInviteStatus.Pending,
            Code = await GenerateUniqueCodeAsync(cancellationToken),
            ExpiresAt = now.Add(expiry),
            CreatedAt = now,
            UpdatedAt = now,
        };
        invite.InviteLink = $"{request.BaseUrl.TrimEnd('/')}/{invite.Code}";

        var qrBytes = _qrCodeService.GeneratePng(invite.InviteLink);
        using (var qrStream = new MemoryStream(qrBytes))
        {
            invite.QrCodeUrl = await _fileStorage.UploadAsync($"invites/{invite.Code}.png", qrStream, "image/png", cancellationToken);
        }

        try
        {
            await _invites.AddAsync(invite, cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            // Closes the race between GenerateUniqueCodeAsync's pre-check and this insert under
            // concurrent invite creation — same pattern as AddMemberAsync's membership race guard.
            throw new DomainException("Could not generate a unique invite code. Please try again.");
        }

        if (email is not null)
        {
            await _emailSender.SendAsync(
                email,
                $"You're invited to join {household.Name} on Koto Dibo",
                BuildInviteEmailBody(household.Name, invite.Code, invite.InviteLink),
                cancellationToken);
        }

        return ToDto(invite);
    }

    public async Task<IReadOnlyList<HouseholdInviteDto>> GetPendingAsync(string householdId, string callerUserId, CancellationToken cancellationToken = default)
    {
        await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.AddMember, cancellationToken);

        var invites = await _invites.FindAsync(
            i => i.HouseholdId == householdId && i.Status == HouseholdInviteStatus.Pending,
            cancellationToken);

        return invites.OrderByDescending(i => i.CreatedAt).Select(ToDto).ToList();
    }

    public async Task RevokeAsync(string householdId, string callerUserId, string inviteId, CancellationToken cancellationToken = default)
    {
        await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.AddMember, cancellationToken);

        var invite = await _invites.GetByIdAsync(inviteId, cancellationToken);
        if (invite is null || invite.HouseholdId != householdId)
        {
            throw new NotFoundException("HouseholdInvite", inviteId);
        }

        if (invite.Status != HouseholdInviteStatus.Pending)
        {
            throw new DomainException("Only a pending invite can be revoked.");
        }

        var now = _dateTimeProvider.UtcNow;
        invite.Status = HouseholdInviteStatus.Revoked;
        invite.RevokedAt = now;
        invite.RevokedByUserId = callerUserId;
        invite.UpdatedAt = now;
        await _invites.UpdateAsync(invite, cancellationToken);
    }

    public async Task<InvitePreviewDto> PreviewAsync(string code, string callerUserId, CancellationToken cancellationToken = default)
    {
        var invite = await _invites.FindOneAsync(i => i.Code == NormalizeCode(code), cancellationToken)
            ?? throw new NotFoundException("HouseholdInvite", code);

        var household = await _households.GetByIdAsync(invite.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Household", invite.HouseholdId);
        var inviter = await _users.GetByIdAsync(invite.InvitedByUserId, cancellationToken);

        var alreadyMember = await _memberships.FindOneAsync(
            m => m.HouseholdId == invite.HouseholdId && m.UserId == callerUserId && m.Status == HouseholdMembershipStatus.Active,
            cancellationToken) is not null;

        return new InvitePreviewDto
        {
            Code = invite.Code,
            HouseholdId = invite.HouseholdId,
            HouseholdName = household.Name,
            Role = invite.Role.ToString(),
            InvitedByName = inviter?.Name ?? "A household member",
            Status = EffectiveStatus(invite).ToString(),
            ExpiresAt = invite.ExpiresAt,
            CallerIsAlreadyMember = alreadyMember,
        };
    }

    public async Task<AcceptInviteResultDto> AcceptAsync(string code, string callerUserId, CancellationToken cancellationToken = default)
    {
        var invite = await _invites.FindOneAsync(i => i.Code == NormalizeCode(code), cancellationToken)
            ?? throw new NotFoundException("HouseholdInvite", code);

        var now = _dateTimeProvider.UtcNow;

        switch (invite.Status)
        {
            case HouseholdInviteStatus.Accepted:
                throw new DomainException("This invite has already been used.");
            case HouseholdInviteStatus.Revoked:
                throw new DomainException("This invite has been revoked.");
        }

        if (invite.ExpiresAt <= now)
        {
            invite.Status = HouseholdInviteStatus.Expired;
            invite.UpdatedAt = now;
            await _invites.UpdateAsync(invite, cancellationToken);
            throw new DomainException("This invite has expired.");
        }

        var household = await _households.GetByIdAsync(invite.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Household", invite.HouseholdId);
        RequireActive(household);

        var existingActive = await _memberships.FindOneAsync(
            m => m.HouseholdId == invite.HouseholdId && m.UserId == callerUserId && m.Status == HouseholdMembershipStatus.Active,
            cancellationToken);
        if (existingActive is not null)
        {
            throw new DomainException("You are already a member of this household.");
        }

        var membership = new HouseholdMembership
        {
            HouseholdId = invite.HouseholdId,
            UserId = callerUserId,
            Role = invite.Role,
            Status = HouseholdMembershipStatus.Active,
            JoinedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        try
        {
            await _memberships.AddAsync(membership, cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            throw new DomainException("You are already a member of this household.");
        }

        invite.Status = HouseholdInviteStatus.Accepted;
        invite.AcceptedAt = now;
        invite.AcceptedByUserId = callerUserId;
        invite.UpdatedAt = now;
        await _invites.UpdateAsync(invite, cancellationToken);

        var user = await _users.GetByIdAsync(callerUserId, cancellationToken)
            ?? throw new NotFoundException("User", callerUserId);

        return new AcceptInviteResultDto
        {
            HouseholdId = household.Id,
            HouseholdName = household.Name,
            Member = new HouseholdMemberDto
            {
                UserId = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = membership.Role.ToString(),
                JoinedAt = membership.JoinedAt,
            },
        };
    }

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxCodeGenerationAttempts; attempt++)
        {
            var candidate = InviteCodeGenerator.Generate();
            var existing = await _invites.FindOneAsync(i => i.Code == candidate, cancellationToken);
            if (existing is null)
            {
                return candidate;
            }
        }

        throw new DomainException("Could not generate a unique invite code. Please try again.");
    }

    private HouseholdInviteStatus EffectiveStatus(HouseholdInvite invite)
        => invite.Status == HouseholdInviteStatus.Pending && invite.ExpiresAt <= _dateTimeProvider.UtcNow
            ? HouseholdInviteStatus.Expired
            : invite.Status;

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private static void RequireActive(Household household)
    {
        if (household.Status != HouseholdStatus.Active)
        {
            throw new DomainException("This household is archived and no longer accepts membership changes.");
        }
    }

    private static string BuildInviteEmailBody(string householdName, string code, string link) =>
        $"You've been invited to join \"{householdName}\" on Koto Dibo.\n\n" +
        $"Join with code: {code}\n" +
        $"Or open this link: {link}\n\n" +
        "This invite will expire — ask whoever sent it for a new one if it's no longer valid.";

    private static HouseholdInviteDto ToDto(HouseholdInvite invite) => new()
    {
        Id = invite.Id,
        HouseholdId = invite.HouseholdId,
        InvitedByUserId = invite.InvitedByUserId,
        Code = invite.Code,
        Role = invite.Role.ToString(),
        Email = invite.Email,
        Status = invite.Status.ToString(),
        InviteLink = invite.InviteLink,
        QrCodeUrl = invite.QrCodeUrl,
        ExpiresAt = invite.ExpiresAt,
        CreatedAt = invite.CreatedAt,
    };
}
