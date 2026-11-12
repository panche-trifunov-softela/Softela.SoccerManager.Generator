using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Core.Randomness;
using Softela.SoccerManager.Generator.Application.Services.Calendar;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.DomesticCup;

/// <summary>
/// Reads a season's domestic cup back from its written matches: who enters, what the calendar is, what the draw is and
/// which ties are decided. Nothing is stored about the cup apart from its matches, so every tick reads it again.
/// </summary>
public static class DomesticCupReader
{
    /// <summary>
    /// The stage of a domestic cup match: the backend's <c>MatchStage.DomesticCupRound</c> (V1_0_0_57).
    /// </summary>
    internal const byte DomesticCupRoundStage = 1;

    /// <summary>
    /// The purpose that the draw's random source is derived for.
    /// </summary>
    private const string DrawPurpose = "domesticCup";

    /// <summary>
    /// Reads the cup of a season. Nothing depends on the order of the matches given. The result promises:
    /// <list type="bullet">
    /// <item>
    /// <description>With no league match yet, the status is waiting for the league and nothing is set.</description>
    /// </item>
    /// <item>
    /// <description>
    /// With a league and no cup match, the cup is not started; it has entrants and a calendar.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// With a cup match, the bye clubs are the entrants without a round 1 match, so the draw is the same on every tick
    /// whatever the ratings do, and every written match has to be a tie of that draw as its earlier results stand.
    /// </description>
    /// </item>
    /// </list>
    /// </summary>
    /// <param name="seasonId">The id of the season.</param>
    /// <param name="cupCompetitionId">The id of the cup's competition.</param>
    /// <param name="divisionIdsInOrder">The competition ids of the league's divisions, top division first.</param>
    /// <param name="seasonMatches">All the matches of the season, in any order.</param>
    /// <returns>What the season's matches say about the cup.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="divisionIdsInOrder"/> or <paramref name="seasonMatches"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a division id is listed twice or is the cup's own id. That is a bug in the caller, not a refusal.
    /// </exception>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the written matches cannot be read as a league and a cup: a league match lacks a club, a club is in
    /// two divisions or a division has no clubs, the league is not on its calendar, a cup match is not a tie of the
    /// draw or has two matches, or a finished cup match has no winner.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the draw does not have the calendar's number of rounds, which is a bug.
    /// </exception>
    public static DomesticCupReading Read(
        int seasonId,
        int cupCompetitionId,
        IReadOnlyList<int> divisionIdsInOrder,
        IReadOnlyList<SeasonMatch> seasonMatches)
    {
        ArgumentNullException.ThrowIfNull(divisionIdsInOrder);
        ArgumentNullException.ThrowIfNull(seasonMatches);

        CheckDivisionIds(cupCompetitionId, divisionIdsInOrder);

        HashSet<int> divisionSet = new(divisionIdsInOrder);

        List<SeasonMatch> leagueMatches = seasonMatches
            .Where(match => match.Stage is null && match.CompetitionId is { } id && divisionSet.Contains(id))
            .OrderBy(match => match.Kickoff)
            .ThenBy(match => match.Id)
            .ToList();

        if (leagueMatches.Count == 0)
        {
            return new DomesticCupReading(
                DomesticCupReadingStatus.WaitingForLeague,
                seasonId,
                cupCompetitionId,
                [],
                null,
                null,
                null,
                [],
                seasonMatches);
        }

        Dictionary<int, int> divisionOfClub = DomesticCupLeagueReader.MapClubsToDivisions(leagueMatches);
        List<int> clubCounts = DomesticCupLeagueReader.CountClubsPerDivision(divisionIdsInOrder, divisionOfClub);
        SeasonCalendar calendar = DomesticCupLeagueReader.RebuildCalendar(clubCounts, leagueMatches);

        Dictionary<int, int> rankOfDivision = new();

        for (int index = 0; index < divisionIdsInOrder.Count; index++)
        {
            rankOfDivision.Add(divisionIdsInOrder[index], index + 1);
        }

        List<DomesticCupEntrant> entrants = divisionOfClub
            .OrderBy(pair => pair.Key)
            .Select(pair => new DomesticCupEntrant(pair.Key, rankOfDivision[pair.Value]))
            .ToList();

        List<SeasonMatch> cupMatches = seasonMatches
            .Where(match => match.CompetitionId == cupCompetitionId)
            .OrderBy(match => match.Kickoff)
            .ThenBy(match => match.Id)
            .ToList();

        CheckCupMatches(cupMatches, calendar.DomesticCupRounds.Count);

        if (cupMatches.Count == 0)
        {
            return new DomesticCupReading(
                DomesticCupReadingStatus.NotStarted,
                seasonId,
                cupCompetitionId,
                entrants,
                calendar,
                null,
                null,
                [],
                seasonMatches);
        }

        return ReadStarted(seasonId, cupCompetitionId, entrants, calendar, cupMatches, seasonMatches);
    }

