using System.Globalization;
using Microsoft.Extensions.Logging;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Repositories;
using Softela.SoccerManager.Generator.Application.Services.Calendar;
using Softela.SoccerManager.Generator.Application.Services.ContinentalCup;
using Softela.SoccerManager.Generator.Application.Services.DomesticCup;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// The continental knockout step: it writes the knockout ties of the league's two continental cups, with the statistics
/// rows of both clubs, as their clubs become known, once a cup's league phase is finished. It reads the season again
/// inside its own lock, and it refuses a cup it cannot write without ever failing the run or stopping the other cup.
/// </summary>
internal sealed class ContinentalKnockoutStep : ILeagueStep
{
    /// <summary>
    /// The start of every log line about the league's season.
    /// </summary>
    private const string LogPrefix = LeagueStepContext.LogPrefix;

    private readonly LeagueFixtureWriter _leagueFixtureWriter;
    private readonly MatchInserter _matchInserter;
    private readonly IMatchRepository _matchRepository;
    private readonly ILogger<ContinentalKnockoutStep> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContinentalKnockoutStep"/> class.
    /// </summary>
    /// <param name="leagueFixtureWriter">The writer that holds the season's transaction and lock.</param>
    /// <param name="matchInserter">The inserter of the matches and their statistics rows.</param>
    /// <param name="matchRepository">The repository that reads the season's scores.</param>
    /// <param name="logger">The logger.</param>
    public ContinentalKnockoutStep(
        LeagueFixtureWriter leagueFixtureWriter,
        MatchInserter matchInserter,
        IMatchRepository matchRepository,
        ILogger<ContinentalKnockoutStep> logger)
    {
        _leagueFixtureWriter = leagueFixtureWriter;
        _matchInserter = matchInserter;
        _matchRepository = matchRepository;
        _logger = logger;
    }

    /// <summary>
    /// Writes the knockout ties of the league's two continental cups that are known and have no match yet, or says in
    /// the log why it does not.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes once the cups have been handled.</returns>
    public async Task RunAsync(LeagueStepContext context, CancellationToken cancellationToken)
    {
        List<GetCompetitionsResult> cups = ContinentalCupFinder.Find(context.Competitions);

        // The tick's read of the matches is from its start, so on the tick that writes the league phase this step
        // finds no cup match yet, and nothing can be finished by then anyway. The step before it logs the other cases.
        if (cups.Count != 2
            || !cups.All(cup => ContinentalCupFinder.HasMatchInSeason(context.LeagueMatches, context.Season.Id, cup)))
        {
            return;
        }

        List<CupPlan> written = [];

        FixtureWriteOutcome outcome = await _leagueFixtureWriter.WriteAsync(
            context.League,
            context.Season,
            seasonMatches => WriteKnockoutsAsync(context, cups, seasonMatches, written),
            cancellationToken);

        LogOutcome(context, outcome, written);
    }

    /// <summary>
    /// Reads the season again inside the lock, plans the next ties of both cups and inserts them. Only the planning is
    /// guarded: a failure of the insert must reach the writer, which rolls back and fails the run.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="cups">The two continental cups, the one with the lower id first.</param>
    /// <param name="seasonMatches">The season's matches, read inside the transaction after the lock was taken.</param>
    /// <param name="written">The list that receives what each cup was given.</param>
    /// <returns>A task whose result is true when matches were inserted.</returns>
    private async Task<bool> WriteKnockoutsAsync(
        LeagueStepContext context,
        IReadOnlyList<GetCompetitionsResult> cups,
        IReadOnlyList<GetMatchesBySeasonResult> seasonMatches,
        List<CupPlan> written)
    {
        List<CupPlan> plans;

        try
        {
            plans = await PlanAsync(context, cups, seasonMatches);
        }
        catch (SeasonRefusedException refusal)
        {
            LogRefusal(context, refusal.Message);

            return false;
        }
        catch (ArgumentException exception) when (exception is not ArgumentNullException
            and not ArgumentOutOfRangeException)
        {
            string reason = DomesticCupReader.ReasonOf(exception);

            LogRefusal(context, $"A helper of the continental cups refused its input: {reason}");

            return false;
        }

        if (plans.Count == 0)
        {
            return false;
        }

        await _matchInserter.InsertStagedMatchesAsync(context.Season.Id, ToStagedMatches(plans), context.RunTime);

        written.AddRange(plans);

        return true;
    }

