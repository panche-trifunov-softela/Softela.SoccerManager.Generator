using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Services.Calendar;
using Softela.SoccerManager.Generator.Application.Services.Knockout;

namespace Softela.SoccerManager.Generator.Application.Services.DomesticCup;

/// <summary>
/// Plans the next matches of a season's domestic cup from what the written matches say about it. The cup is a
/// single-match knockout on Tuesdays at 20:00 UTC, and the weaker squad plays every tie at home, the final included.
/// </summary>
public static class DomesticCupPlanner
{
    /// <summary>
    /// Lists the clubs whose squads the plan needs measured: all the entrants before the cup starts, and the clubs of
    /// the ties still to write after it. Measuring is slow, so the caller measures only these.
    /// </summary>
    /// <param name="reading">What the season's matches say about the cup.</param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <returns>
    /// The league team ids, in ascending order; empty when the league is not written yet, when round 1 can no longer
    /// be dated before round 2, or when no tie is waiting.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reading"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="runTime"/> is not in UTC.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the reading lacks a member its status promises, which is a bug.
    /// </exception>
    public static IReadOnlyList<int> ClubsToMeasure(DomesticCupReading reading, DateTime runTime)
    {
        ArgumentNullException.ThrowIfNull(reading);

        if (runTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The run time must be in UTC.", nameof(runTime));
        }

        if (reading.Status == DomesticCupReadingStatus.WaitingForLeague)
        {
            return [];
        }

        if (reading.Status == DomesticCupReadingStatus.NotStarted)
        {
            SeasonCalendar calendar = reading.Calendar ?? throw MissingMember(nameof(reading.Calendar));

            return CupCannotStart(calendar, runTime, out _)
                ? []
                : reading.Entrants.Select(entrant => entrant.LeagueTeamId).Order().ToList();
        }

        return reading.NewTies
            .SelectMany(tie => new[] { tie.FirstLeagueTeamId, tie.SecondLeagueTeamId })
            .Distinct()
            .Order()
            .ToList();
    }

    /// <summary>
    /// Plans the matches to write on this tick. The result promises:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// Before the cup starts, it draws the cup: the byes go to the first clubs in division order, top division first
    /// and strongest first inside each division, and every tie whose clubs are known is planned at once. If round 1
    /// cannot be dated before round 2, the season plays without the cup.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// After the cup starts, it plans only the ties whose clubs are known and that have no match yet.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Every tie gets the date <see cref="MatchDateRule.Pick"/> gives from its round's calendar date, at least 24 hours
    /// from every other match of both clubs and after its own matches in the round before.
    /// </description>
    /// </item>
    /// </list>
    /// </summary>
    /// <param name="reading">What the season's matches say about the cup; the league must be written.</param>
    /// <param name="strengths">The squad strength of every club <see cref="ClubsToMeasure"/> named.</param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <returns>The plan.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="reading"/> or <paramref name="strengths"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="runTime"/> is not in UTC.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the league is not written yet, or when the reading and the draw disagree, which is a bug.
    /// </exception>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when a strength is missing or given twice, the draw library refuses the clubs, or no date fits a tie.
    /// </exception>
    public static DomesticCupPlan Plan(
        DomesticCupReading reading,
        IReadOnlyList<SquadStrength> strengths,
        DateTime runTime)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(strengths);

        if (runTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The run time must be in UTC.", nameof(runTime));
        }

        if (reading.Status == DomesticCupReadingStatus.WaitingForLeague)
        {
            throw new InvalidOperationException("The league is not written yet, so the cup cannot be planned.");
        }

        Dictionary<int, SquadStrength?> strengthOf = DomesticCupPlanIndexes.IndexStrengths(strengths);
        Dictionary<int, List<DateTime>> kickoffsOfClub = DomesticCupPlanIndexes.IndexKickoffs(reading.SeasonMatches);

