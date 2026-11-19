using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Randomness;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// Draws the groups of the World Cup finals. The 32 nations are ranked by strength and put in 4 pots of 8, so every
/// group holds one nation of each pot. The draw is fixed by the season: the same nations and season give the same
/// groups. It reads nothing and writes nothing.
/// </summary>
internal static class FinalsGroupDraw
{
    /// <summary>
    /// The purpose the draw's random source is derived with.
    /// </summary>
    private const string RandomPurpose = "worldCupFinalsDraw";

    /// <summary>
    /// Draws the groups. The nations are ranked by strength, the strongest first, and by national team id when the
    /// strengths are equal. Pot k holds the nations at the ranked places from 8 times k, eight of them; each pot is
    /// shuffled and its i-th nation goes to group i.
    /// </summary>
    /// <param name="qualified">
    /// The 32 nations: the roster entry, the national team id and the strength of each.
    /// </param>
    /// <param name="seasonId">The id of the season, which seeds the draw.</param>
    /// <returns>
    /// The 8 groups, numbered 1 to 8 in the order they were dealt, each with its 4 nations in pot order, playing each
    /// pair once.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="qualified"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the nations are not exactly 32, or a nation is listed more than once.
    /// </exception>
    internal static IReadOnlyList<WorldCupGroup> Draw(
        IReadOnlyList<(int LeagueTeamId, int NationalTeamId, int Strength)> qualified,
        int seasonId)
    {
        ArgumentNullException.ThrowIfNull(qualified);

        if (qualified.Count != WorldCupFinalsShape.NationCount)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The finals have {WorldCupFinalsShape.NationCount} nations, but {qualified.Count} were given."),
                nameof(qualified));
        }

        List<int> ranked = qualified
            .OrderByDescending(nation => nation.Strength)
            .ThenBy(nation => nation.NationalTeamId)
            .Select(nation => nation.LeagueTeamId)
            .ToList();

        SeededRandom random = new SeededRandom(seasonId).Derive(RandomPurpose);

        IReadOnlyList<IReadOnlyList<int>> members = PotDraw.Deal(ranked, WorldCupFinalsShape.GroupCount, random);

        List<WorldCupGroup> groups = [];

        for (int group = 0; group < WorldCupFinalsShape.GroupCount; group++)
        {
            groups.Add(new WorldCupGroup(checked((byte)(group + 1)), members[group], WorldCupFinalsShape.Legs));
        }

        return groups;
    }
}
