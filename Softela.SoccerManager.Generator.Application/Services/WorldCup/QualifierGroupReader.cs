using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// Reads the World Cup qualifier groups back from the national competition's written matches: which nations play in
/// each group, which confederation each group is in and how many of the matches are finished. Nothing is stored about
/// a group apart from its matches, so every tick reads it again.
/// </summary>
internal static class QualifierGroupReader
{
    /// <summary>
    /// The fewest nations a qualifier group has.
    /// </summary>
    private const int MinNations = 2;

    /// <summary>
    /// The most nations a qualifier group has.
    /// </summary>
    private const int MaxNations = 5;

    /// <summary>
    /// The number of nations in the one group that plays each pair once.
    /// </summary>
    private const int SingleLegSize = 5;

    /// <summary>
    /// The number of times each pair of a group of 2, 3 or 4 meets: at home and away.
    /// </summary>
    private const int DoubleLegs = 2;

    /// <summary>
    /// The number of times each pair of a group of 5 meets.
    /// </summary>
    private const int SingleLeg = 1;

    /// <summary>
    /// The fix for a qualifier match that cannot be read.
    /// </summary>
    private const string FixMatch =
        "Restore the match, or reset the World Cup (the readme's national reset).";

    /// <summary>
    /// The fix for a nation whose confederation cannot be read.
    /// </summary>
    private const string FixConfederation =
        "Restore the nation's confederation, or reset the World Cup (the readme's national reset).";

    /// <summary>
    /// Reads the groups. Nothing depends on the order of the matches given. The result promises that every group has 2
    /// to 5 nations of one confederation, that no nation is in two groups, and that every pair of a group met exactly
    /// as often as the group's number of legs says.
    /// </summary>
    /// <param name="qualifierMatches">
    /// The qualifier matches of the season, which are the national competition's matches of stage 10, with their
    /// scores, in any order.
    /// </param>
    /// <param name="roster">The national teams on the league's roster, with their roster entries.</param>
    /// <param name="confederations">Every national team with its confederation.</param>
    /// <returns>The groups in ascending order of group number, and how many matches are finished.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a national team is on the roster or in the confederations read twice.
    /// </exception>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the matches cannot be read as qualifier groups: a match has no group number or lacks a side, a group
    /// has too few or too many nations, a nation is in two groups, a pair did not meet as often as it should, a nation
    /// is not on the roster, or the nations of a group have no one confederation.
    /// </exception>
    public static QualifierGroupReading Read(
        IReadOnlyList<SeasonMatch> qualifierMatches,
        IReadOnlyList<GetLeagueNationalTeamsResult> roster,
        IReadOnlyList<GetNationalTeamConfederationsResult> confederations)
    {
        ArgumentNullException.ThrowIfNull(qualifierMatches);
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(confederations);

        CheckMatches(qualifierMatches);

        Dictionary<int, int> nationalTeamOf = IndexRoster(roster);
        Dictionary<int, byte?> confederationOf = IndexConfederations(confederations);

        Dictionary<int, byte> groupOfNation = new();
        List<WrittenQualifierGroup> groups = [];

        IEnumerable<IGrouping<byte, SeasonMatch>> written = qualifierMatches
            .GroupBy(match => match.GroupNumber!.Value)
            .OrderBy(group => group.Key);

        foreach (IGrouping<byte, SeasonMatch> group in written)
        {
            List<SeasonMatch> matches = group.OrderBy(match => match.Id).ToList();

            groups.Add(ReadGroup(group.Key, matches, nationalTeamOf, confederationOf, groupOfNation));
        }

        return new QualifierGroupReading(
            groups,
            qualifierMatches.Count(match => match.IsFinished),
            qualifierMatches.Count);
    }

    /// <summary>
    /// Checks that every match has a group number and both nations, and that no match is a nation against itself.
    /// </summary>
    /// <param name="qualifierMatches">The qualifier matches.</param>
    /// <exception cref="SeasonRefusedException">Thrown when a match cannot be read.</exception>
    private static void CheckMatches(IReadOnlyList<SeasonMatch> qualifierMatches)
    {
        foreach (SeasonMatch match in qualifierMatches.OrderBy(qualifierMatch => qualifierMatch.Id))
        {
            if (match.GroupNumber is null)
            {
                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"World Cup qualifier match {match.Id} has no group number, so its group cannot be read. ")
                    + FixMatch);
            }

