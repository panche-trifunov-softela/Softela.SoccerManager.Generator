using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Repositories;
using Softela.SoccerManager.Generator.Application.Services.DomesticCup;
using Softela.SoccerManager.Generator.Application.Services.Playoffs;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// Reads a league's previous season for the continental cups of the season after it: the final tables of its divisions
/// and its domestic cup's winner. It never refuses: a previous season whose tables cannot be read is reported as not
/// usable, with the reason, and the cups fall back to squad strength. A domestic cup that cannot be read costs only its
/// winner. It writes nothing.
/// </summary>
internal sealed class PreviousSeasonReader
{
    /// <summary>
    /// The fix that a refusal about the league's matches or the Standings' structure offers besides fixing it, after
    /// the word "or". The previous season is over, so it is never reset; left as it is, its tables or its cup winner go
    /// unused.
    /// </summary>
    private const string OtherFix = "leave the season as it is";

    /// <summary>
    /// The fix, as a sentence, that a refusal of a table's ranking ends with. The previous season is over, so it is
    /// never reset; left as it is, its entrants come from squad strength.
    /// </summary>
    private const string RankFix = "Fix the matches, or leave the season as it is.";

    private readonly IMatchRepository _matchRepository;
    private readonly IStandingRepository _standingRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreviousSeasonReader"/> class.
    /// </summary>
    /// <param name="matchRepository">The repository that reads a season's matches and scores.</param>
    /// <param name="standingRepository">The repository that reads the divisions' Standings.</param>
    public PreviousSeasonReader(IMatchRepository matchRepository, IStandingRepository standingRepository)
    {
        _matchRepository = matchRepository;
        _standingRepository = standingRepository;
    }

    /// <summary>
    /// Reads the previous season: the one of the league with the highest number below the season handled, which is not
    /// always the number before it, as numbers can skip. When it has one, the divisions' tables are ranked from its
    /// matches and its domestic cup's winner is read.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <returns>
    /// A task whose result is the previous season's reading: none when there is no earlier season, not usable with the
    /// reason when its tables cannot be read, and otherwise its tables and its cup winner.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public async Task<PreviousSeasonReading> ReadAsync(LeagueStepContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        GetSeasonsResult? previous = context.Seasons
            .Where(season => season.SeasonNumber < context.Season.SeasonNumber)
            .MaxBy(season => season.SeasonNumber);

        if (previous is null)
        {
            return new PreviousSeasonReading(PreviousSeasonStatus.None, null, [], null, null);
        }

