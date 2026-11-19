namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// What the written qualifier matches of a season say: the groups they form and how many of the matches are finished.
/// </summary>
/// <param name="Groups">The groups, in ascending order of group number.</param>
/// <param name="FinishedMatches">The number of qualifier matches that are finished.</param>
/// <param name="TotalMatches">The number of qualifier matches written.</param>
internal sealed record QualifierGroupReading(
    IReadOnlyList<WrittenQualifierGroup> Groups,
    int FinishedMatches,
    int TotalMatches);
