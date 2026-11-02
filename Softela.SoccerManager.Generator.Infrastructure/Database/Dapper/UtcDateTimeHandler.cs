using System.Data;
using Dapper;

namespace Softela.SoccerManager.Generator.Infrastructure.Database.Dapper;

/// <summary>
/// SQL Server's DATETIME2 column type carries no time zone, so Dapper returns every value with <see cref="DateTimeKind.Unspecified"/>, which System.Text.Json then serializes without the trailing "Z". This handler marks each value read from the database as UTC so it serializes the same way whether it came from memory or from SQL Server.
/// </summary>
public sealed class UtcDateTimeHandler : SqlMapper.TypeHandler<DateTime>
{
    /// <summary>
    /// Converts a raw database value into a UTC-marked <see cref="DateTime"/>.
    /// </summary>
    /// <param name="value">The raw value returned by the database.</param>
    /// <returns>The value as a <see cref="DateTime"/> with <see cref="DateTimeKind.Utc"/>.</returns>
    public override DateTime Parse(object value)
    {
        return DateTime.SpecifyKind((DateTime)value, DateTimeKind.Utc);
    }

    /// <summary>
    /// Writes a <see cref="DateTime"/> value to a command parameter as a DATETIME2.
    /// </summary>
    /// <param name="parameter">The parameter to populate.</param>
    /// <param name="value">The value to write.</param>
    public override void SetValue(IDbDataParameter parameter, DateTime value)
    {
        parameter.DbType = DbType.DateTime2;
        parameter.Value = value;
    }
}
