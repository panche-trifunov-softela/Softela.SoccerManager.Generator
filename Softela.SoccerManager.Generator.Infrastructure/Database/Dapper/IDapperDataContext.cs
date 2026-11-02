using System.Data;

namespace Softela.SoccerManager.Generator.Infrastructure.Database.Dapper;

/// <summary>
/// Exposes the database connection and active transaction shared by Dapper-based repositories.
/// </summary>
public interface IDapperDataContext
{
    /// <summary>
    /// Gets an open database connection.
    /// </summary>
    IDbConnection Connection { get; }

    /// <summary>
    /// Gets or sets the transaction currently active on <see cref="Connection"/>, or <see langword="null"/> when none is active.
    /// </summary>
    IDbTransaction? Transaction { get; set; }
}
