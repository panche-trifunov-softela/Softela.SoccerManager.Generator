namespace Softela.SoccerManager.Generator.Application.Services.Ranking;

/// <summary>
/// One participant's totals over a set of matches.
/// </summary>
/// <param name="Id">The id of the participant.</param>
/// <param name="Played">The number of matches counted.</param>
/// <param name="Wins">The number of matches won.</param>
/// <param name="Draws">The number of matches drawn.</param>
/// <param name="Losses">The number of matches lost.</param>
/// <param name="GoalsFor">The goals scored.</param>
/// <param name="GoalsAgainst">The goals conceded.</param>
/// <param name="Points">The points: 3 for a win and 1 for a draw.</param>
internal sealed record TableTotals(
    int Id,
    int Played,
    int Wins,
    int Draws,
    int Losses,
    int GoalsFor,
    int GoalsAgainst,
    int Points)
{
    /// <summary>
    /// The goals scored minus the goals conceded.
    /// </summary>
    internal int GoalDifference => GoalsFor - GoalsAgainst;
}
