using System.Globalization;
using Microsoft.Extensions.Logging;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Repositories;
using Softela.SoccerManager.Generator.Application.Services.DomesticCup;
using Softela.SoccerManager.Generator.Application.Services.Playoffs;
using Softela.SoccerManager.Generator.Application.Services.Rollover;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// The season rollover step, the ninth and last step of a tick: it writes the league's next season once the newest
/// season has ended. It does nothing until the last match of the divisions has kicked off, and from then on it reads
/// the season again inside its own lock at every tick, to see whether the season has ended by its shape. When it has,
/// it moves the clubs between the divisions as the divisions' settings say, and writes the next season with the
/// divisions' Standings at zero and its league matches, in one transaction. It refuses a season it cannot roll over
/// without ever failing the run. See the readme's "Season rollover".
/// </summary>
internal sealed class SeasonRolloverStep : ILeagueStep
{
    /// <summary>
    /// The start of every log line about the league's season.
    /// </summary>
    private const string LogPrefix = LeagueStepContext.LogPrefix;

    private readonly IMatchRepository _matchRepository;
    private readonly IStandingRepository _standingRepository;
    private readonly ILeagueTeamRepository _leagueTeamRepository;
    private readonly ISeasonRepository _seasonRepository;
    private readonly LeagueFixtureWriter _leagueFixtureWriter;
    private readonly MatchInserter _matchInserter;
    private readonly ILogger<SeasonRolloverStep> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonRolloverStep"/> class.
    /// </summary>
    /// <param name="matchRepository">The repository that reads the season's scores.</param>
    /// <param name="standingRepository">The repository that reads and writes the divisions' Standings.</param>
    /// <param name="leagueTeamRepository">The repository that reads the league's roster.</param>
    /// <param name="seasonRepository">The repository that reads and creates the league's seasons.</param>
    /// <param name="leagueFixtureWriter">The writer that holds the season's transaction and lock.</param>
    /// <param name="matchInserter">The inserter of the matches and their statistics rows.</param>
    /// <param name="logger">The logger.</param>
    public SeasonRolloverStep(
        IMatchRepository matchRepository,
        IStandingRepository standingRepository,
        ILeagueTeamRepository leagueTeamRepository,
        ISeasonRepository seasonRepository,
        LeagueFixtureWriter leagueFixtureWriter,
        MatchInserter matchInserter,
        ILogger<SeasonRolloverStep> logger)
    {
        _matchRepository = matchRepository;
        _standingRepository = standingRepository;
        _leagueTeamRepository = leagueTeamRepository;
        _seasonRepository = seasonRepository;
        _leagueFixtureWriter = leagueFixtureWriter;
        _matchInserter = matchInserter;
        _logger = logger;
    }

    /// <summary>
    /// Writes the league's next season when the newest season has ended, or says in the log why it does not.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes once the season has been handled.</returns>
    public async Task RunAsync(LeagueStepContext context, CancellationToken cancellationToken)
    {
        HashSet<int> divisionIds = context.Divisions.Select(division => division.Id).ToHashSet();

        List<GetMatchesResult> divisionMatches = context.LeagueMatches
            .Where(match => match.SeasonId == context.Season.Id
                && match.CompetitionId is { } competitionId
                && divisionIds.Contains(competitionId))
            .ToList();

        if (divisionMatches.Count == 0)
        {
            LogNoLeagueMatches(context);

            return;
        }

        DateTime lastKickoff = divisionMatches.Max(match => match.StartDateTime);

        if (lastKickoff > context.RunTime)
        {
            LogSeasonRuns(context, lastKickoff);

            return;
        }

        List<RolloverWrite> written = [];

        FixtureWriteOutcome outcome = await _leagueFixtureWriter.WriteAsync(
            context.League,
            context.Season,
            seasonMatches => RollOverAsync(context, seasonMatches, written),
            cancellationToken);

        LogOutcome(context, outcome, written);
    }

