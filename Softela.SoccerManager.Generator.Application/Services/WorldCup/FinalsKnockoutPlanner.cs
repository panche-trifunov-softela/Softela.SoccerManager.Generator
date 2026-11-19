using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Services.Calendar;
using Softela.SoccerManager.Generator.Application.Services.Knockout;

namespace Softela.SoccerManager.Generator.Application.Services.WorldCup;

/// <summary>
/// Plans the next knockout matches of the World Cup finals from what the written matches say about them. A tie is
/// planned as soon as both of its nations are known; the first nation of the tie is the home nation, which is only a
/// label. The third-place match, between the two semi-final losers, is planned before the final.
/// </summary>
internal static class FinalsKnockoutPlanner
{
    /// <summary>
    /// Plans the matches to write on this tick. Every tie that has both nations known and no match, and the third place
    /// when it is not written, is planned on the finals day of its round, or on the first later day that the date rule
    /// allows: later than the matches that lead to it (the group matches for the round of 16), at least 24 hours from
    /// every other match of both nations, and at least 12 hours after the run. Every planned kickoff is added to the
    /// nations' kickoffs, so the matches planned after it keep their distance.
    /// </summary>
    /// <param name="reading">What the finals' matches say about them; its status must be ready.</param>
    /// <param name="finalsDays">The 8 finals days, earliest first; the knockout rounds use the 4th to the 8th.</param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <param name="kickoffsOfClub">
    /// The kickoffs of every match of each nation in the season, by league team id; the planned matches are added.
    /// </param>
    /// <returns>
    /// The matches to write: the new ties of the bracket in round order and then tie order, then the third place when
    /// it is new, then the final when it is new.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="reading"/>, <paramref name="finalsDays"/> or <paramref name="kickoffsOfClub"/> is
    /// null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="runTime"/> is not in UTC, there are not 8 finals days, or the reading is not ready.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the reading lacks a member its status promises, or a feeder tie has no written match, which is a
    /// bug.
    /// </exception>
    /// <exception cref="SeasonRefusedException">Thrown when no day fits a match.</exception>
    internal static IReadOnlyList<FinalsPlannedMatch> Plan(
        FinalsReading reading,
        IReadOnlyList<DateTime> finalsDays,
        DateTime runTime,
        Dictionary<int, List<DateTime>> kickoffsOfClub)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(finalsDays);
        ArgumentNullException.ThrowIfNull(kickoffsOfClub);

        if (runTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The run time must be in UTC.", nameof(runTime));
        }