    /// <summary>
    /// Reads the season and plans each cup's next ties, the cup with the lower id first. A cup that is refused is
    /// logged and writes nothing, and the other cup goes on. It writes nothing itself.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="cups">The two continental cups, the one with the lower id first.</param>
    /// <param name="seasonMatches">The season's matches, read inside the transaction after the lock was taken.</param>
    /// <returns>A task whose result is the plan of each cup that has ties to write, in cup order.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the season cannot be read for either cup: it has no league matches, its calendar has no continental
    /// cups or sizes them wrongly, or a helper refuses the league matches.
    /// </exception>
    private async Task<List<CupPlan>> PlanAsync(
        LeagueStepContext context,
        IReadOnlyList<GetCompetitionsResult> cups,
        IReadOnlyList<GetMatchesBySeasonResult> seasonMatches)
    {
        IReadOnlyList<GetMatchScoresBySeasonResult> scores =
            await _matchRepository.GetScoresBySeasonIdAsync(context.Season.Id);

        IReadOnlyList<SeasonMatch> season = SeasonMatchJoiner.Join(seasonMatches, scores);
        List<int> divisionIds = context.Divisions.Select(division => division.Id).ToList();
        List<SeasonMatch> leagueMatches = DomesticCupLeagueReader.LeagueMatchesOf(season, divisionIds);

        if (leagueMatches.Count == 0)
        {
            throw new SeasonRefusedException(
                "The season has no league matches, but its continental cups have matches. Reset both cups together "
                + "(the readme's 'Writing a season again' has the steps).");
        }

        Dictionary<int, int> divisionOfClub = DomesticCupLeagueReader.MapClubsToDivisions(leagueMatches);
        List<int> clubCounts = DomesticCupLeagueReader.CountClubsPerDivision(divisionIds, divisionOfClub);
        SeasonCalendar calendar = DomesticCupLeagueReader.RebuildCalendar(clubCounts, leagueMatches);

        if (calendar.ContinentalCups is not { } dates)
        {
            string counts = string.Join(", ", clubCounts.Select(count => count.ToString(CultureInfo.InvariantCulture)));

            throw new SeasonRefusedException(
                $"The calendar at {counts} clubs per division has no continental cups, but the cups have matches. "
                + "Reset both cups together (the readme's 'Writing a season again' has the steps).");
        }

        if (!ContinentalMatchdayRules.TryGetCupSize(dates.LeaguePhase.Count, out int cupSize))
        {
            throw CupSizeDefect(dates);
        }

        Dictionary<int, List<DateTime>> kickoffs = DomesticCupPlanIndexes.IndexKickoffs(season);
        List<CupPlan> plans = [];

        foreach (GetCompetitionsResult cup in cups)
        {
            List<SeasonMatch> cupMatches = season.Where(match => match.CompetitionId == cup.Id).ToList();

            // The step before this one refuses a cup that has no matches while the other has them.
            if (cupMatches.Count == 0)
            {
                continue;
            }

            // A copy, so a cup that is refused part way leaves the other cup's dates as they were.
            Dictionary<int, List<DateTime>> cupKickoffs = DomesticCupPlanIndexes.CopyKickoffs(kickoffs);

            try
            {
                CupPlan? plan = PlanCup(context, cup, cupSize, dates, cupMatches, cupKickoffs);

                if (plan is not null)
                {
                    plans.Add(plan);
                    kickoffs = cupKickoffs;
                }
            }
            catch (SeasonRefusedException refusal)
            {
                LogCupRefusal(context, cup, refusal.Message);
            }
            catch (ArgumentException exception) when (exception is not ArgumentNullException
                and not ArgumentOutOfRangeException)
            {
                string reason = DomesticCupReader.ReasonOf(exception);

                LogCupRefusal(context, cup, $"A helper of the continental cups refused its input: {reason}");
            }
        }

        return plans;
    }

