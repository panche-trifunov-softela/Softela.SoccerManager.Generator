# Softela.SoccerManager.Generator

An Azure Functions app (.NET 10, isolated worker) that writes fixtures into the SoccerManager database through its
stored procedures.

The database needs the backend's scripts up to `V1_0_0_57`: each write reads its season through
`dbo.GetMatchesBySeason`, so against an older database the first league with a season to write fails the run. The
domestic cup step needs `dbo.GetMatchScoresBySeason`, `dbo.GetLeagueTeamSquad` and `dbo.InsertStagedMatch` too, which
come with the same scripts: `V1_0_0_57` adds the stage columns that `dbo.InsertStagedMatch` writes.

The continental cup step, which writes the league phase, needs backend `V1_0_0_65` (`IsGuest` on `dbo.GetLeagueTeams`)
and the new read `dbo.GetLeagueSquadRatings`, and it writes standings rows through `dbo.InsertStanding`. Deploy the
backend first: without the procedure the step refuses with a log line, and tries again at the next tick. The
continental knockout step needs nothing more than the domestic cup step does: it writes its matches through
`dbo.InsertStagedMatch`, with stages 5 to 9 and no round number. The promotion playoff step needs the same, with
stages 2 and 3, and no round number and no leg.

## GenerateLeagueFixtures

A timer function with the schedule `0 0 */6 * * *`: every six hours, at 00:00, 06:00, 12:00 and 18:00 host time.

For each league, in id order, it takes the newest season (the highest SeasonNumber) and its divisions (the competitions
with an Order above 0), and runs five steps for it, one after the other: the league matches step, then the domestic
cup step, then the continental cup step, then the continental knockout step, and then the promotion playoff step.

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
- the divisions' promotion, relegation or playoff settings contradict each other or their clubs, with or without
  playoffs (see "Promotion playoffs");
- three clubs are on one stadium, or more than half of a division's clubs share a stadium with another club;
- the first match would be more than 365 days away, because an existing match is that far ahead.

The domestic cup step reads the season again inside its own lock, so it sees the league matches the step before it
wrote. The cup is the league's one competition with Order 0, IsDomestic 1, IsNational 0, Format 1 and no age limit
(MaxAgeAllowed NULL); a youth cup is never it. Its entrants are the clubs of the season's written league matches.

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
continental cups are its competitions with Order 0, IsDomestic 0, IsNational 0, Format 3 and no age limit
(MaxAgeAllowed NULL), ordered by id: cup 1 is the lower id.

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

A written draw is frozen: once both cups have a match of the season, later ticks write no league phase, so later sales
or purchases do not change it. Those ticks do not read the cups' standings, so a standings row added or deleted by
hand after the write is not reported. The knockouts that follow the league phase are written by the continental
knockout step (see "Continental knockouts").

It skips the cups, and tries again at the next tick, when:

- the league matches are not written yet;
- the calendar has no continental cups at the divisions' size (they need 12 clubs or more in a division);
- the league has no competition with the cups' shape;
- both cups already have matches, which is to say the league phase is written;
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

A failed write, of league matches, of cup ties, of continental cups, of their knockouts or of promotion playoffs, rolls
that write back and fails the run, so the host reports it.
Everything written before it stays written, and the next tick starts over from the first league.

## Continental knockouts

The continental knockout step runs after the continental cup step, which writes the league phase. It does nothing until
both continental cups have a match of the season: on the tick that writes the league phase it finds nothing yet, and
nothing can be finished by then anyway. From then on it reads the season again inside its own lock at every tick, so it
sees every result entered up to that moment. Each cup is handled on its own, cup 1 first.

A cup's knockout starts once all of its league-phase matches are finished: 48 matches for a cup of 16 clubs, 128 for a
cup of 32. Until then the step logs one information line a tick for the cup: "waits for its league phase: n of 48
matches are finished".

The cup's table is ranked from its league phase at every tick, so it is never stored:

- points, then goal difference, then goals scored;
- head-to-head, only when the clubs still level all met the same number of times, at least once;
- then a shuffle seeded by the season and the clubs.

A club has met only 6 of the other 15 clubs (8 of 31 in a cup of 32), so clubs that are level are usually ordered by the
shuffle, also at the cut (8th and 9th place, 24th and 25th place).

The bracket comes from the table, and the higher place hosts the second leg and the final:

- 16 clubs: the quarter-finals are 1 v 8, 4 v 5, 2 v 7 and 3 v 6; places 9 to 16 are out;
- 32 clubs: the play-offs are 9 v 23, 11 v 21, 13 v 19 and 15 v 17, and 10 v 24, 12 v 22, 14 v 20 and 16 v 18; the
  round of 16 pairs their winners with places 1 to 8; places 25 to 32 are out;
