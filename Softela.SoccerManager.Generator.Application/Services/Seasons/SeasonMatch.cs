namespace Softela.SoccerManager.Generator.Application.Services.Seasons;

/// <summary>
/// One match of a season as the generator's steps see it: when it is played, which competition and round it belongs
/// to, its two clubs and its score. An unplayed match reads 0-0, and the goals already include the opponents' own
/// goals.
/// </summary>
/// <param name="Id">The id of the match.</param>
/// <param name="CompetitionId">The id of the match's competition, or null when it has none.</param>
/// <param name="Kickoff">The kickoff of the match, in UTC.</param>
/// <param name="IsFinished">Whether the match has been played to the end.</param>
/// <param name="Stage">The backend's match stage, or null for a league match or a friendly.</param>
/// <param name="RoundNumber">The 1-based round of a staged match, or null.</param>
/// <param name="Leg">The leg of a two-legged tie, or null for a single match.</param>
/// <param name="HomeLeagueTeamId">
/// The league team id of the home club, or null when the match lacks exactly one home statistic row.
/// </param>
/// <param name="AwayLeagueTeamId">
/// The league team id of the away club, or null when the match lacks exactly one away statistic row.
/// </param>
/// <param name="HomeGoals">The goals scored for the home club, own goals of the opponent included.</param>
/// <param name="AwayGoals">The goals scored for the away club, own goals of the opponent included.</param>
/// <param name="HomeShootoutGoals">The home club's shootout goals, or null when there was no shootout.</param>
/// <param name="AwayShootoutGoals">The away club's shootout goals, or null when there was no shootout.</param>
public sealed record SeasonMatch(
    int Id,
    int? CompetitionId,
    DateTime Kickoff,
    bool IsFinished,
    byte? Stage,
    byte? RoundNumber,
    byte? Leg,
    int? HomeLeagueTeamId,
    int? AwayLeagueTeamId,
    int HomeGoals,
    int AwayGoals,
    int? HomeShootoutGoals,
    int? AwayShootoutGoals)
{
    /// <summary>
    /// Gets the league team id of the club that won the match, or null when there is no winner. A match has a winner
    /// only when it is finished and both clubs are known. The club with more goals wins; when the goals are level, the
    /// club with more shootout goals wins, provided both shootout values are present and differ.
    /// </summary>
    public int? WinnerLeagueTeamId
    {
        get
        {
            if (!IsFinished || HomeLeagueTeamId is not { } home || AwayLeagueTeamId is not { } away)
            {
                return null;
            }

            if (HomeGoals != AwayGoals)
            {
                return HomeGoals > AwayGoals ? home : away;
            }

            if (HomeShootoutGoals is not { } homeShootout
                || AwayShootoutGoals is not { } awayShootout
                || homeShootout == awayShootout)
            {
                return null;
            }

            return homeShootout > awayShootout ? home : away;
        }
    }
}
