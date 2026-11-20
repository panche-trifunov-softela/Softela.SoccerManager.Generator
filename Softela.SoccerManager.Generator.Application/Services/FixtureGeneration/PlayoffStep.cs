using System.Globalization;
using Microsoft.Extensions.Logging;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Repositories;
using Softela.SoccerManager.Generator.Application.Services.Calendar;
using Softela.SoccerManager.Generator.Application.Services.DomesticCup;
using Softela.SoccerManager.Generator.Application.Services.Playoffs;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// The promotion playoff step: it writes the matches of the promotion playoff of each division that has one, with the
/// statistics rows of both clubs, as their clubs become known, once the division's league matches are finished and its
/// Standings agree with them. It reads the season again inside its own lock, and it refuses a division it cannot write
/// without ever failing the run or stopping the other divisions.
/// </summary>
internal sealed class PlayoffStep : ILeagueStep
{
    /// <summary>
    /// The start of every log line about the league's season.
    /// </summary>
    private const string LogPrefix = LeagueStepContext.LogPrefix;

    private readonly LeagueFixtureWriter _leagueFixtureWriter;
    private readonly MatchInserter _matchInserter;
    private readonly IMatchRepository _matchRepository;
    private readonly IStandingRepository _standingRepository;
    private readonly ILeagueTeamRepository _leagueTeamRepository;
    private readonly ILogger<PlayoffStep> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayoffStep"/> class.
    /// </summary>
    /// <param name="leagueFixtureWriter">The writer that holds the season's transaction and lock.</param>
    /// <param name="matchInserter">The inserter of the matches and their statistics rows.</param>
    /// <param name="matchRepository">The repository that reads the season's scores.</param>
    /// <param name="standingRepository">The repository that reads the divisions' Standings.</param>
    /// <param name="leagueTeamRepository">The repository that reads the league's roster.</param>
    /// <param name="logger">The logger.</param>
    public PlayoffStep(
        LeagueFixtureWriter leagueFixtureWriter,
        MatchInserter matchInserter,
        IMatchRepository matchRepository,
        IStandingRepository standingRepository,
        ILeagueTeamRepository leagueTeamRepository,
        ILogger<PlayoffStep> logger)
    {
        _leagueFixtureWriter = leagueFixtureWriter;
        _matchInserter = matchInserter;
        _matchRepository = matchRepository;
        _standingRepository = standingRepository;
        _leagueTeamRepository = leagueTeamRepository;
        _logger = logger;
    }

    /// <summary>
    /// Writes the playoff matches of the league's divisions that are known and have no match yet, or says in the log
    /// why it does not.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes once the divisions have been handled.</returns>
    public async Task RunAsync(LeagueStepContext context, CancellationToken cancellationToken)
    {
        // The tick's read of the matches is from its start, so on the tick that writes the league matches this step
        // finds none and stays silent; the league matches step logs that tick.
        if (!HasPlayoffDivisionWithMatches(context))
        {
            return;
        }

        List<DivisionPlan> written = [];

        FixtureWriteOutcome outcome = await _leagueFixtureWriter.WriteAsync(
            context.League,
            context.Season,
            seasonMatches => WritePlayoffsAsync(context, seasonMatches, written),
            cancellationToken);

        LogOutcome(context, outcome, written);
    }

    /// <summary>
    /// Tells whether some division asks for a playoff and already has a match in the season.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <returns>True when the step has something to look at.</returns>
    private static bool HasPlayoffDivisionWithMatches(LeagueStepContext context)
    {
        return context.Divisions
            .Where(division => division.TeamsInPlayoffs != 0)
            .Any(division => context.LeagueMatches.Any(
                match => match.CompetitionId == division.Id && match.SeasonId == context.Season.Id));
    }

