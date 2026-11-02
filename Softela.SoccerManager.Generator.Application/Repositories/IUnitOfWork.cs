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
}
