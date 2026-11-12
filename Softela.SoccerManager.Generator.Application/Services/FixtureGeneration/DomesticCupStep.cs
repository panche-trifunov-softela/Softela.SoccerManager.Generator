using System.Globalization;
using Microsoft.Extensions.Logging;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Repositories;
using Softela.SoccerManager.Generator.Application.Services.DomesticCup;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// The domestic cup step: it writes the ties of a league's domestic cup, with the statistics rows of both clubs, as
/// they become known. It reads the season again inside its own lock, so it sees the league matches that the step
/// before it wrote, and it refuses a cup it cannot read without ever failing the run.
/// </summary>
internal sealed class DomesticCupStep : ILeagueStep
{
    /// <summary>
    /// The competition format of a domestic cup: a knockout only, as the backend's
    /// <c>CompetitionFormat.KnockoutOnly</c>.
    /// </summary>
    private const byte KnockoutOnlyFormat = 1;

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
    private readonly ILeagueTeamRepository _leagueTeamRepository;
    private readonly ILogger<DomesticCupStep> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DomesticCupStep"/> class.
    /// </summary>
    /// <param name="leagueFixtureWriter">The writer that holds the season's transaction and lock.</param>
    /// <param name="matchInserter">The inserter of the matches and their statistics rows.</param>
    /// <param name="matchRepository">The repository that reads the season's scores.</param>
    /// <param name="leagueTeamRepository">The repository that reads leagues' rosters and clubs' squads.</param>
    /// <param name="logger">The logger.</param>
    public DomesticCupStep(
        LeagueFixtureWriter leagueFixtureWriter,
        MatchInserter matchInserter,
        IMatchRepository matchRepository,
        ILeagueTeamRepository leagueTeamRepository,
        ILogger<DomesticCupStep> logger)
    {
        _leagueFixtureWriter = leagueFixtureWriter;
        _matchInserter = matchInserter;
        _matchRepository = matchRepository;
        _leagueTeamRepository = leagueTeamRepository;
        _logger = logger;
    }

    /// <summary>
    /// Writes the ties of the league's domestic cup that are known and have no match yet, or says in the log why it
    /// does not.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes once the cup has been handled.</returns>
    public async Task RunAsync(LeagueStepContext context, CancellationToken cancellationToken)
    {
        GetLeaguesResult league = context.League;
        GetSeasonsResult season = context.Season;

        List<GetCompetitionsResult> cups = context.Competitions
            .Where(IsDomesticCup)
            .OrderBy(competition => competition.Id)
            .ToList();

        if (cups.Count == 0)
        {
            _logger.LogInformation(
                LogPrefix + ": the league has no domestic cup (a competition with Order 0, IsDomestic, Format 1 and "
                + "no age limit), so there is no cup to write.",
                league.Name,
                league.Id,
                season.SeasonNumber,
                season.Id);

            return;
        }

        if (cups.Count > 1)
        {
            _logger.LogError(
                LogPrefix + ": {CupCount} competitions have the domestic cup's shape, {Cups}, so the cup is refused. "
                + "Keep that shape on one of them. The next tick tries again.",
                league.Name,
                league.Id,
                season.SeasonNumber,
                season.Id,
                cups.Count,
                string.Join(", ", cups.Select(DescribeCup)));

            return;
        }

        GetCompetitionsResult cup = cups[0];
        DomesticCupReading? reading = null;
        DomesticCupPlan? plan = null;
        string? refusal = null;
        IReadOnlyList<int> thinClubs = [];
        int measuredCount = 0;

        FixtureWriteOutcome outcome = await _leagueFixtureWriter.WriteAsync(
            league,
            season,
            async seasonMatches =>
            {
                (reading, plan, refusal, thinClubs, measuredCount) =
                    await ReadAndPlanAsync(context, cup, seasonMatches);

                if (plan is null || plan.Status != DomesticCupPlanStatus.Write)
                {
                    return false;
                }

                await _matchInserter.InsertStagedMatchesAsync(season.Id, ToStagedMatches(cup, plan), context.RunTime);

                return true;
            },
            cancellationToken);

        LogThinClubs(context, thinClubs, measuredCount);
        LogOutcome(context, cup, outcome, reading, plan, refusal);
    }

