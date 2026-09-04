using FluentValidation;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Bazar.Services;
using KotoDibo.Application.Features.Contributions.DTOs;
using KotoDibo.Application.Features.Contributions.Interfaces;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using KotoDibo.Domain.Exceptions;

namespace KotoDibo.Application.Features.Contributions.Services;

public class ContributionService : IContributionService
{
    private readonly IRepository<Contribution> _contributions;
    private readonly IHouseholdAccessService _access;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IValidator<CreateContributionRequest> _createValidator;
    private readonly IValidator<UpdateContributionRequest> _updateValidator;

    public ContributionService(
        IRepository<Contribution> contributions,
        IHouseholdAccessService access,
        IDateTimeProvider dateTimeProvider,
        IValidator<CreateContributionRequest> createValidator,
        IValidator<UpdateContributionRequest> updateValidator)
    {
        _contributions = contributions;
        _access = access;
        _dateTimeProvider = dateTimeProvider;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<ContributionDto> CreateAsync(string householdId, string callerUserId, string targetUserId, CreateContributionRequest request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var membership = await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.AddContribution, cancellationToken);
        BazarPurchaseService.RequireTargetAccess(membership.Role, callerUserId, targetUserId, HouseholdPermission.AddAnyContribution);
        RequireNotFuture(request.Date, nameof(request.Date));

        // Confirms the target is an active member of this household (everyone has ViewHousehold) —
        // mirrors BazarPurchaseService.CreateAsync so an Owner/Manager can't credit a cross-household
        // user ID.
        await _access.RequireMembershipAsync(householdId, targetUserId, HouseholdPermission.ViewHousehold, cancellationToken);

        var now = _dateTimeProvider.UtcNow;
        var contribution = new Contribution
        {
            HouseholdId = householdId,
            ContributedByUserId = targetUserId,
            CreatedByUserId = callerUserId,
            Date = request.Date,
            Amount = request.Amount,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Notes = request.Notes?.Trim(),
            Status = FinancialEntryStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _contributions.AddAsync(contribution, cancellationToken);
        return ToDto(contribution);
    }

    public async Task<ContributionDto> GetByIdAsync(string householdId, string callerUserId, string contributionId, CancellationToken cancellationToken = default)
    {
        await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.ViewContributions, cancellationToken);
        var contribution = await GetOwnedContributionAsync(householdId, contributionId, cancellationToken);
        return ToDto(contribution);
    }

    public async Task<IReadOnlyList<ContributionDto>> GetListAsync(string householdId, string callerUserId, DateOnly? from, DateOnly? to, string? status, CancellationToken cancellationToken = default)
    {
        await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.ViewContributions, cancellationToken);

        var effectiveFrom = from ?? DateOnly.MinValue;
        var effectiveTo = to ?? DateOnly.MaxValue;
        var contributions = await _contributions.FindAsync(
            c => c.HouseholdId == householdId && c.Date >= effectiveFrom && c.Date <= effectiveTo,
            cancellationToken);

        IEnumerable<Contribution> filtered = contributions;
        if (status is not null)
        {
            var parsedStatus = Enum.Parse<FinancialEntryStatus>(status, ignoreCase: true);
            filtered = filtered.Where(c => c.Status == parsedStatus);
        }

        return filtered
            .OrderByDescending(c => c.Date)
            .ThenByDescending(c => c.CreatedAt)
            .Select(ToDto)
            .ToList();
    }

    public async Task<ContributionDto> UpdateAsync(string householdId, string callerUserId, string contributionId, UpdateContributionRequest request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var membership = await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.ViewContributions, cancellationToken);
        var contribution = await GetOwnedContributionAsync(householdId, contributionId, cancellationToken);

        BazarPurchaseService.RequireEditAccess(membership.Role, contribution.ContributedByUserId, callerUserId, HouseholdPermission.UpdateContribution, "contribution");
        RequireActive(contribution);
        RequireNotAutoGenerated(contribution);

        if (request.Date is { } newDate)
        {
            RequireNotFuture(newDate, nameof(request.Date));
            contribution.Date = newDate;
        }

        if (request.Amount is not null)
        {
            contribution.Amount = request.Amount.Value;
        }

        if (request.Currency is not null)
        {
            contribution.Currency = request.Currency.Trim().ToUpperInvariant();
        }

        if (request.Notes is not null)
        {
            contribution.Notes = request.Notes.Trim();
        }

        contribution.UpdatedAt = _dateTimeProvider.UtcNow;
        await _contributions.UpdateAsync(contribution, cancellationToken);
        return ToDto(contribution);
    }

    // A hard delete, not a soft-cancel: permanently removes the contribution from the database.
    // There's no undo and no "Cancelled" row left behind.
    public async Task DeleteAsync(string householdId, string callerUserId, string contributionId, CancellationToken cancellationToken = default)
    {
        var membership = await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.ViewContributions, cancellationToken);
        var contribution = await GetOwnedContributionAsync(householdId, contributionId, cancellationToken);

        BazarPurchaseService.RequireEditAccess(membership.Role, contribution.ContributedByUserId, callerUserId, HouseholdPermission.DeleteContribution, "contribution");
        RequireNotAutoGenerated(contribution);

        await _contributions.DeleteAsync(contribution.Id, cancellationToken);
    }

    private async Task<Contribution> GetOwnedContributionAsync(string householdId, string contributionId, CancellationToken cancellationToken)
    {
        var contribution = await _contributions.GetByIdAsync(contributionId, cancellationToken);
        if (contribution is null || contribution.HouseholdId != householdId)
        {
            throw new NotFoundException("Contribution", contributionId);
        }

        return contribution;
    }

    private void RequireNotFuture(DateOnly date, string field)
    {
        var today = Common.LocalDate.TodayFor(_dateTimeProvider.UtcNow);
        if (date > today)
        {
            throw new KotoDibo.Application.Common.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                [field] = ["Date cannot be in the future."],
            });
        }
    }

    private static void RequireActive(Contribution contribution)
    {
        if (contribution.Status != FinancialEntryStatus.Active)
        {
            throw new DomainException("This contribution has been cancelled and can no longer be edited.");
        }
    }

    // Auto-generated rows exist purely to mirror a Bazar purchase paid personally — editing or
    // deleting one directly here would desync it from (or orphan it relative to) the purchase it
    // mirrors. The purchase is the source of truth; changes flow through BazarPurchaseService
    // instead, which deletes the mirror along with the purchase itself.
    private static void RequireNotAutoGenerated(Contribution contribution)
    {
        if (contribution.SourceType == ContributionSourceType.AutoFromBazar)
        {
            throw new DomainException("This contribution was auto-generated from a Bazar purchase. Edit or delete that Bazar purchase instead.");
        }
    }

    private static ContributionDto ToDto(Contribution contribution) => new()
    {
        Id = contribution.Id,
        HouseholdId = contribution.HouseholdId,
        ContributedByUserId = contribution.ContributedByUserId,
        CreatedByUserId = contribution.CreatedByUserId,
        Date = contribution.Date,
        Amount = contribution.Amount,
        Currency = contribution.Currency,
        Notes = contribution.Notes,
        SourceType = contribution.SourceType.ToString(),
        SourceBazarPurchaseId = contribution.SourceBazarPurchaseId,
        Status = contribution.Status.ToString(),
        CreatedAt = contribution.CreatedAt,
        UpdatedAt = contribution.UpdatedAt,
    };
}
