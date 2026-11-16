using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// Maps the knockout rounds of a continental cup to the match stages the matches table stores, and back, and names the
/// rounds for refusal messages.
/// </summary>
public static class ContinentalKnockoutStages
{
    /// <summary>
    /// Gets the match stage of a knockout round: 5 for the play-off up to 9 for the final.
    /// </summary>
    /// <param name="round">The round.</param>
    /// <returns>The stage code the round's matches are stored with.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="round"/> is not a defined round.
    /// </exception>
    public static byte StageOf(ContinentalRound round)
    {
        return round switch
        {
            ContinentalRound.PlayOff => (byte)MatchStage.ContinentalKnockoutPlayOff,
            ContinentalRound.RoundOf16 => (byte)MatchStage.ContinentalRoundOf16,
            ContinentalRound.QuarterFinal => (byte)MatchStage.ContinentalQuarterFinal,
            ContinentalRound.SemiFinal => (byte)MatchStage.ContinentalSemiFinal,
            ContinentalRound.Final => (byte)MatchStage.ContinentalFinal,
            _ => throw new ArgumentOutOfRangeException(nameof(round), round, "The round is not a continental round."),
        };
    }

    /// <summary>
    /// Gets the name of a knockout round as a refusal message writes it, in lower case.
    /// </summary>
    /// <param name="round">The round.</param>
    /// <returns>"play-off", "round of 16", "quarter-final", "semi-final" or "final".</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="round"/> is not a defined round.
    /// </exception>
    internal static string NameOf(ContinentalRound round)
    {
        return round switch
        {
            ContinentalRound.PlayOff => "play-off",
            ContinentalRound.RoundOf16 => "round of 16",
            ContinentalRound.QuarterFinal => "quarter-final",
            ContinentalRound.SemiFinal => "semi-final",
            ContinentalRound.Final => "final",
            _ => throw new ArgumentOutOfRangeException(nameof(round), round, "The round is not a continental round."),
        };
    }

    /// <summary>
    /// Finds the knockout round that a match stage stores.
    /// </summary>
    /// <param name="stage">The stage code of a match.</param>
    /// <param name="round">The round, or the play-off when the stage is no knockout stage.</param>
    /// <returns>True for the stages 5 to 9; false for any other stage, the league phase's 4 included.</returns>
    public static bool TryGetRound(byte stage, out ContinentalRound round)
    {
        foreach (ContinentalRound candidate in Enum.GetValues<ContinentalRound>())
        {
            if (StageOf(candidate) == stage)
            {
                round = candidate;

                return true;
            }
        }

        round = default;

        return false;
    }
}
