using System.Globalization;
using Softela.SoccerManager.Generator.Application.Models;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// Chooses the nations that enter the World Cup qualifiers from the league's roster, the national teams'
/// confederations and the ratings of their players, and measures each nation's strength. It reads nothing and writes
/// nothing.
/// </summary>
internal static class QualifierEntrantSelector
{
    /// <summary>
    /// The number of players a nation needs to enter. Every player counts, rated or not.
    /// </summary>
    internal const int PlayersToEnter = 18;

    /// <summary>
    /// The number of a nation's best ratings that make up its strength; a missing rating counts 0.
    /// </summary>
    internal const int RatingsInStrength = 18;

    /// <summary>
    /// Chooses the entrants. A nation with fewer players than <see cref="PlayersToEnter"/> is not an entrant and is not
    /// reported. A nation with enough players and no confederation, or with a confederation but no place on the roster,
    /// is reported and left out.
    /// </summary>
    /// <param name="roster">The national teams on the league's roster, with their roster entries.</param>
    /// <param name="confederations">Every national team with its confederation.</param>
    /// <param name="ratings">Every player who has a national team, with that team and the player's rating.</param>
    /// <returns>The entrants, and the nations that were left out with a reason to report.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a rostered nation or a nation with enough players is missing from the confederations, a nation is on
    /// the roster twice or in the confederations twice, or a confederation is not one of the six.
    /// </exception>
    public static QualifierEntrants Select(
        IReadOnlyList<GetLeagueNationalTeamsResult> roster,
        IReadOnlyList<GetNationalTeamConfederationsResult> confederations,
        IReadOnlyList<GetNationalTeamPlayerRatingsResult> ratings)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(confederations);
        ArgumentNullException.ThrowIfNull(ratings);

        Dictionary<int, GetNationalTeamConfederationsResult> nationOf = IndexNations(confederations);
        Dictionary<int, int> leagueTeamOf = IndexRoster(roster, nationOf);

        List<QualifierNation> entrants = [];
        List<GetNationalTeamConfederationsResult> withoutConfederation = [];
        List<GetNationalTeamConfederationsResult> notOnRoster = [];

        IEnumerable<IGrouping<int, GetNationalTeamPlayerRatingsResult>> squads =
            ratings.GroupBy(row => row.NationalTeamId);

        foreach (IGrouping<int, GetNationalTeamPlayerRatingsResult> squad in squads)
        {
            int playerCount = squad.Count();

            if (playerCount < PlayersToEnter)
            {
                continue;
            }

            if (!nationOf.TryGetValue(squad.Key, out GetNationalTeamConfederationsResult? nation))
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"The players of national team {squad.Key} are read, but the confederations read does not ")
                    + "have that national team.",
                    nameof(confederations));
            }

            if (nation.Confederation is not { } code)
            {
                withoutConfederation.Add(nation);

                continue;
            }

            Confederation confederation = ToConfederation(nation, code);

            if (!leagueTeamOf.TryGetValue(nation.Id, out int leagueTeamId))
            {
                notOnRoster.Add(nation);

                continue;
            }

            entrants.Add(new QualifierNation(
                leagueTeamId,
                nation.Id,
                nation.Name,
                confederation,
                playerCount,
                StrengthOf(squad)));
        }

        return new QualifierEntrants(
            entrants
                .OrderBy(entrant => entrant.Confederation)
                .ThenByDescending(entrant => entrant.Strength)
                .ThenBy(entrant => entrant.NationalTeamId)
                .ToList(),
            withoutConfederation.OrderBy(nation => nation.Id).ToList(),
            notOnRoster.OrderBy(nation => nation.Id).ToList());
    }

    /// <summary>
    /// Indexes the national teams by id.
    /// </summary>
    /// <param name="confederations">Every national team with its confederation.</param>
    /// <returns>Each national team, by its id.</returns>
    /// <exception cref="ArgumentException">Thrown when a national team is in the read twice.</exception>
    private static Dictionary<int, GetNationalTeamConfederationsResult> IndexNations(
        IReadOnlyList<GetNationalTeamConfederationsResult> confederations)
    {
        Dictionary<int, GetNationalTeamConfederationsResult> nationOf = new();

        foreach (GetNationalTeamConfederationsResult nation in confederations)
        {
            if (!nationOf.TryAdd(nation.Id, nation))
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"National team {nation.Id} is in the confederations read twice."),
                    nameof(confederations));
            }
        }

        return nationOf;
    }

    /// <summary>
    /// Indexes the roster entries by national team, and checks that every rostered nation is a known national team.
    /// </summary>
    /// <param name="roster">The national teams on the league's roster.</param>
    /// <param name="nationOf">Every national team, by its id.</param>
    /// <returns>The id of the roster entry, by national team id.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when a rostered nation is not in the confederations, or is on the roster twice.
    /// </exception>
    private static Dictionary<int, int> IndexRoster(
        IReadOnlyList<GetLeagueNationalTeamsResult> roster,
        Dictionary<int, GetNationalTeamConfederationsResult> nationOf)
    {
        Dictionary<int, int> leagueTeamOf = new();

        foreach (GetLeagueNationalTeamsResult entry in roster)
        {
            if (!nationOf.ContainsKey(entry.NationalTeamId))
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"National team {entry.NationalTeamId} is on the roster as entry {entry.Id}, but the ")
                    + "confederations read does not have it.",
                    nameof(roster));
            }

            if (!leagueTeamOf.TryAdd(entry.NationalTeamId, entry.Id))
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"National team {entry.NationalTeamId} is on the roster twice."),
                    nameof(roster));
            }
        }

        return leagueTeamOf;
    }

    /// <summary>
    /// Turns the backend's confederation code into a confederation.
    /// </summary>
    /// <param name="nation">The national team the code belongs to.</param>
    /// <param name="code">The backend's confederation code.</param>
    /// <returns>The confederation.</returns>
    /// <exception cref="ArgumentException">Thrown when the code is not one of the six confederations.</exception>
    private static Confederation ToConfederation(GetNationalTeamConfederationsResult nation, byte code)
    {
        if (!Enum.IsDefined((Confederation)code))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"National team {nation.Id} has confederation {code}, which is not one of the six ")
                + "confederations.",
                nameof(nation));
        }

        return (Confederation)code;
    }

    /// <summary>
    /// Measures a nation's strength: the sum of its best ratings, a missing rating counting 0. The qualifier draw and
    /// the finals draw both rank nations by it.
    /// </summary>
    /// <param name="squad">The nation's players.</param>
    /// <returns>The sum of the <see cref="RatingsInStrength"/> highest ratings.</returns>
    internal static int StrengthOf(IEnumerable<GetNationalTeamPlayerRatingsResult> squad)
    {
        return squad
            .Select(player => player.Rating ?? 0)
            .OrderByDescending(rating => rating)
            .Take(RatingsInStrength)
            .Sum();
    }
}