    /// <summary>
    /// Tells whether a competition has the shape of a domestic cup: it is not a division, it is domestic, it is a
    /// knockout and it has no age limit, which sets it apart from a youth cup.
    /// </summary>
    /// <param name="competition">The competition.</param>
    /// <returns>True when the competition is shaped like a domestic cup.</returns>
    private static bool IsDomesticCup(GetCompetitionsResult competition)
    {
        return competition.Order == 0
            && competition.IsDomestic
            && competition.Format == KnockoutOnlyFormat
            && competition.MaxAgeAllowed is null;
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
    /// Turns the ties of a plan into the matches to insert, in the plan's order. Each is a single match of the cup's
    /// round.
    /// </summary>
    /// <param name="cup">The cup's competition.</param>
    /// <param name="plan">The plan to write.</param>
    /// <returns>One staged match for each tie of the plan.</returns>
    /// <exception cref="OverflowException">
    /// Thrown when a round number does not fit in a byte, which no domestic cup reaches.
    /// </exception>
    private static List<StagedMatch> ToStagedMatches(GetCompetitionsResult cup, DomesticCupPlan plan)
    {
        return plan.Ties
            .Select(tie => new StagedMatch(
                cup.Id,
                tie.Kickoff,
                MatchStage.DomesticCupRound,
                checked((byte)tie.RoundNumber),
                Leg: null,
                tie.HomeLeagueTeamId,
                tie.AwayLeagueTeamId))
            .ToList();
    }

    /// <summary>
    /// Reads the cup from the season's matches and scores, measures the squads the plan asks for and plans the cup's
    /// next ties. It runs inside the writer's transaction, after the season's lock was taken. A refusal is caught here
    /// and handed back as its message; any other exception goes on to the writer.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="cup">The cup's competition.</param>
    /// <param name="seasonMatches">The season's matches, read inside the transaction after the lock was taken.</param>
    /// <returns>
    /// A task whose result holds the reading, the plan, the refusal message, the ids of the clubs with a thin squad
    /// and the number of clubs measured. The plan is null when the league is not written yet and when the cup was
    /// refused; the refusal is null unless the cup was refused. The clubs with a thin squad and the number measured
    /// are kept when the plan refuses the cup after the squads were measured, and are empty and 0 when the refusal
    /// came from the measuring itself.
    /// </returns>
    private async Task<(
        DomesticCupReading? Reading,
        DomesticCupPlan? Plan,
        string? Refusal,
        IReadOnlyList<int> ThinClubs,
        int MeasuredCount)> ReadAndPlanAsync(
        LeagueStepContext context,
        GetCompetitionsResult cup,
        IReadOnlyList<GetMatchesBySeasonResult> seasonMatches)
    {
        IReadOnlyList<int> thinClubs = [];
        int measuredCount = 0;

        try
        {
            IReadOnlyList<GetMatchScoresBySeasonResult> scores =
                await _matchRepository.GetScoresBySeasonIdAsync(context.Season.Id);

            List<int> divisionIds = context.Divisions.Select(division => division.Id).ToList();

            DomesticCupReading reading = DomesticCupReader.Read(
                context.Season.Id,
                cup.Id,
                divisionIds,
                SeasonMatchJoiner.Join(seasonMatches, scores));

            if (reading.Status == DomesticCupReadingStatus.WaitingForLeague)
            {
                return (reading, null, null, [], 0);
            }

            IReadOnlyList<int> clubs = DomesticCupPlanner.ClubsToMeasure(reading, context.RunTime);
            (List<SquadStrength> strengths, thinClubs) = await MeasureAsync(context.League.Id, clubs);
            measuredCount = clubs.Count;

            DomesticCupPlan plan = DomesticCupPlanner.Plan(reading, strengths, context.RunTime);

            return (reading, plan, null, thinClubs, measuredCount);
        }
        catch (SeasonRefusedException refusal)
        {
            return (null, null, refusal.Message, thinClubs, measuredCount);
        }
    }

    /// <summary>
    /// Measures the squads of the given clubs. It reads nothing when no club is given.
    /// </summary>
    /// <param name="leagueId">The id of the league, whose roster gives each club's team row.</param>
    /// <param name="clubs">The league team ids of the clubs to measure.</param>
    /// <returns>
    /// A task whose result holds the strength of each club, in the order given, and the ids of the clubs with fewer
    /// rated players than a full squad, in ascending order.
    /// </returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when a club is not on the roster, or a squad lists a player with two different ratings.
    /// </exception>
    private async Task<(List<SquadStrength> Strengths, List<int> ThinClubs)> MeasureAsync(
        int leagueId,
        IReadOnlyList<int> clubs)
    {
        List<SquadStrength> strengths = [];
        List<int> thinClubs = [];

        if (clubs.Count == 0)
        {
            return (strengths, thinClubs);
        }

        IReadOnlyList<GetLeagueTeamsResult> roster = await _leagueTeamRepository.GetByLeagueIdAsync(leagueId);
        Dictionary<int, int> teamIdOf = roster.ToDictionary(entry => entry.Id, entry => entry.TeamId);

        foreach (int club in clubs)
        {
            if (!teamIdOf.TryGetValue(club, out int teamId))
            {
                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Club {club} plays the league's matches but is not on ")
                    + "its roster, so its squad cannot be measured for the cup. "
                    + "Put the club back on the roster, or reset the season.");
            }

            IReadOnlyList<GetLeagueTeamSquadResult> rows = await _leagueTeamRepository.GetSquadAsync(club);

            int ratedPlayers = rows.Where(row => row.Rating is not null).Select(row => row.Id).Distinct().Count();

            if (ratedPlayers < SquadStrengthRanking.BestCount)
            {
                thinClubs.Add(club);
            }

            strengths.Add(Measure(club, teamId, rows));
        }

        thinClubs.Sort();

        return (strengths, thinClubs);
    }

