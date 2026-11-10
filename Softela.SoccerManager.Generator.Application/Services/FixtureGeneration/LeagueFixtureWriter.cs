using System.Globalization;
using Microsoft.Extensions.Logging;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Repositories;
using Softela.SoccerManager.Generator.Application.Services.RoundRobin;

namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// Writes the matches of one league's season, with their statistics rows, in a single transaction.
/// </summary>
internal sealed class LeagueFixtureWriter
{
    /// <summary>
    /// The user that every row the generator writes is created and modified by. No user table holds it; the backend's
    /// importer stamps its own rows with the id that ends in 1.
    /// </summary>
    private static readonly Guid GeneratorUserId = new("00000000-0000-0000-0000-000000000002");

    /// <summary>
    /// The start of the lock's name; the season's id follows, so two runs never write the same season and different
    /// seasons never wait for each other.
    /// </summary>
    private const string LockResourcePrefix = "SeasonFixtures_";

    /// <summary>
    /// The start of every log line about a league's season. The service's log lines start with it too.
    /// </summary>
    internal const string LogPrefix = "League '{LeagueName}' (id {LeagueId}), season {SeasonNumber} (id {SeasonId})";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IMatchRepository _matchRepository;
    private readonly IMatchTeamStatisticRepository _matchTeamStatisticRepository;
    private readonly ILogger<LeagueFixtureWriter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeagueFixtureWriter"/> class.
    /// </summary>
    /// <param name="unitOfWork">The unit of work that holds the transaction and the lock.</param>
    /// <param name="matchRepository">The repository that reads and writes matches.</param>
    /// <param name="matchTeamStatisticRepository">The repository that writes the statistics rows of matches.</param>
    /// <param name="logger">The logger.</param>
    public LeagueFixtureWriter(
        IUnitOfWork unitOfWork,
        IMatchRepository matchRepository,
        IMatchTeamStatisticRepository matchTeamStatisticRepository,
        ILogger<LeagueFixtureWriter> logger)
    {
        _unitOfWork = unitOfWork;
        _matchRepository = matchRepository;
        _matchTeamStatisticRepository = matchTeamStatisticRepository;
        _logger = logger;
    }

    /// <summary>
    /// Writes every match of the season and two statistics rows for each, or nothing at all. It leaves the season
    /// alone when another run holds its lock, or when another run has written its matches first.
    /// </summary>
    /// <param name="input">The league, its season, its fixtures and their dates.</param>
    /// <param name="runTime">The moment of the run, in UTC; it is the time stamp of each row.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task whose result is true when the season was written, and false when it was left alone.</returns>
    public async Task<bool> WriteAsync(
        LeagueInput input,
        DateTime runTime,
        CancellationToken cancellationToken = default)
    {
        GetLeaguesResult league = input.League;
        GetSeasonsResult season = input.Season;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            string resource = LockResourcePrefix + season.Id.ToString(CultureInfo.InvariantCulture);

            if (!await _unitOfWork.TryLockAsync(resource, cancellationToken))
            {
                await _unitOfWork.RollbackAsync(CancellationToken.None);

                _logger.LogInformation(
                    LogPrefix + ": another run holds this season's lock, so this run leaves the season to it.",
                    league.Name,
                    league.Id,
                    season.SeasonNumber,
                    season.Id);

                return false;
            }

            // Read inside the transaction and after the lock, so a run that wrote the season first is seen.
            IReadOnlyList<GetMatchesResult> matches = await _matchRepository.GetAllAsync();
            HashSet<int> divisionIds = input.Divisions.Select(division => division.CompetitionId).ToHashSet();

            if (LeagueInputBuilder.FindDivisionMatch(season.Id, divisionIds, matches) is not null)
            {
                await _unitOfWork.RollbackAsync(CancellationToken.None);

                _logger.LogInformation(
                    LogPrefix + ": another run wrote its matches first, so this run writes nothing.",
                    league.Name,
                    league.Id,
                    season.SeasonNumber,
                    season.Id);

                return false;
            }

            await InsertMatchesAsync(input, runTime);

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // Logged before the rollback, which can itself throw after a dropped connection and must not hide this.
            _logger.LogError(
                exception,
                "Writing league '{LeagueName}' (id {LeagueId}), season {SeasonNumber} (id {SeasonId}) failed, "
                + "so it is rolled back and the run fails.",
                league.Name,
                league.Id,
                season.SeasonNumber,
                season.Id);

            await TryRollbackAsync(league, season);

            throw;
        }

        // After the try, so nothing that fails once the rows are committed is reported as a rollback.
        LogWritten(input);

        return true;
    }

    /// <summary>
    /// Inserts every match, one at a time, each followed by the statistics rows of its two clubs. The match goes first
    /// because the statistics procedure refuses a club whose league does not own the match's season.
    /// </summary>
    /// <param name="input">The league, its season, its fixtures and their dates.</param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <returns>A task that completes once every row has been inserted.</returns>
    private async Task InsertMatchesAsync(LeagueInput input, DateTime runTime)
    {
        for (int index = 0; index < input.Divisions.Count; index++)
        {
            LeagueDivision division = input.Divisions[index];

            foreach (RoundRobinMatch fixture in input.Fixtures[index])
            {
                int matchId = await _matchRepository.CreateAsync(
                    seasonId: input.Season.Id,
                    competitionId: division.CompetitionId,
                    refereeId: null,
                    startDateTime: input.Matchdays[fixture.Round - 1],
                    commentary: null,
                    attendance: 0,
                    isStarted: false,
                    createdAt: runTime,
                    modifiedAt: runTime,
                    createdBy: GeneratorUserId,
                    modifiedBy: GeneratorUserId);

                await InsertStatisticAsync(fixture.HomeClubId, matchId, true, runTime);
                await InsertStatisticAsync(fixture.AwayClubId, matchId, false, runTime);
            }
        }
    }

    /// <summary>
    /// Inserts the statistics row of one club in one match, with every count at zero.
    /// </summary>
    /// <param name="leagueTeamId">The id of the club's roster entry.</param>
    /// <param name="matchId">The id of the match.</param>
    /// <param name="isHomeTeam">Whether the club is at home.</param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <returns>A task that completes once the row has been inserted.</returns>
    private async Task InsertStatisticAsync(int leagueTeamId, int matchId, bool isHomeTeam, DateTime runTime)
    {
        await _matchTeamStatisticRepository.CreateAsync(
            leagueTeamId: leagueTeamId,
            matchId: matchId,
            isHomeTeam: isHomeTeam,
            shotsTotal: 0,
            shotsOnTarget: 0,
            possession: 0,
            createdAt: runTime,
            modifiedAt: runTime,
            createdBy: GeneratorUserId,
            modifiedBy: GeneratorUserId);
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

    /// <summary>
    /// Rolls the transaction back after a failure, and logs a failed rollback instead of throwing it, so the failure
    /// that caused the rollback is the one that escapes.
    /// </summary>
    /// <param name="league">The league whose season failed.</param>
    /// <param name="season">The season that failed.</param>
    /// <returns>A task that completes once the rollback has been tried.</returns>
    private async Task TryRollbackAsync(GetLeaguesResult league, GetSeasonsResult season)
    {
        try
        {
            await _unitOfWork.RollbackAsync(CancellationToken.None);
        }
        catch (Exception rollbackException)
        {
            _logger.LogError(
                rollbackException,
                "Rolling back league '{LeagueName}' (id {LeagueId}), season {SeasonNumber} (id {SeasonId}) failed "
                + "as well.",
                league.Name,
                league.Id,
                season.SeasonNumber,
                season.Id);
        }
    }
}
