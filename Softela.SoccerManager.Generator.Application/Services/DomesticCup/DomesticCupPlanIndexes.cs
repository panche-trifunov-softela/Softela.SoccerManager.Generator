using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Services.Seasons;

namespace Softela.SoccerManager.Generator.Application.Services.DomesticCup;

/// <summary>
/// Indexes what a cup plan looks up by club: the squad strengths and the kickoffs of the clubs' matches.
/// </summary>
internal static class DomesticCupPlanIndexes
{
    /// <summary>
    /// Indexes the squad strengths by club without throwing on a duplicate: a club given twice holds null, so a lookup
    /// can refuse it with a message.
    /// </summary>
    /// <param name="strengths">The squad strengths given.</param>
    /// <returns>The strength by league team id; null for a club given more than once.</returns>
    public static Dictionary<int, SquadStrength?> IndexStrengths(IReadOnlyList<SquadStrength> strengths)
    {
        Dictionary<int, SquadStrength?> strengthOf = new();

        foreach (SquadStrength strength in strengths)
        {
            strengthOf[strength.LeagueTeamId] = strengthOf.ContainsKey(strength.LeagueTeamId) ? null : strength;
        }

        return strengthOf;
    }

    /// <summary>
    /// Gathers the kickoff of every season match of each club.
    /// </summary>
    /// <param name="seasonMatches">The matches of the season.</param>
    /// <returns>The kickoffs, by league team id.</returns>
    public static Dictionary<int, List<DateTime>> IndexKickoffs(IReadOnlyList<SeasonMatch> seasonMatches)
    {
        Dictionary<int, List<DateTime>> kickoffsOfClub = new();

        foreach (SeasonMatch match in seasonMatches)
        {
            foreach (int? clubId in new[] { match.HomeLeagueTeamId, match.AwayLeagueTeamId })
            {
                if (clubId is { } id)
                {
                    AddKickoff(kickoffsOfClub, id, match.Kickoff);
                }
            }
        }

        return kickoffsOfClub;
    }

    /// <summary>
    /// Records a kickoff against a club.
    /// </summary>
    /// <param name="kickoffsOfClub">The kickoffs of each club's matches.</param>
    /// <param name="leagueTeamId">The league team id of the club.</param>
    /// <param name="kickoff">The kickoff to record.</param>
    public static void AddKickoff(Dictionary<int, List<DateTime>> kickoffsOfClub, int leagueTeamId, DateTime kickoff)
    {
        if (!kickoffsOfClub.TryGetValue(leagueTeamId, out List<DateTime>? kickoffs))
        {
            kickoffs = new List<DateTime>();
            kickoffsOfClub.Add(leagueTeamId, kickoffs);
        }

        kickoffs.Add(kickoff);
    }

    /// <summary>
    /// Gathers the kickoffs of every match of two clubs.
    /// </summary>
    /// <param name="kickoffsOfClub">The kickoffs of each club's matches.</param>
    /// <param name="firstLeagueTeamId">The league team id of the first club.</param>
    /// <param name="secondLeagueTeamId">The league team id of the second club.</param>
    /// <returns>The first club's kickoffs and then the second club's; a club with no match adds nothing.</returns>
    public static List<DateTime> KickoffsOf(
        Dictionary<int, List<DateTime>> kickoffsOfClub,
        int firstLeagueTeamId,
        int secondLeagueTeamId)
    {
        List<DateTime> gathered = new();

        foreach (int clubId in new[] { firstLeagueTeamId, secondLeagueTeamId })
        {
            if (kickoffsOfClub.TryGetValue(clubId, out List<DateTime>? kickoffs))
            {
                gathered.AddRange(kickoffs);
            }
        }

        return gathered;
    }

    /// <summary>
    /// Copies the kickoffs of every club, so the copy can grow without changing the original.
    /// </summary>
    /// <param name="kickoffsOfClub">The kickoffs of each club's matches, by league team id.</param>
    /// <returns>A copy with its own list for each club.</returns>
    public static Dictionary<int, List<DateTime>> CopyKickoffs(Dictionary<int, List<DateTime>> kickoffsOfClub)
    {
        return kickoffsOfClub.ToDictionary(pair => pair.Key, pair => new List<DateTime>(pair.Value));
    }

    /// <summary>
    /// Looks up the squad strength of a club.
    /// </summary>
    /// <param name="strengthOf">The squad strength by league team id; a duplicated club holds null.</param>
    /// <param name="leagueTeamId">The league team id of the club.</param>
    /// <returns>The club's squad strength.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when the strength is missing or was given twice.</exception>
    public static SquadStrength StrengthOf(Dictionary<int, SquadStrength?> strengthOf, int leagueTeamId)
    {
        if (strengthOf.TryGetValue(leagueTeamId, out SquadStrength? strength) && strength is not null)
        {
            return strength;
        }

        throw new SeasonRefusedException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"No squad strength was given for club {leagueTeamId}, or it was given twice."));
    }
}
