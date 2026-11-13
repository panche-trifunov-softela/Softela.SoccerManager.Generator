using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Core.Randomness;
using Softela.SoccerManager.Generator.Application.Services.Knockout;

namespace Softela.SoccerManager.Generator.Application.Services.DomesticCup;

/// <summary>
/// Draws a cup that has no match yet: it chooses the bye clubs by division and squad strength and builds the bracket.
/// </summary>
internal static class DomesticCupDrawer
{
    /// <summary>
    /// Chooses the bye clubs and builds the bracket. The bye order is the division order, top division first, and then
    /// squad strength, strongest first.
    /// </summary>
    /// <param name="reading">The reading of a cup that has no match.</param>
    /// <param name="strengthOf">The squad strength by league team id; a duplicated club holds null.</param>
    /// <param name="roundCount">The number of rounds the calendar has for the cup.</param>
    /// <returns>The bracket and the bye clubs.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when a strength is missing or the library refuses the clubs.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the draw does not have the calendar's number of rounds, which is a bug.
    /// </exception>
    public static (KnockoutBracket Bracket, IReadOnlyList<int> Byes) Draw(
        DomesticCupReading reading,
        Dictionary<int, SquadStrength?> strengthOf,
        int roundCount)
    {
        SeededRandom random = DomesticCupReader.CreateDrawRandom(reading.SeasonId);
        List<int> entrantIds = reading.Entrants.Select(entrant => entrant.LeagueTeamId).ToList();

        List<List<SquadStrength>> squadsByDivision = reading.Entrants
            .GroupBy(entrant => entrant.DivisionRank)
            .OrderBy(division => division.Key)
            .Select(division => division
                .Select(entrant => DomesticCupPlanIndexes.StrengthOf(strengthOf, entrant.LeagueTeamId))
                .ToList())
            .ToList();

        List<int> byeOrder = new();
        KnockoutBracket bracket;
        IReadOnlyList<int> byes;

        try
        {
            foreach (List<SquadStrength> squads in squadsByDivision)
            {
                byeOrder.AddRange(SquadStrengthRanking.StrongestFirst(squads).Select(squad => squad.LeagueTeamId));
            }

            byes = DomesticCupBracketBuilder.ChooseByes(byeOrder);
            bracket = DomesticCupBracketBuilder.Build(entrantIds, byes, random);
        }
        catch (ArgumentException exception) when (exception is not ArgumentNullException
            and not ArgumentOutOfRangeException)
        {
            throw DomesticCupReader.DrawRefused(exception);
        }

        if (bracket.Rounds.Count != roundCount)
        {
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The draw has {bracket.Rounds.Count} rounds but the calendar has {roundCount}."));
        }

        return (bracket, byes);
    }
}
