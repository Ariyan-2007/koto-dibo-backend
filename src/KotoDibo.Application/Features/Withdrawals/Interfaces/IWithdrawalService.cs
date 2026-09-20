using KotoDibo.Application.Features.Withdrawals.DTOs;

namespace KotoDibo.Application.Features.Withdrawals.Interfaces;

public interface IWithdrawalService
{
    // Rejected with InsufficientFundsException (409) when request.Amount exceeds the household's
    // current balance.
    Task<WithdrawalDto> CreateAsync(string householdId, string callerUserId, string targetUserId, CreateWithdrawalRequest request, CancellationToken cancellationToken = default);

    Task<WithdrawalDto> GetByIdAsync(string householdId, string callerUserId, string withdrawalId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WithdrawalDto>> GetListAsync(string householdId, string callerUserId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);

    // Hard delete — permanently removes the withdrawal, returning the money to the household balance.
    Task DeleteAsync(string householdId, string callerUserId, string withdrawalId, CancellationToken cancellationToken = default);
}
