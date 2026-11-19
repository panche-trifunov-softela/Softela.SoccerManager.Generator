namespace Softela.SoccerManager.Generator.Application.Services.Seasons;

/// <summary>
/// The stage of a staged match, as the matches table stores it. It mirrors the backend's <c>MatchStage</c> enum
/// (<c>SoccerManager.Domain\Enums\MatchStage.cs</c>), and lists every stage the generator writes.
/// </summary>
internal enum MatchStage : byte
{
    /// <summary>
    /// A round of a domestic cup: a single match, and the match's round number says which round it is.
    /// </summary>
    DomesticCupRound = 1,

    /// <summary>
    /// A semi-final of a division's promotion playoff: a single match, its round number and its leg are null. The value
    /// is the backend's <c>MatchStage.PromotionPlayoffSemiFinal</c>.
    /// </summary>
    PromotionPlayoffSemiFinal = 2,

    /// <summary>
    /// The final of a division's promotion playoff: a single match, its round number and its leg are null. The value is
    /// the backend's <c>MatchStage.PromotionPlayoffFinal</c>.
    /// </summary>
    PromotionPlayoffFinal = 3,

    /// <summary>
    /// A match of a continental cup's league phase: a single match, its round number is the matchday and its leg is
    /// null. The value is the backend's <c>MatchStage.ContinentalLeaguePhase</c>.
    /// </summary>
    ContinentalLeaguePhase = 4,

    /// <summary>
    /// A tie of a continental cup's play-off, played over two legs: its leg is 1 or 2 and its round number is null. The
    /// value is the backend's <c>MatchStage.ContinentalKnockoutPlayOff</c>.
    /// </summary>
    ContinentalKnockoutPlayOff = 5,

    /// <summary>
    /// A tie of a continental cup's round of 16, played over two legs: its leg is 1 or 2 and its round number is null.
    /// The value is the backend's <c>MatchStage.ContinentalRoundOf16</c>.
    /// </summary>
    ContinentalRoundOf16 = 6,

    /// <summary>
    /// A tie of a continental cup's quarter-final, played over two legs: its leg is 1 or 2 and its round number is
    /// null. The value is the backend's <c>MatchStage.ContinentalQuarterFinal</c>.
    /// </summary>
    ContinentalQuarterFinal = 7,

    /// <summary>
    /// A tie of a continental cup's semi-final, played over two legs: its leg is 1 or 2 and its round number is null.
    /// The value is the backend's <c>MatchStage.ContinentalSemiFinal</c>.
    /// </summary>
    ContinentalSemiFinal = 8,

    /// <summary>
    /// The final of a continental cup: a single match, its leg and its round number are null. The value is the
    /// backend's <c>MatchStage.ContinentalFinal</c>.
    /// </summary>
    ContinentalFinal = 9,

    /// <summary>
    /// A match of a World Cup qualifier group: a single match, its round number is the matchday, its leg is null and
    /// its group number is set. The value is the backend's <c>MatchStage.WorldCupQualifierGroup</c>.
    /// </summary>
    WorldCupQualifierGroup = 10,

    /// <summary>
    /// A match of a World Cup finals group: a single match, its round number is the matchday, its leg is null and its
    /// group number is set. The value is the backend's <c>MatchStage.WorldCupGroup</c>.
    /// </summary>
    WorldCupGroup = 11,

    /// <summary>
    /// A match of the World Cup finals' round of 16: a single match, its round number, its leg and its group number are
    /// null. The value is the backend's <c>MatchStage.WorldCupRoundOf16</c>.
    /// </summary>
    WorldCupRoundOf16 = 12,

    /// <summary>
    /// A quarter-final of the World Cup finals: a single match, its round number, its leg and its group number are
    /// null. The value is the backend's <c>MatchStage.WorldCupQuarterFinal</c>.
    /// </summary>
    WorldCupQuarterFinal = 13,

    /// <summary>
    /// A semi-final of the World Cup finals: a single match, its round number, its leg and its group number are null.
    /// The value is the backend's <c>MatchStage.WorldCupSemiFinal</c>.
    /// </summary>
    WorldCupSemiFinal = 14,

    /// <summary>
    /// The World Cup third-place match: a single match, its round number, its leg and its group number are null. The
    /// value is the backend's <c>MatchStage.WorldCupThirdPlace</c>.
    /// </summary>
    WorldCupThirdPlace = 15,

    /// <summary>
    /// The World Cup final: a single match, its round number, its leg and its group number are null. The value is the
    /// backend's <c>MatchStage.WorldCupFinal</c>.
    /// </summary>
    WorldCupFinal = 16,
}
