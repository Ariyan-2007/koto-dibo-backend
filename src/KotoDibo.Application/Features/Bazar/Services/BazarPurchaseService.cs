using FluentValidation;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Bazar.DTOs;
using KotoDibo.Application.Features.Bazar.Interfaces;
using KotoDibo.Application.Features.HouseholdBalance.Interfaces;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using KotoDibo.Domain.Exceptions;
using KotoDibo.Domain.Policies;

namespace KotoDibo.Application.Features.Bazar.Services;

public class BazarPurchaseService : IBazarPurchaseService
{
    private readonly IRepository<BazarPurchase> _purchases;
    private readonly IRepository<Contribution> _contributions;
    private readonly IHouseholdBalanceService _householdBalanceService;
    private readonly IHouseholdAccessService _access;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateBazarPurchaseRequest> _createValidator;
    private readonly IValidator<UpdateBazarPurchaseRequest> _updateValidator;

    public BazarPurchaseService(
        IRepository<BazarPurchase> purchases,
        IRepository<Contribution> contributions,
        IHouseholdBalanceService householdBalanceService,
        IHouseholdAccessService access,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        IValidator<CreateBazarPurchaseRequest> createValidator,
        IValidator<UpdateBazarPurchaseRequest> updateValidator)
    {
        _purchases = purchases;
        _contributions = contributions;
        _householdBalanceService = householdBalanceService;
        _access = access;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<BazarPurchaseDto> CreateAsync(string householdId, string callerUserId, string targetUserId, CreateBazarPurchaseRequest request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var membership = await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.AddBazarPurchase, cancellationToken);
        RequireTargetAccess(membership.Role, callerUserId, targetUserId, HouseholdPermission.AddAnyBazarPurchase);
        RequireNotFuture(request.Date, nameof(request.Date));

        // Confirms the target is an active member of this household (everyone has ViewHousehold).
        await _access.RequireMembershipAsync(householdId, targetUserId, HouseholdPermission.ViewHousehold, cancellationToken);

        var fundingSource = ParseFundingSource(request.FundingSource);
        var currency = request.Currency.Trim().ToUpperInvariant();

        if (fundingSource == BazarFundingSource.HouseholdFund)
        {
            await RequireSufficientBalanceAsync(householdId, request.Amount, currency, cancellationToken);
        }

        var now = _dateTimeProvider.UtcNow;
        var purchase = new BazarPurchase
        {
            HouseholdId = householdId,
            PurchasedByUserId = targetUserId,
            CreatedByUserId = callerUserId,
            Date = request.Date,
            Amount = request.Amount,
            Currency = currency,
            Note = request.Note?.Trim(),
            FundingSource = fundingSource,
            Status = FinancialEntryStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };

        // The purchase plus its mirrored Contribution (when applicable) must land together or not
        // at all — a crash between the two inserts would otherwise leave a purchase with no credit
        // for the money the buyer put in, or a floating LinkedContributionId pointing nowhere.
        return await _unitOfWork.ExecuteAsync(async ct =>
        {
            await _purchases.AddAsync(purchase, ct);

            // A purchase paid personally is, from the household's point of view, money the buyer just
            // put in and immediately spent — so it counts as a Contribution too. A leftover/correction
            // entry (Amount <= 0) has no such mirror: there's no cash to credit for a negative spend.
            if (fundingSource == BazarFundingSource.Personal && purchase.Amount > 0)
            {
                var contribution = await CreateLinkedContributionAsync(purchase, now, ct);
                purchase.LinkedContributionId = contribution.Id;
                await _purchases.UpdateAsync(purchase, ct);
            }

            return ToDto(purchase);
        }, cancellationToken);
    }

