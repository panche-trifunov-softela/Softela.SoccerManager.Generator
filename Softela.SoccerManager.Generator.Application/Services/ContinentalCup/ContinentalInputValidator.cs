using System.Globalization;

namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// Refuses continental cup input that cannot make a draw or a bracket.
/// </summary>
internal static class ContinentalInputValidator
{
    /// <summary>
    /// The number of clubs in a small continental cup, which has no play-off and no round of 16.
    /// </summary>
    internal const int SmallCupClubCount = 16;

    /// <summary>
    /// The number of clubs in a large continental cup.
    /// </summary>
    internal const int LargeCupClubCount = 32;

    /// <summary>
    /// Checks the clubs of a continental cup.
    /// </summary>
    /// <param name="leagueTeamIds">The league team ids of the clubs that enter the cup.</param>
    /// <param name="paramName">The name of the argument the clubs came in as, for the exception.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="leagueTeamIds"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the number of clubs is not 16 or 32, or a club is listed more than once. The first club found a
    /// second time, in list order, is the one named.
    /// </exception>
    internal static void ValidateClubs(IReadOnlyList<int> leagueTeamIds, string paramName)
    {
        ArgumentNullException.ThrowIfNull(leagueTeamIds, paramName);

        if (leagueTeamIds.Count != SmallCupClubCount && leagueTeamIds.Count != LargeCupClubCount)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"A continental cup needs {SmallCupClubCount} or {LargeCupClubCount} clubs; "
                    + $"{leagueTeamIds.Count} were given."),
                paramName);
        }

        HashSet<int> seen = new();

        foreach (int clubId in leagueTeamIds)
        {
            if (!seen.Add(clubId))
            {
                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"Club {clubId} is listed more than once."),
                    paramName);
            }
        }
    }
}
