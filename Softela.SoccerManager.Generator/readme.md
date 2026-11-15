# Softela.SoccerManager.Generator

An Azure Functions app (.NET 10, isolated worker) that writes fixtures into the SoccerManager database through its
stored procedures.

The database needs the backend's scripts up to `V1_0_0_57`: each write reads its season through
`dbo.GetMatchesBySeason`, so against an older database the first league with a season to write fails the run. The
domestic cup step needs `dbo.GetMatchScoresBySeason`, `dbo.GetLeagueTeamSquad` and `dbo.InsertStagedMatch` too, which
come with the same scripts: `V1_0_0_57` adds the stage columns that `dbo.InsertStagedMatch` writes.

The continental cup step needs backend `V1_0_0_65` (`IsGuest` on `dbo.GetLeagueTeams`) and the new read
`dbo.GetLeagueSquadRatings`, and it writes standings rows through `dbo.InsertStanding`. Deploy the backend first:
without the procedure the step refuses with a log line, and tries again at the next tick.

## GenerateLeagueFixtures

A timer function with the schedule `0 0 */6 * * *`: every six hours, at 00:00, 06:00, 12:00 and 18:00 host time.

For each league, in id order, it takes the newest season (the highest SeasonNumber) and its divisions (the competitions
with an Order above 0), and runs three steps for it, one after the other: the league matches step, then the domestic
cup step, and then the continental cup step.

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

The continental cup step reads the season again inside its own lock, as the domestic cup step does. The league's two
continental cups are its competitions with Order 0, IsDomestic 0, Format 3 and no age limit (MaxAgeAllowed NULL),
ordered by id: cup 1 is the lower id.

Once the season's league matches exist, it writes the league phase of both cups in one transaction: 48 matches over 6
matchdays for a cup of 16 clubs, or 128 over 8 matchdays for a cup of 32 clubs (the calendar's number of league-phase
matchdays says which), and one standings row at zero for each entrant of each cup. A match is on its matchday's
continental Monday at its calendar time, or on a later Monday that is at least 24 hours after the tick, at least 24
hours from every other match of both clubs this season, and after the matchday before; the whole league phase ends
before the first knockout match.

The entrants come from squad strength, measured as the domestic cup measures it (the best 16 ratings, a missing place
counts 0), until the previous season's tables are used; from Season 2 the step says so in the log:

- the pyramid is Division 1 first, the strongest squad first inside a division;
- cup 1 takes the first 8 clubs of the pyramid and cup 2 the next 8;
- the guests, strongest first, fill the places the league clubs leave empty, cup 1 first;
- every guest is eligible: a guest with a thin or no squad ranks low, and the step logs one warning, when the cups are
  written, that lists the entrants with fewer than 16 rated players;
- each cup's entrants are cut into four pots in that order, and the draw is seeded by the season and the cup.

A written draw is frozen: once both cups have a match of the season, later ticks write nothing, so later sales or
purchases do not change it. Those ticks do not read the cups' standings, so a standings row added or deleted by hand
after the write is not reported.

It skips the cups, and tries again at the next tick, when:

- the league matches are not written yet;
- the calendar has no continental cups at the divisions' size (they need 12 clubs or more in a division);
- the league has no competition with the cups' shape;
- both cups already have matches;
- another run holds the season's lock, or wrote the cups first.

It warns, and tries again at the next tick, when:

- the season plays without continental cups: the league phase can no longer be placed before the first knockout date;
  this warning repeats at every tick;
- entrants have fewer than 16 rated players: this warning is logged once, when the cups are written, and is not a
  retry.

It refuses the cups, which is logged as an error and tried again at every tick, when:

- one competition, or three or more, have the cups' shape: exactly two are needed;
- the cups are only partly written and at least one of them has no match: one cup's matches without the other's,
  standings without matches, or a club in both cups; reset both cups together (see "Writing a season again");
- a club that plays league matches is off the league's roster or is a guest;
- the league has fewer guests than the cups need (2 x cup size minus the league clubs taken, at most 16);
- the backend has no `dbo.GetLeagueSquadRatings`: deploy the backend first;
- a squad lists a player with two different ratings;
- a league match was moved by hand, or the season's matches do not fit the league's clubs or its calendar in another
  way, or a draw does not have the cup's matches; the error says what is wrong.

A guest in a continental cup has a standings row, so it cannot be deleted from the league.

A failed write, of league matches, of cup ties or of continental cups, rolls that write back and fails the run, so the
host reports it.
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

To write the continental cups again, find their ids by shape. Cup 1 is the lower id:

```sql
SELECT Id, Name FROM dbo.Competitions WHERE LeagueId = <league id> AND [Order] = 0 AND IsDomestic = 0 AND Format = 3 AND MaxAgeAllowed IS NULL ORDER BY Id;
```

Delete both cups' matches and both cups' standings for the season. Standings do not cascade from matches, and a
standings row left behind makes the cups partly written, so the step refuses them and the next write would hit the
unique key:

```sql
DELETE FROM dbo.Matches WHERE SeasonId = <season id> AND CompetitionId IN (<continental cup 1 id>, <continental cup 2 id>);
DELETE FROM dbo.Standings WHERE SeasonId = <season id> AND CompetitionId IN (<continental cup 1 id>, <continental cup 2 id>);
```

The two cups are always reset together, never one alone. The next tick draws both again from today's squads. The draw is
seeded by the season and the cup's id, and cup 1 is the lower id, so recreating a competition can swap the cups.
