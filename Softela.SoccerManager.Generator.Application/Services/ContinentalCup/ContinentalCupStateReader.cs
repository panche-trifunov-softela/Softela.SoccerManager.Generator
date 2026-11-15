using System.Globalization;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// Tells how much of a season's two continental cups is written. A cup is written when it has matches and standings
/// rows; the two cups are written together, so a state in between is partial and the step refuses to add to it.
/// </summary>
internal static class ContinentalCupStateReader
{
    /// <summary>
    /// Reads the state of the two cups. The first reason that applies is the one given, in this order: cup 1 has
    /// matches without standings or standings without matches, cup 2 the same, only one cup is written, a club is in
    /// both cups.
    /// </summary>
    /// <param name="cup1Id">The competition id of the first continental cup.</param>
    /// <param name="cup2Id">The competition id of the second continental cup.</param>
    /// <param name="seasonMatches">The matches of the season, of every competition.</param>
    /// <param name="cup1StandingClubs">The league team ids that have a standings row in the first cup.</param>
    /// <param name="cup2StandingClubs">The league team ids that have a standings row in the second cup.</param>
    /// <returns>The state of the cups.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a collection is null.</exception>
    public static ContinentalCupState Read(
        int cup1Id,
        int cup2Id,
        IReadOnlyCollection<SeasonMatch> seasonMatches,
        IReadOnlyCollection<int> cup1StandingClubs,
        IReadOnlyCollection<int> cup2StandingClubs)
    {
        ArgumentNullException.ThrowIfNull(seasonMatches);
        ArgumentNullException.ThrowIfNull(cup1StandingClubs);
        ArgumentNullException.ThrowIfNull(cup2StandingClubs);

        bool matches1 = seasonMatches.Any(match => match.CompetitionId == cup1Id);
        bool matches2 = seasonMatches.Any(match => match.CompetitionId == cup2Id);
        bool standings1 = cup1StandingClubs.Count > 0;
        bool standings2 = cup2StandingClubs.Count > 0;

        if (!matches1 && !matches2 && !standings1 && !standings2)
        {
            return new ContinentalCupState(ContinentalCupStateStatus.NotWritten, string.Empty);
        }

        string? reason = InconsistentCup(cup1Id, matches1, standings1)
            ?? InconsistentCup(cup2Id, matches2, standings2);

        if (reason is null && !(matches1 && matches2))
        {
            int writtenId = matches1 ? cup1Id : cup2Id;

            reason = string.Create(CultureInfo.InvariantCulture, $"Only cup {writtenId} is written.");
        }

        if (reason is null)
        {
            HashSet<int> inCup1 = cup1StandingClubs.ToHashSet();
            List<int> shared = cup2StandingClubs.Where(inCup1.Contains).Order().ToList();

            if (shared.Count > 0)
            {
                reason = string.Create(CultureInfo.InvariantCulture, $"League team {shared[0]} is in both cups.");
            }
        }

        return reason is null
            ? new ContinentalCupState(ContinentalCupStateStatus.Written, string.Empty)
            : new ContinentalCupState(ContinentalCupStateStatus.Partial, reason);
    }

    /// <summary>
    /// Describes a cup that has matches without standings rows or standings rows without matches.
    /// </summary>
    /// <param name="cupId">The competition id of the cup.</param>
    /// <param name="hasMatches">Whether the cup has a match in the season.</param>
    /// <param name="hasStandings">Whether the cup has a standings row in the season.</param>
    /// <returns>The reason, or null when the cup has both or neither.</returns>
    private static string? InconsistentCup(int cupId, bool hasMatches, bool hasStandings)
    {
        if (hasMatches && !hasStandings)
        {
            return string.Create(CultureInfo.InvariantCulture, $"Cup {cupId} has matches but no standings.");
        }

        if (hasStandings && !hasMatches)
        {
            return string.Create(CultureInfo.InvariantCulture, $"Cup {cupId} has standings but no matches.");
        }

        return null;
    }
}
