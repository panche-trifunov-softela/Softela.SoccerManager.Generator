using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Services.DomesticCup;
using Softela.SoccerManager.Generator.Application.Services.Ranking;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// Reads one continental cup's knockout back from the cup's written matches: whether its league phase is finished, what
/// the bracket is, which ties are decided and which ties are still to write. Nothing is stored about the knockout apart
/// from its matches, so every tick reads it again.
/// </summary>
public static class ContinentalKnockoutReader
{
    /// <summary>
    /// The <see cref="MatchStage.ContinentalLeaguePhase"/> code as the reader compares it with
    /// <see cref="SeasonMatch.Stage"/>.
    /// </summary>
    private const byte LeaguePhaseStage = (byte)MatchStage.ContinentalLeaguePhase;

    /// <summary>
    /// The fix for a league-phase match that cannot be read: the league phase is written once for both cups.
    /// </summary>
    private const string ResetBothCups =
        "Reset both cups together (the readme's 'Writing a season again' has the steps).";

    /// <summary>
    /// The fix for a knockout match that cannot be read.
    /// </summary>
    private const string FixKnockout = "Delete it, or reset the cup's knockout matches (the readme's knockout reset).";

    /// <summary>
    /// Reads the knockout of one cup. Nothing depends on the order of the matches given. The result promises:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// While a league-phase match is unfinished and no knockout match is written, the status is waiting for the league
    /// phase and only the counts are set.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Once every league-phase match is finished, the status is ready: the table is ranked, the bracket is built from
    /// it, and every written match has to be a tie of that bracket as its earlier results stand.
    /// </description>
    /// </item>
    /// </list>
    /// </summary>
    /// <param name="seasonId">The id of the season, which seeds the table's tie-break shuffle.</param>
    /// <param name="cupSize">The number of clubs in the cup, 16 or 32.</param>
    /// <param name="cupMatches">Every match of this one cup in the season, with its scores, in any order.</param>
    /// <returns>What the cup's matches say about its knockout.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cupMatches"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="cupSize"/> is not 16 or 32. That is a bug in the caller, not a refusal.
    /// </exception>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the written matches cannot be read as a league phase and a knockout: a match lacks a club or has a
    /// stage or leg the cup cannot have, the league phase is not the cup's shape, a league-phase match is unfinished
    /// while the knockout is written, the table is refused, a knockout match is not a tie of the bracket, or a tie has
    /// a wrong number of matches or no winner.
    /// </exception>
    public static ContinentalKnockoutReading Read(int seasonId, int cupSize, IReadOnlyList<SeasonMatch> cupMatches)
    {
        ArgumentNullException.ThrowIfNull(cupMatches);

        if (cupSize != ContinentalInputValidator.SmallCupClubCount
            && cupSize != ContinentalInputValidator.LargeCupClubCount)
        {
            throw new ArgumentOutOfRangeException(nameof(cupSize), cupSize, "The cup size is not 16 or 32.");
        }

        CheckMatches(cupSize, cupMatches);

        List<SeasonMatch> leaguePhase = cupMatches
            .Where(match => match.Stage == LeaguePhaseStage)
            .OrderBy(match => match.Kickoff)
            .ThenBy(match => match.Id)
            .ToList();

        List<SeasonMatch> knockout = cupMatches
            .Where(match => match.Stage != LeaguePhaseStage)
            .OrderBy(match => match.Kickoff)
            .ThenBy(match => match.Id)
            .ToList();

        CheckLeaguePhaseShape(cupSize, leaguePhase);

        List<SeasonMatch> unfinished = leaguePhase.Where(match => !match.IsFinished).ToList();

        if (unfinished.Count > 0 && knockout.Count > 0)
        {
            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"League-phase match {unfinished[0].Id} is not finished, but the cup's knockout matches are ")
                + "already written. Finish it again with its result, or reset the cup's knockout matches.");
        }

        if (unfinished.Count > 0)
        {
            return new ContinentalKnockoutReading(
                ContinentalKnockoutReadingStatus.WaitingForLeaguePhase,
                leaguePhase.Count - unfinished.Count,
                leaguePhase.Count,
                null,
                [],
                [],
                [],
                null);
        }

        ContinentalBracket bracket = ContinentalBracketBuilder.Build(RankTable(seasonId, leaguePhase));
        ContinentalKnockoutProgress progress = ContinentalKnockoutProgressReader.Read(bracket, knockout);

        HashSet<(ContinentalRound Round, int Tie)> written = progress.WrittenTies
            .Select(tie => (tie.Round, tie.TieNumber))
            .ToHashSet();

        List<ContinentalKnownTie> newTies = ContinentalKnockoutProgressReader.KnownTiesOf(bracket, progress.Winners)
            .Where(tie => !written.Contains((tie.Round, tie.TieNumber)))
            .ToList();

        return new ContinentalKnockoutReading(
            ContinentalKnockoutReadingStatus.Ready,
            leaguePhase.Count,
            leaguePhase.Count,
            bracket,
            progress.WrittenTies,
            progress.Winners,
            newTies,
            leaguePhase.Max(match => match.Kickoff));
    }

    /// <summary>
    /// Checks that every match of the cup is one the knockout can read: both clubs known, a stage the cup has, and the
    /// leg that stage has.
    /// </summary>
    /// <param name="cupSize">The number of clubs in the cup.</param>
    /// <param name="cupMatches">The cup's matches.</param>
    /// <exception cref="SeasonRefusedException">Thrown when a match cannot be read.</exception>
    private static void CheckMatches(int cupSize, IReadOnlyList<SeasonMatch> cupMatches)
    {
        foreach (SeasonMatch match in cupMatches.OrderBy(cupMatch => cupMatch.Id))
        {
            if (match.HomeLeagueTeamId is null || match.AwayLeagueTeamId is null)
            {
                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Cup match {match.Id} has no home or no away club, so it lacks exactly one statistics row ")
                    + $"for each side. {FixOf(match)}");
            }

            if (match.Stage == LeaguePhaseStage)
            {
                if (match.Leg is not null)
                {
                    throw new SeasonRefusedException(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"League-phase match {match.Id} has leg {match.Leg}, but a league-phase match has no leg. ")
                        + ResetBothCups);
                }

                continue;
            }

            CheckKnockoutMatch(cupSize, match);
        }
    }

    /// <summary>
    /// Checks that a match that is not a league-phase match is a knockout match the cup has, with the leg its stage
    /// has: 1 or 2 for a two-legged round, none for the final.
    /// </summary>
    /// <param name="cupSize">The number of clubs in the cup.</param>
    /// <param name="match">The match.</param>
    /// <exception cref="SeasonRefusedException">Thrown when the stage or the leg is wrong.</exception>
    private static void CheckKnockoutMatch(int cupSize, SeasonMatch match)
    {
        if (match.Stage is not { } stage
            || !ContinentalKnockoutStages.TryGetRound(stage, out ContinentalRound round)
            || !ContinentalBracketShape.TryGetRoundNumber(cupSize, round, out _))
        {
            string stageText = match.Stage is { } value ? value.ToString(CultureInfo.InvariantCulture) : "none";
            string allowed = cupSize == ContinentalInputValidator.SmallCupClubCount
                ? "4, 7, 8 and 9"
                : "4 to 9";

            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cup match {match.Id} has stage {stageText}, but the matches of a {cupSize}-club cup have the ")
                + $"stages {allowed}. {FixKnockout}");
        }

        bool isFinal = round == ContinentalRound.Final;
        bool legFits = isFinal ? match.Leg is null : match.Leg is 1 or 2;

        if (!legFits)
        {
            string needed = isFinal ? "no leg" : "leg 1 or 2";
            string legText = match.Leg is { } leg ? leg.ToString(CultureInfo.InvariantCulture) : "none";
            string roundName = ContinentalKnockoutStages.NameOf(round);

            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cup match {match.Id} ({roundName}) has leg {legText}, but it needs ")
                + $"{needed}. {FixKnockout}");
        }
    }

    /// <summary>
    /// Checks that the league phase has the shape of the cup's size: every club plays the cup's number of matchdays.
    /// </summary>
    /// <param name="cupSize">The number of clubs in the cup.</param>
    /// <param name="leaguePhase">The league-phase matches; they have been checked for clubs.</param>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the matches are not the cup's number, over the cup's number of clubs, with each club in the same
    /// number of them.
    /// </exception>
    private static void CheckLeaguePhaseShape(int cupSize, List<SeasonMatch> leaguePhase)
    {
        int matchdays = ContinentalMatchdayRules.MatchdayCount(cupSize);
        int expectedMatches = matchdays * cupSize / 2;

        Dictionary<int, int> matchesOfClub = new();

        foreach (SeasonMatch match in leaguePhase)
        {
            foreach (int clubId in new[] { match.HomeLeagueTeamId!.Value, match.AwayLeagueTeamId!.Value })
            {
                matchesOfClub[clubId] = matchesOfClub.GetValueOrDefault(clubId) + 1;
            }
        }

        if (leaguePhase.Count != expectedMatches || matchesOfClub.Count != cupSize)
        {
            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The cup's league phase has {leaguePhase.Count} matches over {matchesOfClub.Count} clubs, and a ")
                + string.Create(
                    CultureInfo.InvariantCulture,
                    $"{cupSize}-club cup has {expectedMatches} over {cupSize}, {matchdays} each. {ResetBothCups}"));
        }

        foreach ((int clubId, int count) in matchesOfClub.OrderBy(pair => pair.Key))
        {
            if (count != matchdays)
            {
                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Club {clubId} plays {count} league-phase matches, but a {cupSize}-club cup has ")
                    + string.Create(CultureInfo.InvariantCulture, $"{matchdays} for each club. {ResetBothCups}"));
            }
        }
    }

    /// <summary>
    /// Ranks the cup's league-phase table from its finished matches.
    /// </summary>
    /// <param name="seasonId">The id of the season, which seeds the tie-break shuffle.</param>
    /// <param name="leaguePhase">The league-phase matches, all finished and checked.</param>
    /// <returns>The league team ids in place order, place 1 first.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when the ranking library refuses the matches.</exception>
    private static List<int> RankTable(int seasonId, List<SeasonMatch> leaguePhase)
    {
        List<int> clubs = leaguePhase
            .SelectMany(match => new[] { match.HomeLeagueTeamId!.Value, match.AwayLeagueTeamId!.Value })
            .Distinct()
            .Order()
            .ToList();

        List<TableMatch> matches = SeasonTableMatches.Of(leaguePhase);

        try
        {
            return TableRanking.Rank(seasonId, clubs, matches)
                .OrderBy(row => row.Place)
                .Select(row => row.Id)
                .ToList();
        }
        catch (ArgumentException exception) when (exception is not ArgumentNullException
            and not ArgumentOutOfRangeException)
        {
            throw new SeasonRefusedException(
                $"The league-phase table refused the cup's matches: {DomesticCupReader.ReasonOf(exception)} "
                + ResetBothCups);
        }
    }

    /// <summary>
    /// Gives the fix for a match that lacks a club, which depends on the part of the cup the match belongs to.
    /// </summary>
    /// <param name="match">The match.</param>
    /// <returns>The fix, as a sentence.</returns>
    private static string FixOf(SeasonMatch match)
    {
        if (match.Stage == LeaguePhaseStage)
        {
            return ResetBothCups;
        }

        return match.Stage is { } stage && ContinentalKnockoutStages.TryGetRound(stage, out _)
            ? FixKnockout
            : "Delete it.";
    }
}
