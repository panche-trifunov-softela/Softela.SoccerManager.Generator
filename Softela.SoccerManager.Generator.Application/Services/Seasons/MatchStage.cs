namespace Softela.SoccerManager.Generator.Application.Services.Seasons;

/// <summary>
/// The stage of a staged match, as the matches table stores it. It mirrors the backend's <c>MatchStage</c> enum
/// (<c>SoccerManager.Domain\Enums\MatchStage.cs</c>), and lists only the stages the generator writes.
/// </summary>
internal enum MatchStage : byte
{
    /// <summary>
    /// A round of a domestic cup: a single match, and the match's round number says which round it is.
    /// </summary>
    DomesticCupRound = 1,
}
