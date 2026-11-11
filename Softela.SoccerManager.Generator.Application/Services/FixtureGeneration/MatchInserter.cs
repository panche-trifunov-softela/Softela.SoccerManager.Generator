using Softela.SoccerManager.Generator.Application.Repositories;
using Softela.SoccerManager.Generator.Application.Services.RoundRobin;

namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// Inserts matches, each with the statistics rows of its two clubs, on the caller's open transaction. It never begins,
/// commits or rolls back a transaction, so its rows are kept or dropped with the caller's.
/// </summary>
internal sealed class MatchInserter
{
    /// <summary>
    /// The user that every row the generator writes is created and modified by. No user table holds it; the backend's
    /// importer stamps its own rows with the id that ends in 1.
    /// </summary>
    private static readonly Guid GeneratorUserId = new("00000000-0000-0000-0000-000000000002");

    private readonly IMatchRepository _matchRepository;
    private readonly IMatchTeamStatisticRepository _matchTeamStatisticRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="MatchInserter"/> class.
    /// </summary>
    /// <param name="matchRepository">The repository that writes matches.</param>
    /// <param name="matchTeamStatisticRepository">The repository that writes the statistics rows of matches.</param>
    public MatchInserter(
        IMatchRepository matchRepository,
        IMatchTeamStatisticRepository matchTeamStatisticRepository)
    {
        _matchRepository = matchRepository;
        _matchTeamStatisticRepository = matchTeamStatisticRepository;
    }

    /// <summary>
    /// Inserts every league match of the season, one at a time, each followed by the statistics rows of its two clubs.
    /// The match goes first because the statistics procedure refuses a club whose league does not own the match's
    /// season.
    /// </summary>
    /// <param name="input">The league, its season, its fixtures and their dates.</param>
    /// <param name="runTime">The moment of the run, in UTC; it is the time stamp of each row.</param>
    /// <returns>A task that completes once every row has been inserted.</returns>
    public async Task InsertLeagueMatchesAsync(LeagueInput input, DateTime runTime)
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
}
