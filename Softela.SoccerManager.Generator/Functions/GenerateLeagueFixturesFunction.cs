using Microsoft.Azure.Functions.Worker;
using Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

namespace Softela.SoccerManager.Generator.Functions;

/// <summary>
/// Writes the league matches of every league's newest season that has none yet. It runs every six hours, at 00:00,
/// 06:00, 12:00 and 18:00 host time. An exception that the service lets through fails the run, so the host reports it
/// and the next tick starts over from the first league.
/// </summary>
public sealed class GenerateLeagueFixturesFunction
{
    private readonly ILeagueFixtureService _leagueFixtureService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateLeagueFixturesFunction"/> class.
    /// </summary>
    /// <param name="leagueFixtureService">The service that writes the matches.</param>
    public GenerateLeagueFixturesFunction(ILeagueFixtureService leagueFixtureService)
    {
        _leagueFixtureService = leagueFixtureService;
    }

    /// <summary>
    /// Runs the service for the current moment.
    /// </summary>
    /// <param name="timer">The timer information; the trigger binding requires it and nothing reads it.</param>
    /// <param name="cancellationToken">
    /// The token that the host signals when it shuts down; it stops the run before the next league.
    /// </param>
    /// <returns>A task that completes once every league has been handled.</returns>
    [Function("GenerateLeagueFixtures")]
    public async Task Run([TimerTrigger("0 0 */6 * * *")] TimerInfo timer, CancellationToken cancellationToken)
    {
        await _leagueFixtureService.GenerateAsync(DateTime.UtcNow, cancellationToken);
    }
}
