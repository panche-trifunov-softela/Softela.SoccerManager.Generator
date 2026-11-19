using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Services.DomesticCup;
using Softela.SoccerManager.Generator.Application.Services.Knockout;
using Softela.SoccerManager.Generator.Application.Services.Ranking;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// Reads the World Cup finals back from the national competition's written matches: whether the group matches are
/// finished, what the group tables and the bracket are, which ties are decided and which ties are still to write.
/// Nothing is stored about the finals apart from their matches, so every tick reads them again.
/// </summary>
internal static class FinalsReader
{
    /// <summary>
    /// The number of matchdays of a group: each nation plays every other nation of its group once.
    /// </summary>
    private const int Matchdays = WorldCupFinalsShape.GroupSize - 1;

    /// <summary>
    /// The fix for a group match that cannot be read.
    /// </summary>
    private const string FixGroups = "Restore the match, or reset the World Cup finals (the readme's finals reset).";

    /// <summary>
    /// The fix for a knockout match that cannot be read.
    /// </summary>
    private const string FixKnockout =
        "Delete it, or reset the knockouts from its stage (the readme's knockouts reset).";

    /// <summary>
    /// Reads the finals. Nothing depends on the order of the matches given. The result promises:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// While a group match is unfinished and no knockout match is written, the status is waiting for the groups and
    /// only the count of finished group matches is set.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Once every group match is finished, the status is ready: the group tables are ranked, the bracket is built from
    /// them, and every written knockout match has to be a tie of that bracket, or the third place, as its earlier
    /// results stand.
    /// </description>
    /// </item>
    /// </list>
    /// </summary>
    /// <param name="seasonId">The id of the season, which seeds the group tables' tie-break shuffle.</param>
    /// <param name="finalsMatches">
    /// The national competition's matches of the season with the stages 11 to 16, with their scores, in any order.
    /// </param>
    /// <returns>What the finals' matches say about them.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="finalsMatches"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a match has a stage outside 11 to 16. That is a bug in the caller, not a refusal.
    /// </exception>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the written matches cannot be read as the finals: a match lacks a nation, the group matches are not
    /// 8 groups of 4 nations playing each pair once, a knockout match has a group, round or leg, a group match is
    /// unfinished while the knockout is written, a table is refused, a knockout match is not a tie of the bracket, or a
    /// tie has a wrong number of matches or no winner.
    /// </exception>
    internal static FinalsReading Read(int seasonId, IReadOnlyList<SeasonMatch> finalsMatches)
    {
        ArgumentNullException.ThrowIfNull(finalsMatches);

        CheckMatches(finalsMatches);

        List<SeasonMatch> groupMatches = finalsMatches
            .Where(match => match.Stage == FinalsStages.GroupStage)
            .OrderBy(match => match.Kickoff)
            .ThenBy(match => match.Id)
            .ToList();

        List<SeasonMatch> knockout = finalsMatches
            .Where(match => match.Stage != FinalsStages.GroupStage)
            .OrderBy(match => match.Kickoff)
            .ThenBy(match => match.Id)
            .ToList();

        List<List<SeasonMatch>> groups = ReadGroups(groupMatches);
        List<SeasonMatch> unfinished = groupMatches.Where(match => !match.IsFinished).ToList();

        if (unfinished.Count > 0 && knockout.Count > 0)
        {
            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"World Cup group match {unfinished[0].Id} is not finished, but the knockout matches are ")
                + "already written. Finish it again with its result, or reset the knockouts.");
        }

        if (unfinished.Count > 0)
        {
            return new FinalsReading(
                FinalsReadingStatus.WaitingForGroups,
                groupMatches.Count - unfinished.Count,
                [],
                null,
                [],
                [],
                [],
                null,
                null);
        }

        List<IReadOnlyList<int>> tables = RankGroups(seasonId, groups);
        KnockoutBracket bracket = FinalsBracket.Build(tables);
        FinalsProgress progress = FinalsProgressReader.Read(bracket, knockout);

        HashSet<(int Round, int Tie)> written = progress.WrittenTies
            .Select(tie => (tie.RoundNumber, tie.TieNumber))
            .ToHashSet();

        List<KnockoutKnownTie> newTies = FinalsProgressReader.KnownTiesOf(bracket, progress.Winners)
            .Where(tie => !written.Contains((tie.RoundNumber, tie.TieNumber)))
            .ToList();

        return new FinalsReading(
            FinalsReadingStatus.Ready,
            groupMatches.Count,
            tables,
            bracket,
            progress.WrittenTies,
            progress.Winners,
            newTies,
            progress.ThirdPlace,
            groupMatches.Max(match => match.Kickoff));
    }

    /// <summary>
    /// Checks that every match is one the finals can read: a stage of the finals, both nations known, and the group,
    /// round and leg that its stage has.
    /// </summary>
    /// <param name="finalsMatches">The finals' matches.</param>
    /// <exception cref="ArgumentException">Thrown when a match has a stage outside 11 to 16.</exception>
    /// <exception cref="SeasonRefusedException">Thrown when a match cannot be read.</exception>
    private static void CheckMatches(IReadOnlyList<SeasonMatch> finalsMatches)
    {
        foreach (SeasonMatch match in finalsMatches.OrderBy(finalsMatch => finalsMatch.Id))
        {
            if (match.Stage is not { } stage || !FinalsStages.IsFinalsStage(stage))
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Match {match.Id} has a stage that is not one of the World Cup finals' stages 11 to 16."),
                    nameof(finalsMatches));
            }

            bool isGroupMatch = stage == FinalsStages.GroupStage;
            string fix = isGroupMatch ? FixGroups : FixKnockout;

            WorldCupGroupChecks.CheckSides(match, "World Cup match", fix);

            if (isGroupMatch)
            {
                CheckGroupMatch(match);

                continue;
            }

            CheckKnockoutMatch(match, stage);
        }
    }

    /// <summary>
    /// Checks that a group match has a group number from 1 to 8, a matchday from 1 to 3 and no leg.
    /// </summary>
    /// <param name="match">The group match.</param>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the group number, the matchday or the leg is wrong.
    /// </exception>
    private static void CheckGroupMatch(SeasonMatch match)
    {
        if (match.GroupNumber is not { } groupNumber || groupNumber < 1 || groupNumber > WorldCupFinalsShape.GroupCount)
        {
            string groupText = match.GroupNumber is { } value ? value.ToString(CultureInfo.InvariantCulture) : "none";

            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"World Cup group match {match.Id} has group number {groupText}, but the finals have groups ")
                + string.Create(
                    CultureInfo.InvariantCulture,
                    $"1 to {WorldCupFinalsShape.GroupCount}. {FixGroups}"));
        }

        if (match.RoundNumber is not { } matchday || matchday < 1 || matchday > Matchdays)
        {
            string matchdayText = match.RoundNumber is { } value
                ? value.ToString(CultureInfo.InvariantCulture)
                : "none";

            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"World Cup group match {match.Id} has round number {matchdayText}, but a group match is on ")
                + string.Create(CultureInfo.InvariantCulture, $"matchday 1 to {Matchdays}. {FixGroups}"));
        }

        if (match.Leg is { } leg)
        {
            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"World Cup group match {match.Id} has leg {leg}, but a group match has no leg. {FixGroups}"));
        }
    }

    /// <summary>
    /// Checks that a knockout match has no group number, no round number and no leg.
    /// </summary>
    /// <param name="match">The knockout match.</param>
    /// <param name="stage">The stage of the match, 12 to 16.</param>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when the match has a group number, a round number or a leg.
    /// </exception>
    private static void CheckKnockoutMatch(SeasonMatch match, byte stage)
    {
        string? extra = ExtraOf(match);

        if (extra is null)
        {
            return;
        }

        string roundName = FinalsStages.NameOf(FinalsStages.RoundOf(stage));

        throw new SeasonRefusedException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"World Cup knockout match {match.Id} ({roundName}) has {extra}, but a knockout match has no ")
            + "group number, round number or leg. "
            + FixKnockout);
    }

    /// <summary>
    /// Describes the first of the group number, the round number and the leg that a knockout match has.
    /// </summary>
    /// <param name="match">The knockout match.</param>
    /// <returns>The description, such as "group number 3", or null when the match has none of them.</returns>
    private static string? ExtraOf(SeasonMatch match)
    {
        if (match.GroupNumber is { } groupNumber)
        {
            return string.Create(CultureInfo.InvariantCulture, $"group number {groupNumber}");
        }

        if (match.RoundNumber is { } roundNumber)
        {
            return string.Create(CultureInfo.InvariantCulture, $"round number {roundNumber}");
        }

        if (match.Leg is { } leg)
        {
            return string.Create(CultureInfo.InvariantCulture, $"leg {leg}");
        }

        return null;
    }

    /// <summary>
    /// Groups the group matches by group number and checks that they are the 8 groups of the finals.
    /// </summary>
    /// <param name="groupMatches">The group matches, which have been checked.</param>
    /// <returns>The matches of each group, in group order.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when a group is missing, has other than 4 nations or other than 6 matches, has a pair that did not meet
    /// exactly once, or shares a nation with another group.
    /// </exception>
    private static List<List<SeasonMatch>> ReadGroups(List<SeasonMatch> groupMatches)
    {
        ILookup<byte, SeasonMatch> byGroup = groupMatches.ToLookup(match => match.GroupNumber!.Value);
        Dictionary<int, byte> groupOfNation = new();
        List<List<SeasonMatch>> groups = [];

        for (byte number = 1; number <= WorldCupFinalsShape.GroupCount; number++)
        {
            List<SeasonMatch> matches = byGroup[number].ToList();

            if (matches.Count == 0)
            {
                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"World Cup finals group {number} has no matches, but the finals have ")
                    + string.Create(
                        CultureInfo.InvariantCulture,
                        $"{WorldCupFinalsShape.GroupCount} groups. {FixGroups}"));
            }

            CheckGroup(number, matches, groupOfNation);

            groups.Add(matches);
        }

        return groups;
    }

    /// <summary>
    /// Checks that one group has 4 nations that no other group has and that every pair of them met exactly once.
    /// </summary>
    /// <param name="number">The number of the group.</param>
    /// <param name="matches">The group's matches.</param>
    /// <param name="groupOfNation">The group of each nation read so far; this group's nations are added.</param>
    /// <exception cref="SeasonRefusedException">Thrown when the group does not fit the finals.</exception>
    private static void CheckGroup(byte number, List<SeasonMatch> matches, Dictionary<int, byte> groupOfNation)
    {
        List<int> nations = WorldCupGroupChecks.NationsOf(matches);

        if (nations.Count != WorldCupFinalsShape.GroupSize || matches.Count != WorldCupFinalsShape.MatchesPerGroup)
        {
            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"World Cup finals group {number} has {matches.Count} matches over {nations.Count} nations, ")
                + string.Create(
                    CultureInfo.InvariantCulture,
                    $"but a group has {WorldCupFinalsShape.MatchesPerGroup} over {WorldCupFinalsShape.GroupSize}. ")
                + FixGroups);
        }

        WorldCupGroupChecks.AddToGroup(groupOfNation, nations, number, "finals", FixGroups);

        WorldCupGroupChecks.CheckMeetings(
            number,
            nations,
            matches,
            WorldCupFinalsShape.Legs,
            "finals",
            "a group plays each pair once",
            FixGroups);
    }

    /// <summary>
    /// Ranks the table of each group from its finished matches in the World Cup order.
    /// </summary>
    /// <param name="seasonId">The id of the season, which seeds the tie-break shuffle.</param>
    /// <param name="groups">The matches of each group, in group order; all finished and checked.</param>
    /// <returns>For each group, the league team ids in place order, place 1 first.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when the ranking library refuses a group's matches.</exception>
    private static List<IReadOnlyList<int>> RankGroups(int seasonId, List<List<SeasonMatch>> groups)
    {
        List<IReadOnlyList<int>> tables = [];

        for (int index = 0; index < groups.Count; index++)
        {
            List<int> nations = WorldCupGroupChecks.NationsOf(groups[index]);
            List<TableMatch> matches = SeasonTableMatches.Of(groups[index]);

            try
            {
                tables.Add(TableRanking.Rank(seasonId, nations, matches, TableOrder.WorldCup)
                    .OrderBy(row => row.Place)
                    .Select(row => row.Id)
                    .ToList());
            }
            catch (ArgumentException exception) when (exception is not ArgumentNullException
                and not ArgumentOutOfRangeException)
            {
                throw new SeasonRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"The table of finals group {index + 1} refused the group's matches: ")
                    + DomesticCupReader.ReasonOf(exception)
                    + " "
                    + FixGroups);
            }
        }

        return tables;
    }
}