        try
        {
            return await ReadSeasonAsync(context, previous);
        }
        catch (SeasonRefusedException refusal)
        {
            return NotUsable(previous, refusal.Message);
        }
        catch (ArgumentException exception) when (exception is not ArgumentNullException
            and not ArgumentOutOfRangeException)
        {
            return NotUsable(previous, DomesticCupReader.ReasonOf(exception));
        }
    }

    /// <summary>
    /// Reads the previous season's matches and Standings and ranks the divisions' tables. A division with no Standings
    /// rows in that season is left out of the tables, and its clubs come after the others, in the order the pyramid has
    /// without tables.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="previous">The previous season.</param>
    /// <returns>
    /// A task whose result is the reading: usable, with the cup winner or else the reason the cup cannot be read, or
    /// not usable with the reason.
    /// </returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the season's matches or Standings cannot be read as tables, for example when a league match lacks a
    /// club or a club is in two divisions.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when a helper refuses what the season holds.</exception>
    private async Task<PreviousSeasonReading> ReadSeasonAsync(LeagueStepContext context, GetSeasonsResult previous)
    {
        IReadOnlyList<GetMatchesBySeasonResult> matches = await _matchRepository.GetBySeasonIdAsync(previous.Id);

        IReadOnlyList<GetMatchScoresBySeasonResult> scores =
            await _matchRepository.GetScoresBySeasonIdAsync(previous.Id);

        IReadOnlyList<SeasonMatch> season = SeasonMatchJoiner.Join(matches, scores);
        List<int> divisionIds = context.Divisions.Select(division => division.Id).ToList();
        List<SeasonMatch> leagueMatches = DomesticCupLeagueReader.LeagueMatchesOf(season, divisionIds);

        if (leagueMatches.Count == 0)
        {
            return NotUsable(
                previous,
                string.Create(CultureInfo.InvariantCulture, $"season {previous.SeasonNumber} has no league matches"));
        }

        // A league match without both clubs, or a club in two divisions, is refused here so it becomes the reason the
        // tables cannot be used rather than an error that ends the run.
        _ = DomesticCupLeagueReader.MapClubsToDivisions(leagueMatches, OtherFix);

        List<int> tableOrder = [];

        foreach (GetCompetitionsResult division in context.Divisions)
        {
            IReadOnlyList<GetStandingsResult> rows =
                await _standingRepository.GetByCompetitionAndSeasonAsync(division.Id, previous.Id);

            if (rows.Count == 0)
            {
                continue;
            }

            List<SeasonMatch> divisionMatches = leagueMatches
                .Where(match => match.CompetitionId == division.Id)
                .ToList();

            int finished = divisionMatches.Count(match => match.IsFinished);

            if (finished < divisionMatches.Count)
            {
                return NotUsable(
                    previous,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{finished} of {divisionMatches.Count} league matches of '{division.Name}' are finished"));
            }

            tableOrder.AddRange(RankDivision(previous, division, rows, divisionMatches));
        }

        if (tableOrder.Count == 0)
        {
            return NotUsable(
                previous,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"season {previous.SeasonNumber} has no Standings rows in any division"));
        }

        // The cup is read with the divisions that played the season, as the cup step read it then; a tier added since
        // has no league matches in it.
        List<int> playedDivisionIds = divisionIds
            .Where(id => leagueMatches.Any(match => match.CompetitionId == id))
            .ToList();

        (int? cupWinner, string? cupProblem) = ReadCupWinner(context, previous, playedDivisionIds, season);

        return new PreviousSeasonReading(PreviousSeasonStatus.Usable, previous, tableOrder, cupWinner, cupProblem);
    }

    /// <summary>
    /// Checks that a division's Standings describe its matches, and ranks its table from the matches, with the seed of
    /// the season that the rollover moved the clubs by.
    /// </summary>
    /// <param name="previous">The previous season.</param>
    /// <param name="division">The division.</param>
    /// <param name="rows">The division's Standings rows in the previous season.</param>
    /// <param name="divisionMatches">The division's league matches in the previous season, all finished.</param>
    /// <returns>The league team ids of the division's clubs in place order, place 1 first.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the Standings disagree with the matches, or the ranking library refuses the matches.
    /// </exception>
    private static List<int> RankDivision(
        GetSeasonsResult previous,
        GetCompetitionsResult division,
        IReadOnlyList<GetStandingsResult> rows,
        IReadOnlyList<SeasonMatch> divisionMatches)
    {
        PlayoffTableCheck.CheckStructure(division, rows, divisionMatches, OtherFix);

        return SeasonTableRanker.RankDivision(
            previous.Id,
            PlayoffTableCheck.DivisionOf(division),
            rows,
            divisionMatches,
            RankFix);
    }

    /// <summary>
    /// Reads the winner of the previous season's domestic cup. The league has a winner to give only when exactly one
    /// competition has the domestic cup's shape and its final was played. A cup that cannot be read costs only its
    /// winner: its reason is handed back, and the tables stay usable.
    /// </summary>
    /// <param name="context">The league, its newest season and what the tick read at its start.</param>
    /// <param name="previous">The previous season.</param>
    /// <param name="divisionIds">The competition ids of the divisions that played the season, top first.</param>
    /// <param name="season">All the matches of the previous season with their scores.</param>
    /// <returns>
    /// The league team id of the cup's winner, or null when there is none; and why the cup cannot be read, or null when
    /// it can.
    /// </returns>
    private static (int? Winner, string? Problem) ReadCupWinner(
        LeagueStepContext context,
        GetSeasonsResult previous,
        IReadOnlyList<int> divisionIds,
        IReadOnlyList<SeasonMatch> season)
    {
        IReadOnlyList<GetCompetitionsResult> cups = DomesticCupFinder.Find(context.Competitions);

        if (cups.Count != 1)
        {
            return (null, null);
        }

        try
        {
            DomesticCupReading reading = DomesticCupReader.Read(previous.Id, cups[0].Id, divisionIds, season, OtherFix);

            int? winner = reading.Status == DomesticCupReadingStatus.Started
                ? reading.Progress?.ChampionLeagueTeamId
                : null;

            return (winner, null);
        }
        catch (SeasonRefusedException refusal)
        {
            return (null, refusal.Message);
        }
        catch (ArgumentException exception) when (exception is not ArgumentNullException
            and not ArgumentOutOfRangeException)
        {
            return (null, DomesticCupReader.ReasonOf(exception));
        }
    }

    /// <summary>
    /// Builds the reading of a previous season whose tables cannot be used.
    /// </summary>
    /// <param name="previous">The previous season.</param>
    /// <param name="reason">Why its tables cannot be used.</param>
    /// <returns>The reading, with no tables and no cup winner.</returns>
    private static PreviousSeasonReading NotUsable(GetSeasonsResult previous, string reason)
    {
        return new PreviousSeasonReading(PreviousSeasonStatus.NotUsable, previous, [], null, reason);
    }
}
