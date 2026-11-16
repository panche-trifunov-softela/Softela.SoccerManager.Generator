using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Services.DomesticCup;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.ContinentalCup;

/// <summary>
/// Matches the written knockout matches of a continental cup to the ties of its bracket, round by round, to find which
/// ties are written and which are decided.
/// </summary>
internal static class ContinentalKnockoutProgressReader
{
    /// <summary>
    /// Matches the knockout matches to the ties of the bracket and collects the winners. A round's ties are those whose
    /// clubs are known from the winners of the rounds before it. A match belongs to the known tie that has the same two
    /// clubs, whichever club is at home.
    /// </summary>
    /// <param name="bracket">The bracket built from the finished league-phase table.</param>
    /// <param name="knockoutMatches">The cup's knockout matches; they have been checked.</param>
    /// <returns>The winners and the written ties, each in round order and then tie order.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when a match is not a tie of the bracket, a tie has too many or too few matches, a tie is finished level
    /// with no shootout, or the bracket refuses the winners.
    /// </exception>
    public static ContinentalKnockoutProgress Read(
        ContinentalBracket bracket,
        IReadOnlyList<SeasonMatch> knockoutMatches)
    {
        int clubCount = bracket.Table.Count;
        List<ContinentalWinner> winners = new();
        List<ContinentalWrittenTie> written = new();

        for (int roundNumber = 1; roundNumber <= bracket.Knockout.Rounds.Count; roundNumber++)
        {
            ContinentalRound round = ContinentalBracketShape.RoundOf(clubCount, roundNumber);
            byte stage = ContinentalKnockoutStages.StageOf(round);
            IReadOnlyList<ContinentalKnownTie> knownAll = KnownTiesOf(bracket, winners);
            SortedDictionary<int, List<SeasonMatch>> matchesOfTie = new();

            IEnumerable<SeasonMatch> roundMatches = knockoutMatches
                .Where(match => match.Stage == stage)
                .OrderBy(match => match.Kickoff)
                .ThenBy(match => match.Id);

            foreach (SeasonMatch match in roundMatches)
            {
                ContinentalKnownTie tie = FindTie(round, stage, match, knownAll, written, winners);

                if (!matchesOfTie.TryGetValue(tie.TieNumber, out List<SeasonMatch>? matches))
                {
                    matches = new List<SeasonMatch>();
                    matchesOfTie.Add(tie.TieNumber, matches);
                }

                matches.Add(match);
            }

            foreach ((int tieNumber, List<SeasonMatch> matches) in matchesOfTie)
            {
                ReadTie(round, stage, tieNumber, matches, written, winners);
            }
        }

        return new ContinentalKnockoutProgress(winners, written);
    }

    /// <summary>
    /// Lists the ties whose two clubs are known, turning a refusal by the bracket library into a season refusal.
    /// </summary>
    /// <param name="bracket">The bracket.</param>
    /// <param name="winners">The ties decided so far.</param>
    /// <returns>The ties with both clubs known, in round order and then tie order.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when the bracket library refuses the winners.</exception>
    internal static IReadOnlyList<ContinentalKnownTie> KnownTiesOf(
        ContinentalBracket bracket,
        IReadOnlyList<ContinentalWinner> winners)
    {
        try
        {
            return ContinentalTieResolver.KnownTies(bracket, winners);
        }
        catch (ArgumentException exception) when (exception is not ArgumentNullException
            and not ArgumentOutOfRangeException)
        {
            byte firstStage = ContinentalKnockoutStages.StageOf(ContinentalRound.PlayOff);

            throw new SeasonRefusedException(
                $"The continental bracket refused the written results: {DomesticCupReader.ReasonOf(exception)}"
                + string.Create(
                    CultureInfo.InvariantCulture,
                    $" Reset the cup's knockout matches from stage {firstStage} (the readme's knockout reset)."));
        }
    }

