using Softela.SoccerManager.Generator.Application.Models;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// The nations that enter the World Cup qualifiers, and the nations with enough players that are left out and why.
/// </summary>
/// <param name="Entrants">
/// The nations that enter, ordered by confederation (in the order of <see cref="Confederation"/>), then by strength,
/// strongest first, then by national team id.
/// </param>
/// <param name="WithoutConfederation">
/// The nations with enough players that have no confederation, ordered by national team id.
/// </param>
/// <param name="NotOnRoster">
/// The nations with enough players and a confederation that are not on the league's roster, ordered by national team
/// id.
/// </param>
internal sealed record QualifierEntrants(
    IReadOnlyList<QualifierNation> Entrants,
    IReadOnlyList<GetNationalTeamConfederationsResult> WithoutConfederation,
    IReadOnlyList<GetNationalTeamConfederationsResult> NotOnRoster);
