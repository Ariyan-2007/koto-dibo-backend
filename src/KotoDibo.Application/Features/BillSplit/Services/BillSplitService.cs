using KotoDibo.Application.Features.BillSplit.DTOs;
using KotoDibo.Application.Features.BillSplit.Interfaces;

namespace KotoDibo.Application.Features.BillSplit.Services;

public class BillSplitService : IBillSplitService
{
    public Task<BillSplitDto> CreateAsync(CreateBillSplitRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<BillSplitDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<BillSplitDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
