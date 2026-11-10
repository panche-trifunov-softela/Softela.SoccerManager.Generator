# Softela.SoccerManager.Generator

An Azure Functions app (.NET 10, isolated worker) that writes fixtures into the SoccerManager database through its
stored procedures.

## GenerateLeagueFixtures

A timer function with the schedule `0 0 */6 * * *`: every six hours, at 00:00, 06:00, 12:00 and 18:00 host time.

For each league, in id order, it takes the newest season (the highest SeasonNumber) and its divisions (the competitions
with an Order above 0). It writes every league match of the season (two legs, on Thursdays and Sundays at 20:00 UTC,
from the first Thursday after the day of the run that is at least 24 hours after the league's latest match) plus
two MatchTeamStatistics rows per match with zeros, in one transaction per league.

It skips a league, and tries again at the next tick, when:

- the league has no season, or no division;
- a division of the newest season already has matches;
- no division has Standings rows yet;
- the Standings changed under an hour ago;
- another run holds the season's lock, or wrote the season first.

It refuses a league, which is logged as an error and tried again at every tick, when:

- a division is not a domestic competition with Format 2;
- only some divisions have Standings;
- a Standings row is for a club that is off the league's roster or whose team row is missing, or a club is in two
  divisions;
- the divisions are not 1 to 5 divisions of an equal, even number of 10 to 20 clubs;
- three clubs are on one stadium, or more than half of a division's clubs share a stadium with another club;
- the first match would be more than 365 days away, because an existing match is that far ahead.

A failed write rolls that league back and fails the run, so the host reports it. Leagues written before it stay
written, and the next tick starts over from the first league.

## Running it locally

The host needs Azurite for `AzureWebJobsStorage` (`UseDevelopmentStorage=true`) and these settings:

- `FUNCTIONS_WORKER_RUNTIME` = `dotnet-isolated`;
- `AzureWebJobsStorage`;
- the connection string, as `ConnectionStrings__soccerteambuilder` in `Values`, or in a top-level `ConnectionStrings`
  section; a plain `soccerteambuilder` value is not found.

Leave `APPLICATIONINSIGHTS_CONNECTION_STRING` unset locally to see the log lines in the console.

Start it from the project folder with `func start --port 7071`, or with
`dotnet run --no-launch-profile -- --port 7071` when `func` is on the PATH; `launchSettings.json`'s port is ignored by
`dotnet run`. A first start does not fire the timer; a restart after a missed tick fires it once within seconds.

To run it now:

```powershell
Invoke-WebRequest -UseBasicParsing -Method Post -Uri 'http://localhost:7071/admin/functions/GenerateLeagueFixtures' -ContentType 'application/json' -Body '{"input":""}'
```

The answer is 202 with an empty body; no key is needed locally.

## Writing a season again

Delete the season's division matches, and the next tick writes them again:

```sql
DELETE m FROM dbo.Matches m INNER JOIN dbo.Competitions c ON c.Id = m.CompetitionId WHERE m.SeasonId = <season id> AND c.[Order] > 0;
```

The delete cascades to each match's statistics, tactics, lineups, substitutions, player statistics and shootout kicks.
