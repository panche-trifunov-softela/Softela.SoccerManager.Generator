namespace Softela.SoccerManager.Generator.Application.Services.DomesticCup;

/// <summary>
/// Works out how many rounds, byes and first-round ties a cup for a given number of clubs has. Callers have already
/// checked that there are at least 2 clubs.
/// </summary>
internal static class DomesticCupShape
{
    /// <summary>
    /// Gets the number of rounds: the smallest K, at least 1, for which 2 to the power K is not below the club count.
    /// </summary>
    /// <param name="clubCount">The number of clubs; it must be at least 2.</param>
    /// <returns>The number of rounds, including the final.</returns>
    public static int RoundCount(int clubCount)
    {
        int rounds = 1;

        // A long shift, so no club count can overflow it.
        while ((1L << rounds) < clubCount)
        {
            rounds++;
        }

        return rounds;
    }

    /// <summary>
    /// Gets the number of clubs that skip round 1: the places a full bracket has beyond the club count.
    /// </summary>
    /// <param name="clubCount">The number of clubs; it must be at least 2.</param>
    /// <returns>The number of byes; 0 when the club count is a power of two.</returns>
    public static int ByeCount(int clubCount)
    {
        long places = 1L << RoundCount(clubCount);

        return (int)(places - clubCount);
    }

    /// <summary>
    /// Gets the number of ties in round 1: the clubs beyond the half bracket, which must play to get into it.
    /// </summary>
    /// <param name="clubCount">The number of clubs; it must be at least 2.</param>
    /// <returns>The number of round 1 ties; it is always at least 1.</returns>
    public static int FirstRoundTieCount(int clubCount)
    {
        long halfPlaces = 1L << (RoundCount(clubCount) - 1);

        return (int)(clubCount - halfPlaces);
    }
}
