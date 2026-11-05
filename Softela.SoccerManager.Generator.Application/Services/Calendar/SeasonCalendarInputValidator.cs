using System.Globalization;

namespace Softela.SoccerManager.Generator.Application.Services.Calendar;

/// <summary>
/// Refuses season calendar input that cannot be laid out.
/// </summary>
internal static class SeasonCalendarInputValidator
{
    /// <summary>
    /// Checks the divisions and then the existing kickoffs, so a bad shape is reported ahead of a bad kickoff.
    /// </summary>
    /// <param name="divisionClubCounts">The number of clubs in each division.</param>
    /// <param name="existingKickoffs">The kickoffs of the matches that already exist.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when:
    /// <list type="bullet">
    /// <item><description>there are no divisions;</description></item>
    /// <item>
    /// <description>there are more divisions than <see cref="CalendarTemplate.MaxDivisionsPerLeague"/>;</description>
    /// </item>
    /// <item><description>the divisions do not all have the same number of clubs;</description></item>
    /// <item>
    /// <description>a division has fewer clubs than <see cref="CalendarTemplate.MinClubsPerDivision"/>;</description>
    /// </item>
    /// <item>
    /// <description>a division has more clubs than <see cref="CalendarTemplate.MaxClubsPerDivision"/>;</description>
    /// </item>
    /// <item><description>a division has an odd number of clubs;</description></item>
    /// <item><description>an existing kickoff is not in UTC.</description></item>
    /// </list>
    /// </exception>
    public static void Validate(IReadOnlyList<int> divisionClubCounts, IReadOnlyList<DateTime> existingKickoffs)
    {
        ValidateDivisions(divisionClubCounts);
        ValidateKickoffs(existingKickoffs);
    }

    /// <summary>
    /// Checks that the league has an allowed number of divisions, that they all have the same number of clubs, and that
    /// the number of clubs is allowed.
    /// </summary>
    /// <param name="divisionClubCounts">The number of clubs in each division.</param>
    /// <exception cref="ArgumentException">Thrown when a division rule is broken.</exception>
    private static void ValidateDivisions(IReadOnlyList<int> divisionClubCounts)
    {
        if (divisionClubCounts.Count == 0)
        {
            throw new ArgumentException("A league needs at least one division.", nameof(divisionClubCounts));
        }

        if (divisionClubCounts.Count > CalendarTemplate.MaxDivisionsPerLeague)
        {
            throw new ArgumentException(
                $"A league can have at most {CalendarTemplate.MaxDivisionsPerLeague} divisions, "
                + $"but this one has {divisionClubCounts.Count}.",
                nameof(divisionClubCounts));
        }

        int size = divisionClubCounts[0];

        if (divisionClubCounts.Any(count => count != size))
        {
            string counts = string.Join(", ", divisionClubCounts);

            throw new ArgumentException(
                $"All divisions of a league must have the same number of clubs, but these have {counts}.",
                nameof(divisionClubCounts));
        }

        if (size < CalendarTemplate.MinClubsPerDivision)
        {
            throw new ArgumentException(
                $"A division needs at least {CalendarTemplate.MinClubsPerDivision} clubs, but these have {size}.",
                nameof(divisionClubCounts));
        }

        if (size > CalendarTemplate.MaxClubsPerDivision)
        {
            throw new ArgumentException(
                $"A division can have at most {CalendarTemplate.MaxClubsPerDivision} clubs, but these have {size}.",
                nameof(divisionClubCounts));
        }

        if (size % 2 != 0)
        {
            throw new ArgumentException(
                $"A division needs an even number of clubs, but these have {size}.",
                nameof(divisionClubCounts));
        }
    }

    /// <summary>
    /// Rejects an existing kickoff that is not in UTC.
    /// </summary>
    /// <param name="existingKickoffs">The kickoffs of the matches that already exist.</param>
    /// <exception cref="ArgumentException">Thrown when an existing kickoff is not in UTC.</exception>
    private static void ValidateKickoffs(IReadOnlyList<DateTime> existingKickoffs)
    {
        foreach (DateTime kickoff in existingKickoffs)
        {
            if (kickoff.Kind != DateTimeKind.Utc)
            {
                // Not format "O": for a local value it would read the machine's time zone to write an offset, and
                // nothing in this class may depend on that zone.
                string value = kickoff.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff", CultureInfo.InvariantCulture);

                throw new ArgumentException(
                    $"An existing kickoff must be UTC, but {value} has kind {kickoff.Kind}.",
                    nameof(existingKickoffs));
            }
        }
    }
}
