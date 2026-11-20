using Softela.SoccerManager.Generator.Application.Models;

namespace Softela.SoccerManager.Generator.Application.Services.Playoffs;

/// <summary>
/// Works out the stadium of each club on a league's roster. The promotion playoff step and the season rollover both
/// need it, so the lookup lives here once.
/// </summary>
internal static class ClubStadiums
{
    /// <summary>
    /// Gets the stadium of every club on the roster: the roster entry's team, and that team's stadium.
    /// </summary>
    /// <param name="roster">The league's roster.</param>
    /// <param name="teams">The teams, whose stadiums the roster entries point at.</param>
    /// <returns>
    /// The stadium id by league team id; null for a club whose team has no stadium or is not among the teams. When
    /// a team or a league team appears twice, the first one wins.
    /// </returns>
    internal static Dictionary<int, int?> Of(
        IReadOnlyList<GetLeagueTeamsResult> roster,
        IReadOnlyList<GetTeamsResult> teams)
    {
        Dictionary<int, int?> stadiumOfTeam = new();

        foreach (GetTeamsResult team in teams)
        {
            stadiumOfTeam.TryAdd(team.Id, team.StadiumId);
        }

        Dictionary<int, int?> stadiumOfClub = new();

        foreach (GetLeagueTeamsResult leagueTeam in roster)
        {
            stadiumOfClub.TryAdd(leagueTeam.Id, stadiumOfTeam.GetValueOrDefault(leagueTeam.TeamId));
        }

        return stadiumOfClub;
    }
}