    /// <summary>
    /// Reads the season again inside the lock, plans the next season and inserts it. Only the planning and the
    /// building of the fixtures are guarded, and only a refusal of the season or of a helper is logged there (see
    /// <see cref="RefusalOf"/>): any other failure, and a failure of an insert, must reach the writer, which rolls back
    /// and fails the run.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="seasonMatches">The season's matches, read inside the transaction after the lock was taken.</param>
    /// <param name="written">The list that receives what was written.</param>
    /// <returns>A task whose result is true when the next season was inserted.</returns>
    private async Task<bool> RollOverAsync(
        LeagueStepContext context,
        IReadOnlyList<GetMatchesBySeasonResult> seasonMatches,
        List<RolloverWrite> written)
    {
        IReadOnlyList<GetSeasonsResult> seasons = await _seasonRepository.GetByLeagueIdAsync(context.League.Id);
        GetSeasonsResult? newest = seasons.MaxBy(season => season.SeasonNumber);

        if (newest is null || newest.Id != context.Season.Id)
        {
            LogNewerSeason(context, newest);

            return false;
        }

        RolloverPlan? plan;

        try
        {
            plan = await PlanAsync(context, seasonMatches);
        }
        catch (Exception exception) when (RefusalOf(exception) is { } reason)
        {
            LogRefusal(context, reason);

            return false;
        }

        if (plan is null)
        {
            return false;
        }

        int nextNumber = context.Season.SeasonNumber + 1;

        int? createdId = await _seasonRepository.CreateAsync(
            context.League.Id,
            nextNumber,
            context.RunTime,
            GeneratorAudit.UserId);

        if (createdId is not { } nextId)
        {
            LogNumberTaken(context, nextNumber);

            return false;
        }

        LeagueInput input;

        try
        {
            input = BuildInput(context, plan, nextId, nextNumber);
        }
        catch (Exception exception) when (RefusalOf(exception) is { } reason)
        {
            LogRefusal(context, reason);

            return false;
        }

        await InsertAsync(context, plan.Moves, input, nextId);

        written.Add(DescribeWrite(context, plan, input, nextNumber, nextId));

        return true;
    }

    /// <summary>
    /// Reads the season's scores, the divisions' Standings and the league's roster, and says whether the season has
    /// ended. When it has, plans the moves of the clubs. It writes nothing. A season that is not over is logged and
    /// gives null.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="seasonMatches">The season's matches, read inside the transaction after the lock was taken.</param>
    /// <returns>The reading of the ended season with its moves; null when the season is not over.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the season cannot be read or its moves cannot be planned because of what it holds.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a helper refuses its input, which is a bug in the caller.
    /// </exception>
    private async Task<RolloverPlan?> PlanAsync(
        LeagueStepContext context,
        IReadOnlyList<GetMatchesBySeasonResult> seasonMatches)
    {
        IReadOnlyList<GetMatchScoresBySeasonResult> scores =
            await _matchRepository.GetScoresBySeasonIdAsync(context.Season.Id);

        IReadOnlyList<SeasonMatch> season = SeasonMatchJoiner.Join(seasonMatches, scores);
        Dictionary<int, IReadOnlyList<GetStandingsResult>> standingsByDivision = [];

        foreach (GetCompetitionsResult division in context.Divisions)
        {
            standingsByDivision[division.Id] =
                await _standingRepository.GetByCompetitionAndSeasonAsync(division.Id, context.Season.Id);
        }

        IReadOnlyList<GetLeagueTeamsResult> roster =
            await _leagueTeamRepository.GetByLeagueIdAsync(context.League.Id);

        Dictionary<int, int?> stadiumOfClub = ClubStadiums.Of(roster, context.Teams);

        SeasonEndReading reading = SeasonEndReader.Read(
            context.Season.Id,
            context.RunTime,
            context.Divisions,
            context.Competitions,
            season,
            standingsByDivision,
            roster,
            stadiumOfClub);

        if (reading.Status == SeasonEndStatus.Running)
        {
            LogNotOver(context, reading.WaitingFor);

            return null;
        }

        RolloverMoves moves = DivisionMoves.Plan(
            context.Divisions,
            reading.Places,
            reading.Tables,
            reading.PlayoffWinners);

        return new RolloverPlan(reading, moves, roster);
    }

