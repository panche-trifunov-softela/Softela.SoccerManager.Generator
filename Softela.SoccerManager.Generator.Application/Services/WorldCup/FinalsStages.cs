using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// Maps the knockout rounds of the World Cup finals to the match stages the matches table stores, to the number of
/// their round in the bracket and to the finals day they are planned on, and names the rounds for log and refusal
/// messages.
/// </summary>
internal static class FinalsStages
{
    /// <summary>
    /// The stage of a finals group match as <see cref="SeasonMatch.Stage"/> holds it.
    /// </summary>
    internal const byte GroupStage = (byte)MatchStage.WorldCupGroup;

    /// <summary>
    /// The stage of the third-place match as <see cref="SeasonMatch.Stage"/> holds it.
    /// </summary>
    internal const byte ThirdPlaceStage = (byte)MatchStage.WorldCupThirdPlace;

    /// <summary>
    /// Gets the match stage of a knockout round: 12 for the round of 16 up to 16 for the final.
    /// </summary>
    /// <param name="round">The round.</param>
    /// <returns>The stage code the round's matches are stored with.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="round"/> is not a defined round.
    /// </exception>
    internal static byte StageOf(FinalsRound round)
    {
        return round switch
        {
            FinalsRound.RoundOf16 => (byte)MatchStage.WorldCupRoundOf16,
            FinalsRound.QuarterFinal => (byte)MatchStage.WorldCupQuarterFinal,
            FinalsRound.SemiFinal => (byte)MatchStage.WorldCupSemiFinal,
            FinalsRound.ThirdPlace => (byte)MatchStage.WorldCupThirdPlace,
            FinalsRound.Final => (byte)MatchStage.WorldCupFinal,
            _ => throw new ArgumentOutOfRangeException(nameof(round), round, "The round is not a finals round."),
        };
    }

    /// <summary>
    /// Gets the knockout round that a match stage stores.
    /// </summary>
    /// <param name="stage">The stage code of a match; 12 to 16.</param>
    /// <returns>The round.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="stage"/> is not a knockout stage of the finals.
    /// </exception>
    internal static FinalsRound RoundOf(byte stage)
    {
        return stage switch
        {
            (byte)MatchStage.WorldCupRoundOf16 => FinalsRound.RoundOf16,
            (byte)MatchStage.WorldCupQuarterFinal => FinalsRound.QuarterFinal,
            (byte)MatchStage.WorldCupSemiFinal => FinalsRound.SemiFinal,
            (byte)MatchStage.WorldCupThirdPlace => FinalsRound.ThirdPlace,
            (byte)MatchStage.WorldCupFinal => FinalsRound.Final,
            _ => throw new ArgumentOutOfRangeException(
                nameof(stage),
                stage,
                "The stage is not a knockout stage of the World Cup finals."),
        };
    }

    /// <summary>
    /// Tells whether a stage code belongs to the World Cup finals: the group matches and the knockout.
    /// </summary>
    /// <param name="stage">The stage code of a match.</param>
    /// <returns>True for the stages 11 to 16.</returns>
    internal static bool IsFinalsStage(byte stage)
    {
        return stage is >= (byte)MatchStage.WorldCupGroup and <= (byte)MatchStage.WorldCupFinal;
    }

    /// <summary>
    /// Gets the name of a knockout round as a log line or a refusal writes it, in lower case.
    /// </summary>
    /// <param name="round">The round.</param>
    /// <returns>
    /// "round of 16", "quarter-final", "semi-final", "third-place match" or "final".
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="round"/> is not a defined round.
    /// </exception>
    internal static string NameOf(FinalsRound round)
    {
        return round switch
        {
            FinalsRound.RoundOf16 => "round of 16",
            FinalsRound.QuarterFinal => "quarter-final",
            FinalsRound.SemiFinal => "semi-final",
            FinalsRound.ThirdPlace => "third-place match",
            FinalsRound.Final => "final",
            _ => throw new ArgumentOutOfRangeException(nameof(round), round, "The round is not a finals round."),
        };
    }

    /// <summary>
    /// Gets the finals day a round is planned on, counted from 1: the three group matchdays take days 1 to 3.
    /// </summary>
    /// <param name="round">The round.</param>
    /// <returns>
    /// 4 for the round of 16, 5 for the quarter-final, and 6, 7 and 8 for the semi-final, third place and final.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="round"/> is not a defined round.
    /// </exception>
    internal static int DayOf(FinalsRound round)
    {
        return round switch
        {
            FinalsRound.RoundOf16 => 4,
            FinalsRound.QuarterFinal => 5,
            FinalsRound.SemiFinal => 6,
            FinalsRound.ThirdPlace => 7,
            FinalsRound.Final => 8,
            _ => throw new ArgumentOutOfRangeException(nameof(round), round, "The round is not a finals round."),
        };
    }

    /// <summary>
    /// Gets the number of a round in the bracket, counted from 1. The third place is no round of the bracket.
    /// </summary>
    /// <param name="round">The round.</param>
    /// <returns>1 for the round of 16, 2 for the quarter-final, 3 for the semi-final and 4 for the final.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="round"/> is the third place or not a defined round.
    /// </exception>
    internal static int BracketNumberOf(FinalsRound round)
    {
        return round switch
        {
            FinalsRound.RoundOf16 => 1,
            FinalsRound.QuarterFinal => 2,
            FinalsRound.SemiFinal => 3,
            FinalsRound.Final => 4,
            _ => throw new ArgumentOutOfRangeException(
                nameof(round),
                round,
                "The round is not a round of the bracket."),
        };
    }

    /// <summary>
    /// Gets the round that has a number in the bracket.
    /// </summary>
    /// <param name="roundNumber">The 1-based number of the round in the bracket; 1 to 4.</param>
    /// <returns>The round.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the bracket has no round with the number.
    /// </exception>
    internal static FinalsRound FromBracketNumber(int roundNumber)
    {
        return roundNumber switch
        {
            1 => FinalsRound.RoundOf16,
            2 => FinalsRound.QuarterFinal,
            3 => FinalsRound.SemiFinal,
            4 => FinalsRound.Final,
            _ => throw new ArgumentOutOfRangeException(
                nameof(roundNumber),
                roundNumber,
                "The bracket has no round with that number."),
        };
    }
}
