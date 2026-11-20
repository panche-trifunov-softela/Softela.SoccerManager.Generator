using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.Playoffs;

/// <summary>
/// Reads one division's promotion playoff back from the division's matches and Standings: whether its league is
/// finished and its Standings settled, what the table gives the playoff, which playoff matches are written and decided,
/// and which ties are still to write. Nothing is stored about the playoff apart from its matches, so every tick reads
/// it again.
/// </summary>
public static class PlayoffReader
{
    /// <summary>
    /// The fix, as a sentence, that the refusal of a table's ranking ends with unless the caller gives another:
    /// resetting the season.
    /// </summary>
    private const string ResetSeason = "Reset the season (the readme's 'Writing a season again').";

    /// <summary>
    /// The fix that writes the playoff's matches again.
    /// </summary>
    private const string DeletePlayoff = "delete the division's playoff matches and they are written again";

    /// <summary>
    /// The number of semi-finals a four-club playoff has.
    /// </summary>
    private const int SemiFinalCount = 2;

    /// <summary>
    /// The number of clubs of a playoff that has semi-finals.
    /// </summary>
    private const int FourClubs = 4;

    /// <summary>
    /// Reads the playoff of one division. Nothing depends on the order of the matches or Standings rows given. The
    /// result promises:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// While a league match is unfinished, or the league has no matches, the status is waiting for the league; a
    /// playoff match that is already written then refuses instead.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// While the Standings changed less than an hour ago, the status is waiting for the Standings.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Otherwise the Standings are checked against the matches, the table is ranked, and the status is ready: every
    /// written playoff match has to be a tie of the table as its earlier results stand.
    /// </description>
    /// </item>
    /// </list>
    /// </summary>
    /// <param name="seasonId">The id of the season, which seeds the table's tie-break shuffle.</param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <param name="division">The division, named in a refusal.</param>
    /// <param name="places">What the division's settings say about the places of its table; it has a playoff.</param>
    /// <param name="leagueMatches">The division's league matches, the ones with no stage, in any order.</param>
    /// <param name="playoffMatches">The division's playoff matches, the ones with stage 2 or 3, in any order.</param>
    /// <param name="standings">The division's Standings rows.</param>
    /// <param name="stadiumOfClub">The stadium id of each club by league team id; null for a club with none.</param>
    /// <param name="otherFix">
    /// The fix that a refusal about the Standings' structure or the table's size offers besides fixing it, after the
    /// word "or". It defaults to resetting the season.
    /// </param>
    /// <param name="rankFix">
    /// The fix, as a sentence, that a refusal of the table's ranking ends with. It defaults to resetting the season.
    /// </param>
    /// <returns>What the division's matches and Standings say about its playoff.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="places"/> has no playoff, or a playoff match has another stage than 2 or 3. That is
    /// a bug in the caller, not a refusal.
    /// </exception>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the written matches cannot be read as a league and a playoff: a playoff match lacks a club, has a
    /// round number or a leg, or has a stage the playoff cannot have; a league match is unfinished while the playoff is
    /// written; the Standings disagree with the matches; the table is refused; a playoff match is not a tie of the
    /// table; a tie has two matches or no winner; or the final does not follow from the semi-finals.
    /// </exception>
    public static PlayoffReading Read(
        int seasonId,
        DateTime runTime,
        GetCompetitionsResult division,
        PromotionPlaces places,
        IReadOnlyList<SeasonMatch> leagueMatches,
        IReadOnlyList<SeasonMatch> playoffMatches,
        IReadOnlyList<GetStandingsResult> standings,
        IReadOnlyDictionary<int, int?> stadiumOfClub,
        string otherFix = PlayoffTableCheck.DefaultOtherFix,
        string rankFix = ResetSeason)
    {
        ArgumentNullException.ThrowIfNull(division);
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(leagueMatches);
        ArgumentNullException.ThrowIfNull(playoffMatches);
        ArgumentNullException.ThrowIfNull(standings);
        ArgumentNullException.ThrowIfNull(stadiumOfClub);
        ArgumentNullException.ThrowIfNull(otherFix);
        ArgumentNullException.ThrowIfNull(rankFix);

        if (places.PlayoffClubs is not (2 or 4))
        {
            throw new ArgumentException("The division has no playoff to read.", nameof(places));
        }

        List<SeasonMatch> playoff = playoffMatches.OrderBy(match => match.Id).ToList();

        CheckPlayoffMatches(division, places, playoff);

        int total = leagueMatches.Count;
        int finished = leagueMatches.Count(match => match.IsFinished);

        if (finished < total && playoff.Count > 0)
        {
            throw UnfinishedLeagueMatch(division, leagueMatches);
        }

        if (total == 0 || finished < total)
        {
            return Waiting(PlayoffReadingStatus.WaitingForLeague, finished, total, null, playoff);
        }

        DateTime? newestChange = standings.Count > 0 ? standings.Max(row => row.ModifiedAt) : null;

        if (newestChange is { } changedAt && !StandingsSettleRule.IsSettled(runTime, changedAt))
        {
            return Waiting(PlayoffReadingStatus.WaitingForStandings, finished, total, changedAt, playoff);
        }

        PlayoffTableCheck.Check(division, standings, leagueMatches, otherFix);

        List<int> ranked = SeasonTableRanker.RankDivision(
            seasonId,
            PlayoffTableCheck.DivisionOf(division),
            standings,
            leagueMatches,
            rankFix);

        CheckTableReachesPlayoff(division, places, ranked, otherFix);

        IReadOnlyList<PlayoffTie> firstRound = PlayoffPairing.FirstRound(places, ranked, stadiumOfClub);
        SeasonMatch? final = playoff.FirstOrDefault(match => match.Stage == PlayoffStages.Final);

        Dictionary<int, SeasonMatch> semiFinalOfTie = places.PlayoffClubs == FourClubs
            ? ReadSemiFinals(division, places, ranked, firstRound, playoff)
            : new Dictionary<int, SeasonMatch>();

        if (places.PlayoffClubs == 2 && final is not null)
        {
            CheckFinalOfTwo(division, places, ranked, firstRound[0], final);
        }

        CheckWinners(division, playoff);

        if (places.PlayoffClubs == FourClubs && final is not null)
        {
            CheckFinalOfFour(division, semiFinalOfTie, final);
        }

        (List<PlayoffTie> newTies, DateTime? latestSemiFinal) = NewTiesOf(
            places,
            ranked,
            firstRound,
            semiFinalOfTie,
            final);

        return new PlayoffReading(
            PlayoffReadingStatus.Ready,
            finished,
            total,
            newestChange,
            ranked,
            firstRound,
            playoff.Count,
            playoff.Count(match => match.WinnerLeagueTeamId is not null),
            final?.WinnerLeagueTeamId,
            newTies,
            latestSemiFinal);
    }

