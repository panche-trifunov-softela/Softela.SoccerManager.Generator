namespace Softela.SoccerManager.Generator.Application.Services.DomesticCup;

/// <summary>
/// What the cup step should do on this tick.
/// </summary>
/// <param name="Status">What the plan asks for.</param>
/// <param name="Ties">
/// For <see cref="DomesticCupPlanStatus.Write"/>, the ties to insert in round order and then tie order. Empty for the
/// other statuses.
/// </param>
/// <param name="ByeLeagueTeamIds">
/// The clubs that skip round 1, in ascending id, when this plan starts the cup; empty otherwise.
/// </param>
/// <param name="RoundTwoDate">
/// For <see cref="DomesticCupPlanStatus.PlaysWithoutCup"/>, the calendar date of round 2; null otherwise.
/// </param>
public sealed record DomesticCupPlan(
    DomesticCupPlanStatus Status,
    IReadOnlyList<DomesticCupPlannedTie> Ties,
    IReadOnlyList<int> ByeLeagueTeamIds,
    DateTime? RoundTwoDate);
