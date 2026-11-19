using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// The checks that the reader of the qualifier groups and the reader of the finals groups make in the same way: the
/// nations of a group, a match with a missing or a doubled side, a nation in two groups, and a pair that met too often
/// or too rarely. Each reader keeps the checks that only its own groups have.
/// </summary>
internal static class WorldCupGroupChecks
{
    /// <summary>
    /// Lists the nations of a group's matches.
    /// </summary>
    /// <param name="matches">The group's matches, which have been checked for both nations.</param>
    /// <returns>The league team ids of the nations, in ascending order, each once.</returns>
    internal static List<int> NationsOf(IEnumerable<SeasonMatch> matches)
    {
        return matches
            .SelectMany(match => new[] { match.HomeLeagueTeamId!.Value, match.AwayLeagueTeamId!.Value })
            .Distinct()
            .Order()
            .ToList();
    }

    /// <summary>
    /// Checks that a match has both nations and that they are not the same nation.
    /// </summary>
    /// <param name="match">The match.</param>
    /// <param name="matchNoun">The words that name the match in a message, such as "World Cup match".</param>
    /// <param name="fix">The fix that ends the message.</param>
    /// <exception cref="SeasonRefusedException">Thrown when a side is missing or both sides are one nation.</exception>
    internal static void CheckSides(SeasonMatch match, string matchNoun, string fix)
    {
        if (match.HomeLeagueTeamId is not { } home || match.AwayLeagueTeamId is not { } away)
        {
            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{matchNoun} {match.Id} has no home or no away nation, so it lacks exactly one ")
                + "statistics row for each side. "
                + fix);
        }

        if (home == away)
        {
            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{matchNoun} {match.Id} has league team {home} on both sides. ")
                + fix);
        }
    }

    /// <summary>
    /// Adds the nations of a group to the group of each nation, and checks that none of them is in another group.
    /// </summary>
    /// <param name="groupOfNation">The group of each nation read so far; this group's nations are added.</param>
    /// <param name="nations">The league team ids of the group's nations.</param>
    /// <param name="groupNumber">The number of the group.</param>
    /// <param name="groupNoun">The word that names the kind of group in a message, "qualifier" or "finals".</param>
    /// <param name="fix">The fix that ends the message.</param>
    /// <exception cref="SeasonRefusedException">Thrown when a nation is already in another group.</exception>
    internal static void AddToGroup(
        Dictionary<int, byte> groupOfNation,
        IReadOnlyList<int> nations,
        byte groupNumber,
        string groupNoun,
        string fix)
    {
        foreach (int nation in nations)
        {
            if (!groupOfNation.TryAdd(nation, groupNumber))
            {
                byte other = groupOfNation[nation];

                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"League team {nation} plays in {groupNoun} groups {Math.Min(groupNumber, other)} and ")
                    + string.Create(CultureInfo.InvariantCulture, $"{Math.Max(groupNumber, other)}. ")
                    + fix);
            }
        }
    }

    /// <summary>
    /// Checks that every pair of a group met exactly as often as the group's number of legs says.
    /// </summary>
    /// <param name="groupNumber">The number of the group.</param>
    /// <param name="nations">The league team ids of the group's nations, in ascending order.</param>
    /// <param name="matches">The group's matches, which have been checked for both nations.</param>
    /// <param name="legs">The number of times each pair is to meet.</param>
    /// <param name="groupNoun">The word that names the kind of group in a message, "qualifier" or "finals".</param>
    /// <param name="rule">The words that say how often the group plays each pair, which follow "but".</param>
    /// <param name="fix">The fix that ends the message.</param>
    /// <exception cref="SeasonRefusedException">Thrown when a pair met too often or too rarely.</exception>
    internal static void CheckMeetings(
        byte groupNumber,
        IReadOnlyList<int> nations,
        IEnumerable<SeasonMatch> matches,
        int legs,
        string groupNoun,
        string rule,
        string fix)
    {
        Dictionary<(int Lower, int Higher), int> meetings = new();

        foreach (SeasonMatch match in matches)
        {
            int home = match.HomeLeagueTeamId!.Value;
            int away = match.AwayLeagueTeamId!.Value;
            (int Lower, int Higher) pair = (Math.Min(home, away), Math.Max(home, away));

            meetings[pair] = meetings.GetValueOrDefault(pair) + 1;
        }

        for (int first = 0; first < nations.Count; first++)
        {
            for (int second = first + 1; second < nations.Count; second++)
            {
                int count = meetings.GetValueOrDefault((nations[first], nations[second]));

                if (count == legs)
                {
                    continue;
                }

                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"League teams {nations[first]} and {nations[second]} meet {count} times in {groupNoun} ")
                    + string.Create(CultureInfo.InvariantCulture, $"group {groupNumber}, but {rule}. ")
                    + fix);
            }
        }
    }
}
