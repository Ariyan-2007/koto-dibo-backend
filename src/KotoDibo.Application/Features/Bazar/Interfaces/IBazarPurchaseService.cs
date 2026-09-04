using KotoDibo.Application.Features.Bazar.DTOs;

namespace KotoDibo.Application.Features.Bazar.Interfaces;

public interface IBazarPurchaseService
{
    Task<BazarPurchaseDto> CreateAsync(string householdId, string callerUserId, string targetUserId, CreateBazarPurchaseRequest request, CancellationToken cancellationToken = default);

    Task<BazarPurchaseDto> GetByIdAsync(string householdId, string callerUserId, string purchaseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BazarPurchaseDto>> GetListAsync(string householdId, string callerUserId, DateOnly? from, DateOnly? to, string? status, CancellationToken cancellationToken = default);

    Task<BazarPurchaseDto> UpdateAsync(string householdId, string callerUserId, string purchaseId, UpdateBazarPurchaseRequest request, CancellationToken cancellationToken = default);

    // Hard delete — permanently removes the purchase (and its mirrored Contribution, if any) from
    // the database. No soft-cancel state; nothing is left behind to undo.
    Task DeleteAsync(string householdId, string callerUserId, string purchaseId, CancellationToken cancellationToken = default);
}
