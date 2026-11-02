using System.Data;
using Dapper;

namespace Softela.SoccerManager.Generator.Infrastructure.Database.Dapper;

/// <summary>
/// Converts between a DATE column and <see cref="DateOnly"/> explicitly, rather than relying on the ADO provider's own <see cref="DateOnly"/> support, so the round-trip does not depend on the driver version.
/// </summary>
public sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    /// <summary>
    /// Converts a raw database value into a <see cref="DateOnly"/>.
    /// </summary>
    /// <param name="value">The raw value returned by the database.</param>
    /// <returns>The value as a <see cref="DateOnly"/>.</returns>
    public override DateOnly Parse(object value)
    {
        return DateOnly.FromDateTime((DateTime)value);
    }

    /// <summary>
    /// Writes a <see cref="DateOnly"/> value to a command parameter as a DATE.
    /// </summary>
    /// <param name="parameter">The parameter to populate.</param>
    /// <param name="value">The value to write.</param>
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }
}