            WorldCupGroupChecks.CheckSides(match, "World Cup qualifier match", FixMatch);
        }
    }

    /// <summary>
    /// Indexes the national team of each roster entry.
    /// </summary>
    /// <param name="roster">The national teams on the league's roster.</param>
    /// <returns>The id of the national team, by the id of the roster entry.</returns>
    /// <exception cref="ArgumentException">Thrown when a roster entry is listed twice.</exception>
    private static Dictionary<int, int> IndexRoster(IReadOnlyList<GetLeagueNationalTeamsResult> roster)
    {
        Dictionary<int, int> nationalTeamOf = new();

        foreach (GetLeagueNationalTeamsResult entry in roster)
        {
            if (!nationalTeamOf.TryAdd(entry.Id, entry.NationalTeamId))
            {
                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"Roster entry {entry.Id} is on the roster twice."),
                    nameof(roster));
            }
        }

        return nationalTeamOf;
    }

    /// <summary>
    /// Indexes the confederation code of each national team.
    /// </summary>
    /// <param name="confederations">Every national team with its confederation.</param>
    /// <returns>The backend's confederation code, or null for none, by national team id.</returns>
    /// <exception cref="ArgumentException">Thrown when a national team is in the read twice.</exception>
    private static Dictionary<int, byte?> IndexConfederations(
        IReadOnlyList<GetNationalTeamConfederationsResult> confederations)
    {
        Dictionary<int, byte?> confederationOf = new();

        foreach (GetNationalTeamConfederationsResult nation in confederations)
        {
            if (!confederationOf.TryAdd(nation.Id, nation.Confederation))
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"National team {nation.Id} is in the confederations read twice."),
                    nameof(confederations));
            }
        }

        return confederationOf;
    }

    /// <summary>
    /// Reads one group from its matches.
    /// </summary>
    /// <param name="groupNumber">The number of the group.</param>
    /// <param name="matches">The group's matches, which have been checked for a group number and both nations.</param>
    /// <param name="nationalTeamOf">The id of the national team, by the id of the roster entry.</param>
    /// <param name="confederationOf">The backend's confederation code, by national team id.</param>
    /// <param name="groupOfNation">The group of each nation read so far; this group's nations are added.</param>
    /// <returns>The group.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the matches cannot be read as one qualifier group.
    /// </exception>
    private static WrittenQualifierGroup ReadGroup(
        byte groupNumber,
        List<SeasonMatch> matches,
        Dictionary<int, int> nationalTeamOf,
        Dictionary<int, byte?> confederationOf,
        Dictionary<int, byte> groupOfNation)
    {
        List<int> nations = WorldCupGroupChecks.NationsOf(matches);

        CheckSize(groupNumber, nations);

        WorldCupGroupChecks.AddToGroup(groupOfNation, nations, groupNumber, "qualifier", FixMatch);

        int legs = nations.Count == SingleLegSize ? SingleLeg : DoubleLegs;

        WorldCupGroupChecks.CheckMeetings(
            groupNumber,
            nations,
            matches,
            legs,
            "qualifier",
            string.Create(CultureInfo.InvariantCulture, $"a group of {nations.Count} plays each pair {legs} times"),
            FixMatch);

        Confederation confederation = ConfederationOf(groupNumber, nations, nationalTeamOf, confederationOf);

        return new WrittenQualifierGroup(groupNumber, confederation, nations, legs, matches);
    }

    /// <summary>
    /// Checks that a group has 2 to 5 nations.
    /// </summary>
    /// <param name="groupNumber">The number of the group.</param>
    /// <param name="nations">The league team ids of the group's nations.</param>
    /// <exception cref="SeasonRefusedException">Thrown when the group has too few or too many nations.</exception>
    private static void CheckSize(byte groupNumber, List<int> nations)
    {
        if (nations.Count >= MinNations && nations.Count <= MaxNations)
        {
            return;
        }

        throw new SeasonRefusedException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"World Cup qualifier group {groupNumber} has {nations.Count} nations, but a group has {MinNations} ")
            + string.Create(CultureInfo.InvariantCulture, $"to {MaxNations}. ")
            + FixMatch);
    }

    /// <summary>
    /// Finds the one confederation of a group's nations.
    /// </summary>
    /// <param name="groupNumber">The number of the group.</param>
    /// <param name="nations">The league team ids of the group's nations.</param>
    /// <param name="nationalTeamOf">The id of the national team, by the id of the roster entry.</param>
    /// <param name="confederationOf">The backend's confederation code, by national team id.</param>
    /// <returns>The confederation every nation of the group belongs to.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when a nation is not on the roster, has no confederation or one that is not of the six, or the nations
    /// belong to different confederations.
    /// </exception>
    private static Confederation ConfederationOf(
        byte groupNumber,
        List<int> nations,
        Dictionary<int, int> nationalTeamOf,
        Dictionary<int, byte?> confederationOf)
    {
        List<Confederation> found = [];

        foreach (int nation in nations)
        {
            if (!nationalTeamOf.TryGetValue(nation, out int nationalTeamId))
            {
                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"League team {nation} plays in qualifier group {groupNumber} but is not on the league's ")
                    + "roster of national teams. "
                    + FixMatch);
            }

            if (!confederationOf.TryGetValue(nationalTeamId, out byte? code) || code is not { } value)
            {
                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"National team {nationalTeamId} (league team {nation}) plays in qualifier group ")
                    + string.Create(
                        CultureInfo.InvariantCulture,
                        $"{groupNumber} but has no confederation. ")
                    + FixConfederation);
            }

            if (!Enum.IsDefined((Confederation)value))
            {
                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"National team {nationalTeamId} (league team {nation}) has confederation {value}, which ")
                    + "is not one of the six confederations. "
                    + FixConfederation);
            }

            found.Add((Confederation)value);
        }

        List<Confederation> distinct = found.Distinct().Order().ToList();

        if (distinct.Count > 1)
        {
            string labels = string.Join(" and ", distinct.Select(ConfederationLabel.Of));

            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The nations of qualifier group {groupNumber} belong to different confederations ({labels}). ")
                + FixConfederation);
        }

        return distinct[0];
    }
}
