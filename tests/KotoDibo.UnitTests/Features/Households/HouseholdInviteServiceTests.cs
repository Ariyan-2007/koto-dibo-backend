using System.Linq.Expressions;
using FluentAssertions;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Households.Services;
using KotoDibo.Application.Features.Invites.DTOs;
using KotoDibo.Application.Features.Invites.Services;
using KotoDibo.Application.Features.Invites.Validators;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using KotoDibo.Domain.Exceptions;
using Moq;

namespace KotoDibo.UnitTests.Features.Households;

public class HouseholdInviteServiceTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IRepository<Household>> _households = new();
    private readonly Mock<IRepository<HouseholdMembership>> _memberships = new();
    private readonly Mock<IRepository<HouseholdInvite>> _invites = new();
    private readonly Mock<IRepository<User>> _users = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();
    private readonly Mock<IInviteSettings> _inviteSettings = new();
    private readonly Mock<IFileStorageService> _fileStorage = new();
    private readonly Mock<IQrCodeService> _qrCodeService = new();
    private readonly Mock<IEmailSender> _emailSender = new();

    private readonly HouseholdInviteService _sut;

    public HouseholdInviteServiceTests()
    {
        _dateTimeProvider.Setup(x => x.UtcNow).Returns(Now);
        _inviteSettings.Setup(x => x.DefaultExpiry).Returns(TimeSpan.FromHours(168));
        _inviteSettings.Setup(x => x.MaxExpiry).Returns(TimeSpan.FromHours(720));
        _inviteSettings.Setup(x => x.AllowedBaseUrls).Returns(["https://koto-dibo.example"]);

        _households.Setup(x => x.GetByIdAsync("household-1", It.IsAny<CancellationToken>())).ReturnsAsync(ActiveHousehold());
        _qrCodeService.Setup(x => x.GeneratePng(It.IsAny<string>())).Returns([1, 2, 3]);
        _fileStorage.Setup(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, Stream _, string _, CancellationToken _) => $"https://cdn.example/{key}");
        _invites.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<HouseholdInvite, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((HouseholdInvite?)null); // no code collisions by default
        _invites.Setup(x => x.AddAsync(It.IsAny<HouseholdInvite>(), It.IsAny<CancellationToken>()))
            .Callback<HouseholdInvite, CancellationToken>((i, _) => i.Id = "invite-new")
            .ReturnsAsync((HouseholdInvite i, CancellationToken _) => i);

        var access = new HouseholdAccessService(_memberships.Object);

        _sut = new HouseholdInviteService(
            _households.Object,
            _memberships.Object,
            _invites.Object,
            _users.Object,
            access,
            _dateTimeProvider.Object,
            _inviteSettings.Object,
            _fileStorage.Object,
            _qrCodeService.Object,
            _emailSender.Object,
            new CreateHouseholdInviteRequestValidator());
    }

    private static Household ActiveHousehold() => new()
    {
        Id = "household-1",
        Name = "Test House",
        Status = HouseholdStatus.Active,
        OwnerUserId = "owner-1",
        CreatedAt = Now,
        UpdatedAt = Now,
    };

    private static HouseholdMembership Membership(HouseholdRole role, string userId) => new()
    {
        Id = $"membership-{userId}",
        HouseholdId = "household-1",
        UserId = userId,
        Role = role,
        Status = HouseholdMembershipStatus.Active,
        JoinedAt = Now,
        CreatedAt = Now,
        UpdatedAt = Now,
    };

    private static HouseholdInvite Invite(HouseholdInviteStatus status, DateTime expiresAt, HouseholdRole role = HouseholdRole.Member) => new()
    {
        Id = "invite-1",
        HouseholdId = "household-1",
        InvitedByUserId = "owner-1",
        Code = "ABCD1234",
        Role = role,
        Status = status,
        InviteLink = "https://koto-dibo.example/invites/ABCD1234",
        ExpiresAt = expiresAt,
        CreatedAt = Now.AddHours(-1),
        UpdatedAt = Now.AddHours(-1),
    };

    private static User ExistingUser(string id, string email) => new()
    {
        Id = id,
        Email = email,
        NormalizedEmail = email,
        Name = "Some User",
        Status = AccountStatus.Active,
        CreatedAt = Now,
        UpdatedAt = Now,
    };

    private void GivenMemberships(params HouseholdMembership[] candidates)
        => _memberships.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<HouseholdMembership, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<HouseholdMembership, bool>> predicate, CancellationToken _) =>
                candidates.FirstOrDefault(predicate.Compile()));

    private void GivenInvite(HouseholdInvite invite)
        => _invites.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<HouseholdInvite, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<HouseholdInvite, bool>> predicate, CancellationToken _) =>
                new[] { invite }.FirstOrDefault(predicate.Compile()));

    [Fact]
    public async Task CreateAsync_ByOwner_GeneratesCodeUploadsQrAndPersists()
    {
        GivenMemberships(Membership(HouseholdRole.Owner, "owner-1"));

        var result = await _sut.CreateAsync("household-1", "owner-1", new CreateHouseholdInviteRequest { Role = "Member", BaseUrl = "https://koto-dibo.example/invites" });

        result.Code.Should().NotBeNullOrWhiteSpace();
        result.QrCodeUrl.Should().Be($"https://cdn.example/invites/{result.Code}.png");
        result.InviteLink.Should().Be($"https://koto-dibo.example/invites/{result.Code}");
        result.Status.Should().Be(nameof(HouseholdInviteStatus.Pending));
        _invites.Verify(x => x.AddAsync(It.Is<HouseholdInvite>(i => i.Role == HouseholdRole.Member), It.IsAny<CancellationToken>()), Times.Once);
        _emailSender.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithEmail_SendsInviteEmail()
    {
        GivenMemberships(Membership(HouseholdRole.Owner, "owner-1"));

        await _sut.CreateAsync("household-1", "owner-1", new CreateHouseholdInviteRequest { Role = "Member", Email = "friend@example.com", BaseUrl = "https://koto-dibo.example/invites" });

        _emailSender.Verify(x => x.SendAsync("friend@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_UntrustedBaseUrl_ThrowsValidationException()
    {
        GivenMemberships(Membership(HouseholdRole.Owner, "owner-1"));

        var act = () => _sut.CreateAsync("household-1", "owner-1", new CreateHouseholdInviteRequest { Role = "Member", BaseUrl = "https://evil.example/invites" });

        await act.Should().ThrowAsync<ValidationException>();
        _invites.Verify(x => x.AddAsync(It.IsAny<HouseholdInvite>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ByViewer_ThrowsForbidden()
    {
        GivenMemberships(Membership(HouseholdRole.Viewer, "viewer-1"));

        var act = () => _sut.CreateAsync("household-1", "viewer-1", new CreateHouseholdInviteRequest { Role = "Member", BaseUrl = "https://koto-dibo.example/invites" });

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task CreateAsync_RoleOwner_ThrowsValidationException()
    {
        GivenMemberships(Membership(HouseholdRole.Owner, "owner-1"));

        var act = () => _sut.CreateAsync("household-1", "owner-1", new CreateHouseholdInviteRequest { Role = "Owner", BaseUrl = "https://koto-dibo.example/invites" });

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task AcceptAsync_PendingUnexpiredCode_CreatesActiveMembership()
    {
        GivenInvite(Invite(HouseholdInviteStatus.Pending, Now.AddHours(1), HouseholdRole.Manager));
        _users.Setup(x => x.GetByIdAsync("new-user", It.IsAny<CancellationToken>())).ReturnsAsync(ExistingUser("new-user", "new@example.com"));
        _memberships.Setup(x => x.AddAsync(It.IsAny<HouseholdMembership>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((HouseholdMembership m, CancellationToken _) => m);

        var result = await _sut.AcceptAsync("abcd1234", "new-user", CancellationToken.None);

        result.HouseholdId.Should().Be("household-1");
        result.Member.Role.Should().Be(nameof(HouseholdRole.Manager));
        _memberships.Verify(x => x.AddAsync(It.Is<HouseholdMembership>(m => m.UserId == "new-user" && m.Role == HouseholdRole.Manager), It.IsAny<CancellationToken>()), Times.Once);
        _invites.Verify(x => x.UpdateAsync(It.Is<HouseholdInvite>(i => i.Status == HouseholdInviteStatus.Accepted && i.AcceptedByUserId == "new-user"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AcceptAsync_ExpiredInvite_ThrowsAndMarksExpired()
    {
        GivenInvite(Invite(HouseholdInviteStatus.Pending, Now.AddHours(-1)));

        var act = () => _sut.AcceptAsync("abcd1234", "new-user", CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        _invites.Verify(x => x.UpdateAsync(It.Is<HouseholdInvite>(i => i.Status == HouseholdInviteStatus.Expired), It.IsAny<CancellationToken>()), Times.Once);
        _memberships.Verify(x => x.AddAsync(It.IsAny<HouseholdMembership>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AcceptAsync_AlreadyAccepted_ThrowsDomainException()
    {
        GivenInvite(Invite(HouseholdInviteStatus.Accepted, Now.AddHours(1)));

        var act = () => _sut.AcceptAsync("abcd1234", "new-user", CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        _memberships.Verify(x => x.AddAsync(It.IsAny<HouseholdMembership>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AcceptAsync_Revoked_ThrowsDomainException()
    {
        GivenInvite(Invite(HouseholdInviteStatus.Revoked, Now.AddHours(1)));

        var act = () => _sut.AcceptAsync("abcd1234", "new-user", CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task AcceptAsync_CallerAlreadyMember_ThrowsDomainException()
    {
        var invite = Invite(HouseholdInviteStatus.Pending, Now.AddHours(1));
        GivenInvite(invite);
        GivenMemberships(Membership(HouseholdRole.Member, "existing-user"));

        var act = () => _sut.AcceptAsync("abcd1234", "existing-user", CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        _memberships.Verify(x => x.AddAsync(It.IsAny<HouseholdMembership>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AcceptAsync_UnknownCode_ThrowsNotFoundException()
    {
        _invites.Setup(x => x.FindOneAsync(It.IsAny<Expression<Func<HouseholdInvite, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((HouseholdInvite?)null);

        var act = () => _sut.AcceptAsync("nonexistent", "new-user", CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task PreviewAsync_PastExpiryButStillPending_ReportsExpiredWithoutMutating()
    {
        GivenInvite(Invite(HouseholdInviteStatus.Pending, Now.AddHours(-1)));
        _users.Setup(x => x.GetByIdAsync("owner-1", It.IsAny<CancellationToken>())).ReturnsAsync(ExistingUser("owner-1", "owner@example.com"));

        var preview = await _sut.PreviewAsync("abcd1234", "someone", CancellationToken.None);

        preview.Status.Should().Be(nameof(HouseholdInviteStatus.Expired));
        _invites.Verify(x => x.UpdateAsync(It.IsAny<HouseholdInvite>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RevokeAsync_PendingInvite_MarksRevoked()
    {
        GivenMemberships(Membership(HouseholdRole.Owner, "owner-1"));
        _invites.Setup(x => x.GetByIdAsync("invite-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Invite(HouseholdInviteStatus.Pending, Now.AddHours(1)));

        await _sut.RevokeAsync("household-1", "owner-1", "invite-1", CancellationToken.None);

        _invites.Verify(x => x.UpdateAsync(It.Is<HouseholdInvite>(i => i.Status == HouseholdInviteStatus.Revoked && i.RevokedByUserId == "owner-1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeAsync_AlreadyAccepted_ThrowsDomainException()
    {
        GivenMemberships(Membership(HouseholdRole.Owner, "owner-1"));
        _invites.Setup(x => x.GetByIdAsync("invite-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Invite(HouseholdInviteStatus.Accepted, Now.AddHours(1)));

        var act = () => _sut.RevokeAsync("household-1", "owner-1", "invite-1", CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }
}
