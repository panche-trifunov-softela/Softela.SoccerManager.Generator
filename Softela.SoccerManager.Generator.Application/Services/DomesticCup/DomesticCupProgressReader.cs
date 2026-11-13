using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Services.Knockout;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.DomesticCup;

/// <summary>
/// Matches the written cup matches to the ties of the draw, round by round, to find which ties are decided.
/// </summary>
internal static class DomesticCupProgressReader
{
    /// <summary>
    /// Matches the cup's matches to the ties of the draw and collects the winners. A round's ties are those whose clubs
    /// are known from the winners of the rounds before it.
    /// </summary>
    /// <param name="bracket">The draw.</param>
    /// <param name="cupMatches">The cup's matches, in kickoff order; they have been checked.</param>
    /// <param name="roundCount">The number of rounds.</param>
    /// <returns>The winners, the written ties and the champion.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when a match is not a tie of the draw, a tie has two matches, or a finished match has no winner.
    /// </exception>
    public static DomesticCupProgress Read(KnockoutBracket bracket, List<SeasonMatch> cupMatches, int roundCount)
    {
        List<KnockoutWinner> winners = new();
        List<DomesticCupWrittenTie> written = new();

        for (int round = 1; round <= roundCount; round++)
        {
            List<KnockoutKnownTie> known = DomesticCupReader.KnownTiesOf(bracket, winners)
                .Where(tie => tie.RoundNumber == round)
                .ToList();

            Dictionary<int, SeasonMatch> matchOfTie = new();

            foreach (SeasonMatch match in cupMatches.Where(cupMatch => cupMatch.RoundNumber == round))
            {
                int home = match.HomeLeagueTeamId!.Value;
                int away = match.AwayLeagueTeamId!.Value;

                KnockoutKnownTie tie = FindTie(known, match, home, away);

                if (matchOfTie.TryGetValue(tie.TieNumber, out SeasonMatch? earlier))
                {
                    throw new SeasonRefusedException(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"Round {round}, tie {tie.TieNumber} of the cup has two matches, {earlier.Id} and ")
                        + string.Create(CultureInfo.InvariantCulture, $"{match.Id}. Delete one of them."));
                }

                matchOfTie.Add(tie.TieNumber, match);
                written.Add(new DomesticCupWrittenTie(round, tie.TieNumber, match.Id, match.Kickoff));

                if (match.IsFinished)
                {
                    winners.Add(new KnockoutWinner(round, tie.TieNumber, WinnerOf(match)));
                }
            }
        }

        List<KnockoutWinner> orderedWinners = winners
            .OrderBy(winner => winner.RoundNumber)
            .ThenBy(winner => winner.TieNumber)
            .ToList();

        int? champion = orderedWinners
            .FirstOrDefault(winner => winner.RoundNumber == roundCount && winner.TieNumber == 1)
            ?.LeagueTeamId;

        return new DomesticCupProgress(
            orderedWinners,
            written.OrderBy(tie => tie.RoundNumber).ThenBy(tie => tie.TieNumber).ToList(),
            champion);
    }

    /// <summary>
    /// Finds the known tie that a match plays: the one with the same two clubs, whichever is at home.
    /// </summary>
    /// <param name="known">The known ties of the match's round.</param>
    /// <param name="match">The cup match.</param>
    /// <param name="home">The league team id of the match's home club.</param>
    /// <param name="away">The league team id of the match's away club.</param>
    /// <returns>The tie.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when the match is not a known tie of its round.</exception>
    private static KnockoutKnownTie FindTie(List<KnockoutKnownTie> known, SeasonMatch match, int home, int away)
    {
        KnockoutKnownTie? tie = known.FirstOrDefault(candidate =>
            (candidate.FirstLeagueTeamId == home && candidate.SecondLeagueTeamId == away)
            || (candidate.FirstLeagueTeamId == away && candidate.SecondLeagueTeamId == home));

        return tie ?? throw new SeasonRefusedException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Cup match {match.Id} (round {match.RoundNumber}, club {home} v club {away}) is not a tie of the ")
            + "draw as its earlier results stand. Remove it with the cup-only reset.");
    }

    /// <summary>
    /// Gives the winner of a finished cup match.
    /// </summary>
    /// <param name="match">The finished match.</param>
    /// <returns>The league team id of the winner.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when the goals are level and no shootout decided it.</exception>
    private static int WinnerOf(SeasonMatch match)
    {
        return match.WinnerLeagueTeamId ?? throw new SeasonRefusedException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Cup match {match.Id} is finished but has no winner: its goals are level and no shootout ")
            + "decided it. Fix its result.");
    }
}
