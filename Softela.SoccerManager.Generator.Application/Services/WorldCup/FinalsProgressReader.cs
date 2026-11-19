using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Services.DomesticCup;
using Softela.SoccerManager.Generator.Application.Services.Knockout;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// Matches the written knockout matches of the World Cup finals to the ties of the bracket, round by round, to find
/// which ties are written and which are decided, and reads the third-place match beside them.
/// </summary>
internal static class FinalsProgressReader
{
    /// <summary>
    /// Matches the knockout matches to the ties of the bracket and collects the winners. A round's ties are those whose
    /// nations are known from the winners of the rounds before it. A match belongs to the known tie that has the same
    /// two nations, whichever is at home. The third-place match is read apart from the bracket.
    /// </summary>
    /// <param name="bracket">The bracket built from the finished group tables.</param>
    /// <param name="knockoutMatches">The knockout matches, stages 12 to 16; they have been checked.</param>
    /// <returns>
    /// The winners and the written ties, each in round order and then tie order, and the third place.
    /// </returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when a match is not a tie of the bracket, a tie has two matches, a finished match has no winner, the
    /// bracket refuses the winners, or the third-place match does not fit the semi-finals.
    /// </exception>
    internal static FinalsProgress Read(KnockoutBracket bracket, IReadOnlyList<SeasonMatch> knockoutMatches)
    {
        List<KnockoutWinner> winners = [];
        List<FinalsWrittenTie> written = [];

        for (int roundNumber = 1; roundNumber <= bracket.Rounds.Count; roundNumber++)
        {
            FinalsRound round = FinalsStages.FromBracketNumber(roundNumber);
            byte stage = FinalsStages.StageOf(round);
            IReadOnlyList<KnockoutKnownTie> knownAll = KnownTiesOf(bracket, winners);
            Dictionary<int, SeasonMatch> matchOfTie = new();

            IEnumerable<SeasonMatch> roundMatches = knockoutMatches
                .Where(match => match.Stage == stage)
                .OrderBy(match => match.Kickoff)
                .ThenBy(match => match.Id);

            foreach (SeasonMatch match in roundMatches)
            {
                KnockoutKnownTie tie = FindTie(round, stage, match, knownAll, written, winners);

                if (matchOfTie.TryGetValue(tie.TieNumber, out SeasonMatch? earlier))
                {
                    throw new SeasonRefusedException(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"{Capitalised(round)} tie {tie.TieNumber} has two matches, {earlier.Id} and ")
                        + string.Create(CultureInfo.InvariantCulture, $"{match.Id}. Delete one of them."));
                }

                matchOfTie.Add(tie.TieNumber, match);
                written.Add(new FinalsWrittenTie(roundNumber, tie.TieNumber, match.Kickoff));

                if (match.IsFinished)
                {
                    winners.Add(new KnockoutWinner(roundNumber, tie.TieNumber, WinnerOf(match, round)));
                }
            }
        }

        FinalsThirdPlace? thirdPlace = ReadThirdPlace(bracket, knockoutMatches, winners);

