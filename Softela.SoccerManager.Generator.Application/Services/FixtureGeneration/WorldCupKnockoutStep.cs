using System.Globalization;
using Microsoft.Extensions.Logging;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Repositories;
using Softela.SoccerManager.Generator.Application.Services.Calendar;
using Softela.SoccerManager.Generator.Application.Services.ContinentalCup;
using Softela.SoccerManager.Generator.Application.Services.DomesticCup;
using Softela.SoccerManager.Generator.Application.Services.Seasons;
using Softela.SoccerManager.Generator.Application.Services.WorldCup;

namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// The World Cup knockout step: once the finals groups are written and every group match is finished, it writes the
/// round of 16, then each later match as the two matches that lead to it are decided, and the third-place match
/// together with the final, with the statistics rows of both nations. It reads the season again inside its own lock, so
/// it sees what the steps before it wrote, and it refuses a season it cannot write without ever failing the run. The
/// finals groups are written by the World Cup group step.
/// </summary>
internal sealed class WorldCupKnockoutStep : ILeagueStep
{
    /// <summary>
    /// The format of a date in a log line.
    /// </summary>
    private const string DateFormat = "yyyy-MM-dd HH:mm";

    /// <summary>
    /// The start of every log line about the league's season.
    /// </summary>
    private const string LogPrefix = LeagueStepContext.LogPrefix;

    private readonly LeagueFixtureWriter _leagueFixtureWriter;
    private readonly MatchInserter _matchInserter;
    private readonly IMatchRepository _matchRepository;
    private readonly ILogger<WorldCupKnockoutStep> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldCupKnockoutStep"/> class.
    /// </summary>
    /// <param name="leagueFixtureWriter">The writer that holds the season's transaction and lock.</param>
    /// <param name="matchInserter">The inserter of the matches and their statistics rows.</param>
    /// <param name="matchRepository">The repository that reads the season's scores.</param>
    /// <param name="logger">The logger.</param>
    public WorldCupKnockoutStep(
        LeagueFixtureWriter leagueFixtureWriter,
        MatchInserter matchInserter,
        IMatchRepository matchRepository,
        ILogger<WorldCupKnockoutStep> logger)
    {
        _leagueFixtureWriter = leagueFixtureWriter;
        _matchInserter = matchInserter;
        _matchRepository = matchRepository;
        _logger = logger;
    }

    /// <summary>
    /// Writes the World Cup knockout matches of the league's national competition that are known and have no match yet,
    /// or says in the log why it does not.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes once the knockout has been handled.</returns>
    public async Task RunAsync(LeagueStepContext context, CancellationToken cancellationToken)
    {
        IReadOnlyList<GetCompetitionsResult> nationals = NationalCompetitionFinder.Find(context.Competitions);

        // The qualifier step logs a league with no national competition or with several of them.
        if (nationals.Count != 1)
        {
            return;
        }

        GetCompetitionsResult national = nationals[0];

        // No qualifier match in the tick's read: the season plays no qualifiers, or this tick wrote them, and then no
        // finals group can be written, let alone finished.
        if (!ContinentalCupFinder.HasMatchInSeason(context.LeagueMatches, context.Season.Id, national))
        {
            return;
        }

        IReadOnlyList<FinalsPlannedMatch>? written = null;

        FixtureWriteOutcome outcome = await _leagueFixtureWriter.WriteAsync(
            context.League,
            context.Season,
            async seasonMatches =>
            {
                IReadOnlyList<FinalsPlannedMatch>? plan = await TryPlanAsync(context, seasonMatches, national);

                if (plan is null)
                {
                    return false;
                }

                await _matchInserter.InsertStagedMatchesAsync(
                    context.Season.Id,
                    ToStagedMatches(national, plan),
                    context.RunTime);

                written = plan;

                return true;
            },
            cancellationToken);

        LogOutcome(context, outcome, written);
    }

    /// <summary>
    /// Plans the knockout matches and logs a refusal. Only the planning is guarded: a failure of the inserts must reach
    /// the writer, which rolls back.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="seasonMatches">The season's matches, read inside the transaction after the lock was taken.</param>
    /// <param name="national">The league's national competition.</param>
    /// <returns>A task whose result is the planned matches, or null when there is nothing to write.</returns>
    private async Task<IReadOnlyList<FinalsPlannedMatch>?> TryPlanAsync(
        LeagueStepContext context,
        IReadOnlyList<GetMatchesBySeasonResult> seasonMatches,
        GetCompetitionsResult national)
    {
        try
        {
            return await PlanAsync(context, seasonMatches, national);
        }
        catch (SeasonRefusedException refusal)
        {
            LogRefusal(context, refusal.Message);

            return null;
        }
        catch (ArgumentException exception) when (exception is not ArgumentNullException
            and not ArgumentOutOfRangeException)
        {
            string reason = DomesticCupReader.ReasonOf(exception);

            LogRefusal(context, $"A helper of the World Cup finals refused its input: {reason}");

            return null;
        }
    }

