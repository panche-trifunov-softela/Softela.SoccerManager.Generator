namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// A nation that has a place in the World Cup finals.
/// </summary>
/// <param name="LeagueTeamId">The roster entry of the nation, which is the nation in its World Cup matches.</param>
/// <param name="Confederation">The confederation the nation qualified in.</param>
internal sealed record QualifiedNation(int LeagueTeamId, Confederation Confederation);
