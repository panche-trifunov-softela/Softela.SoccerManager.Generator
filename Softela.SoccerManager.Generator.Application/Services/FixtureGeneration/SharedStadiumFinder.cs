using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Services.RoundRobin;

namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// Finds the clubs of a league's divisions that play on the same stadium.
/// </summary>
internal static class SharedStadiumFinder
{
    /// <summary>
    /// Finds the pairs of division clubs whose teams have the same stadium. Only division clubs count: a club whose
    /// team has no stadium is never paired, and a club outside the divisions is ignored even when it uses the same
    /// stadium.
    /// </summary>
    /// <param name="divisions">The divisions of the season, with their clubs as league team ids.</param>
    /// <param name="teamOfClub">The team of each division club, by league team id.</param>
    /// <returns>The pairs, ordered by the id of their first club, which is the smaller id of the two.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when:
    /// <list type="bullet">
    /// <item><description>three or more clubs are on one stadium;</description></item>
    /// <item>
    /// <description>a division has more clubs in pairs than a round robin of its size can place.</description>
    /// </item>
    /// </list>
    /// </exception>
    public static IReadOnlyList<SharedStadiumPair> Find(
        IReadOnlyList<LeagueDivision> divisions,
        IReadOnlyDictionary<int, GetTeamsResult> teamOfClub)
    {
        SortedDictionary<int, List<int>> clubsByStadium = GroupByStadium(divisions, teamOfClub);
        List<SharedStadiumPair> pairs = [];

        foreach (KeyValuePair<int, List<int>> stadium in clubsByStadium)
        {
            List<int> clubs = stadium.Value;

            if (clubs.Count < 2)
            {
                continue;
            }

            clubs.Sort();

            if (clubs.Count > 2)
            {
                string clubList = DescribeClubs(clubs, teamOfClub);

                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Stadium {stadium.Key} is the ground of {clubs.Count} clubs in the divisions: ")
                    + clubList
                    + ". Only two clubs can share a stadium.");
            }

            pairs.Add(new SharedStadiumPair(clubs[0], clubs[1]));
        }

        CheckPairLimits(divisions, teamOfClub, pairs);

        return pairs.OrderBy(pair => pair.FirstClubId).ToList();
    }

    /// <summary>
    /// Groups the division clubs by the stadium of their team.
    /// </summary>
    /// <param name="divisions">The divisions of the season.</param>
    /// <param name="teamOfClub">The team of each division club, by league team id.</param>
    /// <returns>
    /// The league team ids on each stadium, by ascending stadium id; a club with no stadium is left out.
    /// </returns>
    private static SortedDictionary<int, List<int>> GroupByStadium(
        IReadOnlyList<LeagueDivision> divisions,
        IReadOnlyDictionary<int, GetTeamsResult> teamOfClub)
    {
        SortedDictionary<int, List<int>> clubsByStadium = new();

        foreach (LeagueDivision division in divisions)
        {
            foreach (int clubId in division.LeagueTeamIds)
            {
                int? stadiumId = teamOfClub[clubId].StadiumId;

                if (stadiumId is null)
                {
                    continue;
                }

                if (!clubsByStadium.TryGetValue(stadiumId.Value, out List<int>? clubs))
                {
                    clubs = [];
                    clubsByStadium.Add(stadiumId.Value, clubs);
                }

                clubs.Add(clubId);
            }
        }

        return clubsByStadium;
    }

    /// <summary>
    /// Checks that no division has more clubs in pairs than the scheduler can place.
    /// </summary>
    /// <param name="divisions">The divisions of the season.</param>
    /// <param name="teamOfClub">The team of each division club, by league team id.</param>
    /// <param name="pairs">The pairs of clubs that share a stadium.</param>
    /// <exception cref="SeasonRefusedException">Thrown when a division is over the limit.</exception>
    private static void CheckPairLimits(
        IReadOnlyList<LeagueDivision> divisions,
        IReadOnlyDictionary<int, GetTeamsResult> teamOfClub,
        IReadOnlyList<SharedStadiumPair> pairs)
    {
        HashSet<int> pairedClubs = [];

        foreach (SharedStadiumPair pair in pairs)
        {
            pairedClubs.Add(pair.FirstClubId);
            pairedClubs.Add(pair.SecondClubId);
        }

        foreach (LeagueDivision division in divisions)
        {
            List<int> paired = division.LeagueTeamIds
                .Where(clubId => pairedClubs.Contains(clubId))
                .OrderBy(clubId => clubId)
                .ToList();

            int limit = RoundRobinInputValidator.MaxPairedClubs(division.LeagueTeamIds.Count);

            if (paired.Count > limit)
            {
                string clubList = DescribeClubs(paired, teamOfClub);

                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Division '{division.Name}' (competition {division.CompetitionId}) has {paired.Count} clubs ")
                    + "that share a stadium: "
                    + clubList
                    + string.Create(
                        CultureInfo.InvariantCulture,
                        $". At most {limit} of its {division.LeagueTeamIds.Count} clubs can."));
            }
        }
    }

    /// <summary>
    /// Describes clubs for a refusal, in the order given.
    /// </summary>
    /// <param name="clubIds">The league team ids of the clubs.</param>
    /// <param name="teamOfClub">The team of each division club, by league team id.</param>
    /// <returns>The clubs as "name (league team id)" joined with commas.</returns>
    private static string DescribeClubs(IEnumerable<int> clubIds, IReadOnlyDictionary<int, GetTeamsResult> teamOfClub)
    {
        return string.Join(
            ", ",
            clubIds.Select(clubId => string.Create(
                CultureInfo.InvariantCulture,
                $"{teamOfClub[clubId].Name} (league team {clubId})")));
    }
}