    /// <summary>
    /// Builds the next season's league fixtures and dates from the moved clubs, on the divisions' Standings at zero.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="plan">The reading of the ended season and the moves of its clubs.</param>
    /// <param name="nextId">The id of the season that was just created.</param>
    /// <param name="nextNumber">The number of the season that was just created.</param>
    /// <returns>The fixtures and dates of the next season's league matches.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the moved clubs cannot be laid out as a season, for example because too many clubs of one division
    /// share a stadium.
    /// </exception>
    private static LeagueInput BuildInput(LeagueStepContext context, RolloverPlan plan, int nextId, int nextNumber)
    {
        Dictionary<int, IReadOnlyList<GetStandingsResult>> standingsByDivision = [];

        foreach (LeagueDivision division in plan.Moves.Divisions)
        {
            standingsByDivision[division.CompetitionId] = division.LeagueTeamIds
                .Select(club => new GetStandingsResult { LeagueTeamId = club, ModifiedAt = context.RunTime })
                .ToList();
        }

        GetSeasonsResult nextSeason = new() { Id = nextId, SeasonNumber = nextNumber };

        // The tick's snapshot of the league's matches is enough for the new season's dates: once the old season has
        // ended by its shape, no earlier step of this tick had a match of it left to write (its league, playoffs, cup
        // and finals are decided, every continental round is written before the World Cup finals are, and a continental
        // cup can no longer start), so no match of the old season is missing from the snapshot.
        return LeagueInputBuilder.Build(
            context.League,
            nextSeason,
            context.Divisions,
            standingsByDivision,
            plan.Roster,
            context.Teams,
            context.LeagueMatches,
            context.RunTime,
            plan.Reading.StartDate(context.RunTime));
    }

    /// <summary>
    /// Inserts the next season's Standings rows at zero, one for each club of each division, and then its league
    /// matches with their statistics rows. A failure is not caught: it reaches the writer, which rolls back.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="moves">The clubs of each division in the next season.</param>
    /// <param name="input">The fixtures and dates of the next season's league matches.</param>
    /// <param name="nextId">The id of the season that was just created.</param>
    /// <returns>A task that completes once every row has been inserted.</returns>
    private async Task InsertAsync(LeagueStepContext context, RolloverMoves moves, LeagueInput input, int nextId)
    {
        foreach (LeagueDivision division in moves.Divisions)
        {
            foreach (int club in division.LeagueTeamIds)
            {
                await _standingRepository.CreateAsync(
                    division.CompetitionId,
                    nextId,
                    club,
                    context.RunTime,
                    GeneratorAudit.UserId);
            }
        }

        await _matchInserter.InsertLeagueMatchesAsync(input, context.RunTime);
    }

    /// <summary>
    /// Describes what was written, for the log line that follows the commit.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="plan">The reading of the ended season and the moves of its clubs.</param>
    /// <param name="input">The fixtures and dates that were written.</param>
    /// <param name="nextNumber">The number of the season that was written.</param>
    /// <param name="nextId">The id of the season that was written.</param>
    /// <returns>What the log line says about the write.</returns>
    private static RolloverWrite DescribeWrite(
        LeagueStepContext context,
        RolloverPlan plan,
        LeagueInput input,
        int nextNumber,
        int nextId)
    {
        Dictionary<int, string> teamNameOfClub = ClubNames.Of(plan.Roster, context.Teams);

        return new RolloverWrite(
            nextNumber,
            nextId,
            input.Divisions.Sum(division => division.LeagueTeamIds.Count),
            input.Divisions.Count,
            input.Fixtures.Sum(fixtures => fixtures.Count),
            input.Matchdays.Min(),
            input.Matchdays.Max(),
            ClubNames.DescribeAll(plan.Moves.Promoted, teamNameOfClub),
            ClubNames.DescribeAll(plan.Moves.Relegated, teamNameOfClub),
            plan.Reading.FormerDivisions);
    }

