using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Domain.Entities;

namespace KotoDibo.Application.Common;

public class HouseholdLedgerLockService : IHouseholdLedgerLock
{
    private readonly IRepository<HouseholdLedgerLock> _locks;
    private readonly IDateTimeProvider _dateTimeProvider;

    public HouseholdLedgerLockService(IRepository<HouseholdLedgerLock> locks, IDateTimeProvider dateTimeProvider)
    {
        _locks = locks;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task AcquireAsync(string householdId, CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.UtcNow;
        var row = await _locks.GetByIdAsync(householdId, cancellationToken);
        if (row is null)
        {
            await _locks.AddAsync(new HouseholdLedgerLock { Id = householdId, Version = 1, UpdatedAt = now }, cancellationToken);
            return;
        }

        row.Version++;
        row.UpdatedAt = now;
        await _locks.UpdateAsync(row, cancellationToken);
    }
}
