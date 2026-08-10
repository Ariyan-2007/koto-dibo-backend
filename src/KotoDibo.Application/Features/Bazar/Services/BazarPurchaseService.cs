using FluentValidation;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Bazar.DTOs;
using KotoDibo.Application.Features.Bazar.Interfaces;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using KotoDibo.Domain.Exceptions;
using KotoDibo.Domain.Policies;

namespace KotoDibo.Application.Features.Bazar.Services;

public class BazarPurchaseService : IBazarPurchaseService
{
    private readonly IRepository<BazarPurchase> _purchases;
    private readonly IHouseholdAccessService _access;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IValidator<CreateBazarPurchaseRequest> _createValidator;
    private readonly IValidator<UpdateBazarPurchaseRequest> _updateValidator;

    public BazarPurchaseService(
        IRepository<BazarPurchase> purchases,
        IHouseholdAccessService access,
        IDateTimeProvider dateTimeProvider,
        IValidator<CreateBazarPurchaseRequest> createValidator,
        IValidator<UpdateBazarPurchaseRequest> updateValidator)
    {
        _purchases = purchases;
        _access = access;
        _dateTimeProvider = dateTimeProvider;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<BazarPurchaseDto> CreateAsync(string householdId, string callerUserId, CreateBazarPurchaseRequest request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.AddBazarPurchase, cancellationToken);
        RequireNotFuture(request.Date, nameof(request.Date));

        var now = _dateTimeProvider.UtcNow;
        var purchase = new BazarPurchase
        {
            HouseholdId = householdId,
            PurchasedByUserId = callerUserId,
            Date = request.Date,
            Amount = request.Amount,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Note = request.Note?.Trim(),
            Status = FinancialEntryStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _purchases.AddAsync(purchase, cancellationToken);
        return ToDto(purchase);
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

        purchase.UpdatedAt = _dateTimeProvider.UtcNow;
        await _purchases.UpdateAsync(purchase, cancellationToken);
        return ToDto(purchase);
    }

    public async Task<BazarPurchaseDto> CancelAsync(string householdId, string callerUserId, string purchaseId, CancellationToken cancellationToken = default)
    {
        var membership = await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.ViewBazar, cancellationToken);
        var purchase = await GetOwnedPurchaseAsync(householdId, purchaseId, cancellationToken);

        RequireEditAccess(membership.Role, purchase.PurchasedByUserId, callerUserId, HouseholdPermission.CancelBazarPurchase, "Bazar purchase");
        RequireActive(purchase);

        purchase.Status = FinancialEntryStatus.Cancelled;
        purchase.UpdatedAt = _dateTimeProvider.UtcNow;
        await _purchases.UpdateAsync(purchase, cancellationToken);
        return ToDto(purchase);
    }

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
        var today = DateOnly.FromDateTime(_dateTimeProvider.UtcNow);
        if (date > today)
        {
            throw new KotoDibo.Application.Common.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                [field] = ["Date cannot be in the future."],
            });
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

    private static BazarPurchaseDto ToDto(BazarPurchase purchase) => new()
    {
        Id = purchase.Id,
        HouseholdId = purchase.HouseholdId,
        PurchasedByUserId = purchase.PurchasedByUserId,
        Date = purchase.Date,
        Amount = purchase.Amount,
        Currency = purchase.Currency,
        Note = purchase.Note,
        Status = purchase.Status.ToString(),
        CreatedAt = purchase.CreatedAt,
        UpdatedAt = purchase.UpdatedAt,
    };
}