        if (finalsDays.Count != CalendarTemplate.WorldCupFinalsMatchdayCount)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"There are {finalsDays.Count} finals days, but the finals have ")
                + string.Create(CultureInfo.InvariantCulture, $"{CalendarTemplate.WorldCupFinalsMatchdayCount}."),
                nameof(finalsDays));
        }

        if (reading.Status != FinalsReadingStatus.Ready)
        {
            throw new ArgumentException(
                "The knockout can only be planned once the group matches are finished.",
                nameof(reading));
        }

        KnockoutBracket bracket = reading.Bracket
            ?? throw new InvalidOperationException("The reading has no bracket, which its status promises.");

        int finalNumber = FinalsStages.BracketNumberOf(FinalsRound.Final);
        List<FinalsPlannedMatch> planned = [];

        foreach (KnockoutKnownTie tie in reading.NewTies.Where(newTie => newTie.RoundNumber < finalNumber))
        {
            DateTime? after = EarliestAfter(reading, bracket, tie);

            planned.Add(Place(
                FinalsStages.FromBracketNumber(tie.RoundNumber),
                tie.TieNumber,
                after,
                (tie.FirstLeagueTeamId, tie.SecondLeagueTeamId),
                finalsDays,
                runTime,
                kickoffsOfClub));
        }

        DateTime? thirdPlaceKickoff = reading.ThirdPlace?.Kickoff;

        if (reading.ThirdPlace is { IsWritten: false } thirdPlace)
        {
            FinalsPlannedMatch match = Place(
                FinalsRound.ThirdPlace,
                1,
                LaterSemiFinal(reading),
                (thirdPlace.HomeLeagueTeamId, thirdPlace.AwayLeagueTeamId),
                finalsDays,
                runTime,
                kickoffsOfClub);

            planned.Add(match);

            thirdPlaceKickoff = match.Kickoff;
        }

        KnockoutKnownTie? final = reading.NewTies.FirstOrDefault(newTie => newTie.RoundNumber == finalNumber);

        if (final is not null)
        {
            DateTime semiFinalKickoff = LaterSemiFinal(reading);
            DateTime after = thirdPlaceKickoff is { } third && third > semiFinalKickoff ? third : semiFinalKickoff;

            planned.Add(Place(
                FinalsRound.Final,
                final.TieNumber,
                after,
                (final.FirstLeagueTeamId, final.SecondLeagueTeamId),
                finalsDays,
                runTime,
                kickoffsOfClub));
        }

        return planned;
    }

    /// <summary>
    /// Finds the date a tie must follow: the last group match for a tie of the round of 16, otherwise the later kickoff
    /// of the two ties that lead to it.
    /// </summary>
    /// <param name="reading">The reading of finals that are ready.</param>
    /// <param name="bracket">The bracket.</param>
    /// <param name="tie">The tie to date.</param>
    /// <returns>The date the tie must follow.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a feeder tie has no written match, which is a bug.
    /// </exception>
    private static DateTime? EarliestAfter(FinalsReading reading, KnockoutBracket bracket, KnockoutKnownTie tie)
    {
        if (tie.RoundNumber == 1)
        {
            return reading.LastGroupKickoff;
        }

        KnockoutTie shape = bracket.Rounds[tie.RoundNumber - 1][tie.TieNumber - 1];
        DateTime? latest = null;

        foreach (KnockoutSide side in new[] { shape.First, shape.Second })
        {
            if (side.IsClub)
            {
                continue;
            }

            FinalsWrittenTie feeder = reading.WrittenTies.FirstOrDefault(written =>
                written.RoundNumber == side.WinnerOfRoundNumber && written.TieNumber == side.WinnerOfTieNumber)
                ?? throw new InvalidOperationException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Tie {side.WinnerOfTieNumber} of round {side.WinnerOfRoundNumber} has no written match."));

            if (latest is null || feeder.Kickoff > latest)
            {
                latest = feeder.Kickoff;
            }
        }

        return latest;
    }

    /// <summary>
    /// Finds the later kickoff of the two semi-finals, which the third place and the final must follow.
    /// </summary>
    /// <param name="reading">The reading of finals that are ready.</param>
    /// <returns>The later kickoff of the two semi-finals.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a semi-final has no written match, which the third place and the final rule out.
    /// </exception>
    private static DateTime LaterSemiFinal(FinalsReading reading)
    {
        int semiFinal = FinalsStages.BracketNumberOf(FinalsRound.SemiFinal);

        List<DateTime> kickoffs = reading.WrittenTies
            .Where(written => written.RoundNumber == semiFinal)
            .Select(written => written.Kickoff)
            .ToList();

        if (kickoffs.Count == 0)
        {
            throw new InvalidOperationException("The semi-finals have no written match, which the final needs.");
        }

        return kickoffs.Max();
    }

    /// <summary>
    /// Dates one match on the finals day of its round and records its kickoff for both nations.
    /// </summary>
    /// <param name="round">The round of the match.</param>
    /// <param name="tieNumber">The 1-based number of the tie within its round.</param>
    /// <param name="after">The match must be later than this, or null when nothing has to come before it.</param>
    /// <param name="nations">The league team ids of the home nation and the away nation.</param>
    /// <param name="finalsDays">The 8 finals days.</param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <param name="kickoffsOfClub">The kickoffs of each nation's matches; the new one is added.</param>
    /// <returns>The planned match.</returns>
    /// <exception cref="SeasonRefusedException">Thrown when no day fits the match.</exception>
    private static FinalsPlannedMatch Place(
        FinalsRound round,
        int tieNumber,
        DateTime? after,
        (int Home, int Away) nations,
        IReadOnlyList<DateTime> finalsDays,
        DateTime runTime,
        Dictionary<int, List<DateTime>> kickoffsOfClub)
    {
        DateTime kickoff = WorldCupFinalsDates.PickKnockout(
            finalsDays[FinalsStages.DayOf(round) - 1],
            runTime,
            after,
            nations.Home,
            nations.Away,
            kickoffsOfClub);

        return new FinalsPlannedMatch(kickoff, round, tieNumber, nations.Home, nations.Away);
    }
}
