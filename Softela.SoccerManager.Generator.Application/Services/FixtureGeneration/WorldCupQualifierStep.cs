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
/// The World Cup qualifier step: it writes the group matches of the qualifiers of the league's national competition,
/// with the statistics rows of both nations, once the season's league matches exist. It reads the season again inside
/// its own lock, so it sees the league matches that the steps before it wrote, and it refuses a competition it cannot
/// write without ever failing the run. Written qualifiers are never changed: later ticks leave them as they are. The
/// finals that follow them are not written by this step.
/// </summary>
internal sealed class WorldCupQualifierStep : ILeagueStep
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
    private readonly INationalTeamRepository _nationalTeamRepository;
    private readonly ILogger<WorldCupQualifierStep> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldCupQualifierStep"/> class.
    /// </summary>
    /// <param name="leagueFixtureWriter">The writer that holds the season's transaction and lock.</param>
    /// <param name="matchInserter">The inserter of the matches and their statistics rows.</param>
    /// <param name="matchRepository">The repository that reads the season's scores.</param>
    /// <param name="nationalTeamRepository">The repository that reads the national teams.</param>
    /// <param name="logger">The logger.</param>
    public WorldCupQualifierStep(
        LeagueFixtureWriter leagueFixtureWriter,
        MatchInserter matchInserter,
        IMatchRepository matchRepository,
        INationalTeamRepository nationalTeamRepository,
        ILogger<WorldCupQualifierStep> logger)
    {
        _leagueFixtureWriter = leagueFixtureWriter;
        _matchInserter = matchInserter;
        _matchRepository = matchRepository;
        _nationalTeamRepository = nationalTeamRepository;
        _logger = logger;
    }

    /// <summary>
    /// Writes the World Cup qualifiers of the league's national competition when they are due and not written, or says
    /// in the log why it does not.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes once the qualifiers have been handled.</returns>
    public async Task RunAsync(LeagueStepContext context, CancellationToken cancellationToken)
    {
        List<GetCompetitionsResult> nationals = context.Competitions
            .Where(IsNationalCompetition)
            .OrderBy(competition => competition.Id)
            .ToList();

        if (nationals.Count == 0)
        {
            LogNoNationalCompetition(context);

            return;
        }

        if (nationals.Count > 1)
        {
            LogSeveralNationalCompetitions(context, nationals);

            return;
        }

        GetCompetitionsResult national = nationals[0];

        // Cheap check before the lock: a tick that finds the qualifiers written has nothing to do.
        if (ContinentalCupFinder.HasMatchInSeason(context.LeagueMatches, context.Season.Id, national))
        {
            LogAlreadyWritten(context, national);

            return;
        }

        QualifierPlan? written = null;

        FixtureWriteOutcome outcome = await _leagueFixtureWriter.WriteAsync(
            context.League,
            context.Season,
            async seasonMatches =>
            {
                // Another run wrote them first; the outcome says nothing more, as the cheap check already looked.
                if (ContinentalCupFinder.HasMatchInSeason(seasonMatches, context.Season.Id, national))
                {
                    return false;
                }

                QualifierPlan? plan;

                // Only the planning is guarded: a failure of the inserts must reach the writer, which rolls back.
                try
                {
                    plan = await PlanAsync(context, national, seasonMatches);
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

                    LogRefusal(context, $"A helper of the World Cup qualifiers refused its input: {reason}");

                    return false;
                }

                if (plan is null)
                {
                    return false;
                }

                await _matchInserter.InsertStagedMatchesAsync(context.Season.Id, plan.Matches, context.RunTime);

                written = plan;

                return true;
            },
            cancellationToken);

        LogOutcome(context, national, outcome, written);
    }

    /// <summary>
    /// Tells whether a competition has the shape of a league's national competition, the World Cup: it is not a
    /// division, it is national and it has no age limit.
    /// </summary>
    /// <param name="competition">The competition.</param>
    /// <returns>True when the competition is shaped like a national competition.</returns>
    private static bool IsNationalCompetition(GetCompetitionsResult competition)
    {
        return competition.Order == 0
            && competition.IsNational
            && competition.MaxAgeAllowed is null;
    }

    /// <summary>
    /// Names a confederation for a log line, in capitals.
    /// </summary>
    /// <param name="confederation">The confederation.</param>
    /// <returns>The confederation's name in capitals, such as CONCACAF.</returns>
    private static string LabelOf(Confederation confederation)
    {
        return confederation.ToString().ToUpperInvariant();
    }

    /// <summary>
    /// Reads the season again inside the lock and decides whether the qualifiers are due, and plans them when they are.
    /// It writes nothing. A refusal is thrown to the caller, which logs it; every other reason to write nothing is
    /// logged here.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="national">The league's national competition.</param>
    /// <param name="seasonMatches">The season's matches, read inside the transaction after the lock was taken.</param>
    /// <returns>A task whose result is the plan of the qualifiers, or null when there is nothing to write.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the qualifiers cannot be written as the rows stand, or a backend read is missing.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a helper refuses what the season or the nations hold.
    /// </exception>
    private async Task<QualifierPlan?> PlanAsync(
        LeagueStepContext context,
        GetCompetitionsResult national,
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

        if (calendar.WorldCupQualifierMatchdays.Count == 0 || calendar.WorldCupFinalsMatchdays.Count == 0)
        {
            LogNoWorldCupCalendar(context, national, clubCounts);

            return null;
        }

        DateTime firstFinalsDay = calendar.WorldCupFinalsMatchdays[0];

        // Checked before any national read, so a lasting skip never costs the reads.
        if (!ContinentalLeaguePhaseDatePlanner.CanStart(
            calendar.WorldCupQualifierMatchdays,
            firstFinalsDay,
            context.RunTime))
        {
            LogPlaysWithoutQualifiers(context, firstFinalsDay);

            return null;
        }

        QualifierEntrants entrants = await ReadEntrantsAsync(context);

        LogDirectPlaces(context, entrants.Entrants);

        IReadOnlyList<QualifierGroup> groups = QualifierGroupDraw.Draw(entrants.Entrants, context.Season.Id);

        if (groups.Count == 0)
        {
            LogNoGroups(context);

            return null;
        }

        IReadOnlyList<(byte GroupNumber, ContinentalFixture Fixture)> fixtures =
            QualifierRoundBuilder.Build(groups, context.Season.Id);

        IReadOnlyList<DateTime>? kickoffs = ContinentalLeaguePhaseDatePlanner.Place(
            fixtures.Select(entry => entry.Fixture).ToList(),
            calendar.WorldCupQualifierMatchdays,
            firstFinalsDay,
            context.RunTime,
            DomesticCupPlanIndexes.IndexKickoffs(season));

        if (kickoffs is null)
        {
            LogPlaysWithoutQualifiers(context, firstFinalsDay);

            return null;
        }

        return new QualifierPlan(
            ToStagedMatches(national, fixtures, kickoffs),
            groups.Count,
            groups.Sum(group => group.LeagueTeamIds.Count),
            DescribeEntrants(groups),
            kickoffs.Min(),
            kickoffs.Max());
    }

    /// <summary>
    /// Reads the roster, the confederations and the players' ratings of the national teams, chooses the entrants and
    /// logs the nations with enough players that are left out.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <returns>A task whose result is the entrants and the nations left out.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when a backend read is missing.</exception>
    /// <exception cref="ArgumentException">Thrown when the selector refuses what the reads hold.</exception>
    private async Task<QualifierEntrants> ReadEntrantsAsync(LeagueStepContext context)
    {
        IReadOnlyList<GetLeagueNationalTeamsResult> roster =
            await _nationalTeamRepository.GetLeagueNationalTeamsAsync(context.League.Id);

        IReadOnlyList<GetNationalTeamConfederationsResult> confederations =
            await _nationalTeamRepository.GetConfederationsAsync();

        IReadOnlyList<GetNationalTeamPlayerRatingsResult> ratings =
            await _nationalTeamRepository.GetPlayerRatingsAsync();

        QualifierEntrants entrants = QualifierEntrantSelector.Select(roster, confederations, ratings);

        LogNationsWithoutConfederation(context, entrants.WithoutConfederation);
        LogNationsNotOnRoster(context, entrants.NotOnRoster);

        return entrants;
    }

    /// <summary>
    /// Turns the matches of the groups into the matches to insert, in the order given.
    /// </summary>
    /// <param name="national">The league's national competition.</param>
    /// <param name="fixtures">The matches with the number of their group.</param>
    /// <param name="kickoffs">The kickoff of each match, in the order of <paramref name="fixtures"/>.</param>
    /// <returns>One staged match for each fixture.</returns>
    /// <exception cref="OverflowException">
    /// Thrown when a matchday does not fit in a byte, which no group reaches.
    /// </exception>
    private static List<StagedMatch> ToStagedMatches(
        GetCompetitionsResult national,
        IReadOnlyList<(byte GroupNumber, ContinentalFixture Fixture)> fixtures,
        IReadOnlyList<DateTime> kickoffs)
    {
        List<StagedMatch> matches = [];

        for (int index = 0; index < fixtures.Count; index++)
        {
            ContinentalFixture fixture = fixtures[index].Fixture;

            matches.Add(new StagedMatch(
                national.Id,
                kickoffs[index],
                MatchStage.WorldCupQualifierGroup,
                checked((byte)fixture.Matchday),
                Leg: null,
                fixture.HomeLeagueTeamId,
                fixture.AwayLeagueTeamId,
                fixtures[index].GroupNumber));
        }

        return matches;
    }

    /// <summary>
    /// Counts the nations that play a qualifier in each confederation, for the log.
    /// </summary>
    /// <param name="groups">The drawn groups.</param>
    /// <returns>One part for each confederation that has a group, in confederation order, such as "AFC 4".</returns>
    private static string DescribeEntrants(IReadOnlyList<QualifierGroup> groups)
    {
        IEnumerable<string> parts = groups
            .GroupBy(group => group.Confederation)
            .OrderBy(confederation => confederation.Key)
            .Select(confederation => string.Create(
                CultureInfo.InvariantCulture,
                $"{LabelOf(confederation.Key)} {confederation.Sum(group => group.LeagueTeamIds.Count)}"));

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Names national teams for a log line, each with its id.
    /// </summary>
    /// <param name="nations">The national teams.</param>
    /// <returns>The names with their ids, joined with commas.</returns>
    private static string DescribeNations(IEnumerable<GetNationalTeamConfederationsResult> nations)
    {
        return string.Join(
            ", ",
            nations.Select(nation => string.Create(CultureInfo.InvariantCulture, $"{nation.Name} (id {nation.Id})")));
    }

    /// <summary>
    /// Logs what came of the write: the lock, or the qualifiers written. Every other reason to write nothing was logged
    /// where it was found.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="national">The league's national competition.</param>
    /// <param name="outcome">What came of writing under the season's lock.</param>
    /// <param name="written">What was written; null unless the rows were written.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the outcome has no reason that the step knows, which is a bug.
    /// </exception>
    private void LogOutcome(
        LeagueStepContext context,
        GetCompetitionsResult national,
        FixtureWriteOutcome outcome,
        QualifierPlan? written)
    {
        switch (outcome)
        {
            case FixtureWriteOutcome.LockHeld:
                _logger.LogInformation(
                    LogPrefix + ": another run holds this season's lock, so this run leaves the World Cup qualifiers "
                    + "to it.",
                    context.League.Name,
                    context.League.Id,
                    context.Season.SeasonNumber,
                    context.Season.Id);

                break;

            case FixtureWriteOutcome.Written when written is not null:
                _logger.LogInformation(
                    LogPrefix + ": wrote the World Cup qualifiers of '{CompetitionName}' (competition "
                    + "{CompetitionId}): {MatchCount} matches in {GroupCount} groups, {EntrantCount} entrants "
                    + "({EntrantsByConfederation}), {FirstKickoff} to {LastKickoff} UTC.",
                    context.League.Name,
                    context.League.Id,
                    context.Season.SeasonNumber,
                    context.Season.Id,
                    national.Name,
                    national.Id,
                    written.Matches.Count,
                    written.GroupCount,
                    written.EntrantCount,
                    written.EntrantsByConfederation,
                    written.FirstKickoff.ToString(DateFormat, CultureInfo.InvariantCulture),
                    written.LastKickoff.ToString(DateFormat, CultureInfo.InvariantCulture));

                break;

            case FixtureWriteOutcome.NotWritten:
                break;

            default:
                throw new InvalidOperationException(
                    $"The World Cup qualifier step has no reason for the outcome {outcome}, which is a bug.");
        }
    }

    /// <summary>
    /// Logs that the league has no national competition.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    private void LogNoNationalCompetition(LeagueStepContext context)
    {
        _logger.LogInformation(
            LogPrefix + ": the league has no national competition (Order 0, national, no age limit), so it plays no "
            + "World Cup qualifiers.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id);
    }

    /// <summary>
    /// Logs that the league has more than one national competition, so the qualifiers are refused.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="nationals">The league's national competitions, ordered by id.</param>
    private void LogSeveralNationalCompetitions(
        LeagueStepContext context,
        IReadOnlyList<GetCompetitionsResult> nationals)
    {
        string ids = string.Join(
            ", ",
            nationals.Select(competition => competition.Id.ToString(CultureInfo.InvariantCulture)));

        _logger.LogError(
            LogPrefix + ": the World Cup qualifiers are refused: the league has {CompetitionCount} national "
            + "competitions ({CompetitionIds}) and needs exactly one. The next tick tries again.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            nationals.Count,
            ids);
    }

    /// <summary>
    /// Logs that the national competition already has matches in the season, so this step has nothing to add.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="national">The league's national competition.</param>
    private void LogAlreadyWritten(LeagueStepContext context, GetCompetitionsResult national)
    {
        _logger.LogInformation(
            LogPrefix + ": the World Cup qualifiers of '{CompetitionName}' (competition {CompetitionId}) are already "
            + "written, so this step has nothing to add.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            national.Name,
            national.Id);
    }

    /// <summary>
    /// Logs that the league matches are not written yet.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    private void LogWaitingForLeague(LeagueStepContext context)
    {
        _logger.LogInformation(
            LogPrefix + ": the league matches are not written yet, so the World Cup qualifiers wait for a later "
            + "tick.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id);
    }

    /// <summary>
    /// Logs that the calendar the league was written on has no World Cup.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="national">The league's national competition.</param>
    /// <param name="clubCounts">The number of clubs in each division, in division order.</param>
    private void LogNoWorldCupCalendar(
        LeagueStepContext context,
        GetCompetitionsResult national,
        IReadOnlyList<int> clubCounts)
    {
        _logger.LogInformation(
            LogPrefix + ": the calendar has no World Cup at {ClubCounts} clubs per division, so the national "
            + "competition '{CompetitionName}' gets no qualifiers.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            string.Join(", ", clubCounts.Select(count => count.ToString(CultureInfo.InvariantCulture))),
            national.Name);
    }

    /// <summary>
    /// Logs that the season plays without World Cup qualifiers because they cannot be dated in time.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="firstFinalsDay">The date of the first matchday of the finals.</param>
    private void LogPlaysWithoutQualifiers(LeagueStepContext context, DateTime firstFinalsDay)
    {
        _logger.LogWarning(
            LogPrefix + ": the season plays without World Cup qualifiers: they can no longer be placed before "
            + "{FirstFinalsDay} UTC.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            firstFinalsDay.ToString(DateFormat, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Logs the nations with enough players that have no confederation, and so are left out. It logs nothing when there
    /// are none.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="nations">The nations left out.</param>
    private void LogNationsWithoutConfederation(
        LeagueStepContext context,
        IReadOnlyList<GetNationalTeamConfederationsResult> nations)
    {
        if (nations.Count == 0)
        {
            return;
        }

        _logger.LogWarning(
            LogPrefix + ": nations with 18 or more players but no confederation are left out of the World Cup "
            + "qualifiers: {Nations}.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            DescribeNations(nations));
    }

    /// <summary>
    /// Logs the nations with enough players that are not on the league's roster, and so play no qualifier. It logs
    /// nothing when there are none.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="nations">The nations left out.</param>
    private void LogNationsNotOnRoster(
        LeagueStepContext context,
        IReadOnlyList<GetNationalTeamConfederationsResult> nations)
    {
        if (nations.Count == 0)
        {
            return;
        }

        _logger.LogWarning(
            LogPrefix + ": nations with 18 or more players are not on this league's roster, so they play no World "
            + "Cup qualifiers: {Nations}.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id,
            DescribeNations(nations));
    }

    /// <summary>
    /// Logs, for each confederation with exactly one entrant, that its nation plays no qualifier and keeps a direct
    /// place.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="entrants">The entrants, ordered by confederation.</param>
    private void LogDirectPlaces(LeagueStepContext context, IReadOnlyList<QualifierNation> entrants)
    {
        IEnumerable<IGrouping<Confederation, QualifierNation>> alone = entrants
            .GroupBy(entrant => entrant.Confederation)
            .Where(confederation => confederation.Count() == 1)
            .OrderBy(confederation => confederation.Key);

        foreach (IGrouping<Confederation, QualifierNation> confederation in alone)
        {
            QualifierNation nation = confederation.Single();

            _logger.LogInformation(
                LogPrefix + ": '{NationName}' (national team {NationalTeamId}) is {Confederation}'s only entrant, "
                + "so it plays no qualifier and keeps a direct place.",
                context.League.Name,
                context.League.Id,
                context.Season.SeasonNumber,
                context.Season.Id,
                nation.Name,
                nation.NationalTeamId,
                LabelOf(confederation.Key));
        }
    }

    /// <summary>
    /// Logs that no confederation has two or more nations that can enter, so no qualifier is written.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    private void LogNoGroups(LeagueStepContext context)
    {
        _logger.LogWarning(
            LogPrefix + ": no confederation has two or more nations that can enter, so no World Cup qualifiers are "
            + "written.",
            context.League.Name,
            context.League.Id,
            context.Season.SeasonNumber,
            context.Season.Id);
    }

    /// <summary>
    /// Logs that the World Cup qualifiers are refused, and why.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="reason">What is wrong and how to fix it.</param>
    private void LogRefusal(LeagueStepContext context, string reason)
    {
        _logger.LogError(
            LogPrefix + ": the World Cup qualifiers are refused: {Reason} The next tick tries again.",
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
    /// <param name="EntrantCount">The number of nations that play a qualifier.</param>
    /// <param name="EntrantsByConfederation">The nations that play, counted for each confederation, as text.</param>
    /// <param name="FirstKickoff">The kickoff of the first match.</param>
    /// <param name="LastKickoff">The kickoff of the last match.</param>
    private sealed record QualifierPlan(
        IReadOnlyList<StagedMatch> Matches,
        int GroupCount,
        int EntrantCount,
        string EntrantsByConfederation,
        DateTime FirstKickoff,
        DateTime LastKickoff);
}
