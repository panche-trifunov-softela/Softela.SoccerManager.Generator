namespace Softela.SoccerManager.Generator.Application.Services.Ranking;

/// <summary>
/// One row of a ranked table: a participant's place and its totals over the matches of the table.
/// </summary>
/// <param name="Place">The place in the table, counted from 1.</param>
/// <param name="Id">The id of the participant.</param>
/// <param name="Played">The number of matches the participant played.</param>
/// <param name="Wins">The number of matches the participant won.</param>
/// <param name="Draws">The number of matches the participant drew.</param>
/// <param name="Losses">The number of matches the participant lost.</param>
/// <param name="GoalsFor">The goals the participant scored.</param>
/// <param name="GoalsAgainst">The goals the participant conceded.</param>
/// <param name="Points">The points: 3 for a win and 1 for a draw.</param>
/// <param name="ShuffledFromPlace">
/// The first place of the participants that the seeded shuffle ordered together, this participant included, or null
/// when the shuffle did not set this participant's place. Two adjacent rows were separated by the shuffle exactly when
/// both have the same value that is not null.
/// </param>
public sealed record TableRow(
    int Place,
    int Id,
    int Played,
    int Wins,
    int Draws,
    int Losses,
    int GoalsFor,
    int GoalsAgainst,
    int Points,
    int? ShuffledFromPlace)
{
    /// <summary>
    /// The goals scored minus the goals conceded.
    /// </summary>
    public int GoalDifference => GoalsFor - GoalsAgainst;
}
