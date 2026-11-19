using Softela.SoccerManager.Generator.Application.Models;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// The first two reads about the national teams: the league's roster and the national teams' confederations. The
/// players' ratings are read after them, and only when they are needed.
/// </summary>
/// <param name="Roster">The national teams on the league's roster, with their roster entries.</param>
/// <param name="Confederations">Every national team with its confederation.</param>
internal sealed record RosterWithConfederations(
    IReadOnlyList<GetLeagueNationalTeamsResult> Roster,
    IReadOnlyList<GetNationalTeamConfederationsResult> Confederations);
