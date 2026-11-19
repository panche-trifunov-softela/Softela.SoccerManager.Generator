namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// One nation that can play the World Cup qualifiers: it is on the league's roster, it has a confederation and it has
/// enough players.
/// </summary>
/// <param name="LeagueTeamId">
/// The id of the nation's roster entry, which is the nation in its World Cup matches.
/// </param>
/// <param name="NationalTeamId">The id of the national team.</param>
/// <param name="Name">The name of the national team.</param>
/// <param name="Confederation">The confederation the nation qualifies in.</param>
/// <param name="PlayerCount">The number of players the nation has, rated or not.</param>
/// <param name="Strength">
/// The strength of the nation: the sum of the ratings of its 18 best players, a missing rating counting 0.
/// </param>
internal sealed record QualifierNation(
    int LeagueTeamId,
    int NationalTeamId,
    string Name,
    Confederation Confederation,
    int PlayerCount,
    int Strength);