    /// <summary>
    /// Reads one cup's knockout from its matches and plans its next ties.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="cup">The cup's competition.</param>
    /// <param name="cupSize">The number of clubs in the cup, 16 or 32.</param>
    /// <param name="dates">The calendar's continental cup dates.</param>
    /// <param name="cupMatches">The season's matches of this cup, with their scores.</param>
    /// <param name="kickoffs">The kickoffs of every club's matches in the season; the planned ones are added.</param>
    /// <returns>
    /// The plan of the cup, or null when the cup waits for its league phase or has no new tie, which is logged.
    /// </returns>
    /// <exception cref="SeasonRefusedException">Thrown when the cup's matches cannot be read or dated.</exception>
    /// <exception cref="ArgumentException">Thrown when a helper refuses what the cup holds.</exception>
    private CupPlan? PlanCup(
        LeagueStepContext context,
        GetCompetitionsResult cup,
        int cupSize,
        ContinentalCupDates dates,
        IReadOnlyList<SeasonMatch> cupMatches,
        Dictionary<int, List<DateTime>> kickoffs)
    {
        ContinentalKnockoutReading reading = ContinentalKnockoutReader.Read(context.Season.Id, cupSize, cupMatches);

        if (reading.Status == ContinentalKnockoutReadingStatus.WaitingForLeaguePhase)
        {
            LogWaiting(context, cup, reading);

            return null;
        }

        IReadOnlyList<ContinentalPlannedLeg> legs = ContinentalKnockoutPlanner.Plan(
            reading,
            dates,
            context.RunTime,
            kickoffs);

        if (legs.Count == 0)
        {
            LogNothingNew(context, cup, reading);

            return null;
        }

        return new CupPlan(cup, legs);
    }