- the quarter-finals, the semi-finals and the final follow.

Each tie is written once both of its clubs are known, with both legs together. The matches have these values:

- Stage 5 for a play-off, 6 for the round of 16, 7 for a quarter-final, 8 for a semi-final and 9 for the final;
- Leg 1 and 2 for a tie of two legs, none for the final;
- RoundNumber is empty, because a knockout stage has no rounds;
- two MatchTeamStatistics rows at zero, one for each side;
- no Standings rows: a knockout match does not change the league phase's table.

A match is on its round's calendar Monday at its calendar time when that Monday is at least 24 hours after the tick and
at least 24 hours from every other match of both clubs this season, otherwise on the next Monday that is. The first leg
is after the round before (after the league phase for the first round), and the second leg after the first. The
calendar has no spare week, so a result entered more than about 6 days late, or one clash with another match, moves that
tie and the later rounds a week, and the final can land on the first calendar day of the World Cup finals. The ticks run
every 6 hours (UTC on Azure), so a tie is written within 6 hours of its second club being known. The knockout's dates
have no end bound.

A tie is decided by its goals over both legs; own goals count and shootouts do not, and there is no away-goals rule.
When the legs are level, the shootout of the second leg decides. The final is decided by its goals, and then by its
shootout. Enter a match's goals first and set `IsFinished` last: a finished match with no goals recorded reads 0-0.

It skips a cup, and tries again at the next tick, when:

- its league phase is not finished: the information line above;
- no tie has become known since the last tick: it logs that the cup has no new knockout tie, with the number of ties
  written and decided;
- another run holds the season's lock.

It refuses a cup, which is logged as an error and tried again at every tick, when:

- a finished tie is level on aggregate and its second leg has no shootout, or the final is finished with no winner:
  fix the result;
- a match of the cup has no home or no away club, or a stage or a leg that the cup does not have: delete it, or reset
  the cup's knockout matches (for a league-phase match, reset both cups together);
- a knockout match is not a leg of a known tie, because it was made by hand or because a result was edited after the
  next round was written: reset the cup's knockout matches from the stage the line names (see "Writing a season
  again");
- a tie has two matches for one leg, or the final has two matches: delete one of them;
- a tie has only one leg, or its two legs are not played with the home and away clubs swapped: delete its legs, and
  the next tick writes the tie again; if its next round is already written, reset the cup's knockout matches from the
  stage the line names instead;
- a league-phase result, or an earlier round's result, was reopened after the next round was written: finish it
  again, or reset the knockout matches;
