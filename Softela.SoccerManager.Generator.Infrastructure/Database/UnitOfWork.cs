using System.Data;
using Softela.SoccerManager.Generator.Application.Repositories;
using Softela.SoccerManager.Generator.Infrastructure.Database.Dapper;

namespace Softela.SoccerManager.Generator.Infrastructure.Database;

/// <summary>
/// Coordinates a database transaction shared across repositories within a scope.
/// </summary>
internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly IDapperDataContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnitOfWork"/> class.
    /// </summary>
    /// <param name="context">The data context whose connection and transaction this instance manages.</param>
    public UnitOfWork(IDapperDataContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Begins a new transaction on the shared connection.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A completed task once the transaction has started.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a transaction is already active.</exception>
    public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_context.Transaction is not null)
        {
            throw new InvalidOperationException("A transaction is already active. Commit or roll back the current transaction before starting a new one.");
        }

        var connection = _context.Connection;
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        _context.Transaction = connection.BeginTransaction();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Commits the active transaction.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A completed task once the transaction has been committed.</returns>
    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        _context.Transaction?.Commit();
        _context.Transaction?.Dispose();
        _context.Transaction = null;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Rolls back the active transaction, if any.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A completed task once the rollback has completed.</returns>
    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        _context.Transaction?.Rollback();
        _context.Transaction?.Dispose();
        _context.Transaction = null;

        return Task.CompletedTask;
    }
}
