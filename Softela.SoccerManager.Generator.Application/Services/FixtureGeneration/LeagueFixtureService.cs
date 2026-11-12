using Microsoft.Extensions.Logging;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Repositories;

namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// Runs the steps of every league's newest season, league by league. The steps are the registered
/// <see cref="ILeagueStep"/> implementations, and they run in the order they are registered.
/// </summary>
internal sealed class LeagueFixtureService : ILeagueFixtureService
{
    private readonly ILeagueRepository _leagueRepository;
    private readonly ISeasonRepository _seasonRepository;
    private readonly ICompetitionRepository _competitionRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly IReadOnlyList<ILeagueStep> _steps;
    private readonly ILogger<LeagueFixtureService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeagueFixtureService"/> class.
    /// </summary>
    /// <param name="leagueRepository">The repository that reads leagues.</param>
    /// <param name="seasonRepository">The repository that reads seasons.</param>
    /// <param name="competitionRepository">The repository that reads competitions.</param>
    /// <param name="teamRepository">The repository that reads teams.</param>
    /// <param name="matchRepository">The repository that reads matches.</param>
    /// <param name="steps">The steps of a league's newest season, in the order they run.</param>
    /// <param name="logger">The logger.</param>
    public LeagueFixtureService(
        ILeagueRepository leagueRepository,
        ISeasonRepository seasonRepository,
        ICompetitionRepository competitionRepository,
        ITeamRepository teamRepository,
        IMatchRepository matchRepository,
        IEnumerable<ILeagueStep> steps,
        ILogger<LeagueFixtureService> logger)
    {
        _leagueRepository = leagueRepository;
        _seasonRepository = seasonRepository;
        _competitionRepository = competitionRepository;
        _teamRepository = teamRepository;
        _matchRepository = matchRepository;
        _steps = steps.ToList();
        _logger = logger;
    }

    /// <summary>
    /// Runs the steps of every league's newest season, which write its league matches when it has none and its
    /// domestic cup ties as they become known. A step that cannot write a season as its rows stand logs it and leaves
    /// it to a later run, and the next step still runs; any other exception ends the run.
    /// </summary>
    /// <param name="runTime">
    /// The moment of this run, in UTC. It is the start date of any season written, the creation and modification time
    /// of its rows, the clock that the one-hour rule on Standings and the 365-day rule on the first match read, and
    /// the clock that the dates of the cup's ties are measured from.
    /// </param>
    /// <param name="cancellationToken">The cancellation token; it stops the run before the next league.</param>
    /// <returns>A task that completes once every league has been handled.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="runTime"/> is not in UTC.</exception>
    public async Task GenerateAsync(DateTime runTime, CancellationToken cancellationToken = default)
    {
        if (runTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The run time must be UTC.", nameof(runTime));
        }

        // One after another, never in parallel: every repository shares one connection.
        IReadOnlyList<GetLeaguesResult> leagues = await _leagueRepository.GetAllAsync();
        IReadOnlyList<GetTeamsResult> teams = await _teamRepository.GetAllAsync();
        IReadOnlyList<GetMatchesResult> matches = await _matchRepository.GetAllAsync();

        foreach (GetLeaguesResult league in leagues.OrderBy(candidate => candidate.Id))
        {
            cancellationToken.ThrowIfCancellationRequested();

            await GenerateLeagueAsync(league, runTime, teams, matches, cancellationToken);
        }
    }

    /// <summary>
    /// Runs the steps of one league's newest season, or says in the log why there is nothing to run them on.
    /// </summary>
    /// <param name="league">The league.</param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <param name="teams">Every team.</param>
    /// <param name="matches">Every match of every league.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes once every step has been handled.</returns>
    private async Task GenerateLeagueAsync(
        GetLeaguesResult league,
        DateTime runTime,
        IReadOnlyList<GetTeamsResult> teams,
        IReadOnlyList<GetMatchesResult> matches,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<GetSeasonsResult> seasons = await _seasonRepository.GetByLeagueIdAsync(league.Id);

        if (seasons.Count == 0)
        {
            _logger.LogInformation(
                "League '{LeagueName}' (id {LeagueId}) has no season, so there is nothing to write.",
                league.Name,
                league.Id);

            return;
        }

        GetSeasonsResult season = seasons.OrderByDescending(candidate => candidate.SeasonNumber).First();

        IReadOnlyList<GetCompetitionsResult> competitions = await _competitionRepository.GetByLeagueIdAsync(league.Id);

        List<GetCompetitionsResult> divisions = competitions
            .Where(competition => competition.Order > 0)
            .OrderBy(competition => competition.Order)
            .ToList();

        if (divisions.Count == 0)
        {
            _logger.LogInformation(
                "League '{LeagueName}' (id {LeagueId}) has no division: none of its competitions has an Order above 0.",
                league.Name,
                league.Id);

            return;
        }

        HashSet<int> seasonIds = seasons.Select(candidate => candidate.Id).ToHashSet();
        List<GetMatchesResult> leagueMatches = matches.Where(match => seasonIds.Contains(match.SeasonId)).ToList();
        LeagueStepContext context = new(league, season, divisions, competitions, teams, leagueMatches, runTime);

        foreach (ILeagueStep step in _steps)
        {
            await step.RunAsync(context, cancellationToken);
        }
    }
}
