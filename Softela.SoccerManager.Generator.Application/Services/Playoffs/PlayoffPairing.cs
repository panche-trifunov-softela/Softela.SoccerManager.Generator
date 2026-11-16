using System.Globalization;

namespace Softela.SoccerManager.Generator.Application.Services.Playoffs;

/// <summary>
/// Pairs the clubs of a division's promotion playoff from its ranked table. The first playoff place is place P, the
/// number of clubs that go up. With four clubs the semi-finals are place P against place P + 3 and place P + 1 against
/// place P + 2, and the final is between their winners; with two clubs there is only a final, place P against place
/// P + 1. The club placed higher hosts every match.
/// </summary>
public static class PlayoffPairing
{
    /// <summary>
    /// The number of places a four-club playoff spans.
    /// </summary>
    private const int FourClubPlayoffSpan = 4;

    /// <summary>
    /// Pairs the first round of the playoff: two semi-finals for four clubs, the final for two. The higher club hosts,
    /// except that the second semi-final's two clubs swap when its higher club shares a stadium with the first
    /// semi-final's host, so that one stadium does not host both matches. Only that one swap is made; a missing stadium
    /// counts as no stadium.
    /// </summary>
    /// <param name="places">What the division's settings say about the places of its table.</param>
    /// <param name="rankedLeagueTeamIds">The division's league team ids in table order, place 1 first.</param>
    /// <param name="stadiumOfClub">The stadium id of each club by league team id; null for a club with none.</param>
    /// <returns>The first round's ties in tie order.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="places"/>, <paramref name="rankedLeagueTeamIds"/> or
    /// <paramref name="stadiumOfClub"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the playoff does not have 2 or 4 clubs, or the ranked list is shorter than the places the playoff
    /// spans, which is a bug in the caller.
    /// </exception>
    public static IReadOnlyList<PlayoffTie> FirstRound(
        PromotionPlaces places,
        IReadOnlyList<int> rankedLeagueTeamIds,
        IReadOnlyDictionary<int, int?> stadiumOfClub)
    {
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(rankedLeagueTeamIds);
        ArgumentNullException.ThrowIfNull(stadiumOfClub);

        CheckPlaces(places, rankedLeagueTeamIds);

        int first = places.FirstPlayoffPlace;

        if (places.PlayoffClubs == 2)
        {
            return [TieOf(PlayoffStages.Final, 1, rankedLeagueTeamIds, first, first + 1)];
        }

        int secondHigh = first + 1;
        int secondLow = first + 2;

        if (SharesStadium(stadiumOfClub, rankedLeagueTeamIds[first - 1], rankedLeagueTeamIds[secondHigh - 1]))
        {
            (secondHigh, secondLow) = (secondLow, secondHigh);
        }

        return
        [
            TieOf(PlayoffStages.SemiFinal, 1, rankedLeagueTeamIds, first, first + FourClubPlayoffSpan - 1),
            TieOf(PlayoffStages.SemiFinal, 2, rankedLeagueTeamIds, secondHigh, secondLow),
        ];
    }

    /// <summary>
    /// Pairs the final of a four-club playoff from the winners of its two semi-finals. The winner placed higher in the
    /// table hosts.
    /// </summary>
    /// <param name="places">What the division's settings say about the places of its table.</param>
    /// <param name="rankedLeagueTeamIds">The division's league team ids in table order, place 1 first.</param>
    /// <param name="firstWinner">The league team id of one semi-final's winner.</param>
    /// <param name="secondWinner">The league team id of the other semi-final's winner.</param>
    /// <returns>The final: stage <see cref="PlayoffStages.Final"/>, tie 1.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="places"/> or <paramref name="rankedLeagueTeamIds"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the playoff does not have 4 clubs, the ranked list is too short, or a winner is not among the
    /// playoff's places or is given twice, which is a bug in the caller.
    /// </exception>
    public static PlayoffTie Final(
        PromotionPlaces places,
        IReadOnlyList<int> rankedLeagueTeamIds,
        int firstWinner,
        int secondWinner)
    {
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(rankedLeagueTeamIds);

        if (places.PlayoffClubs != FourClubPlayoffSpan)
        {
            throw new ArgumentException("Only a four-club playoff has a final after semi-finals.", nameof(places));
        }

        CheckPlaces(places, rankedLeagueTeamIds);

        if (firstWinner == secondWinner)
        {
            throw new ArgumentException("The two winners are the same club.", nameof(secondWinner));
        }

        int firstPlace = PlaceOf(places, rankedLeagueTeamIds, firstWinner, nameof(firstWinner));
        int secondPlace = PlaceOf(places, rankedLeagueTeamIds, secondWinner, nameof(secondWinner));
        int higher = Math.Min(firstPlace, secondPlace);
        int lower = Math.Max(firstPlace, secondPlace);

        return TieOf(PlayoffStages.Final, 1, rankedLeagueTeamIds, higher, lower);
    }