    /// <summary>
    /// Measures one club's squad, turning the library's complaint about the squad's rows into a refusal.
    /// </summary>
    /// <param name="club">The league team id of the club.</param>
    /// <param name="teamId">The id of the club's team row.</param>
    /// <param name="rows">The club's squad rows.</param>
    /// <returns>The strength of the club's squad.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the squad lists a player with two different ratings.
    /// </exception>
    private static SquadStrength Measure(int club, int teamId, IReadOnlyList<GetLeagueTeamSquadResult> rows)
    {
        List<SquadPlayerRating> players = rows.Select(row => new SquadPlayerRating(row.Id, row.Rating)).ToList();

        try
        {
            return SquadStrengthRanking.Measure(club, teamId, players);
        }
        catch (ArgumentException exception) when (exception is not ArgumentNullException
            and not ArgumentOutOfRangeException)
        {
            string reason = DomesticCupReader.ReasonOf(exception);

            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The squad of club {club} cannot be measured for the cup: {reason} Fix the club's player rows."));
        }
    }

    /// <summary>
    /// Logs the clubs measured for the cup that have fewer rated players than a full squad, once for the tick.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="thinClubs">The ids of the clubs with a thin squad, in ascending order.</param>
    /// <param name="measuredCount">The number of clubs measured.</param>
    private void LogThinClubs(LeagueStepContext context, IReadOnlyList<int> thinClubs, int measuredCount)
    {
        if (thinClubs.Count == 0)
        {
            return;
        }

        _logger.LogWarning(
            LogPrefix + ": clubs measured for the domestic cup with fewer than {RatedPlayerCount} rated players "
            + "({ThinCount} of {MeasuredCount}): {ThinClubs}. A missing place counts as 0 in a squad's strength.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            SquadStrengthRanking.BestCount,
            thinClubs.Count,
            measuredCount,
            string.Join(", ", thinClubs.Select(club => club.ToString(CultureInfo.InvariantCulture))));
    }

