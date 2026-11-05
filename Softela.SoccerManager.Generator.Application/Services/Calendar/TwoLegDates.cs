namespace Softela.SoccerManager.Generator.Application.Services.Calendar;

/// <summary>
/// The kickoffs of a tie that is played over two legs.
/// </summary>
/// <param name="FirstLeg">The kickoff of the first leg, in UTC.</param>
/// <param name="SecondLeg">The kickoff of the second leg, in UTC; it is after the first leg.</param>
public sealed record TwoLegDates(DateTime FirstLeg, DateTime SecondLeg);
