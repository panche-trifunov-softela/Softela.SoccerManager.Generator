using System.Globalization;
using Softela.SoccerManager.Generator.Application.Core.Exceptions;
using Softela.SoccerManager.Generator.Application.Core.Randomness;
using Softela.SoccerManager.Generator.Application.Models;
using Softela.SoccerManager.Generator.Application.Services.Calendar;
using Softela.SoccerManager.Generator.Application.Services.RoundRobin;

namespace Softela.SoccerManager.Generator.Application.Services.FixtureGeneration;

/// <summary>
/// Turns the rows of one league into the fixtures and dates of its newest season, or refuses the season when its rows
/// cannot be written as they stand.
/// </summary>
internal static class LeagueInputBuilder
{
    /// <summary>
    /// The format of every kickoff that a log line or a refusal prints.
    /// </summary>
    internal const string KickoffFormat = "dddd yyyy-MM-dd HH:mm";

    /// <summary>
    /// The competition format of a division: a single table of clubs, as the backend's group-only format.
    /// </summary>
    private const byte GroupOnlyFormat = 2;

    /// <summary>
    /// The number of legs of a league: every club meets every other club at home and away.
    /// </summary>
    private const int LeagueLegs = 2;

    /// <summary>
    /// The purpose that the fixtures' random source is derived for.
    /// </summary>
    private const string LeaguePurpose = "league";

    /// <summary>
    /// The most days after a run that the first match of a season may be.
    /// </summary>
    private const int MaxDaysToFirstMatch = 365;

    /// <summary>
    /// The format of the run time in a refusal.
    /// </summary>
    private const string RunTimeFormat = "yyyy-MM-dd HH:mm";

    /// <summary>
    /// Finds the first match of a season that belongs to one of its divisions. A friendly never counts.
    /// </summary>
    /// <param name="seasonId">The id of the season.</param>
    /// <param name="divisionIds">The competition ids of the divisions.</param>
    /// <param name="matches">The matches to search, in the order that decides which one is first.</param>
    /// <returns>The first match of the season in one of the divisions, or null when there is none.</returns>
    public static GetMatchesResult? FindDivisionMatch(
        int seasonId,
        IReadOnlySet<int> divisionIds,
        IReadOnlyList<GetMatchesResult> matches)
    {
        foreach (GetMatchesResult match in matches)
        {
            if (match.SeasonId != seasonId || match.CompetitionId is not { } competitionId)
            {
                continue;
            }

            if (divisionIds.Contains(competitionId))
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>
    /// Builds the fixtures and the dates of a league's newest season. The caller guarantees that:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <paramref name="divisions"/> are the league's competitions with an Order above 0, in ascending Order, and there
    /// is at least one;
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <paramref name="standingsByDivision"/> has an entry for every division, and at least one entry is not empty;
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <paramref name="leagueMatches"/> are the matches of all of the league's seasons, friendlies included;
    /// </description>
    /// </item>
    /// <item><description><paramref name="runTime"/> is in UTC.</description></item>
    /// </list>
    /// </summary>
    /// <param name="league">The league.</param>
    /// <param name="season">The newest season of the league.</param>
    /// <param name="divisions">The divisions of the league, in ascending Order.</param>
    /// <param name="standingsByDivision">The Standings rows of the season, by competition id.</param>
    /// <param name="roster">The league's roster.</param>
    /// <param name="teams">Every team.</param>
    /// <param name="leagueMatches">The matches of all of the league's seasons.</param>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <returns>The input the writer needs for the season.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when:
    /// <list type="bullet">
    /// <item><description>a division is not domestic or its format is not group-only;</description></item>
    /// <item><description>a division has no Standings rows while another has;</description></item>
    /// <item><description>a Standings row is for a club that is off the roster or has no team;</description></item>
    /// <item><description>a club has Standings rows in two divisions;</description></item>
    /// <item><description>the divisions' shape cannot be laid out;</description></item>
    /// <item><description>too many clubs share stadiums;</description></item>
    /// <item><description>the first match would be more than 365 days after the run.</description></item>
    /// </list>
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the fixtures and the calendar disagree on the number of rounds, which is a bug.
    /// </exception>
    public static LeagueInput Build(
        GetLeaguesResult league,
        GetSeasonsResult season,
        IReadOnlyList<GetCompetitionsResult> divisions,
        IReadOnlyDictionary<int, IReadOnlyList<GetStandingsResult>> standingsByDivision,
        IReadOnlyList<GetLeagueTeamsResult> roster,
        IReadOnlyList<GetTeamsResult> teams,
        IReadOnlyList<GetMatchesResult> leagueMatches,
        DateTime runTime)
    {
        CheckDivisionKinds(divisions);
        CheckDivisionsHaveStandings(divisions, standingsByDivision);

        Dictionary<int, GetTeamsResult> teamOfClub = [];

        List<LeagueDivision> leagueDivisions = BuildDivisions(
            divisions,
            standingsByDivision,
            roster,
            teams,
            teamOfClub);

        List<int> clubCounts = leagueDivisions.Select(division => division.LeagueTeamIds.Count).ToList();

        CheckShape(clubCounts, leagueDivisions);

        IReadOnlyList<SharedStadiumPair> pairs = SharedStadiumFinder.Find(leagueDivisions, teamOfClub);

        DateTime limit = runTime.AddDays(MaxDaysToFirstMatch);
        GetMatchesResult? latest = FindLatestMatch(leagueMatches);

        if (latest is not null && latest.StartDateTime > limit)
        {
            throw TooFarAhead(runTime, latest);
        }

        List<DateTime> kickoffs = leagueMatches.Select(match => match.StartDateTime).ToList();
        SeasonCalendar calendar = SeasonCalendarBuilder.Build(DateOnly.FromDateTime(runTime), clubCounts, kickoffs);

        // Without existing matches the first match is at most 7 days away, so only a latest match can push it out.
        if (latest is not null && calendar.LeagueMatchdays[0] > limit)
        {
            throw TooFarAhead(runTime, latest);
        }

        List<IReadOnlyList<int>> groups = leagueDivisions.Select(division => division.LeagueTeamIds).ToList();
        SeededRandom random = new SeededRandom(season.Id).Derive(LeaguePurpose);

        IReadOnlyList<IReadOnlyList<RoundRobinMatch>> fixtures = RoundRobinScheduler.Generate(
            groups,
            LeagueLegs,
            pairs,
            random);

        int lastRound = fixtures.SelectMany(divisionFixtures => divisionFixtures).Max(match => match.Round);

        if (lastRound != calendar.LeagueMatchdays.Count)
        {
            throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"The fixtures have {lastRound} rounds but the calendar has {calendar.LeagueMatchdays.Count}."));
        }

        return new LeagueInput(league, season, leagueDivisions, fixtures, calendar.LeagueMatchdays);
    }