    /// <summary>
    /// Tells whether an exception that the planning or the building of the fixtures threw is a refusal. A refusal of
    /// the season and a helper that refused its input are refusals; anything else, a null argument or an argument out
    /// of range included, is not, and reaches the writer, which rolls back and fails the run.
    /// </summary>
    /// <param name="exception">The exception that was thrown.</param>
    /// <returns>The reason for the log line when the exception is a refusal; otherwise null.</returns>
    private static string? RefusalOf(Exception exception)
    {
        return exception switch
        {
            SeasonRefusedException refusal => refusal.Message,
            ArgumentNullException or ArgumentOutOfRangeException => null,
            ArgumentException argument => HelperRefusal(argument),
            _ => null,
        };
    }

    /// <summary>
    /// Words the refusal of a helper that was given an input it cannot take, which is a bug in the step, not in the
    /// season.
    /// </summary>
    /// <param name="exception">The exception that the helper threw.</param>
    /// <returns>The reason for the log line.</returns>
    private static string HelperRefusal(ArgumentException exception)
    {
        string reason = DomesticCupReader.ReasonOf(exception);

        return $"A helper of the season rollover refused its input: {reason}";
    }

    /// <summary>
    /// Logs that the season had no league match when the run read the league's matches at its start, so it cannot end.
    /// It is also logged on the tick that writes the season's league, because the run read the matches before they
    /// were written.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    private void LogNoLeagueMatches(LeagueStepContext context)
    {
        _logger.LogInformation(
            LogPrefix + ": the season had no league matches at the start of this run, so it cannot end yet.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id);
    }

    /// <summary>
    /// Logs that the season still has a match to play, so it does not roll over yet.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="lastKickoff">The kickoff of the season's last division match, in UTC.</param>
    private void LogSeasonRuns(LeagueStepContext context, DateTime lastKickoff)
    {
        _logger.LogInformation(
            LogPrefix + ": the season runs until {LastKickoff} UTC, so it does not roll over yet.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            lastKickoff.ToString(LeagueInputBuilder.KickoffFormat, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Logs that the league has a newer season than the one this run looked at, so nothing is rolled over.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="newest">The league's newest season as read inside the lock; null when it has none left.</param>
    private void LogNewerSeason(LeagueStepContext context, GetSeasonsResult? newest)
    {
        if (newest is null)
        {
            _logger.LogInformation(
                LogPrefix + ": the season does not exist any more, so it does not roll over.",
                context.League.Name,
                context.League.Id,
                context.Season.SeasonNumber,
                context.Season.Id);

            return;
        }

        _logger.LogInformation(
            LogPrefix + ": season {NewestNumber} (id {NewestId}) exists already, so this season does not roll over.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            newest.SeasonNumber,
            newest.Id);
    }

    /// <summary>
    /// Logs that the season is not over and what it waits for.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="waitingFor">
    /// The first thing the season waits for, as a phrase that completes "it waits for".
    /// </param>
    private void LogNotOver(LeagueStepContext context, string? waitingFor)
    {
        _logger.LogInformation(
            LogPrefix + ": the season is not over: it waits for {WaitingFor}.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            waitingFor);
    }

    /// <summary>
    /// Logs that the rollover is refused, and why.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="reason">What is wrong and how to fix it.</param>
    private void LogRefusal(LeagueStepContext context, string reason)
    {
        _logger.LogError(
            LogPrefix + ": the season rollover is refused: {Reason} The next tick tries again.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            reason);
    }

    /// <summary>
    /// Logs that another run made the next season while this run rolled the season over.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="nextNumber">The number of the season that this run tried to create.</param>
    private void LogNumberTaken(LeagueStepContext context, int nextNumber)
    {
        _logger.LogInformation(
            LogPrefix + ": season {NextNumber} was made while this run rolled the season over, so nothing is written.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            nextNumber);
    }

    /// <summary>
    /// Logs what came of the write: the lock, or the season written. Every other reason to write nothing was logged
    /// where it was found.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="outcome">What came of writing under the season's lock.</param>
    /// <param name="written">What was written; empty unless the rows were written.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the outcome has no reason that the step knows, which is a bug.
    /// </exception>
    private void LogOutcome(LeagueStepContext context, FixtureWriteOutcome outcome, List<RolloverWrite> written)
    {
        switch (outcome)
        {
            case FixtureWriteOutcome.LockHeld:
                _logger.LogInformation(
                    LogPrefix + ": another run holds this season's lock, so this run leaves the season rollover "
                    + "to it.",
                    context.League.Name,
                    context.League.Id,
                    context.Season.SeasonNumber,
                    context.Season.Id);

                break;

            case FixtureWriteOutcome.Written:
                foreach (RolloverWrite write in written)
                {
                    LogWritten(context, write);
                }

                break;

            case FixtureWriteOutcome.NotWritten:
                break;

            default:
                throw new InvalidOperationException(
                    $"The season rollover step has no reason for the outcome {outcome}, which is a bug.");
        }
    }

    /// <summary>
    /// Logs the season that a committed write made, and each competition that has no Standings in it.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="write">What was written.</param>
    private void LogWritten(LeagueStepContext context, RolloverWrite write)
    {
        _logger.LogInformation(
            LogPrefix + ": rolled the season over: wrote season {NextNumber} (id {NextId}) with {ClubCount} clubs in "
            + "{DivisionCount} divisions and {MatchCount} league matches, {FirstKickoff} to {LastKickoff} UTC; "
            + "promoted {Promoted}; relegated {Relegated}.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            write.NextNumber,
            write.NextId,
            write.ClubCount,
            write.DivisionCount,
            write.MatchCount,
            write.FirstKickoff.ToString(LeagueInputBuilder.KickoffFormat, CultureInfo.InvariantCulture),
            write.LastKickoff.ToString(LeagueInputBuilder.KickoffFormat, CultureInfo.InvariantCulture),
            write.Promoted,
            write.Relegated);

        foreach (GetCompetitionsResult former in write.FormerDivisions)
        {
            _logger.LogInformation(
                LogPrefix + ": competition '{Name}' (id {Id}) had league matches in this season but has Order 0 now, "
                + "so its clubs get no Standings in season {NextNumber}.",
                context.League.Name,
                context.League.Id,
                context.Season.SeasonNumber,
                context.Season.Id,
                former.Name,
                former.Id,
                write.NextNumber);
        }
    }

    /// <summary>
    /// What an ended season gave the rollover: how it ended, and where its clubs go.
    /// </summary>
    /// <param name="Reading">What the season's matches and Standings say about how it ended.</param>
    /// <param name="Moves">The clubs of each division in the next season, and which clubs moved.</param>
    /// <param name="Roster">The league's roster, as read for this rollover.</param>
    private sealed record RolloverPlan(
        SeasonEndReading Reading,
        RolloverMoves Moves,
        IReadOnlyList<GetLeagueTeamsResult> Roster);

    /// <summary>
    /// What a committed rollover wrote, as the log line says it.
    /// </summary>
    /// <param name="NextNumber">The number of the season that was written.</param>
    /// <param name="NextId">The id of the season that was written.</param>
    /// <param name="ClubCount">The clubs in all of its divisions.</param>
    /// <param name="DivisionCount">The number of its divisions.</param>
    /// <param name="MatchCount">The league matches that were written.</param>
    /// <param name="FirstKickoff">The kickoff of its first league round, in UTC.</param>
    /// <param name="LastKickoff">The kickoff of its last league round, in UTC.</param>
    /// <param name="Promoted">The clubs that moved up, named for the log, or "none".</param>
    /// <param name="Relegated">The clubs that moved down, named for the log, or "none".</param>
    /// <param name="FormerDivisions">
    /// The competitions that had league matches in the old season but have Order 0 now.
    /// </param>
    private sealed record RolloverWrite(
        int NextNumber,
        int NextId,
        int ClubCount,
        int DivisionCount,
        int MatchCount,
        DateTime FirstKickoff,
        DateTime LastKickoff,
        string Promoted,
        string Relegated,
        IReadOnlyList<GetCompetitionsResult> FormerDivisions);
}
