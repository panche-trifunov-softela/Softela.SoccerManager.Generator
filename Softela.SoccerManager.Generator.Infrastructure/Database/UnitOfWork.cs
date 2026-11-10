using System.Data;
using System.Globalization;
using Dapper;
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

    /// <summary>
    /// Takes an exclusive named lock that the active transaction holds until it commits or rolls back. It never waits:
    /// when another transaction holds the lock, it returns at once.
    /// </summary>
    /// <param name="resource">The name of the lock, at most 255 characters.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> when the lock was granted; <see langword="false"/> when another transaction holds it.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no transaction is active, or when the server refuses the lock for a reason other than another
    /// transaction holding it.
    /// </exception>
    public async Task<bool> TryLockAsync(string resource, CancellationToken cancellationToken = default)
    {
        if (_context.Transaction is null)
        {
            throw new InvalidOperationException(
                "Begin a transaction before taking a lock; the lock lasts as long as the transaction.");
        }

        var parameters = new DynamicParameters();
        parameters.Add("@Resource", resource, DbType.String, size: 255);
        parameters.Add("@LockMode", "Exclusive", DbType.String, size: 32);
        parameters.Add("@LockOwner", "Transaction", DbType.String, size: 32);
        parameters.Add("@LockTimeout", 0, DbType.Int32);
        parameters.Add("@Result", dbType: DbType.Int32, direction: ParameterDirection.ReturnValue);

        await _context.Connection.ExecuteAsync(
            "sp_getapplock",
            parameters,
            transaction: _context.Transaction,
            commandType: CommandType.StoredProcedure).ConfigureAwait(false);

        var result = parameters.Get<int>("@Result");

        // With a zero timeout the only way the server says "busy" is -1, so that is the one code that is not an error.
        // Codes 0 and 1 are a lock granted at once and one granted after waiting; the rest are failures.
        if (result == -1)
        {
            return false;
        }

        if (result == 0 || result == 1)
        {
            return true;
        }

        throw new InvalidOperationException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"sp_getapplock refused the lock '{resource}' with code {result}."));
    }
}
