namespace Softela.SoccerManager.Generator.Application.Repositories;

/// <summary>
/// Groups the writes of a single use case into one database transaction.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Begins a new database transaction.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes once the transaction has started.</returns>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes once the transaction has been committed.</returns>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes once the transaction has been rolled back.</returns>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes an exclusive named lock that the current transaction holds until it commits or rolls back. It never waits:
    /// when another transaction holds the lock, it returns at once.
    /// </summary>
    /// <param name="resource">The name of the lock, at most 255 characters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when the lock was granted; <see langword="false"/> when another transaction holds it.
    /// </returns>
    Task<bool> TryLockAsync(string resource, CancellationToken cancellationToken = default);
}
