namespace Softela.SoccerManager.Generator.Application.Services.Playoffs;

/// <summary>
/// What a division's matches and Standings say about its promotion playoff. The status decides which members are set:
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="PlayoffReadingStatus.WaitingForLeague"/>: the counts and the final's winner, which describe the written
/// playoff matches; the lists are empty and there is no newest Standings change.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="PlayoffReadingStatus.WaitingForStandings"/>: as above, with the time of the newest Standings change set.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="PlayoffReadingStatus.Ready"/>: as above, with the table members set as well, the ranked clubs and the
/// first round; the new ties may be empty; the latest semi-final kickoff is set only when the final is among the new
/// ties.
/// </description>
/// </item>
/// </list>
/// </summary>
/// <param name="Status">How far the playoff has got.</param>
/// <param name="FinishedLeagueMatches">The number of league matches of the division that are finished.</param>
/// <param name="LeagueMatches">The number of league matches the division has.</param>
/// <param name="NewestStandingsChange">
/// The time of the newest change to the division's Standings rows, in UTC; null when the status is waiting for the
/// league, or when the division has no Standings rows.
/// </param>
/// <param name="RankedLeagueTeamIds">The league team ids in table order, place 1 first; empty unless ready.</param>
/// <param name="FirstRound">
/// The ties the table gives the playoff to start with: two semi-finals for a four-club playoff, the final for a
/// two-club one; empty unless ready.
/// </param>
/// <param name="WrittenMatches">The number of playoff matches that are written.</param>
/// <param name="DecidedMatches">The number of written playoff matches that have a winner.</param>
/// <param name="FinalWinnerLeagueTeamId">
/// The league team id of the final's winner, which is the club promoted by the playoff; null while the final is not
/// written or not decided.
/// </param>
/// <param name="NewTies">
/// The ties whose two clubs are known and that have no match yet, in stage order and then tie order; empty unless
/// ready.
/// </param>
/// <param name="LatestSemiFinalKickoff">
/// The later kickoff of the two written semi-finals, in UTC, when the final is among the new ties; otherwise null.
/// </param>
public sealed record PlayoffReading(
    PlayoffReadingStatus Status,
    int FinishedLeagueMatches,
    int LeagueMatches,
    DateTime? NewestStandingsChange,
    IReadOnlyList<int> RankedLeagueTeamIds,
    IReadOnlyList<PlayoffTie> FirstRound,
    int WrittenMatches,
    int DecidedMatches,
    int? FinalWinnerLeagueTeamId,
    IReadOnlyList<PlayoffTie> NewTies,
    DateTime? LatestSemiFinalKickoff);
