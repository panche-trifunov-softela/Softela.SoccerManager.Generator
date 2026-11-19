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
/// The World Cup finals group step: once every qualifier match of the league's national competition is finished and the
/// continental finals are written, it picks the 32 nations that qualify, draws their 8 groups and writes the group
/// matches, with the statistics rows of both nations. It reads the season again inside its own lock, so it sees what
/// the steps before it wrote, and it refuses a season it cannot write without ever failing the run. The finals draw is
/// fixed once it is written: later changes to the qualifier results are not read. The knockout matches are written by
/// the World Cup knockout step.
/// </summary>
internal sealed class WorldCupGroupStep : ILeagueStep
{
    /// <summary>
    /// The format of a date in a log line.
    /// </summary>
    private const string DateFormat = "yyyy-MM-dd HH:mm";

    /// <summary>
    /// The start of every log line about the league's season.
    /// </summary>
    private const string LogPrefix = LeagueStepContext.LogPrefix;

    /// <summary>
    /// The start of the purpose the finals schedule's random source is derived with; the number of legs follows it.
    /// </summary>
    private const string RandomPurposePrefix = "worldCupFinalsRounds:";

    /// <summary>
    /// The stage of a qualifier group match as <see cref="SeasonMatch.Stage"/> holds it.
    /// </summary>
    private const byte QualifierStage = (byte)MatchStage.WorldCupQualifierGroup;

    private readonly LeagueFixtureWriter _leagueFixtureWriter;
    private readonly MatchInserter _matchInserter;
    private readonly IMatchRepository _matchRepository;
    private readonly INationalTeamRepository _nationalTeamRepository;
    private readonly ILogger<WorldCupGroupStep> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldCupGroupStep"/> class.
    /// </summary>
    /// <param name="leagueFixtureWriter">The writer that holds the season's transaction and lock.</param>
    /// <param name="matchInserter">The inserter of the matches and their statistics rows.</param>
    /// <param name="matchRepository">The repository that reads the season's scores.</param>
    /// <param name="nationalTeamRepository">The repository that reads the national teams.</param>
    /// <param name="logger">The logger.</param>
    public WorldCupGroupStep(
        LeagueFixtureWriter leagueFixtureWriter,
        MatchInserter matchInserter,
        IMatchRepository matchRepository,
        INationalTeamRepository nationalTeamRepository,
        ILogger<WorldCupGroupStep> logger)
    {
        _leagueFixtureWriter = leagueFixtureWriter;
        _matchInserter = matchInserter;
        _matchRepository = matchRepository;
        _nationalTeamRepository = nationalTeamRepository;
        _logger = logger;
    }

    /// <summary>
    /// Writes the World Cup finals groups of the league's national competition when they are due and not written, or
    /// says in the log why it does not.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes once the finals groups have been handled.</returns>
    public async Task RunAsync(LeagueStepContext context, CancellationToken cancellationToken)
    {
        IReadOnlyList<GetCompetitionsResult> nationals = NationalCompetitionFinder.Find(context.Competitions);

        // The qualifier step logs a league with no national competition or with several of them.
        if (nationals.Count != 1)
        {
            return;
        }

        GetCompetitionsResult national = nationals[0];

        // No qualifier match in the tick's read: the season plays no qualifiers, or this tick wrote them, and then
        // nothing can be finished yet.
        if (!ContinentalCupFinder.HasMatchInSeason(context.LeagueMatches, context.Season.Id, national))
        {
            return;
        }

        GroupPlan? written = null;

        FixtureWriteOutcome outcome = await _leagueFixtureWriter.WriteAsync(
            context.League,
            context.Season,
            async seasonMatches =>
            {
                GroupPlan? plan = await TryPlanAsync(context, national, seasonMatches);

                if (plan is null)
                {
                    return false;
                }

                await _matchInserter.InsertStagedMatchesAsync(context.Season.Id, plan.Matches, context.RunTime);

                written = plan;

                return true;
            },
            cancellationToken);

        LogOutcome(context, outcome, written);
    }

