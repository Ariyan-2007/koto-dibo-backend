namespace KotoDibo.Application.Common.Interfaces;

// Serializes balance-checked spending per household. Must be called inside IUnitOfWork.ExecuteAsync,
// before reading the balance: the write it performs makes a concurrent transaction on the same
// household conflict and retry (see HouseholdLedgerLock).
public interface IHouseholdLedgerLock
{
    Task AcquireAsync(string householdId, CancellationToken cancellationToken = default);
}
