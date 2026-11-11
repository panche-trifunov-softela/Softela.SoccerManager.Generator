using System.Globalization;

namespace Softela.SoccerManager.Generator.Application.Services.DomesticCup;

/// <summary>
/// Refuses cup input that cannot make a bracket.
/// </summary>
internal static class DomesticCupInputValidator
{
    private const int MinClubCount = 2;

    /// <summary>
    /// Checks the clubs that enter the cup.
    /// </summary>
    /// <param name="entrants">The league team ids of the clubs that enter the cup.</param>
    /// <param name="paramName">The name of the argument the entrants came in as, for the exception.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entrants"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when there are fewer than 2 entrants, or a club is listed more than once.
    /// </exception>
    public static void ValidateEntrants(IReadOnlyList<int> entrants, string paramName)
    {
        ArgumentNullException.ThrowIfNull(entrants, paramName);

        if (entrants.Count < MinClubCount)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"A domestic cup needs at least {MinClubCount} clubs; {entrants.Count} were given."),
                paramName);
        }

        HashSet<int> seen = new();

        foreach (int clubId in entrants)
        {
            if (!seen.Add(clubId))
            {
                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"Club {clubId} is listed more than once."),
                    paramName);
            }
        }
    }

    /// <summary>
    /// Checks the clubs that skip round 1 against the entrants.
    /// </summary>
    /// <param name="entrants">The league team ids of the clubs that enter the cup; they are already valid.</param>
    /// <param name="byeClubs">The league team ids of the clubs that skip round 1.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="byeClubs"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a bye club is listed more than once, is not an entrant, or the number of bye clubs is not the
    /// number the bracket needs.
    /// </exception>
    public static void ValidateByes(IReadOnlyList<int> entrants, IReadOnlyList<int> byeClubs)
    {
        ArgumentNullException.ThrowIfNull(byeClubs);

        HashSet<int> seen = new();

        foreach (int clubId in byeClubs)
        {
            if (!seen.Add(clubId))
            {
                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"Club {clubId} is given a bye more than once."),
                    nameof(byeClubs));
            }
        }

        HashSet<int> entrantSet = new(entrants);

        foreach (int clubId in byeClubs)
        {
            if (!entrantSet.Contains(clubId))
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Club {clubId} is given a bye but is not an entrant."),
                    nameof(byeClubs));
            }
        }

        int byeCount = DomesticCupShape.ByeCount(entrants.Count);

        if (byeClubs.Count != byeCount)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{entrants.Count} clubs need {byeCount} byes; {byeClubs.Count} were given."),
                nameof(byeClubs));
        }
    }
}
