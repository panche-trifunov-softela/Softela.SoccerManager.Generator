using System.Globalization;

namespace Softela.SoccerManager.Generator.Application.Services.Ranking;

/// <summary>
/// Refuses table input that cannot make a table.
/// </summary>
internal static class TableRankingInputValidator
{
    /// <summary>
    /// Checks the participants and the matches of a table. Any number of participants is accepted, none and one
    /// included. The first problem found is thrown, participants first and then the matches in list order.
    /// </summary>
    /// <param name="participants">The ids of the participants; the caller has checked the list for null.</param>
    /// <param name="matches">The matches of the table; the caller has checked the list for null.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when a participant is listed more than once, or a match is missing, has a side that is not a
    /// participant, has the same participant on both sides, or has negative goals.
    /// </exception>
    internal static void Validate(IReadOnlyList<int> participants, IReadOnlyList<TableMatch> matches)
    {
        HashSet<int> members = new();

        foreach (int id in participants)
        {
            if (!members.Add(id))
            {
                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"Participant {id} is listed more than once."),
                    nameof(participants));
            }
        }

        for (int index = 0; index < matches.Count; index++)
        {
            ValidateMatch(index, matches[index], members, nameof(matches));
        }
    }

    /// <summary>
    /// Checks one match of the list.
    /// </summary>
    /// <param name="index">The 0-based position of the match in the list.</param>
    /// <param name="match">The match; it may be null.</param>
    /// <param name="members">The ids of the participants.</param>
    /// <param name="paramName">The name of the argument the matches came in as, for the exception.</param>
    /// <exception cref="ArgumentException">Thrown when the match is not valid.</exception>
    private static void ValidateMatch(int index, TableMatch? match, HashSet<int> members, string paramName)
    {
        if (match is null)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"Match {index} of the list is missing."),
                paramName);
        }

        ValidateSide(index, match.HomeId, members, paramName);
        ValidateSide(index, match.AwayId, members, paramName);

        if (match.HomeId == match.AwayId)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Match {index} has participant {match.HomeId} on both sides."),
                paramName);
        }

        if (match.HomeGoals < 0 || match.AwayGoals < 0)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"Match {index} has negative goals."),
                paramName);
        }
    }

    /// <summary>
    /// Checks that one side of a match is a participant of the table.
    /// </summary>
    /// <param name="index">The 0-based position of the match in the list.</param>
    /// <param name="id">The id on that side of the match.</param>
    /// <param name="members">The ids of the participants.</param>
    /// <param name="paramName">The name of the argument the matches came in as, for the exception.</param>
    /// <exception cref="ArgumentException">Thrown when the id is not a participant.</exception>
    private static void ValidateSide(int index, int id, HashSet<int> members, string paramName)
    {
        if (!members.Contains(id))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Match {index} has participant {id}, who is not in the table."),
                paramName);
        }
    }
}
