using KotoDibo.Application.Features.BillSplit.DTOs;

namespace KotoDibo.Application.Features.BillSplit.Interfaces;

public interface IBillSplitService
{
    Task<BillSplitDto> CreateAsync(CreateBillSplitRequest request, CancellationToken cancellationToken = default);

    Task<BillSplitDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BillSplitDto>> GetAllAsync(CancellationToken cancellationToken = default);
}
