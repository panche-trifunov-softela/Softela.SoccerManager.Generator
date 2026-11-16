using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.Playoffs;

/// <summary>
/// The match stages of a promotion playoff as the matches table stores them, and their names for refusal messages. The
/// stages are bytes so that this public class does not expose the internal <c>MatchStage</c> enum.
/// </summary>
public static class PlayoffStages
{
    /// <summary>
    /// The stage of a semi-final: a single match, with no round number and no leg.
    /// </summary>
    public const byte SemiFinal = (byte)MatchStage.PromotionPlayoffSemiFinal;

    /// <summary>
    /// The stage of the final: a single match, with no round number and no leg.
    /// </summary>
    public const byte Final = (byte)MatchStage.PromotionPlayoffFinal;

    /// <summary>
    /// Gets the name of a playoff stage as a refusal message writes it, in lower case.
    /// </summary>
    /// <param name="stage">The stage code of a playoff match.</param>
    /// <returns>"semi-final" or "final".</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="stage"/> is neither <see cref="SemiFinal"/> nor <see cref="Final"/>.
    /// </exception>
    public static string NameOf(byte stage)
    {
        return stage switch
        {
            SemiFinal => "semi-final",
            Final => "final",
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "The stage is not a playoff stage."),
        };
    }
}
