namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// The third-place match of the World Cup finals: the two semi-final losers, once both semi-finals are decided.
/// </summary>
/// <param name="HomeLeagueTeamId">The league team id of the loser of semi-final 1, who is the home nation.</param>
/// <param name="AwayLeagueTeamId">The league team id of the loser of semi-final 2, who is the away nation.</param>
/// <param name="Kickoff">The kickoff of the written match, in UTC, or null when it is not written yet.</param>
/// <param name="IsFinished">
/// Whether the written match is finished; the reader refuses a finished one with no winner. False while the match is
/// not written.
/// </param>
internal sealed record FinalsThirdPlace(int HomeLeagueTeamId, int AwayLeagueTeamId, DateTime? Kickoff, bool IsFinished)
{
    /// <summary>
    /// Gets a value that says whether the third-place match is written.
    /// </summary>
    public bool IsWritten => Kickoff.HasValue;
}
