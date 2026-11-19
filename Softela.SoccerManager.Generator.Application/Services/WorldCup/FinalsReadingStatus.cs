namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// How far the World Cup finals have got, as the finals' written matches show it.
/// </summary>
internal enum FinalsReadingStatus
{
    /// <summary>
    /// Some group match is not finished and no knockout match is written, so the group tables are not final.
    /// </summary>
    WaitingForGroups,

    /// <summary>
    /// Every group match is finished; the group tables, the bracket and the knockout's progress are read.
    /// </summary>
    Ready,
}
