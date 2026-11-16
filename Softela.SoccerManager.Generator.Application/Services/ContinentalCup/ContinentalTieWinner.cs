using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// Works out who wins a continental cup tie from its finished matches. A two-legged tie goes to the club with more
/// goals over both legs; a tie that is level is decided by the shootout of its second leg. The away-goals rule does not
/// exist here.
/// </summary>
public static class ContinentalTieWinner
{
    /// <summary>
    /// Finds the winner of a two-legged tie. Each club's goals are counted over both legs, where the sides swap between
    /// the legs. Only the second leg's shootout counts, because the first leg of a tie is never decided by one.
    /// </summary>
    /// <param name="firstLeg">The first leg, finished, with both clubs known.</param>
    /// <param name="secondLeg">The second leg, finished, played by the same two clubs with the sides swapped.</param>
    /// <param name="tieName">The tie as a refusal names it, such as "quarter-final tie 2".</param>
    /// <returns>The league team id of the club that won the tie.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="firstLeg"/>, <paramref name="secondLeg"/> or <paramref name="tieName"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a leg is not finished, a leg lacks a club, or the legs are not played by the same two clubs with the
    /// sides swapped. The caller checks all of this first, so it is a bug in the caller.
    /// </exception>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the tie is level on aggregate and no shootout in its second leg decided it.
    /// </exception>
    public static int OfTwoLegs(SeasonMatch firstLeg, SeasonMatch secondLeg, string tieName)
    {
        ArgumentNullException.ThrowIfNull(firstLeg);
        ArgumentNullException.ThrowIfNull(secondLeg);
        ArgumentNullException.ThrowIfNull(tieName);

        if (!firstLeg.IsFinished
            || !secondLeg.IsFinished
            || firstLeg.HomeLeagueTeamId is not { } clubA
            || firstLeg.AwayLeagueTeamId is not { } clubB
            || secondLeg.HomeLeagueTeamId != clubB
            || secondLeg.AwayLeagueTeamId != clubA)
        {
            throw new ArgumentException("Both legs must be finished and played by the same two clubs, sides swapped.");
        }

        int goalsOfA = firstLeg.HomeGoals + secondLeg.AwayGoals;
        int goalsOfB = firstLeg.AwayGoals + secondLeg.HomeGoals;

        if (goalsOfA != goalsOfB)
        {
            return goalsOfA > goalsOfB ? clubA : clubB;
        }

        if (secondLeg.HomeShootoutGoals is { } homeShootout
            && secondLeg.AwayShootoutGoals is { } awayShootout
            && homeShootout != awayShootout)
        {
            return homeShootout > awayShootout ? clubB : clubA;
        }

        throw new SeasonRefusedException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"The {tieName} of club {clubA} and club {clubB} (matches {firstLeg.Id} and {secondLeg.Id}) is ")
            + "finished level on aggregate and no shootout decided its second leg. Fix its result.");
    }

    /// <summary>
    /// Finds the winner of the final.
    /// </summary>
    /// <param name="final">The final, finished, with both clubs known.</param>
    /// <returns>The league team id of the club that won the final.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="final"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the final is not finished or lacks a club. The caller checks this first, so it is a bug in the
    /// caller.
    /// </exception>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the goals are level and no shootout decided the final.
    /// </exception>
    public static int OfFinal(SeasonMatch final)
    {
        ArgumentNullException.ThrowIfNull(final);

        if (!final.IsFinished || final.HomeLeagueTeamId is null || final.AwayLeagueTeamId is null)
        {
            throw new ArgumentException("The final must be finished and have both clubs.", nameof(final));
        }

        return final.WinnerLeagueTeamId ?? throw new SeasonRefusedException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"The final (match {final.Id}) is finished but has no winner: its goals are level and no shootout ")
            + "decided it. Fix its result.");
    }
}
