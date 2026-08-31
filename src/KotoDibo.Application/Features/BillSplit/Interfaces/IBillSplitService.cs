using KotoDibo.Application.Features.BillSplit.DTOs;

namespace KotoDibo.Application.Features.BillSplit.Interfaces;

public interface IBillSplitService
{
    Task<BillSplitDto> CreateAsync(string householdId, string callerUserId, CreateBillSplitRequest request, CancellationToken cancellationToken = default);

    Task<BillSplitDto> GetByIdAsync(string householdId, string callerUserId, string billSplitId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BillSplitDto>> GetListAsync(string householdId, string callerUserId, DateOnly? from, DateOnly? to, string? status, CancellationToken cancellationToken = default);

    Task<BillSplitDto> UpdateAsync(string householdId, string callerUserId, string billSplitId, UpdateBillSplitRequest request, CancellationToken cancellationToken = default);

    Task<BillSplitDto> CancelAsync(string householdId, string callerUserId, string billSplitId, CancellationToken cancellationToken = default);

    Task<BillSplitSettlementDto> GetSettlementAsync(string householdId, string callerUserId, string billSplitId, CancellationToken cancellationToken = default);
}
