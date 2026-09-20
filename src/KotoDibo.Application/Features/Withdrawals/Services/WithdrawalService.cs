using FluentValidation;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Bazar.Services;
using KotoDibo.Application.Features.HouseholdBalance.Interfaces;
using KotoDibo.Application.Features.Withdrawals.DTOs;
using KotoDibo.Application.Features.Withdrawals.Interfaces;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using KotoDibo.Domain.Exceptions;

namespace KotoDibo.Application.Features.Withdrawals.Services;

public class WithdrawalService : IWithdrawalService
{
    private readonly IRepository<Withdrawal> _withdrawals;
    private readonly IHouseholdAccessService _access;
    private readonly IHouseholdBalanceService _householdBalanceService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IValidator<CreateWithdrawalRequest> _createValidator;

    public WithdrawalService(
        IRepository<Withdrawal> withdrawals,
        IHouseholdAccessService access,
        IHouseholdBalanceService householdBalanceService,
        IDateTimeProvider dateTimeProvider,
        IValidator<CreateWithdrawalRequest> createValidator)
    {
        _withdrawals = withdrawals;
        _access = access;
        _householdBalanceService = householdBalanceService;
        _dateTimeProvider = dateTimeProvider;
        _createValidator = createValidator;
    }

    public async Task<WithdrawalDto> CreateAsync(string householdId, string callerUserId, string targetUserId, CreateWithdrawalRequest request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var membership = await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.AddWithdrawal, cancellationToken);
        BazarPurchaseService.RequireTargetAccess(membership.Role, callerUserId, targetUserId, HouseholdPermission.AddAnyWithdrawal);
        RequireNotFuture(request.Date);

        await _access.RequireMembershipAsync(householdId, targetUserId, HouseholdPermission.ViewHousehold, cancellationToken);

        var currency = request.Currency.Trim().ToUpperInvariant();
        var establishedCurrency = await _householdBalanceService.GetEstablishedCurrencyAsync(householdId, cancellationToken);
        if (establishedCurrency is null)
        {
            // Nothing has ever been put into the pool, so there is nothing to withdraw.
            throw new InsufficientFundsException("The household has no balance to withdraw from.");
        }

        if (establishedCurrency != currency)
        {
            throw new KotoDibo.Application.Common.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.Currency)] = [$"This household's transactions are recorded in {establishedCurrency}. Use that currency instead."],
            });
        }

        var balance = await _householdBalanceService.GetCurrentBalanceAsync(householdId, cancellationToken);
        if (request.Amount > balance)
        {
            throw new InsufficientFundsException(
                $"The household's current balance is {balance} {currency}, which is not enough to cover a {request.Amount} {currency} withdrawal.");
        }

        var now = _dateTimeProvider.UtcNow;
        var withdrawal = new Withdrawal
        {
            HouseholdId = householdId,
            WithdrawnByUserId = targetUserId,
            CreatedByUserId = callerUserId,
            Date = request.Date,
            Amount = request.Amount,
            Currency = currency,
            Notes = request.Notes?.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _withdrawals.AddAsync(withdrawal, cancellationToken);
        return ToDto(withdrawal);
    }

    public async Task<WithdrawalDto> GetByIdAsync(string householdId, string callerUserId, string withdrawalId, CancellationToken cancellationToken = default)
    {
        await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.ViewWithdrawals, cancellationToken);
        var withdrawal = await GetOwnedWithdrawalAsync(householdId, withdrawalId, cancellationToken);
        return ToDto(withdrawal);
    }

    public async Task<IReadOnlyList<WithdrawalDto>> GetListAsync(string householdId, string callerUserId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.ViewWithdrawals, cancellationToken);

        var effectiveFrom = from ?? DateOnly.MinValue;
        var effectiveTo = to ?? DateOnly.MaxValue;
        var withdrawals = await _withdrawals.FindAsync(
            w => w.HouseholdId == householdId && w.Date >= effectiveFrom && w.Date <= effectiveTo,
            cancellationToken);

        return withdrawals
            .OrderByDescending(w => w.Date)
            .ThenByDescending(w => w.CreatedAt)
            .Select(ToDto)
            .ToList();
    }

    public async Task DeleteAsync(string householdId, string callerUserId, string withdrawalId, CancellationToken cancellationToken = default)
    {
        var membership = await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.ViewWithdrawals, cancellationToken);
        var withdrawal = await GetOwnedWithdrawalAsync(householdId, withdrawalId, cancellationToken);

        BazarPurchaseService.RequireEditAccess(membership.Role, withdrawal.WithdrawnByUserId, callerUserId, HouseholdPermission.DeleteWithdrawal, "withdrawal");

        await _withdrawals.DeleteAsync(withdrawal.Id, cancellationToken);
    }

    private async Task<Withdrawal> GetOwnedWithdrawalAsync(string householdId, string withdrawalId, CancellationToken cancellationToken)
    {
        var withdrawal = await _withdrawals.GetByIdAsync(withdrawalId, cancellationToken);
        if (withdrawal is null || withdrawal.HouseholdId != householdId)
        {
            throw new NotFoundException("Withdrawal", withdrawalId);
        }

        return withdrawal;
    }

    private void RequireNotFuture(DateOnly date)
    {
        var today = Common.LocalDate.TodayFor(_dateTimeProvider.UtcNow);
        if (date > today)
        {
            throw new KotoDibo.Application.Common.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(CreateWithdrawalRequest.Date)] = ["Date cannot be in the future."],
            });
        }
    }

    private static WithdrawalDto ToDto(Withdrawal withdrawal) => new()
    {
        Id = withdrawal.Id,
        HouseholdId = withdrawal.HouseholdId,
        WithdrawnByUserId = withdrawal.WithdrawnByUserId,
        CreatedByUserId = withdrawal.CreatedByUserId,
        Date = withdrawal.Date,
        Amount = withdrawal.Amount,
        Currency = withdrawal.Currency,
        Notes = withdrawal.Notes,
        CreatedAt = withdrawal.CreatedAt,
        UpdatedAt = withdrawal.UpdatedAt,
    };
}
