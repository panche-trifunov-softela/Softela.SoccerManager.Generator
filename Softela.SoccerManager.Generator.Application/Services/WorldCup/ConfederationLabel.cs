namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// Names a confederation for a log line or a message. The qualifier step and the finals steps name confederations the
/// same way, so the name lives here once.
/// </summary>
internal static class ConfederationLabel
{
    /// <summary>
    /// Names a confederation, in capitals.
    /// </summary>
    /// <param name="confederation">The confederation.</param>
    /// <returns>The confederation's name in capitals, such as CONCACAF.</returns>
    internal static string Of(Confederation confederation)
    {
        return confederation.ToString().ToUpperInvariant();
    }
}
