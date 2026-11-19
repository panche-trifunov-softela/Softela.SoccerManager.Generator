using Softela.SoccerManager.Generator.Application.Models;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// What the World Cup steps read about the national teams: the league's roster, the national teams' confederations and
/// the ratings of the players who have a national team.
/// </summary>
/// <param name="Roster">The national teams on the league's roster, with their roster entries.</param>
/// <param name="Confederations">Every national team with its confederation.</param>
/// <param name="Ratings">Every player who has a national team, with that team and the player's rating.</param>
internal sealed record NationalTeamReads(
    IReadOnlyList<GetLeagueNationalTeamsResult> Roster,
    IReadOnlyList<GetNationalTeamConfederationsResult> Confederations,
    IReadOnlyList<GetNationalTeamPlayerRatingsResult> Ratings);
