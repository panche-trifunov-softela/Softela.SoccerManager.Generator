namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// One match of a continental cup's league phase.
/// </summary>
/// <param name="Matchday">
/// The 1-based matchday. A 16-club cup has 6 matchdays and a 32-club cup has 8; matchday N is played on the Nth date
/// of <see cref="Calendar.ContinentalCupDates.LeaguePhase"/>.
/// </param>
/// <param name="HomeLeagueTeamId">The league team id of the club that plays at home.</param>
/// <param name="AwayLeagueTeamId">The league team id of the club that plays away.</param>
public sealed record ContinentalFixture(int Matchday, int HomeLeagueTeamId, int AwayLeagueTeamId);
