namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// One step of a tick for one league's newest season. The steps run one after another, in the order they are
/// registered, and each starts once the steps before it have returned, whether or not they wrote anything. A step
/// logs why it writes nothing and handles its own refusals; any other exception it lets through ends the run, so the
/// steps and leagues after it do not run.
/// </summary>
internal interface ILeagueStep
{
    /// <summary>
    /// Runs the step for one league's newest season.
    /// </summary>
    /// <param name="context">
    /// The league, its newest season and what the tick read at its start. A step that must see what an earlier step
    /// wrote reads the season again inside its own lock.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes once the step has been handled.</returns>
    Task RunAsync(LeagueStepContext context, CancellationToken cancellationToken);
}
