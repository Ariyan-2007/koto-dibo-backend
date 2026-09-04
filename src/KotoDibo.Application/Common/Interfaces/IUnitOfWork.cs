namespace KotoDibo.Application.Common.Interfaces;

// Wraps a sequence of repository writes that must succeed or fail together (e.g. a personal-pocket
// Bazar purchase plus its mirrored Contribution). The action runs inside a single database
// transaction — every IRepository call made from within it (directly or transitively) is
// automatically enlisted, so callers don't need to pass a session/context around explicitly.
public interface IUnitOfWork
{
    Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken cancellationToken = default);
}