    public async Task<BazarPurchaseDto> GetByIdAsync(string householdId, string callerUserId, string purchaseId, CancellationToken cancellationToken = default)
    {
        await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.ViewBazar, cancellationToken);
        var purchase = await GetOwnedPurchaseAsync(householdId, purchaseId, cancellationToken);
        return ToDto(purchase);
    }

    public async Task<IReadOnlyList<BazarPurchaseDto>> GetListAsync(string householdId, string callerUserId, DateOnly? from, DateOnly? to, string? status, CancellationToken cancellationToken = default)
    {
        await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.ViewBazar, cancellationToken);

        var effectiveFrom = from ?? DateOnly.MinValue;
        var effectiveTo = to ?? DateOnly.MaxValue;
        var purchases = await _purchases.FindAsync(
            p => p.HouseholdId == householdId && p.Date >= effectiveFrom && p.Date <= effectiveTo,
            cancellationToken);

        IEnumerable<BazarPurchase> filtered = purchases;
        if (status is not null)
        {
            var parsedStatus = Enum.Parse<FinancialEntryStatus>(status, ignoreCase: true);
            filtered = filtered.Where(p => p.Status == parsedStatus);
        }

        return filtered
            .OrderByDescending(p => p.Date)
            .ThenByDescending(p => p.CreatedAt)
            .Select(ToDto)
            .ToList();
    }

    public async Task<BazarPurchaseDto> UpdateAsync(string householdId, string callerUserId, string purchaseId, UpdateBazarPurchaseRequest request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var membership = await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.ViewBazar, cancellationToken);
        var purchase = await GetOwnedPurchaseAsync(householdId, purchaseId, cancellationToken);

        RequireEditAccess(membership.Role, purchase.PurchasedByUserId, callerUserId, HouseholdPermission.UpdateBazarPurchase, "Bazar purchase");
        RequireActive(purchase);

        var oldFundingSource = purchase.FundingSource;
        var oldAmount = purchase.Amount;
        var oldLinkedContributionId = purchase.LinkedContributionId;

        if (request.Date is { } newDate)
        {
            RequireNotFuture(newDate, nameof(request.Date));
            purchase.Date = newDate;
        }

        if (request.Amount is not null)
        {
            purchase.Amount = request.Amount.Value;
        }

        if (request.Currency is not null)
        {
            purchase.Currency = request.Currency.Trim().ToUpperInvariant();
        }

        if (request.Note is not null)
        {
            purchase.Note = request.Note.Trim();
        }

        if (request.FundingSource is not null)
        {
            purchase.FundingSource = ParseFundingSource(request.FundingSource);
        }

        if (purchase.Amount < 0 && purchase.FundingSource == BazarFundingSource.HouseholdFund)
        {
            throw FieldValidationException(nameof(request.FundingSource), "A negative (leftover) amount can only use FundingSource 'Personal'.");
        }

        if (purchase.FundingSource == BazarFundingSource.HouseholdFund)
        {
            // Add back this purchase's own prior draw (if any) before checking, so editing an
            // existing fund-funded purchase isn't compared against a balance that already has it
            // subtracted out once.
            var currentBalance = await _householdBalanceService.GetCurrentBalanceAsync(householdId, cancellationToken);
            var balanceExcludingThisPurchase = currentBalance + (oldFundingSource == BazarFundingSource.HouseholdFund ? oldAmount : 0m);

            if (purchase.Amount > balanceExcludingThisPurchase)
            {
                throw new InsufficientFundsException(
                    $"The household's current balance is {balanceExcludingThisPurchase} {purchase.Currency}, which is not enough to cover a {purchase.Amount} {purchase.Currency} purchase from the shared fund.");
            }
        }

        purchase.UpdatedAt = _dateTimeProvider.UtcNow;

        // Reconciling the mirrored Contribution and saving the purchase's own new state must
        // commit together — otherwise an interrupted edit could leave the Contribution reflecting
        // the new amount while the purchase (or vice versa) still shows the old one.
        return await _unitOfWork.ExecuteAsync(async ct =>
        {
            await ReconcileLinkedContributionAsync(purchase, oldLinkedContributionId, ct);
            await _purchases.UpdateAsync(purchase, ct);
            return ToDto(purchase);
        }, cancellationToken);
    }

    // A hard delete, not a soft-cancel: removing a Bazar purchase permanently wipes it (and its
    // mirrored Contribution, if any) from the database. There's no undo and no "Cancelled" row left
    // behind — the household's financial history for this purchase simply no longer includes it.
    public async Task DeleteAsync(string householdId, string callerUserId, string purchaseId, CancellationToken cancellationToken = default)
    {
        var membership = await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.ViewBazar, cancellationToken);
        var purchase = await GetOwnedPurchaseAsync(householdId, purchaseId, cancellationToken);

        RequireEditAccess(membership.Role, purchase.PurchasedByUserId, callerUserId, HouseholdPermission.DeleteBazarPurchase, "Bazar purchase");

        await _unitOfWork.ExecuteAsync(async ct =>
        {
            // The mirrored Contribution only exists because this purchase does — deleting the
            // purchase must delete the mirror with it, in the same transaction, so neither can be
            // left behind without the other.
            if (purchase.LinkedContributionId is not null)
            {
                await _contributions.DeleteAsync(purchase.LinkedContributionId, ct);
            }

            await _purchases.DeleteAsync(purchase.Id, ct);
            return true;
        }, cancellationToken);
    }

    private async Task RequireSufficientBalanceAsync(string householdId, decimal amount, string currency, CancellationToken cancellationToken)
    {
        var balance = await _householdBalanceService.GetCurrentBalanceAsync(householdId, cancellationToken);
        if (amount > balance)
        {
            throw new InsufficientFundsException(
                $"The household's current balance is {balance} {currency}, which is not enough to cover a {amount} {currency} purchase from the shared fund.");
        }
    }

    private async Task<Contribution> CreateLinkedContributionAsync(BazarPurchase purchase, DateTime now, CancellationToken cancellationToken)
    {
        var contribution = new Contribution
        {
            HouseholdId = purchase.HouseholdId,
            ContributedByUserId = purchase.PurchasedByUserId,
            // Mirrors the purchase's own creator, not necessarily the buyer — e.g. a Manager
            // recording a purchase on a member's behalf is also who "created" the mirrored credit.
            CreatedByUserId = purchase.CreatedByUserId,
            Date = purchase.Date,
            Amount = purchase.Amount,
            Currency = purchase.Currency,
            Notes = "Auto-generated from a Bazar purchase paid personally.",
            SourceType = ContributionSourceType.AutoFromBazar,
            SourceBazarPurchaseId = purchase.Id,
            Status = FinancialEntryStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _contributions.AddAsync(contribution, cancellationToken);
        return contribution;
    }

    // Brings the mirrored Contribution in line with the purchase's post-edit state: creates one if
    // the purchase newly qualifies (e.g. switched to Personal, or a leftover amount became
    // positive), updates it in place if it already exists, or deletes it outright if the purchase no
    // longer qualifies (e.g. switched to HouseholdFund) — there's no "cancelled mirror" state to
    // leave behind, it just stops existing.
    private async Task ReconcileLinkedContributionAsync(BazarPurchase purchase, string? oldLinkedContributionId, CancellationToken cancellationToken)
    {
        var shouldHaveContribution = purchase.FundingSource == BazarFundingSource.Personal && purchase.Amount > 0;

        if (!shouldHaveContribution)
        {
            if (oldLinkedContributionId is not null)
            {
                await _contributions.DeleteAsync(oldLinkedContributionId, cancellationToken);
                purchase.LinkedContributionId = null;
            }

            return;
        }

        if (oldLinkedContributionId is not null)
        {
            var existing = await _contributions.GetByIdAsync(oldLinkedContributionId, cancellationToken);
            if (existing is not null)
            {
                existing.ContributedByUserId = purchase.PurchasedByUserId;
                existing.CreatedByUserId = purchase.CreatedByUserId;
                existing.Date = purchase.Date;
                existing.Amount = purchase.Amount;
                existing.Currency = purchase.Currency;
                existing.UpdatedAt = _dateTimeProvider.UtcNow;
                await _contributions.UpdateAsync(existing, cancellationToken);
                purchase.LinkedContributionId = existing.Id;
                return;
            }
        }

        var created = await CreateLinkedContributionAsync(purchase, _dateTimeProvider.UtcNow, cancellationToken);
        purchase.LinkedContributionId = created.Id;
    }

    private static BazarFundingSource ParseFundingSource(string value) => Enum.Parse<BazarFundingSource>(value, ignoreCase: true);

    private async Task<BazarPurchase> GetOwnedPurchaseAsync(string householdId, string purchaseId, CancellationToken cancellationToken)
    {
        var purchase = await _purchases.GetByIdAsync(purchaseId, cancellationToken);
        if (purchase is null || purchase.HouseholdId != householdId)
        {
            throw new NotFoundException("BazarPurchase", purchaseId);
        }

        return purchase;
    }

    private void RequireNotFuture(DateOnly date, string field)
    {
        var today = Common.LocalDate.TodayFor(_dateTimeProvider.UtcNow);
        if (date > today)
        {
            throw FieldValidationException(field, "Date cannot be in the future.");
        }
    }

    // Shared with ContributionService for its own "create on behalf of another member" flow — the
    // rule (self is always fine; anyone else requires the role-level "any" permission) is identical
    // for both, just gated by a different HouseholdPermission.
    internal static void RequireTargetAccess(HouseholdRole callerRole, string callerUserId, string targetUserId, HouseholdPermission anyPermission)
    {
        if (targetUserId == callerUserId)
        {
            return;
        }

        if (!HouseholdRolePolicy.HasPermission(callerRole, anyPermission))
        {
            throw new ForbiddenException("You do not have permission to add records on behalf of other members.");
        }
    }

    internal static void RequireEditAccess(HouseholdRole callerRole, string ownerUserId, string callerUserId, HouseholdPermission broadPermission, string entityName)
    {
        var canEditAny = HouseholdRolePolicy.HasPermission(callerRole, broadPermission);
        var isOwnEntry = ownerUserId == callerUserId;
        if (!canEditAny && !isOwnEntry)
        {
            throw new ForbiddenException($"You do not have permission to modify this {entityName}.");
        }
    }

    private static void RequireActive(BazarPurchase purchase)
    {
        if (purchase.Status != FinancialEntryStatus.Active)
        {
            throw new DomainException("This Bazar purchase has been cancelled and can no longer be edited.");
        }
    }

    private static KotoDibo.Application.Common.Exceptions.ValidationException FieldValidationException(string field, string message) => new(new Dictionary<string, string[]>
    {
        [field] = [message],
    });

    private static BazarPurchaseDto ToDto(BazarPurchase purchase) => new()
    {
        Id = purchase.Id,
        HouseholdId = purchase.HouseholdId,
        PurchasedByUserId = purchase.PurchasedByUserId,
        CreatedByUserId = purchase.CreatedByUserId,
        Date = purchase.Date,
        Amount = purchase.Amount,
        Currency = purchase.Currency,
        Note = purchase.Note,
        FundingSource = purchase.FundingSource.ToString(),
        LinkedContributionId = purchase.LinkedContributionId,
        Status = purchase.Status.ToString(),
        CreatedAt = purchase.CreatedAt,
        UpdatedAt = purchase.UpdatedAt,
    };
}
