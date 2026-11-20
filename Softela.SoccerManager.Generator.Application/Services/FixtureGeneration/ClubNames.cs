using System.Globalization;
using Softela.SoccerManager.Generator.Application.Models;

namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// Names clubs for log lines, each as its team's name and its league team id. The continental cup step and the season
/// rollover step both name clubs this way, so it lives here once.
/// </summary>
internal static class ClubNames
{
    /// <summary>
    /// The name that a club has in a log line when its team is not known.
    /// </summary>
    private const string UnknownTeam = "unknown team";

    /// <summary>
    /// Finds the team name of every club on the roster. The first team and the first roster entry win when one is
    /// listed twice.
    /// </summary>
    /// <param name="roster">The league's roster.</param>
    /// <param name="teams">Every team.</param>
    /// <returns>The team name by league team id; a roster entry whose team is missing has none.</returns>
    internal static Dictionary<int, string> Of(
        IEnumerable<GetLeagueTeamsResult> roster,
        IEnumerable<GetTeamsResult> teams)
    {
        Dictionary<int, string> nameOfTeam = [];

        foreach (GetTeamsResult team in teams)
        {
            nameOfTeam.TryAdd(team.Id, team.Name);
        }

        Dictionary<int, string> nameOfClub = [];

        foreach (GetLeagueTeamsResult entry in roster)
        {
            if (nameOfTeam.TryGetValue(entry.TeamId, out string? name))
            {
                nameOfClub.TryAdd(entry.Id, name);
            }
        }

        return nameOfClub;
    }

    /// <summary>
    /// Names one club for a log line, with its team name and league team id.
    /// </summary>
    /// <param name="leagueTeamId">The league team id of the club.</param>
    /// <param name="nameOfClub">The team name by league team id.</param>
    /// <returns>The club as "'name' (id)", with "unknown team" in place of the name when it is missing.</returns>
    internal static string Describe(int leagueTeamId, IReadOnlyDictionary<int, string> nameOfClub)
    {
        string name = nameOfClub.GetValueOrDefault(leagueTeamId, UnknownTeam);

        return string.Create(CultureInfo.InvariantCulture, $"'{name}' ({leagueTeamId})");
    }

    /// <summary>
    /// Names clubs for a log line, each with its team name and league team id.
    /// </summary>
    /// <param name="clubs">The league team ids, in the order to print them.</param>
    /// <param name="nameOfClub">The team name by league team id.</param>
    /// <returns>The clubs as "'name' (id)", comma-separated, or "none" when there are no clubs.</returns>
    internal static string DescribeAll(IReadOnlyList<int> clubs, IReadOnlyDictionary<int, string> nameOfClub)
    {
        if (clubs.Count == 0)
        {
            return "none";
        }

        return string.Join(", ", clubs.Select(club => Describe(club, nameOfClub)));
    }
}
