using System.Data;
using Microsoft.Data.SqlClient;
using Softela.SoccerManager.Generator.Infrastructure.Database.Connections;

namespace Softela.SoccerManager.Generator.Infrastructure.Database.Dapper;

/// <summary>
/// Provides the Dapper-based database connection and transaction shared by repositories within a scope.
/// </summary>
public sealed class DapperDataContext : IDapperDataContext, IDisposable
{
    private readonly string _connectionString;
    private SqlConnection? _connection;

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperDataContext"/> class.
    /// </summary>
    /// <param name="dbConnection">The provider used to resolve the database connection string.</param>
    public DapperDataContext(IDatabaseConnection dbConnection)
    {
        _connectionString = dbConnection.GetConnectionString();
    }

    /// <summary>
    /// Gets an open database connection, creating a new one whenever none exists or the current one is closed.
    /// </summary>
    public IDbConnection Connection
    {
        get
        {
            if (_connection is null || _connection.State != ConnectionState.Open)
            {
                // Dapper closes any connection it had to open, so the previous instance is spent by
                // the next call. Disposing it returns its pooled connection deterministically rather
                // than abandoning the object for the finalizer.
                _connection?.Dispose();
                _connection = new SqlConnection(_connectionString);
            }

            return _connection;
        }
    }

    /// <summary>
    /// Gets or sets the transaction currently active on <see cref="Connection"/>, or <see langword="null"/> when none is active.
    /// </summary>
    public IDbTransaction? Transaction { get; set; }

    /// <summary>
    /// Disposes the active transaction and connection.
    /// </summary>
    public void Dispose()
    {
        Transaction?.Dispose();
        Transaction = null;

        _connection?.Dispose();
        _connection = null;
    }
}
