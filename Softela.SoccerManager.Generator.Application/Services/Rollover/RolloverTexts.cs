namespace Softela.SoccerManager.Generator.Application.Services.Rollover;

/// <summary>
/// The texts of the season rollover's waits and refusals that more than one of its readers use. A season that has ended
/// is never reset, so no fix of a rollover refusal is a reset.
/// </summary>
internal static class RolloverTexts
{
    /// <summary>
    /// The fix that a refusal about the league's matches, dates or Standings offers besides fixing them by hand, after
    /// the word "or".
    /// </summary>
    internal const string OtherFix =
        "make the next season by hand (resetting a season that has ended would delete its results)";

    /// <summary>
    /// The fix, as a sentence, that a refusal of a table's ranking ends with.
    /// </summary>
    internal const string RankFix =
        "Fix the matches, or make the next season by hand "
        + "(resetting a season that has ended would delete its results).";

    /// <summary>
    /// The fix, as a sentence, that the refusal of a division with no league matches ends with.
    /// </summary>
    internal const string MissingDivisionFix =
        "Give it Order 0 if that tier is gone, or make the next season by hand "
        + "(resetting a season that has ended would delete its results).";

    /// <summary>
    /// What a season waits for while it has no league matches.
    /// </summary>
    internal const string LeagueMatchesNotWritten = "the season's league matches, which are not written yet";
}
