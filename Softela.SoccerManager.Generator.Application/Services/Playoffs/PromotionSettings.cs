using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Models;

namespace Softela.SoccerManager.Generator.Application.Services.Playoffs;

/// <summary>
/// Checks the promotion and relegation settings of a league's divisions and turns them into the places of each
/// division's table. The settings must agree with each other and with the number of clubs, or no division's playoff can
/// be trusted.
/// </summary>
public static class PromotionSettings
{
    /// <summary>
    /// The fix every refusal of the settings ends with.
    /// </summary>
    private const string FixSettings = "Fix the division's settings.";

    /// <summary>
    /// Checks the settings of every division, top division first, and reads the places they give. A division is
    /// refused when:
    /// <list type="bullet">
    /// <item><description>its promoted or relegated count is negative;</description></item>
    /// <item><description>its playoff size is not 0, 2 or 4;</description></item>
    /// <item><description>it has a playoff but fewer than 2 promoted;</description></item>
    /// <item><description>it is the top division and has clubs promoted or in a playoff;</description></item>
    /// <item><description>it is the bottom division and has clubs relegated;</description></item>
    /// <item>
    /// <description>
    /// it is not the bottom division and relegates another number than the next division promotes;
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// its places do not fit its clubs: without a playoff the promoted and the relegated, with one the automatic
    /// places, the playoff clubs and the relegated.
    /// </description>
    /// </item>
    /// </list>
    /// A league of one division is both the top and the bottom, so it needs all three settings at 0.
    /// </summary>
    /// <param name="divisionsInOrder">The league's divisions, the top division first.</param>
    /// <param name="clubCounts">The number of clubs of each division, in the same order.</param>
    /// <returns>The places of each division, in the same order.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="divisionsInOrder"/> or <paramref name="clubCounts"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when the two lists differ in length.</exception>
    /// <exception cref="SeasonRefusedException">Thrown when a division's settings are not allowed.</exception>
    public static IReadOnlyList<PromotionPlaces> Check(
        IReadOnlyList<GetCompetitionsResult> divisionsInOrder,
        IReadOnlyList<int> clubCounts)
    {
        ArgumentNullException.ThrowIfNull(divisionsInOrder);
        ArgumentNullException.ThrowIfNull(clubCounts);

        if (divisionsInOrder.Count != clubCounts.Count)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"There are {divisionsInOrder.Count} divisions but {clubCounts.Count} club counts."),
                nameof(clubCounts));
        }

        List<PromotionPlaces> places = [];

        for (int index = 0; index < divisionsInOrder.Count; index++)
        {
            GetCompetitionsResult division = divisionsInOrder[index];
            GetCompetitionsResult? next = index + 1 < divisionsInOrder.Count ? divisionsInOrder[index + 1] : null;

            CheckDivision(division, next, index == 0, clubCounts[index]);

            places.Add(new PromotionPlaces(
                division.Id,
                clubCounts[index],
                division.TeamsPromoted,
                division.TeamsInPlayoffs,
                division.TeamsRelegated));
        }

        return places;
    }

    /// <summary>
    /// Checks the settings of one division against the rules, in order, and refuses at the first one it breaks.
    /// </summary>
    /// <param name="division">The division.</param>
    /// <param name="next">The division below it, or null when it is the bottom division.</param>
    /// <param name="isTop">Whether it is the top division.</param>
    /// <param name="clubCount">The number of clubs in the division.</param>
    /// <exception cref="SeasonRefusedException">Thrown when the division's settings are not allowed.</exception>
    private static void CheckDivision(
        GetCompetitionsResult division,
        GetCompetitionsResult? next,
        bool isTop,
        int clubCount)
    {
        int promoted = division.TeamsPromoted;
        int playoff = division.TeamsInPlayoffs;
        int relegated = division.TeamsRelegated;

        if (promoted < 0 || relegated < 0)
        {
            throw Refuse(
                division,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"has TeamsPromoted {promoted} and TeamsRelegated {relegated}, and neither can be negative."));
        }

        if (playoff is not (0 or 2 or 4))
        {
            throw Refuse(
                division,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"has TeamsInPlayoffs {playoff}, but a promotion playoff has 0, 2 or 4 clubs."));
        }

        if (playoff > 0 && promoted < 2)
        {
            throw Refuse(
                division,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"has TeamsInPlayoffs {playoff} and TeamsPromoted {promoted}, but a playoff needs at least one ")
                + "automatic place, so TeamsPromoted must be 2 or more.");
        }

        if (isTop && (promoted > 0 || playoff > 0))
        {
            throw Refuse(
                division,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"is the top division, so no club can go up from it, but it has TeamsPromoted {promoted} and ")
                + string.Create(CultureInfo.InvariantCulture, $"TeamsInPlayoffs {playoff}."));
        }

        if (next is null && relegated > 0)
        {
            throw Refuse(
                division,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"is the bottom division, so no club can go down from it, but it has TeamsRelegated {relegated}."));
        }

        if (next is not null && relegated != next.TeamsPromoted)
        {
            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Division '{division.Name}' (id {division.Id}) sends down {relegated} clubs, but the division ")
                + string.Create(
                    CultureInfo.InvariantCulture,
                    $"below it, '{next.Name}' (id {next.Id}), sends up {next.TeamsPromoted}. The two must be equal. ")
                + FixSettings);
        }

        CheckPlacesFit(division, clubCount);
    }

    /// <summary>
    /// Checks that a division's places fit its clubs.
    /// </summary>
    /// <param name="division">The division.</param>
    /// <param name="clubCount">The number of clubs in the division.</param>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the places need more clubs than the division has.
    /// </exception>
    private static void CheckPlacesFit(GetCompetitionsResult division, int clubCount)
    {
        int playoff = division.TeamsInPlayoffs;
        int needed = playoff > 0
            ? checked(division.TeamsPromoted - 1 + playoff + division.TeamsRelegated)
            : checked(division.TeamsPromoted + division.TeamsRelegated);

        if (needed <= clubCount)
        {
            return;
        }

        string places = playoff > 0
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{division.TeamsPromoted - 1} automatic, {playoff} in the playoff and {division.TeamsRelegated} down")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{division.TeamsPromoted} promoted and {division.TeamsRelegated} relegated");

        throw Refuse(
            division,
            string.Create(
                CultureInfo.InvariantCulture,
                $"has {clubCount} clubs, but its settings need {needed} of them ({places}), which is too many."));
    }

    /// <summary>
    /// Builds the refusal of a division's settings.
    /// </summary>
    /// <param name="division">The division.</param>
    /// <param name="problem">What is wrong, as the rest of a sentence that begins with the division.</param>
    /// <returns>The refusal, ready to throw, which ends with the fix.</returns>
    private static SeasonRefusedException Refuse(GetCompetitionsResult division, string problem)
    {
        return new SeasonRefusedException(
            string.Create(CultureInfo.InvariantCulture, $"Division '{division.Name}' (id {division.Id}) ")
            + $"{problem} {FixSettings}");
    }
}
