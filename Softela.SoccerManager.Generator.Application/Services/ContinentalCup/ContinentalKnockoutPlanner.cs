using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Services.Calendar;
using Softela.SoccerManager.Generator.Application.Services.DomesticCup;
using Softela.SoccerManager.Generator.Application.Services.Knockout;

namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// Plans the next matches of a continental cup's knockout from what the written matches say about it. A tie is planned
/// as soon as both of its clubs are known: the club placed lower in the league phase hosts the first leg and the club
/// placed higher hosts the second leg, and the final is one match at the higher club.
/// </summary>
public static class ContinentalKnockoutPlanner
{
    /// <summary>
    /// Plans the matches to write on this tick. Every tie that has both clubs known and no match gets its dates from
    /// <see cref="MatchDateRule.Pick"/>, from its round's date in the calendar: later than the matches of the ties that
    /// lead to it (the league phase for the first round), at least 24 hours from every other match of both clubs, and
    /// for a second leg later than its first leg. Every planned kickoff is added to the clubs' kickoffs, so the ties
    /// planned after it keep their distance.
    /// </summary>
    /// <param name="reading">What the cup's matches say about its knockout; its status must be ready.</param>
    /// <param name="dates">The dates of the cup, from its calendar.</param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <param name="clubKickoffs">
    /// The kickoffs of every match of each club in the season, by league team id; the planned matches are added to it.
    /// </param>
    /// <returns>The matches to write, in tie order, the first leg before the second leg.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="reading"/>, <paramref name="dates"/> or <paramref name="clubKickoffs"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="runTime"/> is not in UTC or the reading is not ready.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the reading lacks a member its status promises, a feeder tie has no written match, or the calendar
    /// has no date for a round of the bracket, which is a bug.
    /// </exception>
    /// <exception cref="SeasonRefusedException">Thrown when no date fits a tie.</exception>
    public static IReadOnlyList<ContinentalPlannedLeg> Plan(
        ContinentalKnockoutReading reading,
        ContinentalCupDates dates,
        DateTime runTime,
        Dictionary<int, List<DateTime>> clubKickoffs)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(dates);
        ArgumentNullException.ThrowIfNull(clubKickoffs);

        if (runTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The run time must be in UTC.", nameof(runTime));
        }

        if (reading.Status != ContinentalKnockoutReadingStatus.Ready)
        {
            throw new ArgumentException(
                "The knockout can only be planned once its league phase is finished.",
                nameof(reading));
        }

        ContinentalBracket bracket = reading.Bracket
            ?? throw new InvalidOperationException("The reading has no bracket, which its status promises.");

        List<ContinentalPlannedLeg> legs = new();

        foreach (ContinentalKnownTie tie in reading.NewTies)
        {
            DateTime? after = EarliestAfter(reading, bracket, tie);

            if (tie.Legs == 1)
            {
                legs.Add(PlanFinal(dates, runTime, after, tie, clubKickoffs));

                continue;
            }

            legs.AddRange(PlanTwoLegs(dates, runTime, after, tie, clubKickoffs));
        }

