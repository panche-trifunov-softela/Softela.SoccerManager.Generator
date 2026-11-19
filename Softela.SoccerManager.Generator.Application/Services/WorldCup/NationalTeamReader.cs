using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Repositories;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// Reads what the World Cup steps need about the national teams. The qualifier step reads the roster, the
/// confederations and the players' ratings at once. The finals group step reads the roster and the confederations at
/// every tick, and the ratings only on the tick that draws the groups. The reads and their order live here once.
/// </summary>
internal static class NationalTeamReader
{
    /// <summary>
    /// Reads the league's roster of national teams, the confederations and the players' ratings, in that order.
    /// </summary>
    /// <param name="repository">The repository that reads the national teams.</param>
    /// <param name="leagueId">The identifier of the league whose roster is read.</param>
    /// <returns>A task whose result is the three reads.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when a backend read is missing.</exception>
    internal static async Task<NationalTeamReads> ReadAsync(INationalTeamRepository repository, int leagueId)
    {
        return await AddRatingsAsync(repository, await ReadRosterAsync(repository, leagueId));
    }

    /// <summary>
    /// Reads the league's roster of national teams and the confederations, in that order.
    /// </summary>
    /// <param name="repository">The repository that reads the national teams.</param>
    /// <param name="leagueId">The identifier of the league whose roster is read.</param>
    /// <returns>A task whose result is the roster and the confederations.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when a backend read is missing.</exception>
    internal static async Task<RosterWithConfederations> ReadRosterAsync(
        INationalTeamRepository repository,
        int leagueId)
    {
        IReadOnlyList<GetLeagueNationalTeamsResult> roster =
            await repository.GetLeagueNationalTeamsAsync(leagueId);

        IReadOnlyList<GetNationalTeamConfederationsResult> confederations =
            await repository.GetConfederationsAsync();

        return new RosterWithConfederations(roster, confederations);
    }

    /// <summary>
    /// Reads the players' ratings and adds them to a roster and confederations that were read before.
    /// </summary>
    /// <param name="repository">The repository that reads the national teams.</param>
    /// <param name="roster">The roster and the confederations, read by <see cref="ReadRosterAsync"/>.</param>
    /// <returns>A task whose result is the three reads.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when a backend read is missing.</exception>
    internal static async Task<NationalTeamReads> AddRatingsAsync(
        INationalTeamRepository repository,
        RosterWithConfederations roster)
    {
        IReadOnlyList<GetNationalTeamPlayerRatingsResult> ratings =
            await repository.GetPlayerRatingsAsync();

        return new NationalTeamReads(roster.Roster, roster.Confederations, ratings);
    }
}
