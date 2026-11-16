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
/// The continental cup step: it writes the league phase of the league's two continental cups, with one standings row
/// for each entrant, once the season's league matches exist. It reads the season again inside its own lock, so it sees
/// the league matches that the step before it wrote, and it refuses cups it cannot write without ever failing the run.
/// A written league phase is never changed: later ticks leave it as it is, and the knockouts that follow it are written
/// by <see cref="ContinentalKnockoutStep"/>.
/// </summary>
internal sealed class ContinentalCupStep : ILeagueStep
{
    /// <summary>
    /// The number of league clubs that the two cups take together: the first 8 of the pyramid for cup 1 and the next 8
    /// for cup 2.
    /// </summary>
    private const int MaxLeagueClubs = 16;

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
    private readonly IStandingRepository _standingRepository;
    private readonly ILogger<ContinentalCupStep> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContinentalCupStep"/> class.
    /// </summary>
    /// <param name="leagueFixtureWriter">The writer that holds the season's transaction and lock.</param>
    /// <param name="matchInserter">The inserter of the matches and their statistics rows.</param>
    /// <param name="matchRepository">The repository that reads the season's scores.</param>
    /// <param name="leagueTeamRepository">The repository that reads leagues' rosters and clubs' squads.</param>
    /// <param name="standingRepository">The repository that reads and creates standings rows.</param>
    /// <param name="logger">The logger.</param>
    public ContinentalCupStep(
        LeagueFixtureWriter leagueFixtureWriter,
        MatchInserter matchInserter,
        IMatchRepository matchRepository,
        ILeagueTeamRepository leagueTeamRepository,
        IStandingRepository standingRepository,
        ILogger<ContinentalCupStep> logger)
    {
        _leagueFixtureWriter = leagueFixtureWriter;
        _matchInserter = matchInserter;
        _matchRepository = matchRepository;
        _leagueTeamRepository = leagueTeamRepository;
        _standingRepository = standingRepository;
        _logger = logger;
    }

    /// <summary>
    /// Writes the league phase of the league's two continental cups when they are due and not written, or says in the
    /// log why it does not.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes once the cups have been handled.</returns>
    public async Task RunAsync(LeagueStepContext context, CancellationToken cancellationToken)
    {
        List<GetCompetitionsResult> cups = ContinentalCupFinder.Find(context.Competitions);

        // Cheap check before the lock: a tick that finds both cups with matches has nothing to do.
        if (cups.Count == 2
            && cups.All(cup => ContinentalCupFinder.HasMatchInSeason(context.LeagueMatches, context.Season.Id, cup)))
        {
            LogBothCupsHaveMatches(context, cups[0], cups[1]);

            return;
        }

        List<CupWrite> written = [];

        FixtureWriteOutcome outcome = await _leagueFixtureWriter.WriteAsync(
            context.League,
            context.Season,
            async seasonMatches =>
            {
                ContinentalPlan? plan;

                // Only the planning is guarded: a failure of the inserts must reach the writer, which rolls back.
                try
                {
                    plan = await PlanAsync(context, cups, seasonMatches);
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

                if (plan is null)
                {
                    return false;
                }

                await WritePlansAsync(context, plan.Cups, written);

                LogThinEntrants(context, plan);

                return true;
            },
            cancellationToken);

        LogOutcome(context, outcome, written);
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
    /// Reads the season again inside the lock and decides whether the cups are due, and plans them when they are. It
    /// writes nothing. A refusal is thrown to the caller, which logs it; every other reason to write nothing is logged
    /// here.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="cups">The competitions with a continental cup's shape, ordered by id.</param>
    /// <param name="seasonMatches">The season's matches, read inside the transaction after the lock was taken.</param>
    /// <returns>A task whose result is the plan of both cups, or null when there is nothing to write.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when the cups cannot be written as the rows stand.</exception>
    /// <exception cref="ArgumentException">Thrown when a helper refuses what the season holds.</exception>
    private async Task<ContinentalPlan?> PlanAsync(
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
            LogWaitingForLeague(context);

            return null;
        }

        Dictionary<int, int> divisionOfClub = DomesticCupLeagueReader.MapClubsToDivisions(leagueMatches);
        List<int> clubCounts = DomesticCupLeagueReader.CountClubsPerDivision(divisionIds, divisionOfClub);
        SeasonCalendar calendar = DomesticCupLeagueReader.RebuildCalendar(clubCounts, leagueMatches);

        if (calendar.ContinentalCups is not { } dates)
        {
            LogNoContinentalCalendar(context, clubCounts);

            return null;
        }

        if (cups.Count == 0)
        {
            LogNoCups(context);

            return null;
        }

        if (cups.Count != 2)
        {
            string described = string.Join(", ", cups.Select(DescribeCup));

            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The league needs exactly two competitions with the continental cup's shape and has "
                    + $"{cups.Count}: {described}. Keep that shape on two of them."));
        }