    /// <summary>
    /// Logs what came of the write: the lock, the ties written, or the reason none were.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="cup">The cup's competition.</param>
    /// <param name="outcome">What came of writing under the season's lock.</param>
    /// <param name="reading">What the season's matches said about the cup, or null when the cup was refused.</param>
    /// <param name="plan">
    /// What the cup asked for, or null when the league is not written yet or the cup was refused.
    /// </param>
    /// <param name="refusal">The message of the cup's refusal, or null when it was not refused.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the outcome has no reason that the step knows, which is a bug.
    /// </exception>
    private void LogOutcome(
        LeagueStepContext context,
        GetCompetitionsResult cup,
        FixtureWriteOutcome outcome,
        DomesticCupReading? reading,
        DomesticCupPlan? plan,
        string? refusal)
    {
        GetLeaguesResult league = context.League;
        GetSeasonsResult season = context.Season;

        switch (outcome)
        {
            case FixtureWriteOutcome.LockHeld:
                _logger.LogInformation(
                    LogPrefix + ": another run holds this season's lock, so this run leaves the domestic cup to it.",
                    league.Name,
                    league.Id,
                    season.SeasonNumber,
                    season.Id);

                break;

            case FixtureWriteOutcome.Written when plan is not null:
                LogWritten(context, cup, plan);
                break;

            case FixtureWriteOutcome.NotWritten when refusal is not null:
                _logger.LogError(
                    LogPrefix + ": the domestic cup '{CupName}' (competition {CupId}) is refused: {Reason} "
                    + "The next tick tries again.",
                    league.Name,
                    league.Id,
                    season.SeasonNumber,
                    season.Id,
                    cup.Name,
                    cup.Id,
                    refusal);

                break;

            case FixtureWriteOutcome.NotWritten when reading?.Status == DomesticCupReadingStatus.WaitingForLeague:
                _logger.LogInformation(
                    LogPrefix + ": the league matches are not written yet, so the domestic cup '{CupName}' "
                    + "(competition {CupId}) waits for a later tick.",
                    league.Name,
                    league.Id,
                    season.SeasonNumber,
                    season.Id,
                    cup.Name,
                    cup.Id);

                break;

            case FixtureWriteOutcome.NotWritten when plan?.Status == DomesticCupPlanStatus.NothingNew:
                _logger.LogInformation(
                    LogPrefix + ": the domestic cup '{CupName}' (competition {CupId}) has no new tie: every tie whose "
                    + "two clubs are known already has its match.",
                    league.Name,
                    league.Id,
                    season.SeasonNumber,
                    season.Id,
                    cup.Name,
                    cup.Id);

                break;

            case FixtureWriteOutcome.NotWritten when plan?.Status == DomesticCupPlanStatus.PlaysWithoutCup:
                _logger.LogWarning(
                    LogPrefix + ": round 1 of the domestic cup '{CupName}' (competition {CupId}) can no longer be "
                    + "played before round 2 on {RoundTwoDate} UTC, so the season plays without the cup.",
                    league.Name,
                    league.Id,
                    season.SeasonNumber,
                    season.Id,
                    cup.Name,
                    cup.Id,
                    plan.RoundTwoDate?.ToString(DateFormat, CultureInfo.InvariantCulture) ?? "none");

                break;

            default:
                throw new InvalidOperationException(
                    $"The domestic cup step has no reason for the outcome {outcome}, which is a bug.");
        }
    }

    /// <summary>
    /// Logs the ties a committed write holds.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="cup">The cup's competition.</param>
    /// <param name="plan">The plan that was written.</param>
    private void LogWritten(LeagueStepContext context, GetCompetitionsResult cup, DomesticCupPlan plan)
    {
        _logger.LogInformation(
            LogPrefix + ": wrote ties of the domestic cup '{CupName}' (competition {CupId}), {TieCount} in all, "
            + "with {StatisticCount} statistic rows: {Rounds}.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            cup.Name,
            cup.Id,
            plan.Ties.Count,
            plan.Ties.Count * 2,
            DescribeRounds(plan));
    }

    /// <summary>
    /// Describes the rounds that have ties in a plan, in round order, and the clubs that skip round 1 when the plan
    /// starts the cup.
    /// </summary>
    /// <param name="plan">The plan that was written.</param>
    /// <returns>One part for each round, followed by the bye count when the plan starts the cup.</returns>
    private static string DescribeRounds(DomesticCupPlan plan)
    {
        List<string> parts = [];

        foreach (IGrouping<int, DomesticCupPlannedTie> round in plan.Ties
            .GroupBy(tie => tie.RoundNumber)
            .OrderBy(group => group.Key))
        {
            DateTime first = round.Min(tie => tie.Kickoff);
            DateTime last = round.Max(tie => tie.Kickoff);

            string firstText = first.ToString(DateFormat, CultureInfo.InvariantCulture);
            string lastText = last.ToString(DateFormat, CultureInfo.InvariantCulture);

            int tieCount = round.Count();
            string noun = tieCount == 1 ? "tie" : "ties";

            parts.Add(first == last
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"round {round.Key}: {tieCount} {noun} on {firstText} UTC")
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"round {round.Key}: {tieCount} {noun} from {firstText} to {lastText} UTC"));
        }

        if (plan.ByeLeagueTeamIds.Count > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{plan.ByeLeagueTeamIds.Count} clubs skip round 1"));
        }

        return string.Join("; ", parts);
    }
}
