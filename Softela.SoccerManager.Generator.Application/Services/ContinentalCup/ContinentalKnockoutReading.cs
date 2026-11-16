namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// What one continental cup's written matches say about its knockout. The status decides which members are set:
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="ContinentalKnockoutReadingStatus.WaitingForLeaguePhase"/>: the two counts only; no bracket, no written
/// ties, no winners, no new ties and no last league-phase kickoff.
/// </description>
/// </item>
/// <item>
/// <description><see cref="ContinentalKnockoutReadingStatus.Ready"/>: every member is set.</description>
/// </item>
/// </list>
/// </summary>
/// <param name="Status">How far the knockout has got.</param>
/// <param name="FinishedLeaguePhaseMatches">The number of league-phase matches that are finished.</param>
/// <param name="LeaguePhaseMatches">The number of league-phase matches the cup has.</param>
/// <param name="Bracket">The knockout bracket built from the finished league-phase table.</param>
/// <param name="WrittenTies">The ties that have matches, in round order and then tie order.</param>
/// <param name="Winners">The decided ties with their winners, in round order and then tie order.</param>
/// <param name="NewTies">
/// The ties whose two clubs are known and that have no match yet, in round order and then tie order.
/// </param>
/// <param name="LastLeaguePhaseKickoff">The latest kickoff of the league phase, in UTC.</param>
public sealed record ContinentalKnockoutReading(
    ContinentalKnockoutReadingStatus Status,
    int FinishedLeaguePhaseMatches,
    int LeaguePhaseMatches,
    ContinentalBracket? Bracket,
    IReadOnlyList<ContinentalWrittenTie> WrittenTies,
    IReadOnlyList<ContinentalWinner> Winners,
    IReadOnlyList<ContinentalKnownTie> NewTies,
    DateTime? LastLeaguePhaseKickoff);
