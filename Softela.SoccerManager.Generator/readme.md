# Softela.SoccerManager.Generator

An Azure Functions app (.NET 10, isolated worker) that writes fixtures into the SoccerManager database through its
stored procedures.

The database needs the backend's scripts up to `V1_0_0_57`: each write reads its season through
`dbo.GetMatchesBySeason`, so against an older database the first league with a season to write fails the run. The
domestic cup step needs `dbo.GetMatchScoresBySeason`, `dbo.GetLeagueTeamSquad` and `dbo.InsertStagedMatch` too, which
come with the same scripts: `V1_0_0_57` adds the stage columns that `dbo.InsertStagedMatch` writes.

## GenerateLeagueFixtures

A timer function with the schedule `0 0 */6 * * *`: every six hours, at 00:00, 06:00, 12:00 and 18:00 host time.

For each league, in id order, it takes the newest season (the highest SeasonNumber) and its divisions (the competitions
with an Order above 0), and runs two steps for it, one after the other: the league matches step, and then the domestic
cup step.

The league matches step writes every league match of the season (two legs, on Thursdays and Sundays at 20:00 UTC,
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

The domestic cup step reads the season again inside its own lock, so it sees the league matches the step before it
wrote. The cup is the league's one competition with Order 0, IsDomestic 1, Format 1 and no age limit (MaxAgeAllowed
NULL); a youth cup is never it. Its entrants are the clubs of the season's written league matches.

When the cup starts, the clubs of the top divisions get the byes (the top division first, and the strongest squads
first inside a division), the draw is seeded by the season, and the ties of round 1 and the ties of round 2 between two
bye clubs are written together. Later ticks write each tie once both its clubs are known.

Every tie is on a Tuesday at 20:00 UTC, on its round's calendar date or a later Tuesday: at least 24 hours after the
tick, at least 24 hours from every other match of both clubs this season, and after the tie's own matches in the round
before. The weaker squad, measured by its best 16 ratings, is at home. When round 1 can no longer be played before
round 2's date, the season plays without the cup, which is logged as a warning. A squad with fewer than 16 rated
players counts 0 for each missing place, and the step logs one warning per tick that lists those clubs.

It skips the cup, and tries again at the next tick, when:

- the league has no cup;
- the league matches are not written yet;
- no new tie is known;
- another run holds the season's lock.

It refuses the cup, which is logged as an error and tried again at every tick, when:

- two competitions have the cup's shape;
- a league match was moved by hand, until it is moved back;
- a cup match is not a tie the generator can read;
- a club it measures is off the league's roster;
- a squad lists a player with two different ratings;
- the season's matches do not fit the league's clubs, its calendar or the draw in another way, or no Tuesday within a
  year fits a tie; the error says what is wrong.

If a cup match is deleted by hand, the cup is refused until the cup-only reset below when it was a match of round 1.
When it was a match of a later round, the tie is written again as unplayed, but if the next round's tie already exists,
the cup is refused.

A failed write, of league matches or of cup ties, rolls that write back and fails the run, so the host reports it.
Everything written before it stays written, and the next tick starts over from the first league.

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

Find the id of the league's domestic cup:

```sql
SELECT Id, Name FROM dbo.Competitions WHERE LeagueId = <league id> AND [Order] = 0 AND IsDomestic = 1 AND Format = 1 AND MaxAgeAllowed IS NULL;
```

Delete the season's division and cup matches, and the next tick writes them again, the cup included:

```sql
DELETE m FROM dbo.Matches m INNER JOIN dbo.Competitions c ON c.Id = m.CompetitionId WHERE m.SeasonId = <season id> AND (c.[Order] > 0 OR c.Id = <domestic cup id>);
```

To keep the league matches and draw the cup again, delete only the cup's matches. The next tick draws the cup again,
from today's squads:

```sql
DELETE FROM dbo.Matches WHERE SeasonId = <season id> AND CompetitionId = <domestic cup id>;
```

The deletes cascade to each match's statistics, tactics, lineups, substitutions, player statistics and shootout kicks.