    /// <summary>
    /// Reads the matches of one tie: checks that the tie has the matches it should, records the tie as written and
    /// records its winner when every match is finished.
    /// </summary>
    /// <param name="round">The round of the tie.</param>
    /// <param name="stage">The stage of the round.</param>
    /// <param name="tieNumber">The 1-based number of the tie within its round.</param>
    /// <param name="matches">The tie's matches, in kickoff order; there is at least one.</param>
    /// <param name="written">The written ties; this tie is added to it.</param>
    /// <param name="winners">The decided ties; this tie is added to it when it is decided.</param>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the tie has too many or too few matches, or is finished level with no shootout.
    /// </exception>
    private static void ReadTie(
        ContinentalRound round,
        byte stage,
        int tieNumber,
        List<SeasonMatch> matches,
        List<ContinentalWrittenTie> written,
        List<ContinentalWinner> winners)
    {
        if (round == ContinentalRound.Final)
        {
            if (matches.Count > 1)
            {
                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"The final has two matches, {matches[0].Id} and {matches[1].Id}. Delete one of them."));
            }

            written.Add(new ContinentalWrittenTie(round, tieNumber, matches[0].Kickoff));

            if (matches[0].IsFinished)
            {
                winners.Add(new ContinentalWinner(round, tieNumber, ContinentalTieWinner.OfFinal(matches[0])));
            }

