using Softela.SoccerManager.Generator.Application.Services.Calendar;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.DomesticCup;

/// <summary>
/// What a season's written matches say about its domestic cup. The status decides which members are set:
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="DomesticCupReadingStatus.WaitingForLeague"/>: no entrants, no calendar, no bracket, no progress and no
/// new ties.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="DomesticCupReadingStatus.NotStarted"/>: the entrants and the calendar, and nothing else.
/// </description>
/// </item>
/// <item><description><see cref="DomesticCupReadingStatus.Started"/>: every member is set.</description></item>
/// </list>
/// </summary>
/// <param name="Status">How far the cup has got.</param>
/// <param name="SeasonId">The id of the season.</param>
/// <param name="CupCompetitionId">The id of the cup's competition.</param>
/// <param name="Entrants">The clubs of the league with their divisions, in ascending league team id.</param>
/// <param name="Calendar">The calendar the league was written on, rebuilt from its matches.</param>
/// <param name="Bracket">The draw, with the bye clubs read back from the written round 1.</param>
/// <param name="Progress">The winners, the written ties and the champion.</param>
/// <param name="NewTies">
/// The ties whose two clubs are known and that have no match yet, in round order and then tie order.
/// </param>
/// <param name="SeasonMatches">All the matches of the season, as they were given.</param>
public sealed record DomesticCupReading(
    DomesticCupReadingStatus Status,
    int SeasonId,
    int CupCompetitionId,
    IReadOnlyList<DomesticCupEntrant> Entrants,
    SeasonCalendar? Calendar,
    DomesticCupBracket? Bracket,
    DomesticCupProgress? Progress,
    IReadOnlyList<DomesticCupKnownTie> NewTies,
    IReadOnlyList<SeasonMatch> SeasonMatches);