        return legs;
    }

    /// <summary>
    /// Finds the date a tie must follow: the end of the league phase for a tie of the first round, otherwise the later
    /// kickoff of the latest tie that feeds it.
    /// </summary>
    /// <param name="reading">The reading of a cup that is ready.</param>
    /// <param name="bracket">The bracket.</param>
    /// <param name="tie">The tie to date.</param>
    /// <returns>The date the tie must follow.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the tie's round is not in the bracket, or a feeder tie has no written match, which is a bug.
    /// </exception>
    private static DateTime? EarliestAfter(
        ContinentalKnockoutReading reading,
        ContinentalBracket bracket,
        ContinentalKnownTie tie)
    {
        int clubCount = bracket.Table.Count;

        if (!ContinentalBracketShape.TryGetRoundNumber(clubCount, tie.Round, out int roundNumber))
        {
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The bracket of {clubCount} clubs has no {ContinentalKnockoutStages.NameOf(tie.Round)}."));
        }

        if (roundNumber == 1)
        {
            return reading.LastLeaguePhaseKickoff;
        }

        KnockoutTie shape = bracket.Knockout.Rounds[roundNumber - 1][tie.TieNumber - 1];
        DateTime? latest = null;

        foreach (KnockoutSide side in new[] { shape.First, shape.Second })
        {
            if (side.IsClub)
            {
                continue;
            }

            ContinentalRound feederRound = ContinentalBracketShape.RoundOf(clubCount, side.WinnerOfRoundNumber!.Value);

            ContinentalWrittenTie feeder = reading.WrittenTies.FirstOrDefault(written =>
                written.Round == feederRound && written.TieNumber == side.WinnerOfTieNumber)
                ?? throw new InvalidOperationException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Tie {side.WinnerOfTieNumber} of the {ContinentalKnockoutStages.NameOf(feederRound)} has no ")
                    + "written match.");

            if (latest is null || feeder.LastKickoff > latest)
            {
                latest = feeder.LastKickoff;
            }
        }

        return latest;
    }

    /// <summary>
    /// Plans the final: one match, hosted by the club placed higher in the league phase.
    /// </summary>
    /// <param name="dates">The dates of the cup.</param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <param name="after">The date the final must follow.</param>
    /// <param name="tie">The final.</param>
    /// <param name="clubKickoffs">The kickoffs of each club's matches; the final is added to it.</param>
    /// <returns>The planned match.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when no date fits the final.</exception>
    private static ContinentalPlannedLeg PlanFinal(
        ContinentalCupDates dates,
        DateTime runTime,
        DateTime? after,
        ContinentalKnownTie tie,
        Dictionary<int, List<DateTime>> clubKickoffs)
    {
        List<DateTime> kickoffs = DomesticCupPlanIndexes.KickoffsOf(
            clubKickoffs,
            tie.HostLeagueTeamId,
            tie.OtherLeagueTeamId);

        DateTime kickoff = MatchDateRule.Pick(dates.Final, runTime, after, null, kickoffs) ?? throw NoDate(tie);

        AddKickoff(clubKickoffs, tie, kickoff);

        return new ContinentalPlannedLeg(
            kickoff,
            tie.Round,
            tie.TieNumber,
            null,
            tie.HostLeagueTeamId,
            tie.OtherLeagueTeamId);
    }

    /// <summary>
    /// Plans the two legs of a tie: the first leg at the club placed lower in the league phase and the second leg,
    /// later, at the club placed higher.
    /// </summary>
    /// <param name="dates">The dates of the cup.</param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <param name="after">The date the first leg must follow.</param>
    /// <param name="tie">The tie.</param>
    /// <param name="clubKickoffs">The kickoffs of each club's matches; both legs are added to it.</param>
    /// <returns>The first leg and then the second leg.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the calendar has no dates for the tie's round, which is a bug.
    /// </exception>
    /// <exception cref="SeasonRefusedException">Thrown when no date fits a leg.</exception>
    private static List<ContinentalPlannedLeg> PlanTwoLegs(
        ContinentalCupDates dates,
        DateTime runTime,
        DateTime? after,
        ContinentalKnownTie tie,
        Dictionary<int, List<DateTime>> clubKickoffs)
    {
        TwoLegDates roundDates = TwoLegDatesOf(dates, tie.Round)
            ?? throw new InvalidOperationException(
                $"The calendar has no dates for the {ContinentalKnockoutStages.NameOf(tie.Round)}.");

        DateTime firstKickoff = MatchDateRule.Pick(
            roundDates.FirstLeg,
            runTime,
            after,
            null,
            DomesticCupPlanIndexes.KickoffsOf(clubKickoffs, tie.HostLeagueTeamId, tie.OtherLeagueTeamId))
            ?? throw NoDate(tie);

        AddKickoff(clubKickoffs, tie, firstKickoff);

        DateTime secondKickoff = MatchDateRule.Pick(
            roundDates.SecondLeg,
            runTime,
            firstKickoff,
            null,
            DomesticCupPlanIndexes.KickoffsOf(clubKickoffs, tie.HostLeagueTeamId, tie.OtherLeagueTeamId))
            ?? throw NoDate(tie);

        AddKickoff(clubKickoffs, tie, secondKickoff);

        return
        [
            new ContinentalPlannedLeg(
                firstKickoff,
                tie.Round,
                tie.TieNumber,
                1,
                tie.OtherLeagueTeamId,
                tie.HostLeagueTeamId),
            new ContinentalPlannedLeg(
                secondKickoff,
                tie.Round,
                tie.TieNumber,
                2,
                tie.HostLeagueTeamId,
                tie.OtherLeagueTeamId),
        ];
    }

    /// <summary>
    /// Gets the dates of a two-legged round.
    /// </summary>
    /// <param name="dates">The dates of the cup.</param>
    /// <param name="round">The round.</param>
    /// <returns>The round's two dates, or null when the cup has none for it, as for the final.</returns>
    private static TwoLegDates? TwoLegDatesOf(ContinentalCupDates dates, ContinentalRound round)
    {
        return round switch
        {
            ContinentalRound.PlayOff => dates.PlayOff,
            ContinentalRound.RoundOf16 => dates.RoundOf16,
            ContinentalRound.QuarterFinal => dates.QuarterFinal,
            ContinentalRound.SemiFinal => dates.SemiFinal,
            _ => null,
        };
    }

    /// <summary>
    /// Records a planned kickoff against both clubs of a tie.
    /// </summary>
    /// <param name="clubKickoffs">The kickoffs of each club's matches.</param>
    /// <param name="tie">The tie.</param>
    /// <param name="kickoff">The kickoff to record.</param>
    private static void AddKickoff(
        Dictionary<int, List<DateTime>> clubKickoffs,
        ContinentalKnownTie tie,
        DateTime kickoff)
    {
        DomesticCupPlanIndexes.AddKickoff(clubKickoffs, tie.HostLeagueTeamId, kickoff);
        DomesticCupPlanIndexes.AddKickoff(clubKickoffs, tie.OtherLeagueTeamId, kickoff);
    }

    /// <summary>
    /// Builds the refusal for a tie that no Monday within a year suits.
    /// </summary>
    /// <param name="tie">The tie that has no date.</param>
    /// <returns>The refusal, ready to throw.</returns>
    private static SeasonRefusedException NoDate(ContinentalKnownTie tie)
    {
        return new SeasonRefusedException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"No Monday within a year fits the {ContinentalKnockoutStages.NameOf(tie.Round)} tie {tie.TieNumber} ")
            + string.Create(
                CultureInfo.InvariantCulture,
                $"of club {tie.HostLeagueTeamId} and club {tie.OtherLeagueTeamId}. Move the matches of either club ")
            + "that are in the way.");
    }
}