    /// <summary>
    /// Creates the random source of a season's draw. Every call gives a fresh source, so the draw depends only on the
    /// season, the entrants and the bye clubs, however often it is rebuilt.
    /// </summary>
    /// <param name="seasonId">The id of the season.</param>
    /// <returns>A new random source for the draw.</returns>
    internal static SeededRandom CreateDrawRandom(int seasonId)
    {
        return new SeededRandom(seasonId).Derive(DrawPurpose);
    }

    /// <summary>
    /// Gives the reason an argument exception states, without the " (Parameter '...')" suffix that .NET appends.
    /// </summary>
    /// <param name="exception">The exception to read.</param>
    /// <returns>The message of the exception, without the parameter suffix.</returns>
    internal static string ReasonOf(ArgumentException exception)
    {
        return exception.ParamName is null
            ? exception.Message
            : exception.Message.Replace($" (Parameter '{exception.ParamName}')", string.Empty);
    }

    /// <summary>
    /// Builds the refusal for the draw library refusing the league's clubs.
    /// </summary>
    /// <param name="exception">The exception the library threw.</param>
    /// <returns>The refusal, ready to throw.</returns>
    internal static SeasonRefusedException DrawRefused(ArgumentException exception)
    {
        return new SeasonRefusedException($"The cup draw refused the league's clubs: {ReasonOf(exception)}");
    }

    /// <summary>
    /// Lists the ties whose two clubs are known, turning a refusal by the draw library into a season refusal.
    /// </summary>
    /// <param name="bracket">The draw.</param>
    /// <param name="winners">The ties decided so far.</param>
    /// <returns>The ties with both clubs known, in round order and then tie order.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when the draw library refuses the winners.</exception>
    internal static IReadOnlyList<DomesticCupKnownTie> KnownTiesOf(
        DomesticCupBracket bracket,
        IReadOnlyList<DomesticCupWinner> winners)
    {
        try
        {
            return DomesticCupTieResolver.KnownTies(bracket, winners);
        }
        catch (ArgumentException exception) when (exception is not ArgumentNullException
            and not ArgumentOutOfRangeException)
        {
            throw DrawRefused(exception);
        }
    }