        ContinentalCupState state = await ReadStateAsync(context, cups[0], cups[1], season);

        if (state.Status == ContinentalCupStateStatus.Partial)
        {
            throw new SeasonRefusedException(
                $"{state.Reason} The two cups are written together, so reset both cups together (the readme's "
                + "'Writing a season again' has the steps).");
        }

        if (state.Status == ContinentalCupStateStatus.Written)
        {
            LogAlreadyWritten(context, cups[0], cups[1]);

            return null;
        }

        if (context.Season.SeasonNumber > 1)
        {
            LogEntrantsFromStrength(context);
        }

        CupFormat format = ReadFormat(dates);

        if (!ContinentalLeaguePhaseDatePlanner.CanStart(dates.LeaguePhase, format.FirstKnockout, context.RunTime))
        {
            LogPlaysWithoutCups(context, format.FirstKnockout);

            return null;
        }

        (ContinentalEntrantSelection selection, List<int> thinClubs) =
            await SelectEntrantsAsync(context, divisionOfClub, format.CupSize);

        List<CupPlan>? plans = PlanCups(context, cups, selection, format, season);

        if (plans is null)
        {
            return null;
        }

        return new ContinentalPlan(plans, thinClubs, selection.Cup1.Count + selection.Cup2.Count);
    }

    /// <summary>
    /// Reads how much of the two cups the season holds: the matches given and the standings rows of each cup.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="cup1">The first cup, the one with the lower id.</param>
    /// <param name="cup2">The second cup.</param>
    /// <param name="season">The season's matches with their scores.</param>
    /// <returns>A task whose result is the state of the two cups.</returns>
    private async Task<ContinentalCupState> ReadStateAsync(
        LeagueStepContext context,
        GetCompetitionsResult cup1,
        GetCompetitionsResult cup2,
        IReadOnlyList<SeasonMatch> season)
    {
        IReadOnlyList<GetStandingsResult> standings1 =
            await _standingRepository.GetByCompetitionAndSeasonAsync(cup1.Id, context.Season.Id);

        IReadOnlyList<GetStandingsResult> standings2 =
            await _standingRepository.GetByCompetitionAndSeasonAsync(cup2.Id, context.Season.Id);

        return ContinentalCupStateReader.Read(
            cup1.Id,
            cup2.Id,
            season,
            standings1.Select(row => row.LeagueTeamId).ToList(),
            standings2.Select(row => row.LeagueTeamId).ToList());
    }

    /// <summary>
    /// Reads the format of the cups from the calendar: how many clubs a cup has and when its knockout starts.
    /// </summary>
    /// <param name="dates">The calendar's continental cup dates.</param>
    /// <returns>The cup size, the dates and the first knockout date.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the league phase has a number of matchdays that no cup has.
    /// </exception>
    private static CupFormat ReadFormat(ContinentalCupDates dates)
    {
        int smallMatchdays = ContinentalMatchdayRules.MatchdayCount(ContinentalInputValidator.SmallCupClubCount);
        int largeMatchdays = ContinentalMatchdayRules.MatchdayCount(ContinentalInputValidator.LargeCupClubCount);

        if (!ContinentalMatchdayRules.TryGetCupSize(dates.LeaguePhase.Count, out int cupSize))
        {
            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The calendar's continental league phase has {dates.LeaguePhase.Count} matchdays, and a cup has ")
                + string.Create(
                    CultureInfo.InvariantCulture,
                    $"{smallMatchdays} or {largeMatchdays}, so the cups cannot be sized. This is a defect."));
        }

        DateTime firstKnockout = dates.PlayOff?.FirstLeg ?? dates.QuarterFinal.FirstLeg;

        return new CupFormat(dates, cupSize, firstKnockout);
    }

    /// <summary>
    /// Chooses the entrants of both cups: it reads the roster and every squad of the league once, measures the clubs
    /// of the divisions and the guests, and hands them to the selector.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="divisionOfClub">The competition id of the division, by league team id.</param>
    /// <param name="cupSize">The number of clubs in each cup.</param>
    /// <returns>
    /// A task whose result holds the entrants of both cups, in pot order, and the league team ids of the entrants with
    /// fewer rated players than a full squad, in ascending order.
    /// </returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when a club of a division is off the roster or is a guest, there are too few guests, the backend read is
    /// missing, or a squad lists a player with two different ratings.
    /// </exception>
    private async Task<(ContinentalEntrantSelection Selection, List<int> ThinClubs)> SelectEntrantsAsync(
        LeagueStepContext context,
        Dictionary<int, int> divisionOfClub,
        int cupSize)
    {
        IReadOnlyList<GetLeagueTeamsResult> roster = await _leagueTeamRepository.GetByLeagueIdAsync(context.League.Id);
        Dictionary<int, GetLeagueTeamsResult> rosterOf = roster.ToDictionary(entry => entry.Id);

        CheckDivisionClubs(divisionOfClub.Keys.Order(), rosterOf);

        List<GetLeagueTeamsResult> guests = roster.Where(entry => entry.IsGuest).OrderBy(entry => entry.Id).ToList();
        int needed = (2 * cupSize) - Math.Min(divisionOfClub.Count, MaxLeagueClubs);

        // Refused before the squads are read, so a lasting refusal never costs the read.
        if (guests.Count < needed)
        {
            throw TooFewGuests(needed, guests.Count);
        }

        IReadOnlyList<GetLeagueSquadRatingsResult> squadRows =
            await _leagueTeamRepository.GetLeagueSquadRatingsAsync(context.League.Id);

        ILookup<int, GetLeagueSquadRatingsResult> ratingsOf = squadRows.ToLookup(row => row.LeagueTeamId);

        Dictionary<int, int> ratedPlayersOf = new();
        List<IReadOnlyList<SquadStrength>> divisions = [];

        foreach (GetCompetitionsResult division in context.Divisions)
        {
            List<SquadStrength> strengths = [];

            IEnumerable<int> clubs = divisionOfClub
                .Where(pair => pair.Value == division.Id)
                .Select(pair => pair.Key)
                .Order();

            foreach (int club in clubs)
            {
                strengths.Add(MeasureClub(club, rosterOf[club].TeamId, ratingsOf[club], ratedPlayersOf));
            }

            divisions.Add(strengths);
        }

        List<SquadStrength> guestStrengths = [];

        foreach (GetLeagueTeamsResult guest in guests)
        {
            guestStrengths.Add(MeasureClub(guest.Id, guest.TeamId, ratingsOf[guest.Id], ratedPlayersOf));
        }

        ContinentalEntrantSelection selection = ContinentalEntrantSelector.Select(
            ContinentalEntrantSelector.SeasonOnePyramid(divisions),
            ContinentalEntrantSelector.RankGuests(guestStrengths),
            cupSize);

        if (selection.Status == ContinentalEntrantSelectionStatus.TooFewGuests)
        {
            throw TooFewGuests(selection.Needed, selection.Found);
        }

        return (selection, FindThinEntrants(selection, ratedPlayersOf));
    }

    /// <summary>
    /// Checks that every club that plays a league match is on the roster and is not a guest.
    /// </summary>
    /// <param name="divisionClubs">The league team ids of the clubs that play the league matches.</param>
    /// <param name="rosterOf">The roster entry of each league team id.</param>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when a club is off the roster or is flagged as a guest.
    /// </exception>
    private static void CheckDivisionClubs(
        IEnumerable<int> divisionClubs,
        Dictionary<int, GetLeagueTeamsResult> rosterOf)
    {
        foreach (int club in divisionClubs)
        {
            if (!rosterOf.TryGetValue(club, out GetLeagueTeamsResult? entry))
            {
                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Club {club} plays the league's matches but is not on its roster, so its squad cannot be ")
                    + "measured for the continental cups. Put the club back on the roster, or reset the season.");
            }

            if (entry.IsGuest)
            {
                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Club {club} plays the league's matches but is a guest on the roster, and a guest never ")
                    + "plays in a division. Fix the roster, or reset the season.");
            }
        }
    }

    /// <summary>
    /// Builds the refusal for a league that has fewer guests than the two cups need.
    /// </summary>
    /// <param name="needed">The number of guests the cups need.</param>
    /// <param name="found">The number of guests the roster has.</param>
    /// <returns>The refusal, ready to throw.</returns>
    private static SeasonRefusedException TooFewGuests(int needed, int found)
    {
        return new SeasonRefusedException(
            string.Create(CultureInfo.InvariantCulture, $"Too few guests: need {needed}, found {found}. ")
            + "The guests fill the places that the league's clubs leave empty in the two cups. "
            + "Add guests to the league.");
    }

    /// <summary>
    /// Measures one club's squad, turning the library's complaint about the squad's rows into a refusal. A club with no
    /// rows measures as an empty squad.
    /// </summary>
    /// <param name="club">The league team id of the club.</param>
    /// <param name="teamId">The id of the club's team row.</param>
    /// <param name="rows">The club's squad rows.</param>
    /// <param name="ratedPlayersOf">The number of rated players of each club measured; this club is added.</param>
    /// <returns>The strength of the club's squad.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the squad lists a player with two different ratings.
    /// </exception>
    private static SquadStrength MeasureClub(
        int club,
        int teamId,
        IEnumerable<GetLeagueSquadRatingsResult> rows,
        Dictionary<int, int> ratedPlayersOf)
    {
        List<SquadPlayerRating> players = rows.Select(row => new SquadPlayerRating(row.PlayerId, row.Rating)).ToList();

        ratedPlayersOf[club] = players
            .Where(player => player.Rating is not null)
            .Select(player => player.PlayerId)
            .Distinct()
            .Count();

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
                    $"The squad of club {club} cannot be measured for the continental cups: {reason} ")
                + "Fix the club's player rows.");
        }
    }

    /// <summary>
    /// Draws and dates the league phase of both cups. The two cups share one index of the clubs' kickoffs, so the
    /// second cup keeps its distance from the matches of the first.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="cups">The two cups, the one with the lower id first.</param>
    /// <param name="selection">The entrants of the two cups.</param>
    /// <param name="format">The size and the dates of the cups.</param>
    /// <param name="season">The season's matches with their scores.</param>
    /// <returns>The plan of each cup, or null when a match has no date, which is logged.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when a draw does not have the cup's matches.</exception>
    private List<CupPlan>? PlanCups(
        LeagueStepContext context,
        IReadOnlyList<GetCompetitionsResult> cups,
        ContinentalEntrantSelection selection,
        CupFormat format,
        IReadOnlyList<SeasonMatch> season)
    {
        Dictionary<int, List<DateTime>> kickoffsOfClub = DomesticCupPlanIndexes.IndexKickoffs(season);
        IReadOnlyList<int>[] entrantsOf = [selection.Cup1, selection.Cup2];
        List<CupPlan> plans = [];

        for (int index = 0; index < entrantsOf.Length; index++)
        {
            GetCompetitionsResult cup = cups[index];
            IReadOnlyList<ContinentalFixture> fixtures = DrawCup(context.Season.Id, cup, entrantsOf[index], format);

            IReadOnlyList<DateTime>? kickoffs = ContinentalLeaguePhaseDatePlanner.Place(
                fixtures,
                format.Dates.LeaguePhase,
                format.FirstKnockout,
                context.RunTime,
                kickoffsOfClub);

            if (kickoffs is null)
            {
                LogPlaysWithoutCups(context, format.FirstKnockout);

                return null;
            }

            plans.Add(new CupPlan(cup, entrantsOf[index], fixtures, kickoffs));
        }

        return plans;
    }

    /// <summary>
    /// Draws the league phase of one cup and checks that it has the matches and matchdays of the cup's size.
    /// </summary>
    /// <param name="seasonId">The id of the season, which seeds the draw.</param>
    /// <param name="cup">The cup's competition, which seeds the draw.</param>
    /// <param name="entrants">The league team ids of the cup's entrants, in pot order.</param>
    /// <param name="format">The size and the dates of the cups.</param>
    /// <returns>The matches of the cup, sorted by matchday.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the draw has another number of matches or matchdays.
    /// </exception>
    private static IReadOnlyList<ContinentalFixture> DrawCup(
        int seasonId,
        GetCompetitionsResult cup,
        IReadOnlyList<int> entrants,
        CupFormat format)
    {
        IReadOnlyList<ContinentalFixture> fixtures = ContinentalLeaguePhaseDraw.Draw(
            entrants,
            ContinentalLeaguePhaseDraw.CreateRandom(seasonId, cup.Id));

        int matchdays = format.Dates.LeaguePhase.Count;
        int expected = format.CupSize * matchdays / 2;

        if (fixtures.Count != expected || fixtures.Any(fixture => fixture.Matchday < 1 || fixture.Matchday > matchdays))
        {
            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The draw of continental cup {cup.Id} has {fixtures.Count} matches, ")
                + string.Create(
                    CultureInfo.InvariantCulture,
                    $"and {expected} over {matchdays} matchdays are expected for {format.CupSize} clubs. ")
                + "This is a defect.");
        }

        return fixtures;
    }

    /// <summary>
    /// Inserts the matches of both cups, cup 1 first and each cup in draw order, and then one standings row for each
    /// entrant of each cup.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="plans">The plan of each cup.</param>
    /// <param name="written">The list that receives what each cup was given.</param>
    /// <returns>A task that completes once every row has been inserted.</returns>
    /// <exception cref="OverflowException">
    /// Thrown when a matchday does not fit in a byte, which no continental cup reaches.
    /// </exception>
    private async Task WritePlansAsync(LeagueStepContext context, List<CupPlan> plans, List<CupWrite> written)
    {
        List<StagedMatch> staged = [];

        foreach (CupPlan plan in plans)
        {
            for (int index = 0; index < plan.Fixtures.Count; index++)
            {
                ContinentalFixture fixture = plan.Fixtures[index];

                staged.Add(new StagedMatch(
                    plan.Cup.Id,
                    plan.Kickoffs[index],
                    MatchStage.ContinentalLeaguePhase,
                    checked((byte)fixture.Matchday),
                    Leg: null,
                    fixture.HomeLeagueTeamId,
                    fixture.AwayLeagueTeamId));
            }
        }

        await _matchInserter.InsertStagedMatchesAsync(context.Season.Id, staged, context.RunTime);

        foreach (CupPlan plan in plans)
        {
            foreach (int club in plan.Entrants)
            {
                await _standingRepository.CreateAsync(
                    plan.Cup.Id,
                    context.Season.Id,
                    club,
                    context.RunTime,
                    GeneratorAudit.UserId);
            }

            written.Add(new CupWrite(plan.Cup.Id, plan.Fixtures.Count, plan.Entrants.Count));
        }
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
    private void LogOutcome(LeagueStepContext context, FixtureWriteOutcome outcome, List<CupWrite> written)
    {
        switch (outcome)
        {
            case FixtureWriteOutcome.LockHeld:
                _logger.LogInformation(
                    LogPrefix + ": another run holds this season's lock, so this run leaves the continental cups "
                    + "to it.",
                    context.League.Name,
                    context.League.Id,
                    context.Season.SeasonNumber,
                    context.Season.Id);

                break;

            case FixtureWriteOutcome.Written when written.Count == 2:
                _logger.LogInformation(
                    LogPrefix + ": wrote the league phase of the continental cups: cup {Cup1Id} {Matches1} "
                    + "matches, {Standings1} standings; cup {Cup2Id} {Matches2} matches, {Standings2} standings.",
                    context.League.Name,
                    context.League.Id,
                    context.Season.SeasonNumber,
                    context.Season.Id,
                    written[0].CupId,
                    written[0].MatchCount,
                    written[0].StandingCount,
                    written[1].CupId,
                    written[1].MatchCount,
                    written[1].StandingCount);

                break;

            case FixtureWriteOutcome.NotWritten:
                break;

            default:
                throw new InvalidOperationException(
                    $"The continental cup step has no reason for the outcome {outcome}, which is a bug.");
        }
    }

    /// <summary>
    /// Logs that both cups already have their league phase, which leaves this step nothing to add. The knockouts are
    /// the next step's.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="cup1">The first cup.</param>
    /// <param name="cup2">The second cup.</param>
    private void LogAlreadyWritten(LeagueStepContext context, GetCompetitionsResult cup1, GetCompetitionsResult cup2)
    {
        _logger.LogInformation(
            LogPrefix + ": the league phase of the continental cups (competitions {Cup1Id} and {Cup2Id}) is already "
            + "written, so this step has nothing to add; the knockouts are written by the next step.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            cup1.Id,
            cup2.Id);
    }

    /// <summary>
    /// Logs that both cups have matches in the season, so the league phase is written and this step has nothing to add.
    /// The check does not look at the standings, so it does not say that the cups are fully written.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="cup1">The first cup.</param>
    /// <param name="cup2">The second cup.</param>
    private void LogBothCupsHaveMatches(
        LeagueStepContext context,
        GetCompetitionsResult cup1,
        GetCompetitionsResult cup2)
    {
        _logger.LogInformation(
            LogPrefix + ": both continental cups (competitions {Cup1Id} and {Cup2Id}) already have matches this "
            + "season, so their league phase is written and this step has nothing to add; the knockouts are written "
            + "by the next step.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            cup1.Id,
            cup2.Id);
    }

    /// <summary>
    /// Logs that the league matches are not written yet.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    private void LogWaitingForLeague(LeagueStepContext context)
    {
        _logger.LogInformation(
            LogPrefix + ": the league matches are not written yet, so the continental cups wait for a later tick.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id);
    }

    /// <summary>
    /// Logs that the calendar the league was written on has no continental cups.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="clubCounts">The number of clubs in each division, in division order.</param>
    private void LogNoContinentalCalendar(LeagueStepContext context, IReadOnlyList<int> clubCounts)
    {
        _logger.LogInformation(
            LogPrefix + ": the calendar has no continental cups at {ClubCounts} clubs per division, so none are "
            + "written.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            string.Join(", ", clubCounts.Select(count => count.ToString(CultureInfo.InvariantCulture))));
    }

    /// <summary>
    /// Logs that the league has no competition shaped like a continental cup.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    private void LogNoCups(LeagueStepContext context)
    {
        _logger.LogInformation(
            LogPrefix + ": the league has no continental cup competitions (Order 0, not domestic, Format 3 and no "
            + "age limit), so there are no cups to write.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id);
    }

    /// <summary>
    /// Logs that the entrants of a later season are still chosen by squad strength.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    private void LogEntrantsFromStrength(LeagueStepContext context)
    {
        _logger.LogInformation(
            LogPrefix + ": the entrants of the continental cups come from squad strength, because the previous "
            + "season's tables are not used yet.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id);
    }

    /// <summary>
    /// Logs that the season plays without continental cups because the league phase cannot be dated in time.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="firstKnockout">The date of the first knockout match.</param>
    private void LogPlaysWithoutCups(LeagueStepContext context, DateTime firstKnockout)
    {
        _logger.LogWarning(
            LogPrefix + ": the season plays without continental cups: the league phase can no longer be placed "
            + "before {FirstKnockoutDate} UTC.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            firstKnockout.ToString(DateFormat, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Finds the entrants that have fewer rated players than a full squad.
    /// </summary>
    /// <param name="selection">The entrants of both cups.</param>
    /// <param name="ratedPlayersOf">The number of rated players of each club measured.</param>
    /// <returns>The league team ids of the thin entrants, in ascending order.</returns>
    private static List<int> FindThinEntrants(
        ContinentalEntrantSelection selection,
        Dictionary<int, int> ratedPlayersOf)
    {
        return selection.Cup1.Concat(selection.Cup2)
            .Where(club => ratedPlayersOf[club] < SquadStrengthRanking.BestCount)
            .Order()
            .ToList();
    }

    /// <summary>
    /// Logs the entrants that have fewer rated players than a full squad. It runs only when the cups are written, so a
    /// season that plays without cups does not repeat it at every tick.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="plan">The plan that was written.</param>
    private void LogThinEntrants(LeagueStepContext context, ContinentalPlan plan)
    {
        if (plan.ThinClubs.Count == 0)
        {
            return;
        }

        _logger.LogWarning(
            LogPrefix + ": entrants of the continental cups with fewer than {RatedPlayerCount} rated players "
            + "({ThinCount} of {EntrantCount}): {ThinClubs}. A missing place counts as 0 in a squad's strength.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            SquadStrengthRanking.BestCount,
            plan.ThinClubs.Count,
            plan.EntrantCount,
            string.Join(", ", plan.ThinClubs.Select(club => club.ToString(CultureInfo.InvariantCulture))));
    }

    /// <summary>
    /// Logs that the continental cups are refused, and why.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="reason">What is wrong and how to fix it.</param>
    private void LogRefusal(LeagueStepContext context, string reason)
    {
        _logger.LogError(
            LogPrefix + ": the continental cups are refused: {Reason} The next tick tries again.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            reason);
    }

    /// <summary>
    /// The size, the dates and the first knockout date of the league's continental cups.
    /// </summary>
    /// <param name="Dates">The calendar's continental cup dates.</param>
    /// <param name="CupSize">The number of clubs in each cup: 16 or 32.</param>
    /// <param name="FirstKnockout">
    /// The first leg of the play-off, or of the quarter-final when there is no play-off; the league phase ends before
    /// it.
    /// </param>
    private sealed record CupFormat(ContinentalCupDates Dates, int CupSize, DateTime FirstKnockout);

    /// <summary>
    /// What is written for one cup: its entrants, its drawn matches and the kickoff of each match.
    /// </summary>
    /// <param name="Cup">The cup's competition.</param>
    /// <param name="Entrants">The league team ids of the entrants, in pot order.</param>
    /// <param name="Fixtures">The matches of the league phase, sorted by matchday.</param>
    /// <param name="Kickoffs">The kickoff of each match, in the order of <paramref name="Fixtures"/>.</param>
    private sealed record CupPlan(
        GetCompetitionsResult Cup,
        IReadOnlyList<int> Entrants,
        IReadOnlyList<ContinentalFixture> Fixtures,
        IReadOnlyList<DateTime> Kickoffs);

    /// <summary>
    /// What the step writes for both cups, and the warning that goes with it.
    /// </summary>
    /// <param name="Cups">The plan of each cup, the one with the lower id first.</param>
    /// <param name="ThinClubs">
    /// The league team ids of the entrants with fewer rated players than a full squad, in ascending order.
    /// </param>
    /// <param name="EntrantCount">The number of entrants of both cups together.</param>
    private sealed record ContinentalPlan(List<CupPlan> Cups, IReadOnlyList<int> ThinClubs, int EntrantCount);

    /// <summary>
    /// What a cup was given once its rows were inserted.
    /// </summary>
    /// <param name="CupId">The id of the cup's competition.</param>
    /// <param name="MatchCount">The number of matches inserted.</param>
    /// <param name="StandingCount">The number of standings rows inserted.</param>
    private sealed record CupWrite(int CupId, int MatchCount, int StandingCount);
}
