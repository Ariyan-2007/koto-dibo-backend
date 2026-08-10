namespace KotoDibo.Application.Common.Exceptions;

// Identity has already been proven (e.g. correct password) but the account isn't allowed to
// proceed (suspended/deactivated/deleted). Distinct from UnauthorizedException, which covers
// "identity not proven yet".
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message)
    {
    }
}