    /// <summary>
    /// Checks that every written playoff match is one the playoff can read: both clubs known, no round number and no
    /// leg, and a stage this playoff has, and that the playoff does not have more finals or semi-finals than it can.
    /// </summary>
    /// <param name="division">The division.</param>
    /// <param name="places">What the division's settings say about its table.</param>
    /// <param name="playoff">The playoff matches in match id order.</param>
    /// <exception cref="ArgumentException">Thrown when a match has a stage that is not a playoff stage.</exception>
    /// <exception cref="SeasonRefusedException">Thrown when a match or the matches cannot be read.</exception>
    private static void CheckPlayoffMatches(
        GetCompetitionsResult division,
        PromotionPlaces places,
        List<SeasonMatch> playoff)
    {
        string divisionText = PlayoffTableCheck.DivisionOf(division);

        foreach (SeasonMatch match in playoff)
        {
            if (match.Stage is not (PlayoffStages.SemiFinal or PlayoffStages.Final))
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Match {match.Id} has no playoff stage, but the playoff matches are the ones with one."),
                    nameof(playoff));
            }

            if (match.HomeLeagueTeamId is null || match.AwayLeagueTeamId is null)
            {
                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Playoff match {match.Id} of {divisionText} has no home or no away club, so it lacks a ")
                    + "statistics row for one side. Delete it.");
            }

            if (match.RoundNumber is not null || match.Leg is not null)
            {
                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Playoff match {match.Id} of {divisionText} has a round number or a leg, but a playoff ")
                    + "match has neither. Delete it.");
            }

            if (places.PlayoffClubs == 2 && match.Stage == PlayoffStages.SemiFinal)
            {
                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Playoff match {match.Id} of {divisionText} is a semi-final, but the division's playoff ")
                    + "has 2 clubs and so only a final. Delete it.");
            }
        }

        List<SeasonMatch> finals = playoff.Where(match => match.Stage == PlayoffStages.Final).ToList();

        if (finals.Count > 1)
        {
            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The playoff of {divisionText} has two finals, {finals[0].Id} and {finals[1].Id}. ")
                + "Delete one of them.");
        }

        List<SeasonMatch> semiFinals = playoff.Where(match => match.Stage == PlayoffStages.SemiFinal).ToList();

        if (semiFinals.Count > SemiFinalCount)
        {
            string ids = string.Join(", ", semiFinals.Select(match => match.Id.ToString(CultureInfo.InvariantCulture)));

            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The playoff of {divisionText} has {semiFinals.Count} semi-final matches, {ids}, but it has ")
                + "two semi-finals. Delete the extra ones.");
        }
    }

    /// <summary>
    /// Builds the refusal for a league match that is unfinished while the playoff is written.
    /// </summary>
    /// <param name="division">The division.</param>
    /// <param name="leagueMatches">The league matches, at least one of them unfinished.</param>
    /// <returns>The refusal, ready to throw, for the unfinished match with the lowest id.</returns>
    private static SeasonRefusedException UnfinishedLeagueMatch(
        GetCompetitionsResult division,
        IReadOnlyList<SeasonMatch> leagueMatches)
    {
        int matchId = leagueMatches.Where(match => !match.IsFinished).Min(match => match.Id);

        return new SeasonRefusedException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"League match {matchId} of {PlayoffTableCheck.DivisionOf(division)} is not finished, but the ")
            + "division's playoff is already written. Finish it again with its result, or "
            + $"{DeletePlayoff}.");
    }

    /// <summary>
    /// Builds a reading for a playoff that is still waiting: its counts describe the written playoff matches and its
    /// lists are empty.
    /// </summary>
    /// <param name="status">The waiting status.</param>
    /// <param name="finished">The number of finished league matches.</param>
    /// <param name="total">The number of league matches.</param>
    /// <param name="newestChange">The time of the newest Standings change, or null.</param>
    /// <param name="playoff">The playoff matches.</param>
    /// <returns>The reading.</returns>
    private static PlayoffReading Waiting(
        PlayoffReadingStatus status,
        int finished,
        int total,
        DateTime? newestChange,
        List<SeasonMatch> playoff)
    {
        return new PlayoffReading(
            status,
            finished,
            total,
            newestChange,
            [],
            [],
            playoff.Count,
            playoff.Count(match => match.WinnerLeagueTeamId is not null),
            playoff.FirstOrDefault(match => match.Stage == PlayoffStages.Final)?.WinnerLeagueTeamId,
            [],
            null);
    }

    /// <summary>
    /// Checks that the table has the places the playoff spans.
    /// </summary>
    /// <param name="division">The division.</param>
    /// <param name="places">What the division's settings say about its table.</param>
    /// <param name="ranked">The ranked league team ids.</param>
    /// <param name="otherFix">The fix the refusal offers besides fixing the division's settings.</param>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the table has fewer clubs than the playoff needs.
    /// </exception>
    private static void CheckTableReachesPlayoff(
        GetCompetitionsResult division,
        PromotionPlaces places,
        List<int> ranked,
        string otherFix)
    {
        int lastPlace = checked(places.FirstPlayoffPlace + places.PlayoffClubs - 1);

        if (ranked.Count >= lastPlace)
        {
            return;
        }

        throw new SeasonRefusedException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"The table of {PlayoffTableCheck.DivisionOf(division)} has {ranked.Count} clubs, but its playoff ")
            + string.Create(
                CultureInfo.InvariantCulture,
                $"of {places.PlayoffClubs} clubs needs places {places.FirstPlayoffPlace} to {lastPlace}. ")
            + $"Fix the division's settings, or {otherFix}.");
    }

    /// <summary>
    /// Matches the written semi-finals to the first round's two ties, by the pair of clubs and not by who hosts.
    /// </summary>
    /// <param name="division">The division.</param>
    /// <param name="places">What the division's settings say about its table.</param>
    /// <param name="ranked">The ranked league team ids.</param>
    /// <param name="firstRound">The first round's two semi-final ties.</param>
    /// <param name="playoff">The playoff matches in match id order.</param>
    /// <returns>The written semi-final of each tie by tie number; a tie with no match has no entry.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when a written semi-final is not a tie of the table, or a tie has two matches.
    /// </exception>
    private static Dictionary<int, SeasonMatch> ReadSemiFinals(
        GetCompetitionsResult division,
        PromotionPlaces places,
        List<int> ranked,
        IReadOnlyList<PlayoffTie> firstRound,
        List<SeasonMatch> playoff)
    {
        Dictionary<(int Low, int High), PlayoffTie> tieOfPair = firstRound.ToDictionary(
            tie => PairOf(tie.HomeLeagueTeamId, tie.AwayLeagueTeamId));

        Dictionary<int, List<SeasonMatch>> matchesOfTie = new();

        foreach (SeasonMatch match in playoff.Where(playoffMatch => playoffMatch.Stage == PlayoffStages.SemiFinal))
        {
            if (!tieOfPair.TryGetValue(PairOf(match), out PlayoffTie? tie))
            {
                throw NotOfTable(division, places, ranked, match);
            }

            if (!matchesOfTie.TryGetValue(tie.TieNumber, out List<SeasonMatch>? matches))
            {
                matches = new List<SeasonMatch>();
                matchesOfTie.Add(tie.TieNumber, matches);
            }

            matches.Add(match);
        }

        foreach ((int tieNumber, List<SeasonMatch> matches) in matchesOfTie.OrderBy(pair => pair.Key))
        {
            if (matches.Count > 1)
            {
                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Semi-final {tieNumber} of {PlayoffTableCheck.DivisionOf(division)} has two matches, ")
                    + string.Create(
                        CultureInfo.InvariantCulture,
                        $"{matches[0].Id} and {matches[1].Id}. Delete one of them."));
            }
        }

        return matchesOfTie.ToDictionary(pair => pair.Key, pair => pair.Value[0]);
    }

    /// <summary>
    /// Checks that the written final of a two-club playoff is the tie the table gives.
    /// </summary>
    /// <param name="division">The division.</param>
    /// <param name="places">What the division's settings say about its table.</param>
    /// <param name="ranked">The ranked league team ids.</param>
    /// <param name="tie">The first round's only tie, the final.</param>
    /// <param name="final">The written final.</param>
    /// <exception cref="SeasonRefusedException">Thrown when the final is between other clubs.</exception>
    private static void CheckFinalOfTwo(
        GetCompetitionsResult division,
        PromotionPlaces places,
        List<int> ranked,
        PlayoffTie tie,
        SeasonMatch final)
    {
        if (PairOf(final) != PairOf(tie.HomeLeagueTeamId, tie.AwayLeagueTeamId))
        {
            throw NotOfTable(division, places, ranked, final);
        }
    }

    /// <summary>
    /// Checks that no finished playoff match is left without a winner.
    /// </summary>
    /// <param name="division">The division.</param>
    /// <param name="playoff">The playoff matches in match id order.</param>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when a finished match has level goals and no shootout.
    /// </exception>
    private static void CheckWinners(GetCompetitionsResult division, List<SeasonMatch> playoff)
    {
        foreach (SeasonMatch match in playoff.Where(playoffMatch => playoffMatch.IsFinished))
        {
            if (match.WinnerLeagueTeamId is not null)
            {
                continue;
            }

            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Playoff match {match.Id} ({PlayoffStages.NameOf(match.Stage!.Value)}, club ")
                + string.Create(
                    CultureInfo.InvariantCulture,
                    $"{match.HomeLeagueTeamId} v club {match.AwayLeagueTeamId}) of ")
                + $"{PlayoffTableCheck.DivisionOf(division)} is finished but has no winner: its goals are level and "
                + "no shootout decided it. Fix its result.");
        }
    }

    /// <summary>
    /// Checks that the written final of a four-club playoff follows from the two semi-finals: both are written and
    /// decided, and the final is between their winners.
    /// </summary>
    /// <param name="division">The division.</param>
    /// <param name="semiFinalOfTie">The written semi-final of each tie by tie number.</param>
    /// <param name="final">The written final.</param>
    /// <exception cref="SeasonRefusedException">Thrown when the final does not follow from the semi-finals.</exception>
    private static void CheckFinalOfFour(
        GetCompetitionsResult division,
        Dictionary<int, SeasonMatch> semiFinalOfTie,
        SeasonMatch final)
    {
        string divisionText = PlayoffTableCheck.DivisionOf(division);
        List<int> winners = new();

        for (int tieNumber = 1; tieNumber <= SemiFinalCount; tieNumber++)
        {
            if (!semiFinalOfTie.TryGetValue(tieNumber, out SeasonMatch? semiFinal))
            {
                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"The final of {divisionText} (match {final.Id}) is written, but semi-final {tieNumber} is ")
                    + "not written. Delete the final, and the missing semi-final and then the final are written "
                    + "again.");
            }

            if (semiFinal.WinnerLeagueTeamId is not { } winner)
            {
                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"The final of {divisionText} (match {final.Id}) is written, but semi-final {tieNumber} ")
                    + "has no winner yet. Finish it again with its result, or delete the final and it is written "
                    + "again.");
            }

            winners.Add(winner);
        }

        if (PairOf(final) == PairOf(winners[0], winners[1]))
        {
            return;
        }

        throw new SeasonRefusedException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Playoff match {final.Id} (final, club {final.HomeLeagueTeamId} v club {final.AwayLeagueTeamId}) ")
            + string.Create(
                CultureInfo.InvariantCulture,
                $"of {divisionText} is not the final of the semi-final winners, clubs {winners[0]} and ")
            + string.Create(
                CultureInfo.InvariantCulture,
                $"{winners[1]}: a semi-final result changed after the final was written. Put it back, or delete ")
            + "the final and it is written again.");
    }

    /// <summary>
    /// Finds the ties to write now: the first round's ties that have no match, or the final once both semi-finals are
    /// decided. Nothing is to write once the final is written.
    /// </summary>
    /// <param name="places">What the division's settings say about its table.</param>
    /// <param name="ranked">The ranked league team ids.</param>
    /// <param name="firstRound">The first round's ties.</param>
    /// <param name="semiFinalOfTie">The written semi-final of each tie by tie number.</param>
    /// <param name="final">The written final, or null.</param>
    /// <returns>
    /// The new ties, and the later semi-final kickoff when the final is among them; otherwise null.
    /// </returns>
    private static (List<PlayoffTie> NewTies, DateTime? LatestSemiFinalKickoff) NewTiesOf(
        PromotionPlaces places,
        List<int> ranked,
        IReadOnlyList<PlayoffTie> firstRound,
        Dictionary<int, SeasonMatch> semiFinalOfTie,
        SeasonMatch? final)
    {
        if (final is not null)
        {
            return (new List<PlayoffTie>(), null);
        }

        if (places.PlayoffClubs == FourClubs
            && semiFinalOfTie.Count == SemiFinalCount
            && semiFinalOfTie.Values.All(semiFinal => semiFinal.WinnerLeagueTeamId is not null))
        {
            PlayoffTie finalTie = PlayoffPairing.Final(
                places,
                ranked,
                semiFinalOfTie[1].WinnerLeagueTeamId!.Value,
                semiFinalOfTie[2].WinnerLeagueTeamId!.Value);

            return ([finalTie], semiFinalOfTie.Values.Max(semiFinal => semiFinal.Kickoff));
        }

        List<PlayoffTie> unwritten = firstRound.Where(tie => !semiFinalOfTie.ContainsKey(tie.TieNumber)).ToList();

        return (unwritten, null);
    }

    /// <summary>
    /// Builds the refusal for a written playoff match that is not a tie of the table as it stands.
    /// </summary>
    /// <param name="division">The division.</param>
    /// <param name="places">What the division's settings say about its table.</param>
    /// <param name="ranked">The ranked league team ids.</param>
    /// <param name="match">The written match.</param>
    /// <returns>The refusal, ready to throw.</returns>
    private static SeasonRefusedException NotOfTable(
        GetCompetitionsResult division,
        PromotionPlaces places,
        List<int> ranked,
        SeasonMatch match)
    {
        int first = places.FirstPlayoffPlace;
        bool isFourClubs = places.PlayoffClubs == FourClubs;

        string article = isFourClubs ? "a semi-final" : "the final";

        string placesText = isFourClubs
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"places {first} to {first + FourClubs - 1} are now clubs {ranked[first - 1]}, {ranked[first]}, ")
            + string.Create(CultureInfo.InvariantCulture, $"{ranked[first + 1]} and {ranked[first + 2]}")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"places {first} and {first + 1} are now clubs {ranked[first - 1]} and {ranked[first]}");

        return new SeasonRefusedException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Playoff match {match.Id} ({PlayoffStages.NameOf(match.Stage!.Value)}, club ")
            + string.Create(
                CultureInfo.InvariantCulture,
                $"{match.HomeLeagueTeamId} v club {match.AwayLeagueTeamId}) of ")
            + $"{PlayoffTableCheck.DivisionOf(division)} is not {article} of the table as it stands: "
            + $"{placesText}. Put back the result or setting that changed, or {DeletePlayoff}.");
    }

    /// <summary>
    /// Gives the pair of clubs of a match with the lower id first, so a pair is the same whichever club hosts.
    /// </summary>
    /// <param name="match">The match, which has both clubs.</param>
    /// <returns>The two league team ids, lower first.</returns>
    private static (int Low, int High) PairOf(SeasonMatch match)
    {
        return PairOf(match.HomeLeagueTeamId!.Value, match.AwayLeagueTeamId!.Value);
    }

    /// <summary>
    /// Gives a pair of clubs with the lower id first.
    /// </summary>
    /// <param name="firstLeagueTeamId">The league team id of one club.</param>
    /// <param name="secondLeagueTeamId">The league team id of the other club.</param>
    /// <returns>The two league team ids, lower first.</returns>
    private static (int Low, int High) PairOf(int firstLeagueTeamId, int secondLeagueTeamId)
    {
        return (Math.Min(firstLeagueTeamId, secondLeagueTeamId), Math.Max(firstLeagueTeamId, secondLeagueTeamId));
    }
}
