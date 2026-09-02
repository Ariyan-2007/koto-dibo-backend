namespace KotoDibo.Domain.Exceptions;

// Thrown when a Bazar purchase requests BazarFundingSource.HouseholdFund for more than the
// household's current shared-fund balance covers. Kept distinct from DomainException so the API
// can surface it as 409 Conflict (a state conflict against the current balance) rather than 400.
public class InsufficientFundsException : Exception
{
    public InsufficientFundsException(string message) : base(message)
    {
    }
}