- the league phase is damaged, with matches missing or moved to other clubs: reset both cups together (see "Writing a
  season again");
- no Monday within a year fits a tie: move the matches of either club that are in the way.

A refused cup writes nothing on that tick, and the other cup carries on. The refusals of the whole season are the same
for both cups: the season has no league matches, the calendar has no continental cups, or its league phase has a number
of matchdays that no cup has. Both cups are refused then.

Its log lines each start with the league and the season, and are:

- information, "the continental cup ... waits for its league phase: n of 48 matches are finished";
- information, "the continental cup ... has no new knockout tie: n ties are written and n decided";
- information, "wrote knockout matches of the continental cup ..., n in all: semi-final ties 1 and 2 (4 matches)";
- error, "the continental knockouts of ... are refused: ... The next tick tries again";
- error, "the continental knockouts are refused: ... The next tick tries again", for both cups;
- information, "another run holds this season's lock, so this run leaves the continental knockouts to it".

## Promotion playoffs

The promotion playoff step runs last. It acts on a division whose `TeamsInPlayoffs` is above 0 and that has a match of
the season; a League with no such division is left alone, and the step then does nothing and logs nothing. From then on
it reads the season again inside its own lock at every tick, so it sees every result entered up to that moment. Each
division with a playoff is handled on its own, the top division first.

The places of a division's table come from its settings, with P = `TeamsPromoted`, Q = `TeamsInPlayoffs` and
R = `TeamsRelegated`:

- places 1 to P - 1 go up, or 1 to P when Q is 0;
- places P to P + Q - 1 play off for the last promotion place;
- the bottom R places go down.

The settings must fit, and the league matches step refuses a league that breaks one of these before it writes a
season, with or without playoffs. The playoff step checks them again at every tick and refuses every division. A
division is refused at the first rule it breaks:

- `TeamsPromoted` or `TeamsRelegated` is negative;
- `TeamsInPlayoffs` is not 0, 2 or 4;
- `TeamsInPlayoffs` is above 0 and `TeamsPromoted` is below 2, so no place is left for an automatic promotion;
- the top division has `TeamsPromoted` or `TeamsInPlayoffs` above 0;
- the bottom division has `TeamsRelegated` above 0 (a league of one division is both, so all three are 0);
- a division relegates another number of clubs than the division below it promotes;
- the places need more clubs than the division has: P + R without a playoff, P - 1 + Q + R with one.

A division's playoff starts once all of its league matches are finished and its Standings have not changed for an hour,
measured from the newest ModifiedAt of its rows. Until then the step logs one information line a tick for the division:
"waits for its league matches: n of m are finished", or "waits: its newest Standings change, ..., is under an hour old".

The table is ranked from the division's league matches at every tick, so it is never stored: points, then goal
difference, then goals scored, then head-to-head and then the seeded shuffle, as in the continental knockouts. The
Generator's order wins over an API table that lists level clubs another way. The Standings must agree with the results,
or the division is refused. The checks run in this order:

- the division has as many league matches as its Standings clubs play (each club hosts each other club once);
- every club of a league match has a Standings row;
- every club hosted every other club exactly once;
- each club's wins, draws and losses add up to the matches it played, which also means no playoff match is counted in
  them;
- each club's goals for and against equal the sums over its league matches.

Until the events generator exists, the results and the Standings of a division with a playoff are kept by hand. Playoff
matches never count in the Standings, and a drawn playoff match always has a shootout.

Who plays, where the table has P as the first playoff place:

- Q = 4: semi-final 1 is place P v place P + 3, and semi-final 2 is place P + 1 v place P + 2, the higher place at
  home; the final is between the two winners, at home for the winner with the better place;
- Q = 2: only a final, place P at home v place P + 1;
- one stadium does not host both semi-finals: when the home club of semi-final 2 shares a stadium with the home club of
  semi-final 1, semi-final 2 swaps its hosts. This is the only swap, and a club with no stadium counts as having none.

A semi-final is written once the table is known. The final is written once both semi-finals are decided. Every match is
a single match with these values:

- Stage 2 for a semi-final and 3 for the final;
- RoundNumber and Leg are empty;
- two MatchTeamStatistics rows at zero, one for each side;
- no Standings rows.

The step finds a written match again by its stage and its pair of clubs, whichever club hosts it.

A match is at 20:00 UTC on a Tuesday, a Thursday or a Sunday. The semi-finals, or the final of a 2-club playoff, are on
the Tuesday after the last league round, and the final is on the next Tuesday, Thursday or Sunday after the day of the
later semi-final. A match is at least 24 hours after the tick and at least 24 hours from every other match of both clubs
this season, otherwise it is on the next of those days that is. From 12 clubs per division a league has a World Cup, and
no playoff match is dated on or after the first day of the World Cup finals. Those finals start on the Monday eight days
after the last league round, so only the Tuesday, the Thursday and the Sunday between them are left: the semi-finals
take the Tuesday, the final the Thursday, and the Sunday is the one spare day. Semi-finals that move to the Thursday,
because a club has another match within 24 hours or the Standings settled too late for a tick 24 hours before the
Tuesday, push the final to the Sunday; semi-finals on the Sunday leave the final no date. A match that no date fits
before then is named in the error line, and it can be inserted by hand: a match with Stage 2 or 3 on the division's
competition, RoundNumber and Leg empty, and both MatchTeamStatistics rows, the home club at home. The next tick accepts
it when its clubs fit the table.

A playoff match is decided by its goals, and then by its shootout. The shootout counts only when both of its values are
entered and differ. Enter a match's goals first and set `IsFinished` last: a finished match with no goals recorded
reads 0-0. The winner of the final is the club the playoff promotes; the step reads it and writes nothing for it.

It skips a division, and tries again at the next tick, when:

- its league matches are not all finished: the information line above;
- its Standings changed under an hour ago: the information line above;
- no match has become known since the last tick: it logs that the division has no new match, with the number of matches
  written and decided;
- another run holds the season's lock.

It refuses a division, which is logged as an error and tried again at every tick, when:

- a league match is unfinished while the playoff is written: finish it again, or delete the division's playoff matches;
- a playoff match has no home or no away club, or has a round number or a leg, or is a semi-final of a 2-club playoff:
  delete it;
- the playoff has two finals, or more than two semi-final matches, or a semi-final has two matches: delete the extra
  ones;
- the Standings disagree with the league matches, one check above: fix the Standings row, or reset the season;
- the table cannot be ranked, or has fewer clubs than the playoff spans: fix the settings, or reset the season;
- a playoff match is not a tie of the table as it stands, because a result or a setting changed after it was written:
  put back the result or setting that changed, or delete the division's playoff matches;
- a finished playoff match is level and has no shootout: fix its result;
- the final is written while a semi-final is not written or has no winner, or the final is not between the semi-final
  winners: finish the semi-final again, or delete the final and it is written again;
- no Tuesday, Thursday or Sunday within a year fits a match, or none before the World Cup finals fits it at least 24
  hours from both clubs' other matches: move the matches of either club that are in the way;
- a match is too late for the World Cup finals: insert it by hand, as above.

A refused division writes nothing on that tick, and the other divisions carry on. These refusals are for the whole step,
and every division is refused: the settings above; the season has no league matches but its divisions have playoff
matches (delete them); a division has no clubs, a club plays in two divisions or a league match lacks a club; the league
matches are not on the matchdays of their calendar, because one was moved by hand or made by hand (move it back, or
reset the season).

Its log lines each start with the league and the season, and are:

- information, "the promotion playoff of ... waits for its league matches: n of m are finished";
- information, "the promotion playoff of ... waits: its newest Standings change, ... UTC, is under an hour old";
- information, "the promotion playoff of ... has no new match: n are written and n decided";
- information, "wrote promotion playoff matches of ..., n in all: semi-finals 3 v 6 and 4 v 5 (2 matches)", "semi-final
  4 v 5 (1 match)" when one semi-final is written again, or "final 3 v 4 (1 match)", with table places and the home club
  first;
- error, "the promotion playoff of ... is refused: ... The next tick tries again";
- error, "the promotion playoffs are refused: ... The next tick tries again", for every division;
- information, "another run holds this season's lock, so this run leaves the promotion playoffs to it".

To write one division's playoff again, delete its playoff matches. The next tick writes them again from the current
table:

```sql
DELETE FROM dbo.Matches WHERE SeasonId = <season id> AND CompetitionId = <division id> AND Stage IN (2, 3);
```

To write every division's playoff again:

```sql
DELETE m FROM dbo.Matches m INNER JOIN dbo.Competitions c ON c.Id = m.CompetitionId WHERE m.SeasonId = <season id> AND c.[Order] > 0 AND m.Stage IN (2, 3);
```

The deletes cascade to each match's statistics rows, as the resets in "Writing a season again" do.

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
SELECT Id, Name FROM dbo.Competitions WHERE LeagueId = <league id> AND [Order] = 0 AND IsDomestic = 1 AND IsNational = 0 AND Format = 1 AND MaxAgeAllowed IS NULL;
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
The season reset above already deletes the promotion playoff matches, because they are matches of the divisions; the
resets that keep the league matches are in "Promotion playoffs".

To write the continental cups again, find their ids by shape. Cup 1 is the lower id:

```sql
SELECT Id, Name FROM dbo.Competitions WHERE LeagueId = <league id> AND [Order] = 0 AND IsDomestic = 0 AND IsNational = 0 AND Format = 3 AND MaxAgeAllowed IS NULL ORDER BY Id;
```

Delete both cups' matches and both cups' standings for the season. Standings do not cascade from matches, and a
standings row left behind makes the cups partly written, so the step refuses them and the next write would hit the
unique key:

```sql
DELETE FROM dbo.Matches WHERE SeasonId = <season id> AND CompetitionId IN (<continental cup 1 id>, <continental cup 2 id>);
DELETE FROM dbo.Standings WHERE SeasonId = <season id> AND CompetitionId IN (<continental cup 1 id>, <continental cup 2 id>);
```

The two cups' league phases are always reset together, never one alone. The next tick draws both again from today's
squads. The draw is seeded by the season and the cup's id, and cup 1 is the lower id, so recreating a competition can
swap the cups.

To write one cup's knockout again, delete its knockout matches. The league phase stays, which is why one cup can be
reset alone:

```sql
DELETE FROM dbo.Matches WHERE SeasonId = <season id> AND CompetitionId = <continental cup id> AND Stage >= <stage>;
```

Use `<stage>` 5 to delete every knockout match of the cup, or the stage that a refusal names to delete that round and
the rounds after it. The delete takes the matches with their results and statistics, by the cascade. The next tick
writes them again from the current table, on the next free Monday. To delete one whole tie by hand, delete both of its
matches: the next tick writes it again, unless its next round is written.
