namespace Softela.SoccerManager.Generator.Application.Models;

/// <summary>
/// One row returned by the <c>dbo.GetMatchScoresBySeason</c> stored procedure: the score of one club in one match. A
/// match has a row per club only when it has match statistic rows, and an unplayed match reads 0-0, so the
/// <see cref="GetMatchesBySeasonResult.IsFinished"/> of the season read decides whether a score is a result. It holds
/// only the columns the generator reads; Dapper ignores the others.
/// </summary>
public sealed class GetMatchScoresBySeasonResult
{
    /// <summary>
    /// The identifier of the match.
    /// </summary>
    public int MatchId { get; set; }

    /// <summary>
    /// The identifier of the league team, the roster entry, the score belongs to.
    /// </summary>
    public int LeagueTeamId { get; set; }

    /// <summary>
    /// Whether the club plays at home in the match.
    /// </summary>
    public bool IsHomeTeam { get; set; }

    /// <summary>
    /// The goals of the club: those of its players plus the own goals of the opponents. It is never null.
    /// </summary>
    public int Goals { get; set; }

    /// <summary>
    /// The shootout kicks the club scored, or <see langword="null"/> when the match has no shootout kicks at all; it
    /// is 0 when the match has kicks and the club scored none.
    /// </summary>
    public int? ShootoutGoals { get; set; }
}
