using System.Globalization;
using Microsoft.Extensions.Logging;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Repositories;

namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// The league matches step: it writes every league match of a league's newest season, with the statistics rows of
/// both clubs, when none of the season's divisions has a match yet.
/// </summary>
internal sealed class LeagueMatchesStep : ILeagueStep
{
    /// <summary>
    /// How long Standings must have been unchanged before a season is written, so that a person still setting the
    /// season up is not overtaken. This is the hour rule.
    /// </summary>
    private static readonly TimeSpan StandingsSettleTime = TimeSpan.FromHours(1);

    /// <summary>
    /// The format of the time of the newest Standings change in a log line.
    /// </summary>
    private const string StandingsChangedAtFormat = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// The start of every log line about the league's season.
    /// </summary>
    private const string LogPrefix = LeagueStepContext.LogPrefix;

    private readonly IStandingRepository _standingRepository;
    private readonly ILeagueTeamRepository _leagueTeamRepository;
    private readonly LeagueFixtureWriter _leagueFixtureWriter;
    private readonly MatchInserter _matchInserter;
    private readonly ILogger<LeagueMatchesStep> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeagueMatchesStep"/> class.
    /// </summary>
    /// <param name="standingRepository">The repository that reads Standings.</param>
    /// <param name="leagueTeamRepository">The repository that reads leagues' rosters.</param>
    /// <param name="leagueFixtureWriter">The writer that holds the season's transaction and lock.</param>
    /// <param name="matchInserter">The inserter of the matches and their statistics rows.</param>
    /// <param name="logger">The logger.</param>
    public LeagueMatchesStep(
        IStandingRepository standingRepository,
        ILeagueTeamRepository leagueTeamRepository,
        LeagueFixtureWriter leagueFixtureWriter,
        MatchInserter matchInserter,
        ILogger<LeagueMatchesStep> logger)
    {
        _standingRepository = standingRepository;
        _leagueTeamRepository = leagueTeamRepository;
        _leagueFixtureWriter = leagueFixtureWriter;
        _matchInserter = matchInserter;
        _logger = logger;
    }

    /// <summary>
    /// Writes the league matches of the league's newest season, or says in the log why it does not.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes once the season has been handled.</returns>
    public async Task RunAsync(LeagueStepContext context, CancellationToken cancellationToken)
    {
        GetLeaguesResult league = context.League;
        GetSeasonsResult season = context.Season;
        IReadOnlyList<GetCompetitionsResult> divisions = context.Divisions;
        IReadOnlyList<GetTeamsResult> teams = context.Teams;
        IReadOnlyList<GetMatchesResult> leagueMatches = context.LeagueMatches;
        DateTime runTime = context.RunTime;

        HashSet<int> divisionIds = divisions.Select(division => division.Id).ToHashSet();
        GetMatchesResult? existing = LeagueInputBuilder.FindDivisionMatch(season.Id, divisionIds, leagueMatches);

        if (existing is not null)
        {
            GetCompetitionsResult existingDivision = divisions.First(division => division.Id == existing.CompetitionId);

            _logger.LogInformation(
                LogPrefix + ": division '{DivisionName}' (competition {CompetitionId}) already has matches, "
                + "so the season is not written again.",
                league.Name,
                league.Id,
                season.SeasonNumber,
                season.Id,
                existingDivision.Name,
                existingDivision.Id);

            return;
        }

        Dictionary<int, IReadOnlyList<GetStandingsResult>> standingsByDivision = [];

        foreach (GetCompetitionsResult division in divisions)
        {
            standingsByDivision[division.Id] = await _standingRepository.GetByCompetitionAndSeasonAsync(
                division.Id,
                season.Id);
        }

        if (standingsByDivision.Values.All(rows => rows.Count == 0))
        {
            _logger.LogInformation(
                LogPrefix + ": no division has Standings rows yet, so there is nothing to write.",
                league.Name,
                league.Id,
                season.SeasonNumber,
                season.Id);

            return;
        }

        DateTime newestChange = standingsByDivision.Values.SelectMany(rows => rows).Max(row => row.ModifiedAt);

        if (runTime - newestChange < StandingsSettleTime)
        {
            _logger.LogInformation(
                LogPrefix + ": its newest Standings change, {StandingsChangedAt} UTC, is under an hour old, "
                + "so the season waits for a later tick.",
                league.Name,
                league.Id,
                season.SeasonNumber,
                season.Id,
                newestChange.ToString(StandingsChangedAtFormat, CultureInfo.InvariantCulture));

            return;
        }

        IReadOnlyList<GetLeagueTeamsResult> roster = await _leagueTeamRepository.GetByLeagueIdAsync(league.Id);
        LeagueInput input;

        try
        {
            input = LeagueInputBuilder.Build(
                league,
                season,
                divisions,
                standingsByDivision,
                roster,
                teams,
                leagueMatches,
                runTime);
        }
        catch (SeasonRefusedException refusal)
        {
            _logger.LogError(
                LogPrefix + " is refused: {Reason} The next tick tries again.",
                league.Name,
                league.Id,
                season.SeasonNumber,
                season.Id,
                refusal.Message);

            return;
        }

        FixtureWriteOutcome outcome = await _leagueFixtureWriter.WriteAsync(
            league,
            season,
            seasonMatches => InsertUnlessWrittenAsync(input, seasonMatches, runTime),
            cancellationToken);

        switch (outcome)
        {
            case FixtureWriteOutcome.LockHeld:
                _logger.LogInformation(
                    LogPrefix + ": another run holds this season's lock, so this run leaves the season to it.",
                    league.Name,
                    league.Id,
                    season.SeasonNumber,
                    season.Id);

                break;

            case FixtureWriteOutcome.NotWritten:
                _logger.LogInformation(
                    LogPrefix + ": another run wrote its matches first, so this run writes nothing.",
                    league.Name,
                    league.Id,
                    season.SeasonNumber,
                    season.Id);

                break;

            case FixtureWriteOutcome.Written:
                LogWritten(input);
                break;
        }
    }

