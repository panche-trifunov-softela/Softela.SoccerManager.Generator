namespace Softela.SoccerManager.Generator.Application.Services.Seasons;

/// <summary>
/// How long Standings must stay unchanged before a step acts on them, so that a person still entering them is not
/// overtaken. This is the hour rule.
/// </summary>
public static class StandingsSettleRule
{
    /// <summary>
    /// How long Standings must have been unchanged before a step acts on them.
    /// </summary>
    public static readonly TimeSpan SettleTime = TimeSpan.FromHours(1);

    /// <summary>
    /// The format of the time of the newest Standings change in a log line.
    /// </summary>
    public const string ChangedAtFormat = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// Tells whether Standings have been unchanged for at least <see cref="SettleTime"/>.
    /// </summary>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <param name="newestChange">The time of the newest change to the Standings rows, in UTC.</param>
    /// <returns>True when the run is at least <see cref="SettleTime"/> after the newest change.</returns>
    public static bool IsSettled(DateTime runTime, DateTime newestChange)
    {
        return runTime - newestChange >= SettleTime;
    }
}
