using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Services.ContinentalCup;
using Softela.SoccerManager.Generator.Application.Services.Seasons;
using Softela.SoccerManager.Generator.Application.Services.WorldCup;

namespace Softela.SoccerManager.Generator.Application.Services.Rollover;

/// <summary>
/// Reads the international side of a season's end, for a league whose divisions have 12 clubs or more. A season that
/// plays a World Cup, which is when the league's national competition has a match in it, ends on the World Cup final,
/// and its continental matches never hold it. A season that plays no World Cup ends on the finals of the continental
/// cups that have a match in it, so the knockouts of the cups are not cut off.
/// </summary>
internal static class InternationalSeasonEnd
{
    /// <summary>
    /// The <see cref="MatchStage.WorldCupFinal"/> code as the reader compares it with <see cref="SeasonMatch.Stage"/>.
    /// </summary>
    private const byte WorldCupFinalStage = (byte)MatchStage.WorldCupFinal;

    /// <summary>
    /// The <see cref="MatchStage.ContinentalFinal"/> code as the reader compares it with
    /// <see cref="SeasonMatch.Stage"/>.
    /// </summary>
    private const byte ContinentalFinalStage = (byte)MatchStage.ContinentalFinal;

    /// <summary>
    /// Reads whether the international finals that hold the season are finished. Nothing depends on the order of the
    /// competitions or matches given. The result promises:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// With a national competition that has a match in the season, only its World Cup final counts: it waits while the
    /// final is not written or not finished, and the season may end once the final is finished with a winner.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Otherwise the final of every continental cup that has a match in the season counts, in competition id order, and
    /// the first one not finished is what the season waits for. A cup with no match in the season is ignored.
    /// </description>
    /// </item>
    /// </list>
    /// A final that is finished with level goals and no shootout winner, a final that lacks a club, a second final of
    /// one competition and a second national competition are refused.
    /// </summary>
    /// <param name="competitions">The league's competitions.</param>
    /// <param name="season">All the matches of the season with their scores, in any order.</param>
    /// <returns>What the first final not finished is, or the finals that decided; empty while it waits.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the league has more than one national competition, a competition has more than one final, a final
    /// lacks a home or an away club, or a final is finished without a winner.
    /// </exception>
    internal static InternationalEnd Read(
        IReadOnlyList<GetCompetitionsResult> competitions,
        IReadOnlyList<SeasonMatch> season)
    {
        ArgumentNullException.ThrowIfNull(competitions);
        ArgumentNullException.ThrowIfNull(season);

        IReadOnlyList<GetCompetitionsResult> nationals = NationalCompetitionFinder.Find(competitions);

        if (nationals.Count > 1)
        {
            throw TooManyNationalCompetitions(nationals);
        }

        if (nationals.Count == 1 && season.Any(match => match.CompetitionId == nationals[0].Id))
        {
            return ReadWorldCup(nationals[0], season);
        }

        return ReadContinentalCups(competitions, season);
    }