    /// <summary>
    /// Inserts the season's league matches unless one of its divisions already has a match, which another run wrote
    /// first. It runs inside the writer's transaction, after the season's lock was taken.
    /// </summary>
    /// <param name="input">The league, its season, its fixtures and their dates.</param>
    /// <param name="seasonMatches">The season's matches, read inside the transaction after the lock was taken.</param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <returns>A task whose result is true when the matches were inserted, and false when none were.</returns>
    private async Task<bool> InsertUnlessWrittenAsync(
        LeagueInput input,
        IReadOnlyList<GetMatchesResult> seasonMatches,
        DateTime runTime)
    {
        HashSet<int> divisionIds = input.Divisions.Select(division => division.CompetitionId).ToHashSet();

        if (LeagueInputBuilder.FindDivisionMatch(input.Season.Id, divisionIds, seasonMatches) is not null)
        {
            return false;
        }

        await _matchInserter.InsertLeagueMatchesAsync(input, runTime);

        return true;
    }

    /// <summary>
    /// Logs what a committed season holds.
    /// </summary>
    /// <param name="input">The league, its season, its fixtures and their dates.</param>
    private void LogWritten(LeagueInput input)
    {
        int matchCount = input.Fixtures.Sum(fixtures => fixtures.Count);

        string divisionMatchCounts = string.Join(
            " + ",
            input.Fixtures.Select(fixtures => fixtures.Count.ToString(CultureInfo.InvariantCulture)));

        int matchesPerRound = input.Fixtures.Sum(fixtures => fixtures.Count(match => match.Round == 1));
        string firstMatch = input.Matchdays[0].ToString(LeagueInputBuilder.KickoffFormat, CultureInfo.InvariantCulture);
        string lastRound = input.Matchdays[^1].ToString(LeagueInputBuilder.KickoffFormat, CultureInfo.InvariantCulture);

        _logger.LogInformation(
            LogPrefix + ": wrote {MatchCount} matches ({DivisionMatchCounts}), {MatchesPerRound} per round, "
            + "and {StatisticCount} statistic rows, seed {Seed}. First match {FirstMatch} UTC, "
            + "last round {LastRound} UTC.",
            input.League.Name,
            input.League.Id,
            input.Season.SeasonNumber,
            input.Season.Id,
            matchCount,
            divisionMatchCounts,
            matchesPerRound,
            matchCount * 2,
            input.Season.Id,
            firstMatch,
            lastRound);
    }
}