        return new FinalsProgress(
            winners.OrderBy(winner => winner.RoundNumber).ThenBy(winner => winner.TieNumber).ToList(),
            written.OrderBy(tie => tie.RoundNumber).ThenBy(tie => tie.TieNumber).ToList(),
            thirdPlace);
    }

    /// <summary>
    /// Lists the ties whose two nations are known, turning a refusal by the bracket library into a season refusal.
    /// </summary>
    /// <param name="bracket">The bracket.</param>
    /// <param name="winners">The ties decided so far.</param>
    /// <returns>The ties with both nations known, in round order and then tie order.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when the bracket library refuses the winners.</exception>
    internal static IReadOnlyList<KnockoutKnownTie> KnownTiesOf(
        KnockoutBracket bracket,
        IReadOnlyList<KnockoutWinner> winners)
    {
        try
        {
            return KnockoutTieResolver.KnownTies(bracket, winners);
        }
        catch (ArgumentException exception) when (exception is not ArgumentNullException
            and not ArgumentOutOfRangeException)
        {
            byte firstStage = FinalsStages.StageOf(FinalsRound.RoundOf16);

            throw new SeasonRefusedException(
                "The World Cup bracket refused the written results: "
                + DomesticCupReader.ReasonOf(exception)
                + string.Create(
                    CultureInfo.InvariantCulture,
                    $" Reset the knockouts from stage {firstStage} (the readme's knockouts reset)."));
        }
    }

    /// <summary>
    /// Finds the known tie that a match plays: the one with the same two nations, whichever is at home.
    /// </summary>
    /// <param name="round">The round of the match.</param>
    /// <param name="stage">The stage of the round.</param>
    /// <param name="match">The knockout match.</param>
    /// <param name="knownAll">The known ties of every round, given the winners so far.</param>
    /// <param name="written">The ties of the rounds before that are written.</param>
    /// <param name="winners">The ties of the rounds before that are decided.</param>
    /// <returns>The tie.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when the match is not a known tie of its round.</exception>
    private static KnockoutKnownTie FindTie(
        FinalsRound round,
        byte stage,
        SeasonMatch match,
        IReadOnlyList<KnockoutKnownTie> knownAll,
        List<FinalsWrittenTie> written,
        List<KnockoutWinner> winners)
    {
        int roundNumber = FinalsStages.BracketNumberOf(round);
        int home = match.HomeLeagueTeamId!.Value;
        int away = match.AwayLeagueTeamId!.Value;

        KnockoutKnownTie? tie = knownAll.FirstOrDefault(candidate =>
            candidate.RoundNumber == roundNumber
            && ((candidate.FirstLeagueTeamId == home && candidate.SecondLeagueTeamId == away)
                || (candidate.FirstLeagueTeamId == away && candidate.SecondLeagueTeamId == home)));

        if (tie is not null)
        {
            return tie;
        }

        // A tie of the round before that is written but no longer decided is the likely reason this match fits
        // no tie.
        KnockoutKnownTie? feeder = knownAll.FirstOrDefault(candidate =>
            candidate.RoundNumber == roundNumber - 1
            && written.Any(done => done.RoundNumber == candidate.RoundNumber && done.TieNumber == candidate.TieNumber)
            && !winners.Any(won => won.RoundNumber == candidate.RoundNumber && won.TieNumber == candidate.TieNumber)
            && (candidate.FirstLeagueTeamId == home
                || candidate.FirstLeagueTeamId == away
                || candidate.SecondLeagueTeamId == home
                || candidate.SecondLeagueTeamId == away));

        string roundName = FinalsStages.NameOf(round);

        if (feeder is not null)
        {
            string feederName = FinalsStages.NameOf(FinalsStages.FromBracketNumber(feeder.RoundNumber));

            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Match {match.Id} ({roundName}) is written, but {feederName} tie {feeder.TieNumber} that ")
                + "leads to it is no longer decided. Finish that tie again with its result, or reset the "
                + string.Create(CultureInfo.InvariantCulture, $"knockouts from stage {stage}."));
        }

        throw new SeasonRefusedException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Match {match.Id} ({roundName}, league team {home} v league team {away}) is not a tie of the ")
            + "bracket as the group tables and earlier results stand. "
            + string.Create(CultureInfo.InvariantCulture, $"Reset the knockouts from stage {stage}."));
    }

    /// <summary>
    /// Gives the winner of a finished knockout match.
    /// </summary>
    /// <param name="match">The finished match.</param>
    /// <param name="round">The round of the match.</param>
    /// <returns>The league team id of the winner.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when the goals are level and no shootout decided it.</exception>
    private static int WinnerOf(SeasonMatch match, FinalsRound round)
    {
        return match.WinnerLeagueTeamId ?? throw new SeasonRefusedException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Match {match.Id} ({FinalsStages.NameOf(round)}) is finished but has no winner: its goals are ")
            + "level and no shootout decided it. Fix its result.");
    }

    /// <summary>
    /// Reads the third-place match. Its pair is the two semi-final losers, the loser of semi-final 1 at home, once both
    /// semi-finals are decided.
    /// </summary>
    /// <param name="bracket">The bracket.</param>
    /// <param name="knockoutMatches">The knockout matches.</param>
    /// <param name="winners">The ties decided so far.</param>
    /// <returns>
    /// The third place, with its kickoff when it is written; null while the two semi-finals are not both decided.
    /// </returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when a third-place match is written before both semi-finals are decided, there are two of them, one is
    /// not the match of the two losers, or one is finished with no winner.
    /// </exception>
    private static FinalsThirdPlace? ReadThirdPlace(
        KnockoutBracket bracket,
        IReadOnlyList<SeasonMatch> knockoutMatches,
        List<KnockoutWinner> winners)
    {
        IReadOnlyList<KnockoutKnownTie> known = KnownTiesOf(bracket, winners);
        int? home = LoserOf(known, winners, 1);
        int? away = LoserOf(known, winners, 2);

        List<SeasonMatch> matches = knockoutMatches
            .Where(match => match.Stage == FinalsStages.ThirdPlaceStage)
            .OrderBy(match => match.Kickoff)
            .ThenBy(match => match.Id)
            .ToList();

        if (home is not { } homeNation || away is not { } awayNation)
        {
            if (matches.Count == 0)
            {
                return null;
            }

            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Match {matches[0].Id} (third-place match) is written, but the two semi-finals are not both ")
                + "decided. Finish them again with their results, or reset the knockouts from stage "
                + string.Create(CultureInfo.InvariantCulture, $"{FinalsStages.ThirdPlaceStage}."));
        }

        if (matches.Count == 0)
        {
            return new FinalsThirdPlace(homeNation, awayNation, null, IsFinished: false);
        }

        if (matches.Count > 1)
        {
            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The third-place match has two matches, {matches[0].Id} and {matches[1].Id}. Delete one of ")
                + "them.");
        }

        CheckThirdPlace(matches[0], homeNation, awayNation);

        return new FinalsThirdPlace(homeNation, awayNation, matches[0].Kickoff, matches[0].IsFinished);
    }

    /// <summary>
    /// Checks that a written third-place match is played by the two semi-final losers and, when it is finished, has a
    /// winner.
    /// </summary>
    /// <param name="match">The third-place match.</param>
    /// <param name="homeNation">The league team id of the loser of semi-final 1.</param>
    /// <param name="awayNation">The league team id of the loser of semi-final 2.</param>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the match has another pair, or is finished with no winner.
    /// </exception>
    private static void CheckThirdPlace(SeasonMatch match, int homeNation, int awayNation)
    {
        int home = match.HomeLeagueTeamId!.Value;
        int away = match.AwayLeagueTeamId!.Value;

        bool isPair = (home == homeNation && away == awayNation) || (home == awayNation && away == homeNation);

        if (!isPair)
        {
            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Match {match.Id} (third-place match, league team {home} v league team {away}) is not the ")
                + string.Create(
                    CultureInfo.InvariantCulture,
                    $"match of the two semi-final losers, league teams {homeNation} and {awayNation}, as the ")
                + string.Create(
                    CultureInfo.InvariantCulture,
                    $"semi-final results stand. Reset the knockouts from stage {FinalsStages.ThirdPlaceStage}."));
        }

        if (match.IsFinished)
        {
            _ = WinnerOf(match, FinalsRound.ThirdPlace);
        }
    }

    /// <summary>
    /// Finds the loser of a semi-final.
    /// </summary>
    /// <param name="known">The known ties of the bracket.</param>
    /// <param name="winners">The ties decided so far.</param>
    /// <param name="tieNumber">The 1-based number of the semi-final.</param>
    /// <returns>The league team id of the loser, or null when the semi-final is not decided.</returns>
    private static int? LoserOf(
        IReadOnlyList<KnockoutKnownTie> known,
        IReadOnlyList<KnockoutWinner> winners,
        int tieNumber)
    {
        int semiFinal = FinalsStages.BracketNumberOf(FinalsRound.SemiFinal);

        KnockoutWinner? winner = winners.FirstOrDefault(decided =>
            decided.RoundNumber == semiFinal && decided.TieNumber == tieNumber);

        KnockoutKnownTie? tie = known.FirstOrDefault(candidate =>
            candidate.RoundNumber == semiFinal && candidate.TieNumber == tieNumber);

        if (winner is null || tie is null)
        {
            return null;
        }

        return winner.LeagueTeamId == tie.FirstLeagueTeamId ? tie.SecondLeagueTeamId : tie.FirstLeagueTeamId;
    }

    /// <summary>
    /// Gives the name of a round with a capital first letter, for the start of a sentence.
    /// </summary>
    /// <param name="round">The round.</param>
    /// <returns>The round's name, such as "Quarter-final".</returns>
    private static string Capitalised(FinalsRound round)
    {
        string name = FinalsStages.NameOf(round);

        return char.ToUpperInvariant(name[0]) + name[1..];
    }
}