    /// <summary>
    /// Checks that the playoff has a size it can pair and that the ranked list reaches its last place.
    /// </summary>
    /// <param name="places">What the division's settings say about the places of its table.</param>
    /// <param name="rankedLeagueTeamIds">The division's league team ids in table order.</param>
    /// <exception cref="ArgumentException">Thrown when the playoff size or the list is wrong.</exception>
    private static void CheckPlaces(PromotionPlaces places, IReadOnlyList<int> rankedLeagueTeamIds)
    {
        if (places.PlayoffClubs is not (2 or 4))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"A playoff has 2 or 4 clubs, not {places.PlayoffClubs}."),
                nameof(places));
        }

        int lastPlace = checked(places.FirstPlayoffPlace + places.PlayoffClubs - 1);

        if (places.FirstPlayoffPlace < 1 || rankedLeagueTeamIds.Count < lastPlace)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The playoff spans places {places.FirstPlayoffPlace} to {lastPlace}, but the table has ")
                + string.Create(CultureInfo.InvariantCulture, $"{rankedLeagueTeamIds.Count} clubs."),
                nameof(rankedLeagueTeamIds));
        }
    }

    /// <summary>
    /// Finds the table place of a club among the places of a four-club playoff.
    /// </summary>
    /// <param name="places">What the division's settings say about the places of its table.</param>
    /// <param name="rankedLeagueTeamIds">The division's league team ids in table order.</param>
    /// <param name="leagueTeamId">The club to find.</param>
    /// <param name="parameterName">The name of the argument that gave the club, for the exception.</param>
    /// <returns>The 1-based place of the club.</returns>
    /// <exception cref="ArgumentException">Thrown when the club is not in one of the playoff's places.</exception>
    private static int PlaceOf(
        PromotionPlaces places,
        IReadOnlyList<int> rankedLeagueTeamIds,
        int leagueTeamId,
        string parameterName)
    {
        int first = places.FirstPlayoffPlace;

        for (int place = first; place < first + FourClubPlayoffSpan; place++)
        {
            if (rankedLeagueTeamIds[place - 1] == leagueTeamId)
            {
                return place;
            }
        }

        throw new ArgumentException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Club {leagueTeamId} is not among places {first} to {first + FourClubPlayoffSpan - 1}."),
            parameterName);
    }

    /// <summary>
    /// Tells whether two clubs have the same stadium.
    /// </summary>
    /// <param name="stadiumOfClub">The stadium id of each club by league team id; null for a club with none.</param>
    /// <param name="firstLeagueTeamId">The league team id of the first club.</param>
    /// <param name="secondLeagueTeamId">The league team id of the second club.</param>
    /// <returns>True when both clubs have a stadium and it is the same one.</returns>
    private static bool SharesStadium(
        IReadOnlyDictionary<int, int?> stadiumOfClub,
        int firstLeagueTeamId,
        int secondLeagueTeamId)
    {
        return stadiumOfClub.TryGetValue(firstLeagueTeamId, out int? first)
            && stadiumOfClub.TryGetValue(secondLeagueTeamId, out int? second)
            && first is not null
            && first == second;
    }

    /// <summary>
    /// Builds a tie between the clubs at two table places, the first of them at home.
    /// </summary>
    /// <param name="stage">The match stage of the tie.</param>
    /// <param name="tieNumber">The 1-based number of the tie within its stage.</param>
    /// <param name="rankedLeagueTeamIds">The division's league team ids in table order.</param>
    /// <param name="homePlace">The 1-based place of the home club.</param>
    /// <param name="awayPlace">The 1-based place of the away club.</param>
    /// <returns>The tie.</returns>
    private static PlayoffTie TieOf(
        byte stage,
        int tieNumber,
        IReadOnlyList<int> rankedLeagueTeamIds,
        int homePlace,
        int awayPlace)
    {
        return new PlayoffTie(
            stage,
            tieNumber,
            rankedLeagueTeamIds[homePlace - 1],
            rankedLeagueTeamIds[awayPlace - 1],
            homePlace,
            awayPlace);
    }
}