    /// <summary>
    /// Reads the season again inside the lock and decides whether World Cup knockout matches are due, and plans them
    /// when they are. It writes nothing. A refusal is thrown to the caller, which logs it; every other reason to write
    /// nothing is logged here, or is no news.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="seasonMatches">The season's matches, read inside the transaction after the lock was taken.</param>
    /// <param name="national">The league's national competition.</param>
    /// <returns>A task whose result is the planned matches, or null when there is nothing to write.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the finals' matches cannot be read as written, or no day fits a match.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a helper refuses what the season holds.
    /// </exception>
    private async Task<IReadOnlyList<FinalsPlannedMatch>?> PlanAsync(
        LeagueStepContext context,
        IReadOnlyList<GetMatchesBySeasonResult> seasonMatches,
        GetCompetitionsResult national)
    {
        IReadOnlyList<GetMatchScoresBySeasonResult> scores =
            await _matchRepository.GetScoresBySeasonIdAsync(context.Season.Id);

        IReadOnlyList<SeasonMatch> season = SeasonMatchJoiner.Join(seasonMatches, scores);

        List<SeasonMatch> finalsMatches = season
            .Where(match => match.CompetitionId == national.Id)
            .Where(match => match.Stage is { } stage && FinalsStages.IsFinalsStage(stage))
            .ToList();

        // The finals groups are not written yet; the group step does that.
        if (finalsMatches.Count == 0)
        {
            return null;
        }

        SeasonCalendar? calendar = ReadCalendar(context, season);

        if (calendar is null)
        {
            return null;
        }

        FinalsReading reading = FinalsReader.Read(context.Season.Id, finalsMatches);

        if (reading.Status == FinalsReadingStatus.WaitingForGroups)
        {
            LogWaitingForGroups(context, reading);

            return null;
        }

        WorldCupFinalsDays days = WorldCupFinalsStart.Plan(
            calendar.WorldCupFinalsMatchdays,
            ContinentalCupFinder.Find(context.Competitions),
            season);

        if (days.WaitingForCup is { } cup)
        {
            LogWaitingForCup(context, cup);

            return null;
        }

        IReadOnlyList<FinalsPlannedMatch> planned = FinalsKnockoutPlanner.Plan(
            reading,
            days.Days,
            context.RunTime,
            DomesticCupPlanIndexes.IndexKickoffs(season));

        if (planned.Count == 0)
        {
            LogNothingNew(context, reading);

            return null;
        }

        return planned;
    }

    /// <summary>
    /// Rebuilds the calendar the league was written on, and says in the log when it cannot hold the finals.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="season">The season's matches with their scores.</param>
    /// <returns>The calendar, or null when the league matches are not written or the calendar has no finals.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the league matches cannot be read, or are not on the matchdays of a calendar.
    /// </exception>
    private SeasonCalendar? ReadCalendar(LeagueStepContext context, IReadOnlyList<SeasonMatch> season)
    {
        WorldCupCalendarReading reading = WorldCupCalendarReader.Read(context.Divisions, season);

        if (reading.Calendar is not { } calendar)
        {
            LogWaitingForLeague(context);

            return null;
        }

        if (calendar.WorldCupFinalsMatchdays.Count == 0)
        {
            LogNoWorldCupFinals(context, reading.ClubCounts);

            return null;
        }

        return calendar;
    }

