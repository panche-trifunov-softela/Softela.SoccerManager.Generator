namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// Writes the league matches of the seasons that have none yet, and the domestic cup ties of the seasons that have a
/// cup.
/// </summary>
public interface ILeagueFixtureService
{
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
    Task GenerateAsync(DateTime runTime, CancellationToken cancellationToken = default);
}
