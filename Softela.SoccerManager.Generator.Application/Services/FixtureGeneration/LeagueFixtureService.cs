using System.Globalization;
using Microsoft.Extensions.Logging;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Repositories;

namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// Writes the league matches of every league's newest season that has none yet, league by league.
/// </summary>
internal sealed class LeagueFixtureService : ILeagueFixtureService
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
    /// The start of every log line about a league's season, the same as the writer's.
    /// </summary>
    private const string LogPrefix = LeagueFixtureWriter.LogPrefix;

    private readonly ILeagueRepository _leagueRepository;
    private readonly ISeasonRepository _seasonRepository;
    private readonly ICompetitionRepository _competitionRepository;
    private readonly IStandingRepository _standingRepository;
    private readonly ILeagueTeamRepository _leagueTeamRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly LeagueFixtureWriter _leagueFixtureWriter;
    private readonly ILogger<LeagueFixtureService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeagueFixtureService"/> class.
    /// </summary>
    /// <param name="leagueRepository">The repository that reads leagues.</param>
    /// <param name="seasonRepository">The repository that reads seasons.</param>
    /// <param name="competitionRepository">The repository that reads competitions.</param>
    /// <param name="standingRepository">The repository that reads Standings.</param>
    /// <param name="leagueTeamRepository">The repository that reads leagues' rosters.</param>
    /// <param name="teamRepository">The repository that reads teams.</param>
    /// <param name="matchRepository">The repository that reads matches.</param>
    /// <param name="leagueFixtureWriter">The writer of one league's season.</param>
    /// <param name="logger">The logger.</param>
    public LeagueFixtureService(
        ILeagueRepository leagueRepository,
        ISeasonRepository seasonRepository,
        ICompetitionRepository competitionRepository,
        IStandingRepository standingRepository,
        ILeagueTeamRepository leagueTeamRepository,
        ITeamRepository teamRepository,
        IMatchRepository matchRepository,
        LeagueFixtureWriter leagueFixtureWriter,
        ILogger<LeagueFixtureService> logger)
    {
        _leagueRepository = leagueRepository;
        _seasonRepository = seasonRepository;
        _competitionRepository = competitionRepository;
        _standingRepository = standingRepository;
        _leagueTeamRepository = leagueTeamRepository;
        _teamRepository = teamRepository;
        _matchRepository = matchRepository;
        _leagueFixtureWriter = leagueFixtureWriter;
        _logger = logger;
    }

    /// <summary>
    /// Writes the league matches of every league's newest season that has none yet. A league whose season cannot be
    /// written as its rows stand is logged and left for a later run; any other exception ends the run.
    /// </summary>
    /// <param name="runTime">
    /// The moment of this run, in UTC. It is the start date of any season written, the creation and modification time
    /// of its rows, and the clock that the one-hour rule on Standings and the 365-day rule on the first match read.
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
    /// Writes the newest season of one league, or says in the log why it does not.
    /// </summary>
    /// <param name="league">The league.</param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <param name="teams">Every team.</param>
    /// <param name="matches">Every match of every league.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes once the league has been handled.</returns>
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
        HashSet<int> divisionIds = divisions.Select(division => division.Id).ToHashSet();
        List<GetMatchesResult> leagueMatches = matches.Where(match => seasonIds.Contains(match.SeasonId)).ToList();
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

        await _leagueFixtureWriter.WriteAsync(input, runTime, cancellationToken);
    }
}
