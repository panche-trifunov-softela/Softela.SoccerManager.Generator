using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Services.Playoffs;

namespace Softela.SoccerManager.Generator.Application.Services.Rollover;

/// <summary>
/// What a season's matches and Standings say about whether it has ended. The status decides which members are set:
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="SeasonEndStatus.Running"/>: the waiting phrase is set; the places, the tables, the playoff winners and
/// the last deciding kickoff are empty.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="SeasonEndStatus.Ended"/>: the places, the tables, the playoff winners and the last deciding kickoff are
/// set; there is no waiting phrase.
/// </description>
/// </item>
/// </list>
/// The former divisions are set in both cases.
/// </summary>
/// <param name="Status">Whether the season has ended.</param>
/// <param name="WaitingFor">
/// The first thing the season waits for, as a phrase that completes "it waits for ..."; null unless running.
/// </param>
/// <param name="Places">
/// What each division's settings say about its table, in division order; empty unless ended.
/// </param>
/// <param name="Tables">
/// The league team ids of each division in place order, place 1 first, in division order; empty unless ended.
/// </param>
/// <param name="PlayoffWinners">
/// The league team id of each division's promotion playoff winner, in division order; null for a division without a
/// playoff; empty unless ended.
/// </param>
/// <param name="LastDecidingKickoff">
/// The latest kickoff among the matches that decided the end, in UTC; null unless ended.
/// </param>
/// <param name="FormerDivisions">
/// The competitions that have the shape of a division (domestic, not national, group-only, no age limit) but Order 0,
/// and have a league match in the season, by competition id. A tier that was given Order 0 after the season was
/// written is one of them.
/// </param>
internal sealed record SeasonEndReading(
    SeasonEndStatus Status,
    string? WaitingFor,
    IReadOnlyList<PromotionPlaces> Places,
    IReadOnlyList<IReadOnlyList<int>> Tables,
    IReadOnlyList<int?> PlayoffWinners,
    DateTime? LastDecidingKickoff,
    IReadOnlyList<GetCompetitionsResult> FormerDivisions)
{
    /// <summary>
    /// The days from the date of the last deciding match to the date a new season is built from. The season's first
    /// league Thursday is after that date, because the calendar adds a day, so the new season starts at least 7 days
    /// after the last deciding match.
    /// </summary>
    private const int DaysFromLastMatchToStartDate = 6;

    /// <summary>
    /// Gets the date the next season is built from: the later of the run's date and the date of the last deciding
    /// kickoff plus 6 days. The first league Thursday is after this date (the calendar adds a day), so the next season
    /// starts at least 7 days after the last deciding match, counted from its kickoff date, or on the first Thursday
    /// after the run's date when the run is later than that.
    /// </summary>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <returns>The date to build the next season from.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the season has not ended.</exception>
    public DateOnly StartDate(DateTime runTime)
    {
        if (Status != SeasonEndStatus.Ended || LastDecidingKickoff is not { } lastKickoff)
        {
            throw new InvalidOperationException("The season has not ended, so it has no start date for the next one.");
        }

        DateOnly runDate = DateOnly.FromDateTime(runTime);
        DateOnly afterLastMatch = DateOnly.FromDateTime(lastKickoff).AddDays(DaysFromLastMatchToStartDate);

        return afterLastMatch > runDate ? afterLastMatch : runDate;
    }
}
