namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// The knockout rounds of the World Cup finals. The backend stores them as match stages 12 (round of 16) to 16 (final).
/// </summary>
internal enum FinalsRound
{
    /// <summary>
    /// The round of 16: the group winners and runners-up, in eight single matches.
    /// </summary>
    RoundOf16,

    /// <summary>
    /// The quarter-final, in four single matches.
    /// </summary>
    QuarterFinal,

    /// <summary>
    /// The semi-final, in two single matches.
    /// </summary>
    SemiFinal,

    /// <summary>
    /// The third-place match between the two semi-final losers. It is not a round of the bracket.
    /// </summary>
    ThirdPlace,

    /// <summary>
    /// The final, played as one match.
    /// </summary>
    Final,
}