        return reading.Status == DomesticCupReadingStatus.Started
            ? PlanNewTies(reading, strengthOf, kickoffsOfClub, runTime)
            : PlanStart(reading, strengthOf, kickoffsOfClub, runTime);
    }

    /// <summary>
    /// Plans the ties of a started cup that have both clubs known and no match.
    /// </summary>
    /// <param name="reading">The reading of a started cup.</param>
    /// <param name="strengthOf">The squad strength by league team id; a duplicated club holds null.</param>
    /// <param name="kickoffsOfClub">The kickoffs of each club's matches; the planned ties are added to it.</param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <returns>The plan, which is nothing new when no tie is waiting.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when a strength is missing or given twice, the draw library refuses the clubs, or no date fits a tie.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the reading lacks a member its status promises, or a feeder tie has no written match, which is a
    /// bug.
    /// </exception>
    private static DomesticCupPlan PlanNewTies(
        DomesticCupReading reading,
        Dictionary<int, SquadStrength?> strengthOf,
        Dictionary<int, List<DateTime>> kickoffsOfClub,
        DateTime runTime)
    {
        if (reading.NewTies.Count == 0)
        {
            return new DomesticCupPlan(DomesticCupPlanStatus.NothingNew, [], [], null);
        }

        SeasonCalendar calendar = reading.Calendar ?? throw MissingMember(nameof(reading.Calendar));
        KnockoutBracket bracket = reading.Bracket ?? throw MissingMember(nameof(reading.Bracket));
        DomesticCupProgress progress = reading.Progress ?? throw MissingMember(nameof(reading.Progress));

        List<DomesticCupPlannedTie> planned = new();

        foreach (KnockoutKnownTie tie in reading.NewTies)
        {
            DateTime? after = FeederKickoff(bracket.Rounds[tie.RoundNumber - 1][tie.TieNumber - 1], progress);

            DateTime kickoff = PickKickoff(
                tie,
                calendar.DomesticCupRounds[tie.RoundNumber - 1],
                after,
                null,
                runTime,
                kickoffsOfClub) ?? throw NoDate(tie);

            planned.Add(ToPlannedTie(tie, kickoff, strengthOf, kickoffsOfClub));
        }

        return new DomesticCupPlan(DomesticCupPlanStatus.Write, planned, [], null);
    }

    /// <summary>
    /// Draws the cup and plans the ties whose clubs are known: round 1 and the round 2 ties between two bye clubs.
    /// </summary>
    /// <param name="reading">The reading of a cup that has no match.</param>
    /// <param name="strengthOf">The squad strength by league team id; a duplicated club holds null.</param>
    /// <param name="kickoffsOfClub">The kickoffs of each club's matches; the planned ties are added to it.</param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <returns>The plan that starts the cup, or the plan that lets the season play without it.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when a strength is missing or given twice, the draw library refuses the clubs, or no date fits a tie.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the reading lacks a member its status promises, or the draw does not have the calendar's number of
    /// rounds, which is a bug.
    /// </exception>
    private static DomesticCupPlan PlanStart(
        DomesticCupReading reading,
        Dictionary<int, SquadStrength?> strengthOf,
        Dictionary<int, List<DateTime>> kickoffsOfClub,
        DateTime runTime)
    {
        SeasonCalendar calendar = reading.Calendar ?? throw MissingMember(nameof(reading.Calendar));

        if (CupCannotStart(calendar, runTime, out DateTime? roundTwoDate))
        {
            return new DomesticCupPlan(DomesticCupPlanStatus.PlaysWithoutCup, [], [], roundTwoDate);
        }

        int roundCount = calendar.DomesticCupRounds.Count;

        (KnockoutBracket bracket, IReadOnlyList<int> byes) = DomesticCupDrawer.Draw(reading, strengthOf, roundCount);

        IReadOnlyList<KnockoutKnownTie> known = DomesticCupReader.KnownTiesOf(bracket, []);
        List<DomesticCupPlannedTie> planned = new();

        foreach (KnockoutKnownTie tie in known.Where(candidate => candidate.RoundNumber == 1))
        {
            DateTime? kickoff = PickKickoff(
                tie,
                calendar.DomesticCupRounds[0],
                null,
                roundTwoDate,
                runTime,
                kickoffsOfClub);

            if (kickoff is null)
            {
                return new DomesticCupPlan(DomesticCupPlanStatus.PlaysWithoutCup, [], [], roundTwoDate);
            }

            planned.Add(ToPlannedTie(tie, kickoff.Value, strengthOf, kickoffsOfClub));
        }

        foreach (KnockoutKnownTie tie in known.Where(candidate => candidate.RoundNumber == 2))
        {
            DateTime kickoff = PickKickoff(
                tie,
                calendar.DomesticCupRounds[1],
                null,
                null,
                runTime,
                kickoffsOfClub) ?? throw NoDate(tie);

            planned.Add(ToPlannedTie(tie, kickoff, strengthOf, kickoffsOfClub));
        }

        return new DomesticCupPlan(DomesticCupPlanStatus.Write, planned, byes.Order().ToList(), null);
    }

    /// <summary>
    /// Tells whether a cup that has no match can no longer start: round 1 cannot be dated before round 2, even when no
    /// club has another match in the way. Such a season plays without the cup, so nothing needs measuring or drawing.
    /// </summary>
    /// <param name="calendar">The season's calendar.</param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <param name="roundTwoDate">The calendar date of round 2, or null when the cup has one round only.</param>
    /// <returns>True when round 1 has no date before round 2.</returns>
    private static bool CupCannotStart(SeasonCalendar calendar, DateTime runTime, out DateTime? roundTwoDate)
    {
        roundTwoDate = calendar.DomesticCupRounds.Count >= 2 ? calendar.DomesticCupRounds[1] : null;

        return MatchDateRule.Pick(calendar.DomesticCupRounds[0], runTime, null, roundTwoDate, []) is null;
    }

    /// <summary>
    /// Finds the latest kickoff among the ties that feed a tie: the written ties whose winners play in it.
    /// </summary>
    /// <param name="tie">The tie of the draw.</param>
    /// <param name="progress">The written ties of the cup.</param>
    /// <returns>The latest kickoff of its feeder ties, or null when no side is a winner of an earlier tie.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a feeder tie has no written match, which is a bug.
    /// </exception>
    private static DateTime? FeederKickoff(KnockoutTie tie, DomesticCupProgress progress)
    {
        DateTime? latest = null;

        foreach (KnockoutSide side in new[] { tie.First, tie.Second })
        {
            if (side.IsClub)
            {
                continue;
            }

            DomesticCupWrittenTie feeder = progress.WrittenTies.FirstOrDefault(written =>
                written.RoundNumber == side.WinnerOfRoundNumber && written.TieNumber == side.WinnerOfTieNumber)
                ?? throw new InvalidOperationException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Tie {side.WinnerOfTieNumber} of round {side.WinnerOfRoundNumber} has no written match."));

            if (latest is null || feeder.Kickoff > latest)
            {
                latest = feeder.Kickoff;
            }
        }

        return latest;
    }

    /// <summary>
    /// Picks the date of a tie by the date rule, against the other matches of both clubs.
    /// </summary>
    /// <param name="tie">The tie to date.</param>
    /// <param name="planned">The calendar date of the tie's round.</param>
    /// <param name="after">The date the tie must follow, or null.</param>
    /// <param name="before">The date the tie must precede, or null.</param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <param name="kickoffsOfClub">The kickoffs of each club's matches.</param>
    /// <returns>The date, or null when none fits.</returns>
    private static DateTime? PickKickoff(
        KnockoutKnownTie tie,
        DateTime planned,
        DateTime? after,
        DateTime? before,
        DateTime runTime,
        Dictionary<int, List<DateTime>> kickoffsOfClub)
    {
        List<DateTime> clubKickoffs = new();

        foreach (int clubId in new[] { tie.FirstLeagueTeamId, tie.SecondLeagueTeamId })
        {
            if (kickoffsOfClub.TryGetValue(clubId, out List<DateTime>? kickoffs))
            {
                clubKickoffs.AddRange(kickoffs);
            }
        }

        return MatchDateRule.Pick(planned, runTime, after, before, clubKickoffs);
    }

    /// <summary>
    /// Gives a dated tie its home club, the weaker squad, and records its kickoff against both clubs so the ties
    /// planned after it keep their distance.
    /// </summary>
    /// <param name="tie">The tie.</param>
    /// <param name="kickoff">The tie's date.</param>
    /// <param name="strengthOf">The squad strength by league team id; a duplicated club holds null.</param>
    /// <param name="kickoffsOfClub">The kickoffs of each club's matches; this tie is added to it.</param>
    /// <returns>The planned tie.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when a strength is missing or the library refuses the clubs.
    /// </exception>
    private static DomesticCupPlannedTie ToPlannedTie(
        KnockoutKnownTie tie,
        DateTime kickoff,
        Dictionary<int, SquadStrength?> strengthOf,
        Dictionary<int, List<DateTime>> kickoffsOfClub)
    {
        SquadStrength first = DomesticCupPlanIndexes.StrengthOf(strengthOf, tie.FirstLeagueTeamId);
        SquadStrength second = DomesticCupPlanIndexes.StrengthOf(strengthOf, tie.SecondLeagueTeamId);
        int home;

        try
        {
            home = SquadStrengthRanking.WeakerAtHome(first, second);
        }
        catch (ArgumentException exception) when (exception is not ArgumentNullException
            and not ArgumentOutOfRangeException)
        {
            throw DomesticCupReader.DrawRefused(exception);
        }

        int away = home == tie.FirstLeagueTeamId ? tie.SecondLeagueTeamId : tie.FirstLeagueTeamId;

        DomesticCupPlanIndexes.AddKickoff(kickoffsOfClub, tie.FirstLeagueTeamId, kickoff);
        DomesticCupPlanIndexes.AddKickoff(kickoffsOfClub, tie.SecondLeagueTeamId, kickoff);

        return new DomesticCupPlannedTie(tie.RoundNumber, tie.TieNumber, kickoff, home, away);
    }

    /// <summary>
    /// Builds the refusal for a tie that no Tuesday within a year suits.
    /// </summary>
    /// <param name="tie">The tie that has no date.</param>
    /// <returns>The refusal, ready to throw.</returns>
    private static SeasonRefusedException NoDate(KnockoutKnownTie tie)
    {
        return new SeasonRefusedException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"No Tuesday within a year fits round {tie.RoundNumber}, tie {tie.TieNumber} ")
            + string.Create(
                CultureInfo.InvariantCulture,
                $"(club {tie.FirstLeagueTeamId} v club {tie.SecondLeagueTeamId}) at least 24 hours from both ")
            + "clubs' other matches.");
    }

    /// <summary>
    /// Builds the exception for a reading that lacks a member its status promises.
    /// </summary>
    /// <param name="memberName">The name of the missing member.</param>
    /// <returns>The exception, ready to throw.</returns>
    private static InvalidOperationException MissingMember(string memberName)
    {
        return new InvalidOperationException($"The reading has no {memberName}, which its status promises.");
    }
}