    /// <summary>
    /// Checks the division ids the caller gave.
    /// </summary>
    /// <param name="cupCompetitionId">The id of the cup's competition.</param>
    /// <param name="divisionIdsInOrder">The competition ids of the league's divisions.</param>
    /// <exception cref="ArgumentException">Thrown when a division id repeats or is the cup's id.</exception>
    private static void CheckDivisionIds(int cupCompetitionId, IReadOnlyList<int> divisionIdsInOrder)
    {
        HashSet<int> seen = new();

        foreach (int divisionId in divisionIdsInOrder)
        {
            if (divisionId == cupCompetitionId || !seen.Add(divisionId))
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Competition {divisionId} is listed twice or is the cup itself."),
                    nameof(divisionIdsInOrder));
            }
        }
    }

    /// <summary>
    /// Checks that every cup match is one the generator can read.
    /// </summary>
    /// <param name="cupMatches">The cup's matches, in kickoff order.</param>
    /// <param name="roundCount">The number of rounds the calendar has for the cup.</param>
    /// <exception cref="SeasonRefusedException">Thrown when a cup match is not a readable tie.</exception>
    private static void CheckCupMatches(List<SeasonMatch> cupMatches, int roundCount)
    {
        foreach (SeasonMatch match in cupMatches)
        {
            if (match.Stage == DomesticCupRoundStage
                && match.RoundNumber >= 1
                && match.RoundNumber <= roundCount
                && match.Leg is null
                && match.HomeLeagueTeamId is not null
                && match.AwayLeagueTeamId is not null)
            {
                continue;
            }

            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cup match {match.Id} is not a domestic cup tie the generator can read: it needs stage ")
                + string.Create(
                    CultureInfo.InvariantCulture,
                    $"{DomesticCupRoundStage}, a round from 1 to {roundCount}, no leg, and a home and an away club. ")
                + "Remove it with the cup-only reset.");
        }
    }

    /// <summary>
    /// Reads a cup that has matches: the draw, the decided ties and the ties still to write.
    /// </summary>
    /// <param name="seasonId">The id of the season.</param>
    /// <param name="cupCompetitionId">The id of the cup's competition.</param>
    /// <param name="entrants">The clubs of the league.</param>
    /// <param name="calendar">The rebuilt calendar.</param>
    /// <param name="cupMatches">The cup's matches, in kickoff order; they have been checked.</param>
    /// <param name="seasonMatches">All the matches of the season.</param>
    /// <returns>The started reading.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when the cup's matches do not fit the draw.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the draw does not have the calendar's number of rounds, which is a bug.
    /// </exception>
    private static DomesticCupReading ReadStarted(
        int seasonId,
        int cupCompetitionId,
        List<DomesticCupEntrant> entrants,
        SeasonCalendar calendar,
        List<SeasonMatch> cupMatches,
        IReadOnlyList<SeasonMatch> seasonMatches)
    {
        int roundCount = calendar.DomesticCupRounds.Count;

        HashSet<int> firstRoundClubs = cupMatches
            .Where(match => match.RoundNumber == 1)
            .SelectMany(match => new[] { match.HomeLeagueTeamId!.Value, match.AwayLeagueTeamId!.Value })
            .ToHashSet();

        List<int> entrantIds = entrants.Select(entrant => entrant.LeagueTeamId).ToList();
        List<int> byes = entrantIds.Where(clubId => !firstRoundClubs.Contains(clubId)).ToList();
        SeededRandom random = CreateDrawRandom(seasonId);
        DomesticCupBracket bracket;

        try
        {
            bracket = DomesticCupBracketBuilder.Build(entrantIds, byes, random);
        }
        catch (ArgumentException exception) when (exception is not ArgumentNullException
            and not ArgumentOutOfRangeException)
        {
            throw new SeasonRefusedException(
                $"The cup's written round 1 does not fit the league's clubs: {ReasonOf(exception)} "
                + "Remove the cup's matches with the cup-only reset so the cup is drawn again.");
        }

        if (bracket.Rounds.Count != roundCount)
        {
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The draw has {bracket.Rounds.Count} rounds but the calendar has {roundCount}."));
        }

        DomesticCupProgress progress = DomesticCupProgressReader.Read(bracket, cupMatches, roundCount);

        HashSet<(int Round, int Tie)> written = progress.WrittenTies
            .Select(tie => (tie.RoundNumber, tie.TieNumber))
            .ToHashSet();

        List<DomesticCupKnownTie> newTies = KnownTiesOf(bracket, progress.Winners)
            .Where(tie => !written.Contains((tie.RoundNumber, tie.TieNumber)))
            .ToList();

        return new DomesticCupReading(
            DomesticCupReadingStatus.Started,
            seasonId,
            cupCompetitionId,
            entrants,
            calendar,
            bracket,
            progress,
            newTies,
            seasonMatches);
    }
}