    /// <summary>
    /// Reads the season again inside the lock, plans the next playoff matches of every division and inserts them. Only
    /// the planning is guarded: a failure of the insert must reach the writer, which rolls back and fails the run.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="seasonMatches">The season's matches, read inside the transaction after the lock was taken.</param>
    /// <param name="written">The list that receives what each division was given.</param>
    /// <returns>A task whose result is true when matches were inserted.</returns>
    private async Task<bool> WritePlayoffsAsync(
        LeagueStepContext context,
        IReadOnlyList<GetMatchesBySeasonResult> seasonMatches,
        List<DivisionPlan> written)
    {
        List<DivisionPlan> plans;

        try
        {
            plans = await PlanAsync(context, seasonMatches);
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

            LogRefusal(context, $"A helper of the promotion playoffs refused its input: {reason}");

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
    /// Reads the season and plans each playoff division's next matches, the division with the lowest order first. A
    /// division that is refused is logged and writes nothing, and the other divisions go on. It writes nothing itself.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="seasonMatches">The season's matches, read inside the transaction after the lock was taken.</param>
    /// <returns>The plan of each division that has matches to write, in division order.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the season cannot be read for any division: it has no league matches but has playoff matches, a
    /// division has no clubs, the divisions' settings are refused, or the calendar cannot be rebuilt.
    /// </exception>
    private async Task<List<DivisionPlan>> PlanAsync(
        LeagueStepContext context,
        IReadOnlyList<GetMatchesBySeasonResult> seasonMatches)
    {
        IReadOnlyList<GetMatchScoresBySeasonResult> scores =
            await _matchRepository.GetScoresBySeasonIdAsync(context.Season.Id);

        IReadOnlyList<SeasonMatch> season = SeasonMatchJoiner.Join(seasonMatches, scores);
        List<int> divisionIds = context.Divisions.Select(division => division.Id).ToList();
        List<SeasonMatch> leagueMatches = DomesticCupLeagueReader.LeagueMatchesOf(season, divisionIds);

        if (leagueMatches.Count == 0)
        {
            if (season.Any(match => PlayoffStages.IsPlayoffMatch(match)
                && match.CompetitionId is { } id
                && divisionIds.Contains(id)))
            {
                throw new SeasonRefusedException(
                    "The season has no league matches, but its divisions have playoff matches. Delete them (the "
                    + "readme's playoff reset).");
            }

            return [];
        }

        Dictionary<int, int> divisionOfClub = DomesticCupLeagueReader.MapClubsToDivisions(leagueMatches);
        List<int> clubCounts = DomesticCupLeagueReader.CountClubsPerDivision(divisionIds, divisionOfClub);
        IReadOnlyList<PromotionPlaces> places = PromotionSettings.Check(context.Divisions, clubCounts);

        if (places.All(place => place.PlayoffClubs == 0))
        {
            return [];
        }

        Dictionary<int, int?> stadiumOfClub = await ReadStadiumsAsync(context);
        Dictionary<int, List<DateTime>> kickoffs = DomesticCupPlanIndexes.IndexKickoffs(season);
        List<ReadyDivision> ready = [];

        for (int index = 0; index < context.Divisions.Count; index++)
        {
            if (places[index].PlayoffClubs == 0)
            {
                continue;
            }

            GetCompetitionsResult division = context.Divisions[index];
            ReadyDivision? readyDivision = await ReadDivisionAsync(
                context,
                division,
                places[index],
                season,
                leagueMatches,
                stadiumOfClub);

            if (readyDivision is not null)
            {
                ready.Add(readyDivision);
            }
        }

        if (ready.Count == 0)
        {
            return [];
        }

        SeasonCalendar calendar = DomesticCupLeagueReader.RebuildCalendar(clubCounts, leagueMatches);

        return PlanDivisions(context, ready, calendar, kickoffs);
    }

    /// <summary>
    /// Reads one division's playoff from its matches and Standings. A division that waits, has no new match or is
    /// refused is logged and gives nothing.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="division">The division's competition.</param>
    /// <param name="places">What the division's settings say about its table.</param>
    /// <param name="season">The season's matches with their scores.</param>
    /// <param name="leagueMatches">The league matches of every division.</param>
    /// <param name="stadiumOfClub">The stadium of each club by league team id; null for a club without one.</param>
    /// <returns>The division with its reading when it has new ties; otherwise null.</returns>
    private async Task<ReadyDivision?> ReadDivisionAsync(
        LeagueStepContext context,
        GetCompetitionsResult division,
        PromotionPlaces places,
        IReadOnlyList<SeasonMatch> season,
        IReadOnlyList<SeasonMatch> leagueMatches,
        IReadOnlyDictionary<int, int?> stadiumOfClub)
    {
        List<SeasonMatch> divisionLeagueMatches = leagueMatches
            .Where(match => match.CompetitionId == division.Id)
            .ToList();

        List<SeasonMatch> playoffMatches = season
            .Where(match => match.CompetitionId == division.Id && PlayoffStages.IsPlayoffMatch(match))
            .ToList();

        IReadOnlyList<GetStandingsResult> standings =
            await _standingRepository.GetByCompetitionAndSeasonAsync(division.Id, context.Season.Id);

        PlayoffReading reading;

        try
        {
            reading = PlayoffReader.Read(
                context.Season.Id,
                context.RunTime,
                division,
                places,
                divisionLeagueMatches,
                playoffMatches,
                standings,
                stadiumOfClub);
        }
        catch (SeasonRefusedException refusal)
        {
            LogDivisionRefusal(context, division, refusal.Message);

            return null;
        }
        catch (ArgumentException exception) when (exception is not ArgumentNullException
            and not ArgumentOutOfRangeException)
        {
            string reason = DomesticCupReader.ReasonOf(exception);

            LogDivisionRefusal(context, division, $"A helper of the promotion playoffs refused its input: {reason}");

            return null;
        }

        switch (reading.Status)
        {
            case PlayoffReadingStatus.WaitingForLeague:
                LogWaitingForLeague(context, division, reading);

                return null;

            case PlayoffReadingStatus.WaitingForStandings:
                LogWaitingForStandings(context, division, reading);

                return null;

            case PlayoffReadingStatus.Ready when reading.NewTies.Count == 0:
                LogNothingNew(context, division, reading);

                return null;

            case PlayoffReadingStatus.Ready:
                return new ReadyDivision(division, reading);

            default:
                throw new InvalidOperationException(
                    $"The promotion playoff step has no reason for the status {reading.Status}, which is a bug.");
        }
    }

    /// <summary>
    /// Plans the new ties of each division that has some, in division order. A division that is refused is logged and
    /// writes nothing, and its kickoffs are left out, so the next division is dated as if it were not there.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="ready">The divisions that have new ties, in division order.</param>
    /// <param name="calendar">The dates of the season.</param>
    /// <param name="kickoffs">The kickoffs of every club's matches in the season.</param>
    /// <returns>The plan of each division that was dated, in division order.</returns>
    private List<DivisionPlan> PlanDivisions(
        LeagueStepContext context,
        IReadOnlyList<ReadyDivision> ready,
        SeasonCalendar calendar,
        Dictionary<int, List<DateTime>> kickoffs)
    {
        List<DivisionPlan> plans = [];

        foreach (ReadyDivision item in ready)
        {
            // A copy, so a division that is refused part way leaves the next division's dates as they were.
            Dictionary<int, List<DateTime>> divisionKickoffs = DomesticCupPlanIndexes.CopyKickoffs(kickoffs);

            try
            {
                IReadOnlyList<PlayoffPlannedMatch> matches = PlayoffPlanner.Plan(
                    item.Division,
                    item.Reading,
                    calendar,
                    context.RunTime,
                    divisionKickoffs);

                plans.Add(new DivisionPlan(item.Division, matches));
                kickoffs = divisionKickoffs;
            }
            catch (SeasonRefusedException refusal)
            {
                LogDivisionRefusal(context, item.Division, refusal.Message);
            }
            catch (ArgumentException exception) when (exception is not ArgumentNullException
                and not ArgumentOutOfRangeException)
            {
                string reason = DomesticCupReader.ReasonOf(exception);

                LogDivisionRefusal(
                    context,
                    item.Division,
                    $"A helper of the promotion playoffs refused its input: {reason}");
            }
        }

        return plans;
    }

    /// <summary>
    /// Reads the stadium of every club on the league's roster: the roster entry's team, and that team's stadium.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <returns>The stadium id by league team id; null for a club whose team has no stadium.</returns>
    private async Task<Dictionary<int, int?>> ReadStadiumsAsync(LeagueStepContext context)
    {
        IReadOnlyList<GetLeagueTeamsResult> leagueTeams =
            await _leagueTeamRepository.GetByLeagueIdAsync(context.League.Id);

        return ClubStadiums.Of(leagueTeams, context.Teams);
    }

    /// <summary>
    /// Turns the planned matches of the divisions into the matches to insert, division by division, each in tie order.
    /// </summary>
    /// <param name="plans">The plan of each division, in division order.</param>
    /// <returns>One staged match for each planned match. A playoff match has no round number and no leg.</returns>
    private static List<StagedMatch> ToStagedMatches(IReadOnlyList<DivisionPlan> plans)
    {
        return plans
            .SelectMany(plan => plan.Matches.Select(match => new StagedMatch(
                plan.Division.Id,
                match.Kickoff,
                (MatchStage)match.Stage,
                RoundNumber: null,
                Leg: null,
                match.HomeLeagueTeamId,
                match.AwayLeagueTeamId)))
            .ToList();
    }

    /// <summary>
    /// Names a division for a log line, with its id.
    /// </summary>
    /// <param name="division">The division's competition.</param>
    /// <returns>The division's name in quotes, followed by its id.</returns>
    private static string DescribeDivision(GetCompetitionsResult division)
    {
        return string.Create(CultureInfo.InvariantCulture, $"'{division.Name}' (id {division.Id})");
    }

    /// <summary>
    /// Describes the matches of a plan by their stage and the table places of their clubs, home club first.
    /// </summary>
    /// <param name="plan">The plan that was written.</param>
    /// <returns>
    /// One part for each stage, such as "semi-finals 3 v 6 and 4 v 5 (2 matches)" or "final 3 v 4 (1 match)", joined
    /// with a semicolon.
    /// </returns>
    private static string DescribeMatches(DivisionPlan plan)
    {
        List<string> parts = [];

        foreach (IGrouping<byte, PlayoffPlannedMatch> stage in plan.Matches
            .GroupBy(match => match.Stage)
            .OrderBy(group => group.Key))
        {
            List<string> pairs = stage
                .OrderBy(match => match.TieNumber)
                .Select(match => string.Create(CultureInfo.InvariantCulture, $"{match.HomePlace} v {match.AwayPlace}"))
                .ToList();

            string name = PlayoffStages.NameOf(stage.Key);
            string noun = pairs.Count == 1 ? "match" : "matches";
            string plural = pairs.Count == 1 ? name : name + "s";

            parts.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{plural} {string.Join(" and ", pairs)} ({pairs.Count} {noun})"));
        }

        return string.Join("; ", parts);
    }

    /// <summary>
    /// Logs that a division waits for its league matches to finish.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="division">The division's competition.</param>
    /// <param name="reading">What the division's matches say about its playoff.</param>
    private void LogWaitingForLeague(LeagueStepContext context, GetCompetitionsResult division, PlayoffReading reading)
    {
        _logger.LogInformation(
            LogPrefix + ": the promotion playoff of {Division} waits for its league matches: {Finished} of {Total} "
            + "are finished.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            DescribeDivision(division),
            reading.FinishedLeagueMatches,
            reading.LeagueMatches);
    }

    /// <summary>
    /// Logs that a division waits for its Standings to settle.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="division">The division's competition.</param>
    /// <param name="reading">What the division's matches say about its playoff.</param>
    private void LogWaitingForStandings(
        LeagueStepContext context,
        GetCompetitionsResult division,
        PlayoffReading reading)
    {
        string changedAt = reading.NewestStandingsChange!.Value.ToString(
            StandingsSettleRule.ChangedAtFormat,
            CultureInfo.InvariantCulture);

        _logger.LogInformation(
            LogPrefix + ": the promotion playoff of {Division} waits: its newest Standings change, "
            + "{StandingsChangedAt} UTC, is under an hour old.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            DescribeDivision(division),
            changedAt);
    }

    /// <summary>
    /// Logs that a division has no playoff match to write on this tick.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="division">The division's competition.</param>
    /// <param name="reading">What the division's matches say about its playoff.</param>
    private void LogNothingNew(LeagueStepContext context, GetCompetitionsResult division, PlayoffReading reading)
    {
        _logger.LogInformation(
            LogPrefix + ": the promotion playoff of {Division} has no new match: {Written} are written and "
            + "{Decided} decided.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            DescribeDivision(division),
            reading.WrittenMatches,
            reading.DecidedMatches);
    }

    /// <summary>
    /// Logs that one division is refused, and why. The other divisions are not affected.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="division">The division's competition.</param>
    /// <param name="reason">What is wrong and how to fix it.</param>
    private void LogDivisionRefusal(LeagueStepContext context, GetCompetitionsResult division, string reason)
    {
        _logger.LogError(
            LogPrefix + ": the promotion playoff of {Division} is refused: {Reason} The next tick tries again.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            DescribeDivision(division),
            reason);
    }

    /// <summary>
    /// Logs that every division is refused, because of what the season as a whole holds.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="reason">What is wrong and how to fix it.</param>
    private void LogRefusal(LeagueStepContext context, string reason)
    {
        _logger.LogError(
            LogPrefix + ": the promotion playoffs are refused: {Reason} The next tick tries again.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            reason);
    }

    /// <summary>
    /// Logs what came of the write: the lock, or the divisions written. Every other reason to write nothing was logged
    /// where it was found.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="outcome">What came of writing under the season's lock.</param>
    /// <param name="written">What each division was given, in order; empty unless the rows were written.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the outcome has no reason that the step knows, which is a bug.
    /// </exception>
    private void LogOutcome(LeagueStepContext context, FixtureWriteOutcome outcome, List<DivisionPlan> written)
    {
        switch (outcome)
        {
            case FixtureWriteOutcome.LockHeld:
                _logger.LogInformation(
                    LogPrefix + ": another run holds this season's lock, so this run leaves the promotion "
                    + "playoffs to it.",
                    context.League.Name,
                    context.League.Id,
                    context.Season.SeasonNumber,
                    context.Season.Id);

                break;

            case FixtureWriteOutcome.Written:
                foreach (DivisionPlan plan in written)
                {
                    LogWritten(context, plan);
                }

                break;

            case FixtureWriteOutcome.NotWritten:
                break;

            default:
                throw new InvalidOperationException(
                    $"The promotion playoff step has no reason for the outcome {outcome}, which is a bug.");
        }
    }

    /// <summary>
    /// Logs the matches a committed write gave one division.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="plan">The plan of the division that was written.</param>
    private void LogWritten(LeagueStepContext context, DivisionPlan plan)
    {
        _logger.LogInformation(
            LogPrefix + ": wrote promotion playoff matches of {Division}, {Matches} in all: {Details}.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            DescribeDivision(plan.Division),
            plan.Matches.Count,
            DescribeMatches(plan));
    }

    /// <summary>
    /// A division whose playoff has ties to write: what its matches and Standings say about it.
    /// </summary>
    /// <param name="Division">The division's competition.</param>
    /// <param name="Reading">The reading of the division's playoff; it is ready and has new ties.</param>
    private sealed record ReadyDivision(GetCompetitionsResult Division, PlayoffReading Reading);

    /// <summary>
    /// What is written for one division: the playoff matches of the ties that became known.
    /// </summary>
    /// <param name="Division">The division's competition.</param>
    /// <param name="Matches">The matches to write, in tie order.</param>
    private sealed record DivisionPlan(GetCompetitionsResult Division, IReadOnlyList<PlayoffPlannedMatch> Matches);
}
