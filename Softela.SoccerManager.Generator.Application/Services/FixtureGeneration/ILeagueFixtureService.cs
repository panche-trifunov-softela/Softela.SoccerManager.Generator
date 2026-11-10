namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// Writes the league matches of the seasons that have none yet.
/// </summary>
public interface ILeagueFixtureService
{
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
    Task GenerateAsync(DateTime runTime, CancellationToken cancellationToken = default);
}
