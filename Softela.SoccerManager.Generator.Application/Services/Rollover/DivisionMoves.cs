using System.Globalization;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;
using Softela.SoccerManager.Generator.Application.Services.Playoffs;

namespace Softela.SoccerManager.Generator.Application.Services.Rollover;

/// <summary>
/// Moves the clubs between the divisions at the end of a season: the top places of each division go up, the playoff
/// winner takes the last promotion place of a division that has a playoff, and the bottom places go down. A club
/// that moves goes to the division above or below it, and every club starts the next season's Standings at zero.
/// </summary>
internal static class DivisionMoves
{
    /// <summary>
    /// Plans the next season's divisions from the final tables. For division k, counted from 0 at the top:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// the clubs that go up, for k above 0: the first <see cref="PromotionPlaces.Automatic"/> clubs of its table, and
    /// the playoff winner when it has a playoff;
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// the clubs that go down, for k below the last: the last <see cref="PromotionPlaces.Relegated"/> clubs of its
    /// table;
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// its next clubs: its table without the clubs that go up or down, plus the clubs that go down from the division
    /// above it and the clubs that go up from the division below it, in ascending league team id order.
    /// </description>
    /// </item>
    /// </list>
    /// With one division, or with nothing to promote or relegate, nobody moves.
    /// </summary>
    /// <param name="divisionsInOrder">The league's divisions, the top division first.</param>
    /// <param name="places">What each division's settings say about its table, in division order.</param>
    /// <param name="tables">The league team ids of each division in place order, place 1 first.</param>
    /// <param name="playoffWinners">
    /// The league team id of each division's playoff winner; null for a division without a playoff.
    /// </param>
    /// <returns>The next season's divisions and the clubs that moved.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the lists differ in length, a table's length differs from its division's club count, a division's
    /// places do not fit its clubs, a club is in two places, a division that has a playoff has no winner or a winner
    /// that is not one of its playoff places, the top division promotes, the bottom division relegates, or a division
    /// relegates another number of clubs than the one below it promotes. That is a bug in the caller, not a refusal.
    /// </exception>
    internal static RolloverMoves Plan(
        IReadOnlyList<GetCompetitionsResult> divisionsInOrder,
        IReadOnlyList<PromotionPlaces> places,
        IReadOnlyList<IReadOnlyList<int>> tables,
        IReadOnlyList<int?> playoffWinners)
    {
        ArgumentNullException.ThrowIfNull(divisionsInOrder);
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(playoffWinners);

        CheckShape(divisionsInOrder, places, tables, playoffWinners);

        int count = divisionsInOrder.Count;
        List<List<int>> up = [];
        List<List<int>> down = [];

        for (int index = 0; index < count; index++)
        {
            up.Add(index == 0 ? new List<int>() : ClubsGoingUp(places[index], tables[index], playoffWinners[index]));
            down.Add(index == count - 1 ? new List<int>() : tables[index].TakeLast(places[index].Relegated).ToList());
        }

        List<LeagueDivision> divisions = [];

        for (int index = 0; index < count; index++)
        {
            HashSet<int> moved = up[index].Concat(down[index]).ToHashSet();
            List<int> clubs = tables[index].Where(club => !moved.Contains(club)).ToList();

            if (index > 0)
            {
                clubs.AddRange(down[index - 1]);
            }

            if (index < count - 1)
            {
                clubs.AddRange(up[index + 1]);
            }

            clubs.Sort();

            divisions.Add(new LeagueDivision(divisionsInOrder[index].Id, divisionsInOrder[index].Name, clubs));
        }

        List<int> promoted = up.SelectMany(clubs => clubs).ToList();
        List<int> relegated = down.SelectMany(clubs => clubs).ToList();

        return new RolloverMoves(divisions, promoted, relegated);
    }

    /// <summary>
    /// Lists the clubs that go up from a division: its automatic places in place order, and then its playoff winner
    /// when it has a playoff.
    /// </summary>
    /// <param name="places">What the division's settings say about its table.</param>
    /// <param name="table">The division's table, place 1 first.</param>
    /// <param name="playoffWinner">The division's playoff winner; null when it has no playoff.</param>
    /// <returns>The clubs that go up, in place order.</returns>
    private static List<int> ClubsGoingUp(PromotionPlaces places, IReadOnlyList<int> table, int? playoffWinner)
    {
        List<int> clubs = table.Take(places.Automatic).ToList();

        if (places.PlayoffClubs > 0)
        {
            clubs.Add(playoffWinner!.Value);
        }

        return clubs;
    }

