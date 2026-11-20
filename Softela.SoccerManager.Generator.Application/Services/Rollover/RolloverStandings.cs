using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Models;

namespace Softela.SoccerManager.Generator.Application.Services.Rollover;

/// <summary>
/// The season's Standings as the rollover reads them: which rows a division has, and whether they can fill the next
/// season's divisions.
/// </summary>
internal static class RolloverStandings
{
    /// <summary>
    /// Checks that the Standings can fill the next season's divisions: some division has rows, every division has rows
    /// when any has, and no row is for a guest of the roster, which the database would refuse in a division.
    /// </summary>
    /// <param name="divisionsInOrder">The league's divisions, the top division first.</param>
    /// <param name="standingsByDivision">The season's Standings rows by division competition id.</param>
    /// <param name="roster">The league's roster.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when a division has no entry in <paramref name="standingsByDivision"/>, which is a bug in the caller.
    /// </exception>
    /// <exception cref="SeasonRefusedException">Thrown when the Standings cannot fill the divisions.</exception>
    internal static void Check(
        IReadOnlyList<GetCompetitionsResult> divisionsInOrder,
        IReadOnlyDictionary<int, IReadOnlyList<GetStandingsResult>> standingsByDivision,
        IReadOnlyList<GetLeagueTeamsResult> roster)
    {
        if (divisionsInOrder.All(division => Of(standingsByDivision, division).Count == 0))
        {
            throw new SeasonRefusedException(
                "No division has Standings rows in this season, so its divisions' clubs are unknown. Add the "
                + "Standings, or make the next season by hand.");
        }

        foreach (GetCompetitionsResult division in divisionsInOrder)
        {
            if (Of(standingsByDivision, division).Count > 0)
            {
                continue;
            }

            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Division '{division.Name}' (competition {division.Id}) has no Standings rows in this season, ")
                + "but other divisions do. The rollover cannot fill a division: add its Standings, give it Order 0 "
                + "if that tier is gone, or make the next season by hand.");
        }

        HashSet<int> guests = roster.Where(team => team.IsGuest).Select(team => team.Id).ToHashSet();

        foreach (GetCompetitionsResult division in divisionsInOrder)
        {
            foreach (int club in Of(standingsByDivision, division).Select(row => row.LeagueTeamId).Order())
            {
                if (!guests.Contains(club))
                {
                    continue;
                }

                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Club {club} has a Standings row in division '{division.Name}' (competition {division.Id}) ")
                    + "but is a guest on the roster, and a guest never plays in a division. Delete that row.");
            }
        }
    }

    /// <summary>
    /// Gets the Standings rows of a division.
    /// </summary>
    /// <param name="standingsByDivision">The season's Standings rows by division competition id.</param>
    /// <param name="division">The division.</param>
    /// <returns>The division's Standings rows.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the division has no entry, which is a bug in the caller.
    /// </exception>
    internal static IReadOnlyList<GetStandingsResult> Of(
        IReadOnlyDictionary<int, IReadOnlyList<GetStandingsResult>> standingsByDivision,
        GetCompetitionsResult division)
    {
        if (standingsByDivision.TryGetValue(division.Id, out IReadOnlyList<GetStandingsResult>? standings))
        {
            return standings;
        }

        throw new ArgumentException(
            string.Create(CultureInfo.InvariantCulture, $"Division {division.Id} has no entry in the Standings."),
            nameof(standingsByDivision));
    }
}
