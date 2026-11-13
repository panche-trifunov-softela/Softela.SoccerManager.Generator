namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// A continental cup tie whose two clubs are both known, with the club that hosts each leg already decided.
/// </summary>
/// <param name="Round">The round the tie belongs to.</param>
/// <param name="TieNumber">The 1-based number of the tie within its round.</param>
/// <param name="HostLeagueTeamId">
/// The league team id of the club placed higher in the league phase. It hosts the second leg, or the one match of the
/// final.
/// </param>
/// <param name="OtherLeagueTeamId">
/// The league team id of the club placed lower in the league phase. It hosts the first leg of a two-legged tie.
/// </param>
/// <param name="Legs">The number of matches in the tie: 2, or 1 for the final.</param>
public sealed record ContinentalKnownTie(
    ContinentalRound Round,
    int TieNumber,
    int HostLeagueTeamId,
    int OtherLeagueTeamId,
    int Legs);