    /// <summary>
    /// Reads the World Cup final of a season that plays a World Cup.
    /// </summary>
    /// <param name="worldCup">The national competition.</param>
    /// <param name="season">All the matches of the season.</param>
    /// <returns>The wait for the final, or the finished final.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the World Cup has more than one final, its final lacks a home or an away club, or its final is
    /// finished without a winner.
    /// </exception>
    private static InternationalEnd ReadWorldCup(GetCompetitionsResult worldCup, IReadOnlyList<SeasonMatch> season)
    {
        List<SeasonMatch> finals = season
            .Where(match => match.CompetitionId == worldCup.Id && match.Stage == WorldCupFinalStage)
            .OrderBy(match => match.Id)
            .ToList();

        if (finals.Count == 0)
        {
            return Waiting("the World Cup final");
        }

        if (finals.Count > 1)
        {
            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The World Cup has {finals.Count} finals (matches {IdsOf(finals)}). Delete the extra one."));
        }

        SeasonMatch final = finals[0];

        if (!final.IsFinished)
        {
            return Waiting(string.Create(CultureInfo.InvariantCulture, $"the World Cup final (match {final.Id})"));
        }

        if (final.HomeLeagueTeamId is null || final.AwayLeagueTeamId is null)
        {
            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"World Cup final {final.Id} has no home or no away club, so it lacks a statistics row ")
                + "for one side. Fix its statistics rows.");
        }

        if (final.WinnerLeagueTeamId is null)
        {
            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"World Cup final {final.Id} is finished but has no winner: its goals are level and no shootout ")
                + "decided it. Fix its result.");
        }

        return new InternationalEnd(null, [final]);
    }

    /// <summary>
    /// Reads the finals of the continental cups of a season that plays no World Cup. Every cup is read, so a refusal
    /// is found even after a wait, and the first wait found is the one returned.
    /// </summary>
    /// <param name="competitions">The league's competitions.</param>
    /// <param name="season">All the matches of the season.</param>
    /// <returns>The first wait, or the finished finals; empty while it waits.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when a cup has more than one final, a final lacks a home or an away club, or a final is finished without
    /// a winner.
    /// </exception>
    private static InternationalEnd ReadContinentalCups(
        IReadOnlyList<GetCompetitionsResult> competitions,
        IReadOnlyList<SeasonMatch> season)
    {
        string? waitingFor = null;
        List<SeasonMatch> deciding = [];

        foreach (GetCompetitionsResult cup in ContinentalCupFinder.Find(competitions))
        {
            if (!season.Any(match => match.CompetitionId == cup.Id))
            {
                continue;
            }

            List<SeasonMatch> finals = season
                .Where(match => match.CompetitionId == cup.Id && match.Stage == ContinentalFinalStage)
                .OrderBy(match => match.Id)
                .ToList();

            if (finals.Count > 1)
            {
                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"The continental cup '{cup.Name}' (id {cup.Id}) has {finals.Count} finals ")
                    + string.Create(
                        CultureInfo.InvariantCulture,
                        $"(matches {IdsOf(finals)}). Delete the extra one."));
            }

            if (finals.Count == 0)
            {
                waitingFor ??= string.Create(
                    CultureInfo.InvariantCulture,
                    $"the continental final of '{cup.Name}' (id {cup.Id})");

                continue;
            }

            SeasonMatch final = finals[0];

            if (!final.IsFinished)
            {
                waitingFor ??= string.Create(
                    CultureInfo.InvariantCulture,
                    $"the continental final of '{cup.Name}' (match {final.Id})");

                continue;
            }

            if (final.HomeLeagueTeamId is null || final.AwayLeagueTeamId is null)
            {
                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Continental final {final.Id} of '{cup.Name}' has no home or no away club, so it lacks a ")
                    + "statistics row for one side. Fix its statistics rows.");
            }

            if (final.WinnerLeagueTeamId is null)
            {
                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Continental final {final.Id} of '{cup.Name}' is finished but has no winner: its goals ")
                    + "are level and no shootout decided it. Fix its result.");
            }

            deciding.Add(final);
        }

        return waitingFor is null ? new InternationalEnd(null, deciding) : Waiting(waitingFor);
    }

    /// <summary>
    /// Builds the reading of a season that waits for a final.
    /// </summary>
    /// <param name="waitingFor">The phrase that completes "it waits for ...".</param>
    /// <returns>The reading, with no deciding matches.</returns>
    private static InternationalEnd Waiting(string waitingFor)
    {
        return new InternationalEnd(waitingFor, []);
    }

    /// <summary>
    /// Builds the refusal for a league that has more than one national competition.
    /// </summary>
    /// <param name="nationals">The national competitions, at least two, by competition id.</param>
    /// <returns>The refusal, ready to throw.</returns>
    private static SeasonRefusedException TooManyNationalCompetitions(IReadOnlyList<GetCompetitionsResult> nationals)
    {
        string ids = string.Join(
            ", ",
            nationals.Select(competition => competition.Id.ToString(CultureInfo.InvariantCulture)));

        return new SeasonRefusedException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"The league has {nationals.Count} national competitions ({ids}) and needs exactly one, so the ")
            + "World Cup final that ends the season cannot be told apart. Keep one of them.");
    }

    /// <summary>
    /// Writes the ids of matches for a refusal, comma-separated.
    /// </summary>
    /// <param name="matches">The matches, in the order to list them.</param>
    /// <returns>The match ids, comma-separated.</returns>
    private static string IdsOf(IEnumerable<SeasonMatch> matches)
    {
        return string.Join(", ", matches.Select(match => match.Id.ToString(CultureInfo.InvariantCulture)));
    }
}
