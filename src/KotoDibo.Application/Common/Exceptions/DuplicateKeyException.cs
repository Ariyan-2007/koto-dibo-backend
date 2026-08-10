namespace KotoDibo.Application.Common.Exceptions;

// Thrown by repository implementations when a write violates a unique index. Persistence-agnostic
// (doesn't know it's Mongo) so callers can translate it into domain-specific messaging, e.g.
// AuthService turns this into "email already exists" — a generic repository shared by every
// entity shouldn't hardcode messaging for one entity's constraint.
public class DuplicateKeyException : Exception
{
    public DuplicateKeyException(string message) : base(message)
    {
    }
}