    /// <summary>
    /// Builds the refusal for a calendar whose league phase has a number of matchdays that no cup has.
    /// </summary>
    /// <param name="dates">The calendar's continental cup dates.</param>
    /// <returns>The refusal, ready to throw.</returns>
    private static SeasonRefusedException CupSizeDefect(ContinentalCupDates dates)
    {
        int smallMatchdays = ContinentalMatchdayRules.MatchdayCount(ContinentalInputValidator.SmallCupClubCount);
        int largeMatchdays = ContinentalMatchdayRules.MatchdayCount(ContinentalInputValidator.LargeCupClubCount);

        return new SeasonRefusedException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"The calendar's continental league phase has {dates.LeaguePhase.Count} matchdays, and a cup has ")
            + string.Create(
                CultureInfo.InvariantCulture,
                $"{smallMatchdays} or {largeMatchdays}, so the cups cannot be sized. This is a defect."));
    }

    /// <summary>
    /// Turns the planned legs of the cups into the matches to insert, cup 1 first and each cup in plan order.
    /// </summary>
    /// <param name="plans">The plan of each cup, the one with the lower id first.</param>
    /// <returns>One staged match for each planned leg. A knockout match has no round number.</returns>
    private static List<StagedMatch> ToStagedMatches(IReadOnlyList<CupPlan> plans)
    {
        return plans
            .SelectMany(plan => plan.Legs.Select(leg => new StagedMatch(
                plan.Cup.Id,
                leg.Kickoff,
                (MatchStage)ContinentalKnockoutStages.StageOf(leg.Round),
                RoundNumber: null,
                leg.Leg,
                leg.HomeLeagueTeamId,
                leg.AwayLeagueTeamId)))
            .ToList();
    }

    /// <summary>
    /// Names a competition for a log line, with its id.
    /// </summary>
    /// <param name="competition">The competition.</param>
    /// <returns>The competition's name in quotes, followed by its id.</returns>
    private static string DescribeCup(GetCompetitionsResult competition)
    {
        return string.Create(CultureInfo.InvariantCulture, $"'{competition.Name}' (id {competition.Id})");
    }

    /// <summary>
    /// Describes the rounds that have matches in a plan, in round order, with their tie numbers and match counts.
    /// </summary>
    /// <param name="plan">The plan that was written.</param>
    /// <returns>One part for each round, such as "quarter-final ties 1, 2, 3 and 4 (8 matches)".</returns>
    private static string DescribeRounds(CupPlan plan)
    {
        List<string> parts = [];

        foreach (IGrouping<ContinentalRound, ContinentalPlannedLeg> round in plan.Legs
            .GroupBy(leg => leg.Round)
            .OrderBy(group => ContinentalKnockoutStages.StageOf(group.Key)))
        {
            string name = ContinentalKnockoutStages.NameOf(round.Key);
            int matchCount = round.Count();
            string noun = matchCount == 1 ? "match" : "matches";

            parts.Add(round.Key == ContinentalRound.Final
                ? string.Create(CultureInfo.InvariantCulture, $"{name} ({matchCount} {noun})")
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"{name} {DescribeTies(round)} ({matchCount} {noun})"));
        }

        return string.Join("; ", parts);
    }

    /// <summary>
    /// Lists the tie numbers of one round's legs, in ascending order.
    /// </summary>
    /// <param name="legs">The planned legs of one round.</param>
    /// <returns>"tie 2" for one tie, otherwise "ties 1, 2 and 3".</returns>
    private static string DescribeTies(IEnumerable<ContinentalPlannedLeg> legs)
    {
        List<string> numbers = legs
            .Select(leg => leg.TieNumber)
            .Distinct()
            .Order()
            .Select(number => number.ToString(CultureInfo.InvariantCulture))
            .ToList();

        if (numbers.Count == 1)
        {
            return $"tie {numbers[0]}";
        }

        return $"ties {string.Join(", ", numbers.Take(numbers.Count - 1))} and {numbers[^1]}";
    }

    /// <summary>
    /// Logs that a cup waits for its league phase to finish.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="cup">The cup's competition.</param>
    /// <param name="reading">What the cup's matches say about its knockout.</param>
    private void LogWaiting(LeagueStepContext context, GetCompetitionsResult cup, ContinentalKnockoutReading reading)
    {
        _logger.LogInformation(
            LogPrefix + ": the continental cup {Cup} waits for its league phase: {Finished} of {Total} matches are "
            + "finished.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            DescribeCup(cup),
            reading.FinishedLeaguePhaseMatches,
            reading.LeaguePhaseMatches);
    }

    /// <summary>
    /// Logs that a cup has no tie to write on this tick.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="cup">The cup's competition.</param>
    /// <param name="reading">What the cup's matches say about its knockout.</param>
    private void LogNothingNew(LeagueStepContext context, GetCompetitionsResult cup, ContinentalKnockoutReading reading)
    {
        _logger.LogInformation(
            LogPrefix + ": the continental cup {Cup} has no new knockout tie: {Written} ties are written and "
            + "{Decided} decided.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            DescribeCup(cup),
            reading.WrittenTies.Count,
            reading.Winners.Count);
    }

    /// <summary>
    /// Logs that one cup is refused, and why. The other cup is not affected.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="cup">The cup's competition.</param>
    /// <param name="reason">What is wrong and how to fix it.</param>
    private void LogCupRefusal(LeagueStepContext context, GetCompetitionsResult cup, string reason)
    {
        _logger.LogError(
            LogPrefix + ": the continental knockouts of {Cup} are refused: {Reason} The next tick tries again.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            DescribeCup(cup),
            reason);
    }

    /// <summary>
    /// Logs that both cups are refused, because of what the season as a whole holds.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="reason">What is wrong and how to fix it.</param>
    private void LogRefusal(LeagueStepContext context, string reason)
    {
        _logger.LogError(
            LogPrefix + ": the continental knockouts are refused: {Reason} The next tick tries again.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            reason);
    }

    /// <summary>
    /// Logs what came of the write: the lock, or the cups written. Every other reason to write nothing was logged
    /// where it was found.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="outcome">What came of writing under the season's lock.</param>
    /// <param name="written">What each cup was given, in cup order; empty unless the rows were written.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the outcome has no reason that the step knows, which is a bug.
    /// </exception>
    private void LogOutcome(LeagueStepContext context, FixtureWriteOutcome outcome, List<CupPlan> written)
    {
        switch (outcome)
        {
            case FixtureWriteOutcome.LockHeld:
                _logger.LogInformation(
                    LogPrefix + ": another run holds this season's lock, so this run leaves the continental "
                    + "knockouts to it.",
                    context.League.Name,
                    context.League.Id,
                    context.Season.SeasonNumber,
                    context.Season.Id);

                break;

            case FixtureWriteOutcome.Written:
                foreach (CupPlan plan in written)
                {
                    LogWritten(context, plan);
                }

                break;

            case FixtureWriteOutcome.NotWritten:
                break;

            default:
                throw new InvalidOperationException(
                    $"The continental knockout step has no reason for the outcome {outcome}, which is a bug.");
        }
    }

    /// <summary>
    /// Logs the matches a committed write gave one cup.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="plan">The plan of the cup that was written.</param>
    private void LogWritten(LeagueStepContext context, CupPlan plan)
    {
        _logger.LogInformation(
            LogPrefix + ": wrote knockout matches of the continental cup {Cup}, {Matches} in all: {Rounds}.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            DescribeCup(plan.Cup),
            plan.Legs.Count,
            DescribeRounds(plan));
    }

    /// <summary>
    /// What is written for one cup: the knockout matches of the ties that became known.
    /// </summary>
    /// <param name="Cup">The cup's competition.</param>
    /// <param name="Legs">The matches to write, in tie order, the first leg before the second leg.</param>
    private sealed record CupPlan(GetCompetitionsResult Cup, IReadOnlyList<ContinentalPlannedLeg> Legs);
}