            return;
        }

        SeasonMatch? firstLeg = LegOf(matches, 1, round, tieNumber);
        SeasonMatch? secondLeg = LegOf(matches, 2, round, tieNumber);

        if (firstLeg is null || secondLeg is null)
        {
            throw MissingLeg(round, stage, tieNumber, firstLeg, secondLeg);
        }

        written.Add(new ContinentalWrittenTie(round, tieNumber, matches.Max(match => match.Kickoff)));

        if (!firstLeg.IsFinished || !secondLeg.IsFinished)
        {
            return;
        }

        if (firstLeg.HomeLeagueTeamId != secondLeg.AwayLeagueTeamId
            || firstLeg.AwayLeagueTeamId != secondLeg.HomeLeagueTeamId)
        {
            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Capitalised(round)} tie {tieNumber} has legs {firstLeg.Id} and {secondLeg.Id} that are not ")
                + "played with the home and away clubs swapped. Delete both legs and the next tick writes the tie "
                + "again; if the next round is already written, reset the cup's knockout matches from stage "
                + string.Create(CultureInfo.InvariantCulture, $"{stage} instead."));
        }

        string tieName = string.Create(
            CultureInfo.InvariantCulture,
            $"{ContinentalKnockoutStages.NameOf(round)} tie {tieNumber}");

        int winner = ContinentalTieWinner.OfTwoLegs(firstLeg, secondLeg, tieName);

        winners.Add(new ContinentalWinner(round, tieNumber, winner));
    }

    /// <summary>
    /// Finds the match that plays one leg of a two-legged tie.
    /// </summary>
    /// <param name="matches">The tie's matches.</param>
    /// <param name="leg">The leg to find, 1 or 2.</param>
    /// <param name="round">The round of the tie.</param>
    /// <param name="tieNumber">The 1-based number of the tie within its round.</param>
    /// <returns>The match, or null when the tie has no match for that leg.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when the tie has two matches for the leg.</exception>
    private static SeasonMatch? LegOf(List<SeasonMatch> matches, byte leg, ContinentalRound round, int tieNumber)
    {
        List<SeasonMatch> ofLeg = matches.Where(match => match.Leg == leg).ToList();

        if (ofLeg.Count > 1)
        {
            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Capitalised(round)} tie {tieNumber} has two matches for leg {leg}, {ofLeg[0].Id} and ")
                + string.Create(CultureInfo.InvariantCulture, $"{ofLeg[1].Id}. Delete one of them."));
        }

        return ofLeg.FirstOrDefault();
    }

    /// <summary>
    /// Builds the refusal for a two-legged tie that has only one of its legs.
    /// </summary>
    /// <param name="round">The round of the tie.</param>
    /// <param name="stage">The stage of the round.</param>
    /// <param name="tieNumber">The 1-based number of the tie within its round.</param>
    /// <param name="firstLeg">The tie's first leg, or null when it has none.</param>
    /// <param name="secondLeg">The tie's second leg, or null when it has none.</param>
    /// <returns>The refusal, ready to throw.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the tie has neither leg, which the checked matches rule out.
    /// </exception>
    private static SeasonRefusedException MissingLeg(
        ContinentalRound round,
        byte stage,
        int tieNumber,
        SeasonMatch? firstLeg,
        SeasonMatch? secondLeg)
    {
        SeasonMatch present = firstLeg ?? secondLeg
            ?? throw new InvalidOperationException("A tie with matches has neither leg 1 nor leg 2.");
        int legNumber = firstLeg is null ? 2 : 1;

        return new SeasonRefusedException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{Capitalised(round)} tie {tieNumber} has only leg {legNumber} (match {present.Id}): its other leg ")
            + "is missing. Delete it as well and the next tick writes the tie again; if the next round is already "
            + string.Create(
                CultureInfo.InvariantCulture,
                $"written, reset the cup's knockout matches from stage {stage} instead."));
    }

    /// <summary>
    /// Finds the known tie that a match plays: the one with the same two clubs, whichever is at home.
    /// </summary>
    /// <param name="round">The round of the match.</param>
    /// <param name="stage">The stage of the round.</param>
    /// <param name="match">The knockout match.</param>
    /// <param name="knownAll">The known ties of every round, given the winners so far.</param>
    /// <param name="written">The ties of the rounds before that are written.</param>
    /// <param name="winners">The ties of the rounds before that are decided.</param>
    /// <returns>The tie.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when the match is not a known tie of its round.</exception>
    private static ContinentalKnownTie FindTie(
        ContinentalRound round,
        byte stage,
        SeasonMatch match,
        IReadOnlyList<ContinentalKnownTie> knownAll,
        List<ContinentalWrittenTie> written,
        List<ContinentalWinner> winners)
    {
        int home = match.HomeLeagueTeamId!.Value;
        int away = match.AwayLeagueTeamId!.Value;

        ContinentalKnownTie? tie = knownAll.FirstOrDefault(candidate =>
            candidate.Round == round
            && ((candidate.HostLeagueTeamId == home && candidate.OtherLeagueTeamId == away)
                || (candidate.HostLeagueTeamId == away && candidate.OtherLeagueTeamId == home)));

        if (tie is not null)
        {
            return tie;
        }

        // A tie of the round before that is written but no longer decided is the likely reason this match fits
        // no tie.
        ContinentalKnownTie? feeder = knownAll.FirstOrDefault(candidate =>
            candidate.Round == round - 1
            && written.Any(done => done.Round == candidate.Round && done.TieNumber == candidate.TieNumber)
            && !winners.Any(won => won.Round == candidate.Round && won.TieNumber == candidate.TieNumber)
            && (candidate.HostLeagueTeamId == home
                || candidate.HostLeagueTeamId == away
                || candidate.OtherLeagueTeamId == home
                || candidate.OtherLeagueTeamId == away));

        string roundName = ContinentalKnockoutStages.NameOf(round);

        if (feeder is not null)
        {
            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Match {match.Id} ({roundName}) is written, but {ContinentalKnockoutStages.NameOf(feeder.Round)} ")
                + string.Create(
                    CultureInfo.InvariantCulture,
                    $"tie {feeder.TieNumber} that leads to it is no longer decided. Finish that tie again with its ")
                + string.Create(
                    CultureInfo.InvariantCulture,
                    $"result, or reset the cup's knockout matches from stage {stage}."));
        }

        throw new SeasonRefusedException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Match {match.Id} ({roundName}, club {home} v club {away}) is not a tie of the bracket as the ")
            + "league-phase table and the earlier results stand. Reset the cup's knockout matches from stage "
            + string.Create(CultureInfo.InvariantCulture, $"{stage} (the readme's knockout reset)."));
    }

    /// <summary>
    /// Gives the name of a round with a capital first letter, for the start of a sentence.
    /// </summary>
    /// <param name="round">The round.</param>
    /// <returns>The round's name, such as "Quarter-final".</returns>
    private static string Capitalised(ContinentalRound round)
    {
        string name = ContinentalKnockoutStages.NameOf(round);

        return char.ToUpperInvariant(name[0]) + name[1..];
    }
}