    /// <summary>
    /// Plans the finals groups and logs a refusal. Only the planning is guarded: a failure of the inserts must reach
    /// the writer, which rolls back.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="national">The league's national competition.</param>
    /// <param name="seasonMatches">The season's matches, read inside the transaction after the lock was taken.</param>
    /// <returns>A task whose result is the plan, or null when there is nothing to write.</returns>
    private async Task<GroupPlan?> TryPlanAsync(
        LeagueStepContext context,
        GetCompetitionsResult national,
        IReadOnlyList<GetMatchesBySeasonResult> seasonMatches)
    {
        try
        {
            return await PlanAsync(context, national, seasonMatches);
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
    /// Reads the season again inside the lock and decides whether the finals groups are due, and plans them when they
    /// are. It writes nothing. A refusal is thrown to the caller, which logs it; every other reason to write nothing is
    /// logged here, or is no news.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="national">The league's national competition.</param>
    /// <param name="seasonMatches">The season's matches, read inside the transaction after the lock was taken.</param>
    /// <returns>A task whose result is the plan of the finals groups, or null when there is nothing to write.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the finals groups cannot be written as the rows stand, or a backend read is missing.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a helper refuses what the season or the nations hold.
    /// </exception>
    private async Task<GroupPlan?> PlanAsync(
        LeagueStepContext context,
        GetCompetitionsResult national,
        IReadOnlyList<GetMatchesBySeasonResult> seasonMatches)
    {
        IReadOnlyList<GetMatchScoresBySeasonResult> scores =
            await _matchRepository.GetScoresBySeasonIdAsync(context.Season.Id);

        IReadOnlyList<SeasonMatch> season = SeasonMatchJoiner.Join(seasonMatches, scores);
        List<SeasonMatch> nationalMatches = season.Where(match => match.CompetitionId == national.Id).ToList();

        if (nationalMatches.Any(match => match.Stage is { } stage && FinalsStages.IsFinalsStage(stage)))
        {
            LogAlreadyWritten(context);

            return null;
        }

        List<SeasonMatch> qualifierMatches = nationalMatches.Where(match => match.Stage == QualifierStage).ToList();

        if (qualifierMatches.Count == 0)
        {
            return null;
        }

        SeasonCalendar? calendar = ReadCalendar(context, season);

        if (calendar is null)
        {
            return null;
        }

        RosterWithConfederations roster = await NationalTeamReader.ReadRosterAsync(
            _nationalTeamRepository,
            context.League.Id);

        QualifierGroupReading reading = QualifierGroupReader.Read(
            qualifierMatches,
            roster.Roster,
            roster.Confederations);

        if (reading.FinishedMatches < reading.TotalMatches)
        {
            LogWaitingForQualifiers(context, reading);

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

        // Read only now, so a tick that waits never costs the ratings.
        NationalTeamReads reads = await NationalTeamReader.AddRatingsAsync(_nationalTeamRepository, roster);

        return PlanGroups(context, national, season, reads, reading, days.Days);
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
    /// Picks the qualified nations, draws their groups and dates the group matches.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="national">The league's national competition.</param>
    /// <param name="season">The season's matches with their scores.</param>
    /// <param name="reads">What was read about the national teams.</param>
    /// <param name="reading">The finished qualifier groups.</param>
    /// <param name="days">The 8 finals days.</param>
    /// <returns>The plan of the finals groups, or null when the finals have too few nations.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when no day fits a match.</exception>
    /// <exception cref="ArgumentException">Thrown when a helper refuses what the nations hold.</exception>
    private GroupPlan? PlanGroups(
        LeagueStepContext context,
        GetCompetitionsResult national,
        IReadOnlyList<SeasonMatch> season,
        NationalTeamReads reads,
        QualifierGroupReading reading,
        IReadOnlyList<DateTime> days)
    {
        IReadOnlyList<QualifierNation> entrants =
            QualifierEntrantSelector.Select(reads.Roster, reads.Confederations, reads.Ratings).Entrants;

        List<QualifierNation> lonelyNations = FindLonelyNations(context, reading.Groups, entrants);
        Dictionary<Confederation, int> nationsByConfederation = CountNations(reading.Groups, lonelyNations);
        int nationCount = nationsByConfederation.Values.Sum();

        if (nationCount < WorldCupFinalsShape.NationCount)
        {
            LogTooFewNations(context, nationCount);

            return null;
        }

        IReadOnlyDictionary<Confederation, int> places =
            WorldCupPlaces.Share(nationsByConfederation, WorldCupFinalsShape.NationCount);

        IReadOnlyList<QualifiedNation> qualified = WorldCupQualification.Pick(
            context.Season.Id,
            reading.Groups,
            lonelyNations.ToDictionary(nation => nation.Confederation, nation => nation.LeagueTeamId),
            places);

        IReadOnlyList<WorldCupGroup> groups = FinalsGroupDraw.Draw(StrengthsOf(qualified, reads), context.Season.Id);

        IReadOnlyList<(byte GroupNumber, ContinentalFixture Fixture)> fixtures =
            WorldCupRoundBuilder.Build(groups, context.Season.Id, RandomPurposePrefix);

        IReadOnlyList<DateTime> kickoffs = WorldCupFinalsDates.PlaceGroupMatches(
            fixtures,
            days,
            context.RunTime,
            DomesticCupPlanIndexes.IndexKickoffs(season));

        return new GroupPlan(
            WorldCupStagedMatches.Of(national, fixtures, kickoffs, MatchStage.WorldCupGroup),
            groups.Count,
            DescribePlaces(places),
            DescribeDirectPlaces(lonelyNations),
            kickoffs.Min(),
            kickoffs.Max());
    }

    /// <summary>
    /// Finds the nations that keep a direct place: the only nation that can enter, on the tick that draws the finals,
    /// of a confederation that has no qualifier group. "Lone" is decided from the nations that can enter on that tick,
    /// not at the qualifier draw. It logs the nations that cannot play the finals for want of a qualifier: the nations
    /// of a confederation that has no group, when there are two or more of them, and the nations that are new to a
    /// confederation that has groups.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="groups">The written qualifier groups.</param>
    /// <param name="entrants">The nations that can enter today, ordered by confederation.</param>
    /// <returns>The lone nations, in confederation order.</returns>
    private List<QualifierNation> FindLonelyNations(
        LeagueStepContext context,
        IReadOnlyList<WrittenQualifierGroup> groups,
        IReadOnlyList<QualifierNation> entrants)
    {
        HashSet<Confederation> withGroups = groups.Select(group => group.Confederation).ToHashSet();
        HashSet<int> inGroups = groups.SelectMany(group => group.LeagueTeamIds).ToHashSet();

        List<QualifierNation> ignored = entrants
            .Where(entrant => withGroups.Contains(entrant.Confederation))
            .Where(entrant => !inGroups.Contains(entrant.LeagueTeamId))
            .ToList();

        LogIgnoredNations(context, ignored);

        List<QualifierNation> lonelyNations = [];

        IEnumerable<IGrouping<Confederation, QualifierNation>> withoutGroups = entrants
            .Where(entrant => !withGroups.Contains(entrant.Confederation))
            .GroupBy(entrant => entrant.Confederation)
            .OrderBy(confederation => confederation.Key);

        foreach (IGrouping<Confederation, QualifierNation> confederation in withoutGroups)
        {
            List<QualifierNation> nations = confederation.ToList();

            if (nations.Count == 1)
            {
                lonelyNations.Add(nations[0]);

                continue;
            }

            LogNoQualifiersPlayed(context, confederation.Key, nations);
        }

        return lonelyNations;
    }

    /// <summary>
    /// Counts the nations of each confederation that played the qualifiers: the nations of its groups, or its one lone
    /// nation.
    /// </summary>
    /// <param name="groups">The written qualifier groups.</param>
    /// <param name="lonelyNations">The lone nations.</param>
    /// <returns>The number of nations of each confederation that has any.</returns>
    private static Dictionary<Confederation, int> CountNations(
        IReadOnlyList<WrittenQualifierGroup> groups,
        IReadOnlyList<QualifierNation> lonelyNations)
    {
        Dictionary<Confederation, int> counts = groups
            .GroupBy(group => group.Confederation)
            .ToDictionary(
                confederation => confederation.Key,
                confederation => confederation.Sum(group => group.LeagueTeamIds.Count));

        foreach (QualifierNation nation in lonelyNations)
        {
            counts[nation.Confederation] = 1;
        }

        return counts;
    }

    /// <summary>
    /// Measures the strength of each qualified nation from the players' ratings as they are today.
    /// </summary>
    /// <param name="qualified">The qualified nations.</param>
    /// <param name="reads">What was read about the national teams.</param>
    /// <returns>The roster entry, the national team id and the strength of each nation, in the order given.</returns>
    /// <exception cref="ArgumentException">Thrown when the roster lists an entry twice.</exception>
    private static List<(int LeagueTeamId, int NationalTeamId, int Strength)> StrengthsOf(
        IReadOnlyList<QualifiedNation> qualified,
        NationalTeamReads reads)
    {
        Dictionary<int, int> nationalTeamOf =
            reads.Roster.ToDictionary(entry => entry.Id, entry => entry.NationalTeamId);

        ILookup<int, GetNationalTeamPlayerRatingsResult> ratingsOf =
            reads.Ratings.ToLookup(row => row.NationalTeamId);

        List<(int LeagueTeamId, int NationalTeamId, int Strength)> strengths = [];

        foreach (QualifiedNation nation in qualified)
        {
            int nationalTeamId = nationalTeamOf[nation.LeagueTeamId];

            strengths.Add((
                nation.LeagueTeamId,
                nationalTeamId,
                QualifierEntrantSelector.StrengthOf(ratingsOf[nationalTeamId])));
        }

        return strengths;
    }

    /// <summary>
    /// Counts the places of each confederation, for the log.
    /// </summary>
    /// <param name="places">The number of places of each confederation.</param>
    /// <returns>One part for each confederation, in confederation order, such as "AFC 2".</returns>
    private static string DescribePlaces(IReadOnlyDictionary<Confederation, int> places)
    {
        IEnumerable<string> parts = places
            .OrderBy(pair => pair.Key)
            .Select(pair => string.Create(
                CultureInfo.InvariantCulture,
                $"{ConfederationLabel.Of(pair.Key)} {pair.Value}"));

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Names the nations that have a direct place, for the log.
    /// </summary>
    /// <param name="lonelyNations">The lone nations.</param>
    /// <returns>
    /// The text to append to the places: "; direct places: " and the names, or nothing when there are none.
    /// </returns>
    private static string DescribeDirectPlaces(IReadOnlyList<QualifierNation> lonelyNations)
    {
        if (lonelyNations.Count == 0)
        {
            return string.Empty;
        }

        return "; direct places: " + string.Join(", ", lonelyNations.Select(nation => nation.Name));
    }

    /// <summary>
    /// Names national teams for a log line, each with its id.
    /// </summary>
    /// <param name="nations">The nations.</param>
    /// <returns>The names with their national team ids, joined with commas.</returns>
    private static string DescribeNations(IEnumerable<QualifierNation> nations)
    {
        return string.Join(
            ", ",
            nations.Select(nation => string.Create(
                CultureInfo.InvariantCulture,
                $"{nation.Name} (national team {nation.NationalTeamId})")));
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
    /// Logs what came of the write: the lock, or the finals groups written. Every other reason to write nothing was
    /// logged where it was found.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="outcome">What came of writing under the season's lock.</param>
    /// <param name="written">What was written; null unless the rows were written.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the outcome has no reason that the step knows, which is a bug.
    /// </exception>
    private void LogOutcome(LeagueStepContext context, FixtureWriteOutcome outcome, GroupPlan? written)
    {
        switch (outcome)
        {
            case FixtureWriteOutcome.LockHeld:
                _logger.LogInformation(
                    LogPrefix + ": another run holds this season's lock, so this run leaves the World Cup finals "
                    + "groups to it.",
                    context.League.Name,
                    context.League.Id,
                    context.Season.SeasonNumber,
                    context.Season.Id);

                break;

            case FixtureWriteOutcome.Written when written is not null:
                _logger.LogInformation(
                    LogPrefix + ": wrote the World Cup finals groups, {MatchCount} matches in {GroupCount} groups: "
                    + "{PlacesByConfederation}{DirectPlaces}; {FirstKickoff} to {LastKickoff} UTC.",
                    context.League.Name,
                    context.League.Id,
                    context.Season.SeasonNumber,
                    context.Season.Id,
                    written.Matches.Count,
                    written.GroupCount,
                    written.PlacesByConfederation,
                    written.DirectPlaces,
                    written.FirstKickoff.ToString(DateFormat, CultureInfo.InvariantCulture),
                    written.LastKickoff.ToString(DateFormat, CultureInfo.InvariantCulture));

                break;

            case FixtureWriteOutcome.NotWritten:
                break;

            default:
                throw new InvalidOperationException(
                    $"The World Cup group step has no reason for the outcome {outcome}, which is a bug.");
        }
    }

    /// <summary>
    /// Logs that the finals already have matches, the groups or knockout matches, so this step has nothing to add.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    private void LogAlreadyWritten(LeagueStepContext context)
    {
        _logger.LogInformation(
            LogPrefix + ": the World Cup finals already have matches, so the groups are not drawn again.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id);
    }

    /// <summary>
    /// Logs that the league matches are not written yet.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    private void LogWaitingForLeague(LeagueStepContext context)
    {
        _logger.LogInformation(
            LogPrefix + ": the league matches are not written yet, so the World Cup finals groups wait for a later "
            + "tick.",
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
            LogPrefix + ": the calendar has no World Cup finals at {ClubCounts} clubs per division, so no finals "
            + "groups are written.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            string.Join(", ", clubCounts.Select(count => count.ToString(CultureInfo.InvariantCulture))));
    }

    /// <summary>
    /// Logs that the finals wait for the qualifiers to finish.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="reading">What the qualifier matches say.</param>
    private void LogWaitingForQualifiers(LeagueStepContext context, QualifierGroupReading reading)
    {
        _logger.LogInformation(
            LogPrefix + ": the World Cup finals wait for the qualifiers: {Finished} of {Total} matches are finished.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            reading.FinishedMatches,
            reading.TotalMatches);
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
    /// Logs that the season plays without World Cup finals because the qualifiers had too few nations.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="nationCount">The number of nations that played the qualifiers, or kept a direct place.</param>
    private void LogTooFewNations(LeagueStepContext context, int nationCount)
    {
        _logger.LogWarning(
            LogPrefix + ": the season plays without World Cup finals: the qualifiers had {NationCount} nations, and "
            + "the finals need {FinalsNations}.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            nationCount,
            WorldCupFinalsShape.NationCount);
    }

    /// <summary>
    /// Logs the nations that can enter today but are in no group of a confederation that has groups, and so are left
    /// out of the finals. It logs nothing when there are none.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="nations">The nations left out.</param>
    private void LogIgnoredNations(LeagueStepContext context, IReadOnlyList<QualifierNation> nations)
    {
        if (nations.Count == 0)
        {
            return;
        }

        _logger.LogInformation(
            LogPrefix + ": nations that can enter today but played no World Cup qualifier are left out of the "
            + "finals: {Nations}.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            DescribeNations(nations));
    }

    /// <summary>
    /// Logs the nations of a confederation that has no qualifier group but two or more nations that can enter on the
    /// tick that draws the finals: they played no qualifiers, so none of them has a place. A nation that was the
    /// confederation's only entrant at the qualifier draw loses its direct place this way, because "lone" is decided
    /// from the nations that can enter on this tick, not at the qualifier draw.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="confederation">The confederation.</param>
    /// <param name="nations">The confederation's nations today.</param>
    private void LogNoQualifiersPlayed(
        LeagueStepContext context,
        Confederation confederation,
        IReadOnlyList<QualifierNation> nations)
    {
        _logger.LogWarning(
            LogPrefix + ": {Confederation} has {NationCount} nations that can enter, but they played no World Cup "
            + "qualifiers, so none of them has a place in the finals: {Nations}. A nation that was the "
            + "confederation's only entrant at the qualifier draw loses its direct place this way.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            ConfederationLabel.Of(confederation),
            nations.Count,
            DescribeNations(nations));
    }

    /// <summary>
    /// Logs that the World Cup finals groups are refused, and why.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="reason">What is wrong and how to fix it.</param>
    private void LogRefusal(LeagueStepContext context, string reason)
    {
        _logger.LogError(
            LogPrefix + ": the World Cup finals groups are refused: {Reason} The next tick tries again.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            reason);
    }

    /// <summary>
    /// What the step writes: the matches, and the figures its log line reports.
    /// </summary>
    /// <param name="Matches">The matches to insert, in insertion order.</param>
    /// <param name="GroupCount">The number of groups drawn.</param>
    /// <param name="PlacesByConfederation">The places of each confederation, as text.</param>
    /// <param name="DirectPlaces">The direct places and their nations, as text to append, or empty.</param>
    /// <param name="FirstKickoff">The kickoff of the first match.</param>
    /// <param name="LastKickoff">The kickoff of the last match.</param>
    private sealed record GroupPlan(
        IReadOnlyList<StagedMatch> Matches,
        int GroupCount,
        string PlacesByConfederation,
        string DirectPlaces,
        DateTime FirstKickoff,
        DateTime LastKickoff);
}