    /// <summary>
    /// Turns the planned matches into the matches to insert, in the order given. A knockout match has no round number
    /// and no leg, and no group.
    /// </summary>
    /// <param name="national">The league's national competition.</param>
    /// <param name="planned">The planned matches.</param>
    /// <returns>One staged match for each planned match.</returns>
    private static List<StagedMatch> ToStagedMatches(
        GetCompetitionsResult national,
        IReadOnlyList<FinalsPlannedMatch> planned)
    {
        return planned
            .Select(match => new StagedMatch(
                national.Id,
                match.Kickoff,
                (MatchStage)FinalsStages.StageOf(match.Round),
                RoundNumber: null,
                Leg: null,
                match.HomeLeagueTeamId,
                match.AwayLeagueTeamId))
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
    /// Describes the rounds that have matches in a plan, in round order, with their match counts.
    /// </summary>
    /// <param name="planned">The matches that were written.</param>
    /// <returns>One part for each round, such as "round of 16 (8 matches)", joined with commas.</returns>
    private static string DescribeRounds(IReadOnlyList<FinalsPlannedMatch> planned)
    {
        List<string> parts = [];

        foreach (IGrouping<FinalsRound, FinalsPlannedMatch> round in planned
            .GroupBy(match => match.Round)
            .OrderBy(group => FinalsStages.StageOf(group.Key)))
        {
            int matchCount = round.Count();
            string noun = matchCount == 1 ? "match" : "matches";

            parts.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{FinalsStages.NameOf(round.Key)} ({matchCount} {noun})"));
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Logs what came of the write: the lock, or the knockout matches written. Every other reason to write nothing was
    /// logged where it was found.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="outcome">What came of writing under the season's lock.</param>
    /// <param name="written">What was written; null unless the rows were written.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the outcome has no reason that the step knows, which is a bug.
    /// </exception>
    private void LogOutcome(
        LeagueStepContext context,
        FixtureWriteOutcome outcome,
        IReadOnlyList<FinalsPlannedMatch>? written)
    {
        switch (outcome)
        {
            case FixtureWriteOutcome.LockHeld:
                _logger.LogInformation(
                    LogPrefix + ": another run holds this season's lock, so this run leaves the World Cup knockouts "
                    + "to it.",
                    context.League.Name,
                    context.League.Id,
                    context.Season.SeasonNumber,
                    context.Season.Id);

                break;

            case FixtureWriteOutcome.Written when written is not null:
                _logger.LogInformation(
                    LogPrefix + ": wrote World Cup knockout matches, {MatchCount} in all: {Rounds}; {FirstKickoff} "
                    + "to {LastKickoff} UTC.",
                    context.League.Name,
                    context.League.Id,
                    context.Season.SeasonNumber,
                    context.Season.Id,
                    written.Count,
                    DescribeRounds(written),
                    written.Min(match => match.Kickoff).ToString(DateFormat, CultureInfo.InvariantCulture),
                    written.Max(match => match.Kickoff).ToString(DateFormat, CultureInfo.InvariantCulture));

                break;

            case FixtureWriteOutcome.NotWritten:
                break;

            default:
                throw new InvalidOperationException(
                    $"The World Cup knockout step has no reason for the outcome {outcome}, which is a bug.");
        }
    }

    /// <summary>
    /// Logs that the league matches are not written yet.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    private void LogWaitingForLeague(LeagueStepContext context)
    {
        _logger.LogInformation(
            LogPrefix + ": the league matches are not written yet, so the World Cup knockouts wait for a later tick.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id);
    }

    /// <summary>
    /// Logs that the calendar the league was written on has no World Cup finals.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="clubCounts">The number of clubs in each division, in division order.</param>
    private void LogNoWorldCupFinals(LeagueStepContext context, IReadOnlyList<int> clubCounts)
    {
        _logger.LogInformation(
            LogPrefix + ": the calendar has no World Cup finals at {ClubCounts} clubs per division, so no knockout "
            + "matches are written.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            string.Join(", ", clubCounts.Select(count => count.ToString(CultureInfo.InvariantCulture))));
    }

    /// <summary>
    /// Logs that the finals wait for their group matches to finish.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="reading">What the finals' matches say.</param>
    private void LogWaitingForGroups(LeagueStepContext context, FinalsReading reading)
    {
        _logger.LogInformation(
            LogPrefix + ": the World Cup finals wait for their groups: {Finished} of {Total} matches are finished.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            reading.FinishedGroupMatches,
            WorldCupFinalsShape.GroupMatchCount);
    }

    /// <summary>
    /// Logs that the finals wait for a continental cup to write its final.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="cup">The continental cup that has matches but no final yet.</param>
    private void LogWaitingForCup(LeagueStepContext context, GetCompetitionsResult cup)
    {
        _logger.LogInformation(
            LogPrefix + ": the World Cup finals wait for the continental final of {Cup}.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            DescribeCup(cup));
    }

    /// <summary>
    /// Logs that the finals have no knockout match to write on this tick. Both counts take in every knockout match, the
    /// bracket's ties and the third-place match, so when everything is finished the line says 16 and 16.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="reading">What the finals' matches say.</param>
    private void LogNothingNew(LeagueStepContext context, FinalsReading reading)
    {
        int written = reading.WrittenTies.Count + (reading.ThirdPlace is { IsWritten: true } ? 1 : 0);
        int decided = reading.Winners.Count + (reading.ThirdPlace is { IsFinished: true } ? 1 : 0);

        _logger.LogInformation(
            LogPrefix + ": the World Cup finals have no new knockout match: {Written} written and {Decided} decided.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            written,
            decided);
    }

    /// <summary>
    /// Logs that the World Cup knockouts are refused, and why.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="reason">What is wrong and how to fix it.</param>
    private void LogRefusal(LeagueStepContext context, string reason)
    {
        _logger.LogError(
            LogPrefix + ": the World Cup knockouts are refused: {Reason} The next tick tries again.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            reason);
    }
}
