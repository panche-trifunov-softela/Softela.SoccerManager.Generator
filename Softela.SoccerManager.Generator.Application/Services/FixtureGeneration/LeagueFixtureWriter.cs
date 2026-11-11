using System.Globalization;
using Microsoft.Extensions.Logging;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Repositories;

namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// Writes one league's season in a single transaction under the season's lock. It reads the season's matches again
/// after taking the lock, so a run that wrote first is seen, and lets its caller decide from them what to insert.
/// </summary>
internal sealed class LeagueFixtureWriter
{
    /// <summary>
    /// The start of the lock's name; the season's id follows, so two runs never write the same season and different
    /// seasons never wait for each other.
    /// </summary>
    private const string LockResourcePrefix = "SeasonFixtures_";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IMatchRepository _matchRepository;
    private readonly ILogger<LeagueFixtureWriter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeagueFixtureWriter"/> class.
    /// </summary>
    /// <param name="unitOfWork">The unit of work that holds the transaction and the lock.</param>
    /// <param name="matchRepository">The repository that reads the season's matches.</param>
    /// <param name="logger">The logger.</param>
    public LeagueFixtureWriter(
        IUnitOfWork unitOfWork,
        IMatchRepository matchRepository,
        ILogger<LeagueFixtureWriter> logger)
    {
        _unitOfWork = unitOfWork;
        _matchRepository = matchRepository;
        _logger = logger;
    }

    /// <summary>
    /// Writes in one transaction under the season's lock. After taking the lock it reads the season's matches and
    /// hands them to <paramref name="insertAsync"/>, which inserts what the season still needs and says whether it
    /// inserted anything. The transaction is committed when it did and rolled back when it did not, and nothing is
    /// read or written when another run holds the lock. An exception, from <paramref name="insertAsync"/> or the
    /// database, rolls the transaction back and is thrown again.
    /// </summary>
    /// <param name="league">The league whose season is written.</param>
    /// <param name="season">The season; its id names the lock.</param>
    /// <param name="insertAsync">
    /// Inserts the season's rows on the open transaction, given the season's matches as read after the lock was
    /// taken; its result is true when it inserted any, and false when there was nothing to write.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A task whose result says whether the rows were written, another run held the lock, or nothing was written.
    /// </returns>
    public async Task<FixtureWriteOutcome> WriteAsync(
        GetLeaguesResult league,
        GetSeasonsResult season,
        Func<IReadOnlyList<GetMatchesResult>, Task<bool>> insertAsync,
        CancellationToken cancellationToken = default)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            string resource = LockResourcePrefix + season.Id.ToString(CultureInfo.InvariantCulture);

            if (!await _unitOfWork.TryLockAsync(resource, cancellationToken))
            {
                await _unitOfWork.RollbackAsync(CancellationToken.None);

                return FixtureWriteOutcome.LockHeld;
            }

            // Read inside the transaction and after the lock, so a run that wrote the season first is seen.
            IReadOnlyList<GetMatchesResult> seasonMatches = await _matchRepository.GetBySeasonIdAsync(season.Id);

            if (!await insertAsync(seasonMatches))
            {
                await _unitOfWork.RollbackAsync(CancellationToken.None);

                return FixtureWriteOutcome.NotWritten;
            }

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

        return FixtureWriteOutcome.Written;
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