    /// <summary>
    /// Checks everything the plan relies on: that the lists agree, that no club is in two places, and that every
    /// division's places, playoff winner and neighbours' counts are ones the plan can move clubs by.
    /// </summary>
    /// <param name="divisionsInOrder">The league's divisions, the top division first.</param>
    /// <param name="places">What each division's settings say about its table.</param>
    /// <param name="tables">The league team ids of each division in place order.</param>
    /// <param name="playoffWinners">The playoff winner of each division, or null.</param>
    /// <exception cref="ArgumentException">Thrown when the lists cannot be planned from.</exception>
    private static void CheckShape(
        IReadOnlyList<GetCompetitionsResult> divisionsInOrder,
        IReadOnlyList<PromotionPlaces> places,
        IReadOnlyList<IReadOnlyList<int>> tables,
        IReadOnlyList<int?> playoffWinners)
    {
        int count = divisionsInOrder.Count;

        if (places.Count != count || tables.Count != count || playoffWinners.Count != count)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"There are {count} divisions, {places.Count} places, {tables.Count} tables and ")
                + string.Create(CultureInfo.InvariantCulture, $"{playoffWinners.Count} playoff winners."),
                nameof(places));
        }

        HashSet<int> seen = new();

        for (int index = 0; index < count; index++)
        {
            foreach (int club in tables[index])
            {
                if (!seen.Add(club))
                {
                    throw new ArgumentException(
                        string.Create(CultureInfo.InvariantCulture, $"Club {club} is in the tables twice."),
                        nameof(tables));
                }
            }

            CheckDivision(index, places, tables[index], playoffWinners[index], count);
        }
    }

    /// <summary>
    /// Checks one division: its table, its places, its playoff winner, and what it may promote and relegate.
    /// </summary>
    /// <param name="index">The division's position, counted from 0 at the top.</param>
    /// <param name="places">What each division's settings say about its table.</param>
    /// <param name="table">The division's table.</param>
    /// <param name="playoffWinner">The division's playoff winner, or null.</param>
    /// <param name="count">The number of divisions.</param>
    /// <exception cref="ArgumentException">Thrown when the division cannot be planned from.</exception>
    private static void CheckDivision(
        int index,
        IReadOnlyList<PromotionPlaces> places,
        IReadOnlyList<int> table,
        int? playoffWinner,
        int count)
    {
        PromotionPlaces division = places[index];

        if (table.Count != division.ClubCount)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The table of division {division.DivisionId} has {table.Count} clubs but its places say ")
                + string.Create(CultureInfo.InvariantCulture, $"{division.ClubCount}."),
                nameof(places));
        }

        if (index == 0 && (division.Promoted > 0 || division.PlayoffClubs > 0))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Division {division.DivisionId} is the top division and cannot promote."),
                nameof(places));
        }

        if (index == count - 1 && division.Relegated > 0)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Division {division.DivisionId} is the bottom division and cannot relegate."),
                nameof(places));
        }

        if (index < count - 1 && division.Relegated != places[index + 1].Promoted)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Division {division.DivisionId} relegates {division.Relegated} clubs but the division below ")
                + string.Create(CultureInfo.InvariantCulture, $"it promotes {places[index + 1].Promoted}."),
                nameof(places));
        }

        int needed = division.PlayoffClubs > 0
            ? division.Automatic + division.PlayoffClubs + division.Relegated
            : division.Automatic + division.Relegated;

        if (division.Automatic < 0 || division.Relegated < 0 || needed > division.ClubCount)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The places of division {division.DivisionId} do not fit its {division.ClubCount} clubs."),
                nameof(places));
        }

        CheckPlayoffWinner(division, table, playoffWinner);
    }

    /// <summary>
    /// Checks that a division with a playoff has a winner and that it is one of the playoff's places.
    /// </summary>
    /// <param name="division">What the division's settings say about its table.</param>
    /// <param name="table">The division's table.</param>
    /// <param name="playoffWinner">The division's playoff winner, or null.</param>
    /// <exception cref="ArgumentException">Thrown when the division has a playoff and the winner is wrong.</exception>
    private static void CheckPlayoffWinner(PromotionPlaces division, IReadOnlyList<int> table, int? playoffWinner)
    {
        if (division.PlayoffClubs == 0)
        {
            return;
        }

        if (playoffWinner is not { } winner)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Division {division.DivisionId} has a playoff but no playoff winner."),
                nameof(playoffWinner));
        }

        int place = IndexOf(table, winner) + 1;
        int lastPlace = division.FirstPlayoffPlace + division.PlayoffClubs - 1;

        if (place >= division.FirstPlayoffPlace && place <= lastPlace)
        {
            return;
        }

        throw new ArgumentException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Club {winner} is not one of the playoff places {division.FirstPlayoffPlace} to {lastPlace} ")
            + string.Create(CultureInfo.InvariantCulture, $"of division {division.DivisionId}."),
            nameof(playoffWinner));
    }

    /// <summary>
    /// Finds the position of a club in a table.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="club">The league team id to look for.</param>
    /// <returns>The 0-based position of the club, or -1 when the table does not have it.</returns>
    private static int IndexOf(IReadOnlyList<int> table, int club)
    {
        for (int index = 0; index < table.Count; index++)
        {
            if (table[index] == club)
            {
                return index;
            }
        }

        return -1;
    }
}