    /// <summary>
    /// Checks that every division is a domestic competition with the group-only format.
    /// </summary>
    /// <param name="divisions">The divisions of the league.</param>
    /// <exception cref="SeasonRefusedException">Thrown when a division is not domestic or not group-only.</exception>
    private static void CheckDivisionKinds(IReadOnlyList<GetCompetitionsResult> divisions)
    {
        foreach (GetCompetitionsResult division in divisions)
        {
            if (division.IsDomestic && division.Format == GroupOnlyFormat)
            {
                continue;
            }

            int isDomestic = division.IsDomestic ? 1 : 0;

            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Competition '{division.Name}' (id {division.Id}) has Order {division.Order}, ")
                + "so it counts as a division, but a division must have IsDomestic 1 and Format 2; "
                + string.Create(
                    CultureInfo.InvariantCulture,
                    $"this one has IsDomestic {isDomestic} and Format {division.Format}. ")
                + "Give it Order 0 if it is not a division.");
        }
    }

    /// <summary>
    /// Checks that every division has Standings rows, since the caller has found that at least one does.
    /// </summary>
    /// <param name="divisions">The divisions of the league.</param>
    /// <param name="standingsByDivision">The Standings rows of the season, by competition id.</param>
    /// <exception cref="SeasonRefusedException">Thrown when a division has no Standings rows.</exception>
    private static void CheckDivisionsHaveStandings(
        IReadOnlyList<GetCompetitionsResult> divisions,
        IReadOnlyDictionary<int, IReadOnlyList<GetStandingsResult>> standingsByDivision)
    {
        foreach (GetCompetitionsResult division in divisions)
        {
            if (standingsByDivision[division.Id].Count > 0)
            {
                continue;
            }

            throw new SeasonRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Division '{division.Name}' (competition {division.Id}) has no Standings rows in this season, ")
                + "but other divisions do. Add its Standings, or give it Order 0 if that tier is gone.");
        }
    }

    /// <summary>
    /// Builds the divisions from their Standings rows. A roster club with no Standings row stays out.
    /// </summary>
    /// <param name="divisions">The divisions of the league.</param>
    /// <param name="standingsByDivision">The Standings rows of the season, by competition id.</param>
    /// <param name="roster">The league's roster.</param>
    /// <param name="teams">Every team.</param>
    /// <param name="teamOfClub">Receives the team of each division club, by league team id.</param>
    /// <returns>The divisions in the order given, each with its clubs in ascending league team id.</returns>
    /// <exception cref="SeasonRefusedException">
    /// Thrown when a Standings row is for a club that is off the roster or has no team, or a club has Standings rows
    /// in two divisions.
    /// </exception>
    private static List<LeagueDivision> BuildDivisions(
        IReadOnlyList<GetCompetitionsResult> divisions,
        IReadOnlyDictionary<int, IReadOnlyList<GetStandingsResult>> standingsByDivision,
        IReadOnlyList<GetLeagueTeamsResult> roster,
        IReadOnlyList<GetTeamsResult> teams,
        Dictionary<int, GetTeamsResult> teamOfClub)
    {
        Dictionary<int, GetLeagueTeamsResult> rosterById = roster.ToDictionary(entry => entry.Id);
        Dictionary<int, GetTeamsResult> teamById = teams.ToDictionary(team => team.Id);
        Dictionary<int, GetCompetitionsResult> divisionOfClub = [];
        List<LeagueDivision> leagueDivisions = [];

        foreach (GetCompetitionsResult division in divisions)
        {
            List<int> clubIds = [];

            foreach (int clubId in standingsByDivision[division.Id].Select(row => row.LeagueTeamId).Order())
            {
                if (!rosterById.TryGetValue(clubId, out GetLeagueTeamsResult? entry))
                {
                    throw new SeasonRefusedException(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"Division '{division.Name}' (competition {division.Id}) has a Standings row for ")
                        + string.Create(
                            CultureInfo.InvariantCulture,
                            $"league team {clubId}, which is not on the league's roster."));
                }

                if (!teamById.TryGetValue(entry.TeamId, out GetTeamsResult? team))
                {
                    throw new SeasonRefusedException(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"League team {clubId} in division '{division.Name}' (competition {division.Id}) ")
                        + string.Create(
                            CultureInfo.InvariantCulture,
                            $"points at team {entry.TeamId}, which does not exist."));
                }

                if (divisionOfClub.TryGetValue(clubId, out GetCompetitionsResult? earlier))
                {
                    if (earlier.Id == division.Id)
                    {
                        continue;
                    }

                    throw new SeasonRefusedException(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"Club '{team.Name}' (league team {clubId}) has Standings rows in ")
                        + string.Create(
                            CultureInfo.InvariantCulture,
                            $"'{earlier.Name}' (competition {earlier.Id}) and ")
                        + string.Create(
                            CultureInfo.InvariantCulture,
                            $"'{division.Name}' (competition {division.Id})."));
                }

                divisionOfClub.Add(clubId, division);
                teamOfClub.Add(clubId, team);
                clubIds.Add(clubId);
            }

            leagueDivisions.Add(new LeagueDivision(division.Id, division.Name, clubIds));
        }

        return leagueDivisions;
    }

    /// <summary>
    /// Checks that the divisions' sizes can be laid out as a season.
    /// </summary>
    /// <param name="clubCounts">The number of clubs in each division, in division order.</param>
    /// <param name="leagueDivisions">The divisions, for the refusal's text.</param>
    /// <exception cref="SeasonRefusedException">Thrown when the shape of the divisions is not allowed.</exception>
    private static void CheckShape(IReadOnlyList<int> clubCounts, IReadOnlyList<LeagueDivision> leagueDivisions)
    {
        try
        {
            SeasonCalendarInputValidator.Validate(clubCounts, []);
        }
        catch (ArgumentException exception)
        {
            string reason = exception.ParamName is null
                ? exception.Message
                : exception.Message.Replace($" (Parameter '{exception.ParamName}')", string.Empty);

            string described = string.Join(
                "; ",
                leagueDivisions.Select(division => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{division.Name} (competition {division.CompetitionId}) {division.LeagueTeamIds.Count} clubs")));

            throw new SeasonRefusedException(
                string.Create(CultureInfo.InvariantCulture, $"{reason} Divisions in order: {described}."));
        }
    }

    /// <summary>
    /// Finds the league match with the latest kickoff.
    /// </summary>
    /// <param name="leagueMatches">The matches of all of the league's seasons.</param>
    /// <returns>The match with the greatest kickoff, or null when there are no matches.</returns>
    private static GetMatchesResult? FindLatestMatch(IReadOnlyList<GetMatchesResult> leagueMatches)
    {
        GetMatchesResult? latest = null;

        foreach (GetMatchesResult match in leagueMatches)
        {
            if (latest is null || match.StartDateTime > latest.StartDateTime)
            {
                latest = match;
            }
        }

        return latest;
    }

    /// <summary>
    /// Builds the refusal for a first match that would be too far from the run.
    /// </summary>
    /// <param name="runTime">The moment of the run, in UTC.</param>
    /// <param name="latest">The match with the latest kickoff, which the first match must follow.</param>
    /// <returns>The refusal, ready to throw.</returns>
    private static SeasonRefusedException TooFarAhead(DateTime runTime, GetMatchesResult latest)
    {
        string run = runTime.ToString(RunTimeFormat, CultureInfo.InvariantCulture);
        string kickoff = latest.StartDateTime.ToString(KickoffFormat, CultureInfo.InvariantCulture);

        return new SeasonRefusedException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"The first match would be more than {MaxDaysToFirstMatch} days after this run ({run} UTC), ")
            + string.Create(
                CultureInfo.InvariantCulture,
                $"because it must come at least 24 hours after match {latest.Id}, which kicks off {kickoff} UTC. ")
            + "Move or delete that match if its date is a placeholder.");
    }
}
