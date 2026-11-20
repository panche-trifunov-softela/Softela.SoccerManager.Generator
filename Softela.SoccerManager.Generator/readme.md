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

The World Cup qualifier step needs the backend's scripts up to `V1_0_0_69`: `V1_0_0_66` adds the confederation of a
national team, `V1_0_0_68` puts the national teams on a league's roster and `V1_0_0_69` moves the national matches into
`dbo.Matches`, with the group number that `dbo.InsertStagedMatch` writes. It reads through three procedures:
`dbo.GetLeagueNationalTeams` and `dbo.GetNationalTeamPlayerRatings`, which need the scripts up to `V1_0_0_69`, and
`dbo.GetNationalTeamConfederations`, which needs `V1_0_0_66`. Deploy the backend first: without a procedure the step
refuses with a log line, and tries again at the next tick. The two World Cup finals steps need nothing newer from the
backend than the qualifier step does: the finals group step reads the same three procedures, the players' ratings only
on the tick that draws the groups, and the knockout step reads none of them. Both write their matches through
`dbo.InsertStagedMatch`, with stages 11 to 16 and the group number of the group stage.

The season rollover step writes the next season through `dbo.InsertSeason`, which the Generator has not called before:
its SQL user must be allowed to run it. Without that permission the rollover fails inside its transaction, so nothing
is half-written, and like any database error it then stops that run for the leagues after it until the permission is
given. It also writes the new season's standings rows through `dbo.InsertStanding` and its league matches through
`dbo.InsertMatch`, as the league matches step does.

## GenerateLeagueFixtures

A timer function with the schedule `0 0 */6 * * *`: every six hours, at 00:00, 06:00, 12:00 and 18:00 host time.

For each league, in id order, it takes the newest season (the highest SeasonNumber) and its divisions (the competitions
with an Order above 0), and runs nine steps for it, one after the other: the league matches step, then the domestic
cup step, then the continental cup step, then the continental knockout step, then the World Cup qualifier step, then
the World Cup finals group step, then the World Cup knockout step, then the promotion playoff step, and last the
season rollover step.

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

Every tie is on a Tuesday at 20:00 UTC, on its round's calendar date or a later Tuesday: at least 12 hours after the
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
continental Monday at its calendar time, or on a later Monday that is at least 12 hours after the tick, at least 24
hours from every other match of both clubs this season, and after the matchday before; the whole league phase ends
before the first knockout match.

The entrants come from squad strength, measured as the domestic cup measures it (the best 16 ratings, a missing place
counts 0), in Season 1 and whenever the previous season's tables cannot be used; from Season 2 the step says in the log
which it used, and when the tables cannot be used it logs a warning that says why. From Season 2 the pyramid is the
previous season's final tables, Division 1's table first and then Division 2's, and a club that moved counts where it
finished, in the division it left. A club of this season's divisions that the tables do not have comes after the others,
in the order the pyramid has without tables: Division 1 first, the strongest squad first inside a division. Cup 2 takes
the previous season's domestic cup winner first, unless it is in cup 1. When the tables can be used but the previous
season's domestic cup cannot be read, the step logs a warning that says why, and no club is moved up as the cup winner.
A fix that the warning's reason names, such as the cup-only reset, is for a season in play and does not apply to the
previous season. The tables are the season's own results, ranked as the promotion playoffs rank them, so they are the
order the rollover moved the clubs by. In every case:

- the pyramid is Division 1 first, and inside a division the strongest squad first, or the final table's order when the
  tables are used;
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

A failed write, of league matches, of cup ties, of continental cups, of their knockouts, of World Cup qualifiers, of
World Cup finals groups, of World Cup knockouts or of promotion playoffs, rolls that write back and fails the run, so
the host reports it.
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

A match is on its round's calendar Monday at its calendar time when that Monday is at least 12 hours after the tick and
at least 24 hours from every other match of both clubs this season, otherwise on the next Monday that is. The first leg
is after the round before (after the league phase for the first round), and the second leg after the first. The
calendar has no spare week, so a result entered more than about 6 and a half days late (a week less the 12 hours'
notice; a tick may add up to 6 hours), or one clash with another match, moves that tie and the later rounds a week, and
the final can land on the first calendar day of the World Cup finals, which then start a week after it (see "World Cup
finals"). The ticks run every 6 hours (UTC on Azure), so a tie is written within 6 hours of its second club being known.
The knockout's dates have no end bound.

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

## World Cup qualifiers

The World Cup qualifier step runs after the continental knockout step and before the World Cup finals group step. It
writes the group matches of the qualifiers of the league's national competition, the World Cup. It reads the season
again inside its own lock, as the steps before it do, so it sees the league matches that were written on the same
tick. The national competition is the league's one competition with Order 0, IsNational 1 and no age limit
(MaxAgeAllowed NULL); a league with none plays no qualifiers, and a league with more than one is refused.

The qualifiers are written once the season's league matches exist and the calendar has a World Cup (it needs 12 clubs
or more in a division). A nation enters when it has 18 or more players, rated or not, and it is on the league's roster,
with a confederation. Its strength is the sum of its 18 best ratings, a missing rating counting 0. Nations that have
enough players but no confederation, or are not on the roster, are left out, and the step logs one warning for each
kind that lists them. This step writes only the qualifier groups; the finals are written by the two steps that follow
it (see "World Cup finals").

Each confederation is drawn on its own, from the strongest nation to the weakest, and the draw is seeded by the season:

- one entrant has no group and keeps a direct place; the step logs that as information;
- two entrants play one group of 2;
- five entrants play one group of 5, as a single round robin;
- otherwise there is one group for every 4 entrants, rounded up, so the groups hold 4 or 3 nations: pot 1 is the
  strongest nations, as many as there are groups, pot 2 the next ones, and so on, and the last pot may be short. Each
  pot is shuffled and its nations go to the groups in turn, so every group holds one nation of each pot.

The groups are numbered from 1 over the whole draw: the confederations in the order AFC, CAF, CONCACAF, CONMEBOL, OFC,
UEFA, and each confederation's groups in the order they were drawn. A group plays a round robin: two legs (home and
away) for a group of 4, 3 or 2, and one leg for a group of 5.

Every match is a match of the national competition with these values:

- Stage 10, the qualifier group stage, and the group's number in GroupNumber;
- RoundNumber is the group's round: 1 to 6 for a group of 4 or 3 (a group of 3 has a bye in each round), 1 and 2 for a
  group of 2, and 1 to 5 for a group of 5;
- Leg is empty, and so is the referee;
- two MatchTeamStatistics rows at zero, the home nation first, on the nations' roster entries;
- no Standings rows: a national competition has none.

A match is on its round's calendar date, a Friday at 20:00 UTC, or on a later Friday that is at least 12 hours after
the tick, at least 24 hours from every other match of both nations this season, and after the round before. The whole
group stage ends before the first day of the World Cup finals. The calendar has one Friday for each of the 6 rounds.

A written draw is frozen: once the national competition has a match of the season, later ticks only log that the
qualifiers are already written, so later changes to the nations or their players do not change it.

It skips the qualifiers, and tries again at the next tick, when:

- the league has no national competition;
- the league matches are not written yet;
- the calendar has no World Cup at the divisions' size;
- another run holds the season's lock.

It warns, and tries again at the next tick, when:

- the season plays without qualifiers: the groups can no longer be placed before the first day of the finals; this
  warning repeats at every tick;
- no confederation has two or more nations that can enter, so no qualifiers are written.

It refuses the qualifiers, which is logged as an error and tried again at every tick, when:

- the league has two or more national competitions: exactly one is needed;
- the backend has no `dbo.GetLeagueNationalTeams`, `dbo.GetNationalTeamConfederations` or
  `dbo.GetNationalTeamPlayerRatings`: deploy the backend first;
- the national team reads do not fit together: a rostered nation, or a nation with 18 or more players, is missing from
  the national teams read, a nation is on the roster or in that read twice, or a nation with 18 or more players has a
  confederation that is not one of the six;
- a league match was moved by hand, or the season's matches do not fit the league's clubs or its calendar in another
  way; the error says what is wrong.

Its log lines each start with the league and the season, and are:

- information, "the league has no national competition ..., so it plays no World Cup qualifiers";
- information, "the World Cup qualifiers of ... are already written, so this step has nothing to add";
- information, "the calendar has no World Cup at ... clubs per division, so the national competition ... gets no
  qualifiers";
- information, "... is AFC's only entrant, so it plays no qualifier and keeps a direct place";
- information, "the league matches are not written yet, so the World Cup qualifiers wait for a later tick";
- information, "wrote the World Cup qualifiers of ...: n matches in n groups, n entrants (AFC 4, CAF 12, ...), ... to
  ... UTC";
- warning, "the season plays without World Cup qualifiers: they can no longer be placed before ... UTC";
- warning, "nations with 18 or more players but no confederation are left out of the World Cup qualifiers: ...";
- warning, "nations with 18 or more players are not on this league's roster, so they play no World Cup qualifiers: ...";
- warning, "no confederation has two or more nations that can enter, so no World Cup qualifiers are written";
- error, "the World Cup qualifiers are refused: ... The next tick tries again";
- information, "another run holds this season's lock, so this run leaves the World Cup qualifiers to it".

To write the qualifiers again, delete the national competition's matches for the season (see "Writing a season
again"). The next tick draws them again from today's nations.

## World Cup finals

Two steps write the finals of the league's national competition, the World Cup: the finals group step writes the
groups, and the World Cup knockout step writes the knockouts. They run after the World Cup qualifier step and before
the promotion playoff step. Each reads the season again inside its own lock at every tick, so it sees every result
entered up to that moment. They use the national competition that the qualifier step finds. A league with none, or with
more than one, gets no finals, and neither does a league whose qualifiers were not written; the qualifier step logs
why.

The finals groups are written once:

- the season's league matches exist and the calendar has a World Cup (it needs 12 clubs or more in a division);
- every qualifier match is finished: on the tick that writes the qualifiers nothing is finished yet, so the groups come
  at a later tick;
- each continental cup that has a match of the season has its final written; a cup with no match this season is
  ignored.

The finals are played on 8 days in a row, at 20:00 UTC. The first day is the calendar's, the Monday a week after the
continental final. When a continental final is written later than the calendar expects, all 8 days move later by the
same whole number of days, so that the first one is 7 days after the later of the two finals.

A continental cup whose knockouts are refused never writes its final, so it holds the World Cup finals too. The
continental knockout step logs that refusal as an error, and the finals steps log at every tick, at information level,
that they wait for that cup's continental final. Fixing the cup releases the finals.

Who plays is worked out from the qualifier groups as they were written. The finals have 32 nations. A confederation's
places are shared in proportion to its nations, and it has at least 1:

- a confederation whose share of the 32 places is below 1 gets exactly 1 place, and the shares of the others are worked
  out again without it;
- each other confederation gets the whole part of its share, and the places still left go one each to the largest
  fractions, a tie going to the confederation with more nations and then to the lower one in the order AFC, CAF,
  CONCACAF, CONMEBOL, OFC, UEFA;
- the nations of a confederation are its qualifier groups' nations, and a confederation with one entrant and so no group
  counts that one nation, which keeps a direct place and plays no qualifier.

Which confederation has a lone nation is decided from the nations that can enter on the tick that draws the finals
groups, not from the qualifier draw. A confederation that had no entrant at the qualifier draw gets a direct place if
exactly one of its nations can enter by then. If a second nation of a lone nation's confederation can enter before
then, the confederation has two nations and no qualifiers: neither nation has a place, and the step logs a warning
that names them.

With today's 66 nations (AFC 4, CAF 12, CONCACAF 6, CONMEBOL 8, UEFA 36) the places are AFC 2, CAF 6, CONCACAF 3,
CONMEBOL 4 and UEFA 17.

Inside a confederation the winners of its groups come first, then its best runners-up. The nations are put in order by
their place in the group, then by points, goal difference and goals scored, highest first. Those are counted on
comparable matches: in a group of 4, a nation's matches without those against the group's last-placed nation, and in
any other group all its matches. When two nations are level for last place in a group of 4, the group table's seeded
order decides which one is last, and so which results the other three are compared on. Nations that are still level
are put in order by a draw seeded by the season and those nations. A confederation with more places than two for each
of its groups gives the rest to its best third-placed nations in the same way; that is not the case at today's numbers.

The knockout step ranks the tables of the finals groups at every tick once all 48 group matches are finished, and the
group step ranks the tables of the qualifier groups once, on the tick that draws the finals groups. None of them is
stored. All of them are ranked in the World Cup order:

- points;
- head-to-head, only when the nations still level all met the same number of times, at least once: the matches among
  them only, by points, goal difference and goals scored, and a smaller set that is still level is ranked again on its
  own matches;
- goal difference over all the group's matches, then goals scored over all of them;
- then a shuffle seeded by the season and the nations.

The tables of the clubs keep their own order (see "Continental knockouts").

The 32 nations are cut into 4 pots of 8, by strength: the sum of the nation's 18 best ratings as they are when the
groups are written, a missing rating counting 0, and then the lower national team id first. Each pot is shuffled, with
the shuffle seeded by the season, and its nations go to the 8 groups in turn, so every group holds one nation of each
pot. The groups are numbered 1 to 8 in the order they were drawn, A to H.

Each group plays a round robin of one leg, 6 matches over 3 matchdays, 2 matches of each group on each matchday: 48
matches in all, written together. Every match is a match of the national competition with these values:

- Stage 11, the finals group stage, and the group's number, 1 to 8, in GroupNumber;
- RoundNumber is the matchday, 1 to 3, and Leg is empty, and so is the referee;
- two MatchTeamStatistics rows at zero, the home nation first, on the nations' roster entries;
- no Standings rows.

The knockouts follow from the group tables, which are ranked once all 48 group matches are finished. The round of 16 is
1A v 2B, 1C v 2D, 1E v 2F, 1G v 2H, 1B v 2A, 1D v 2C, 1F v 2E and 1H v 2G, the winner of the group first; the winners
of ties 1 and 2 meet in quarter-final 1, those of ties 3 and 4 in quarter-final 2, and so on to the final, so two
nations of one group can meet again only in the final. The third-place match is between the two semi-final losers, the
loser of semi-final 1 first. Home and away are only labels.

A match is written once its two nations are known: the round of 16 once all 48 group matches are finished, a later tie
once the two ties that lead to it are decided, and the third-place match and the final together once both semi-finals
are decided. The ticks run every 6 hours, so a match is written within 6 hours of its nations being known. Every match
is a single match of the national competition with these values:

- Stage 12 for the round of 16, 13 for a quarter-final, 14 for a semi-final, 15 for the third place and 16 for the
  final;
- GroupNumber, RoundNumber and Leg are empty;
- two MatchTeamStatistics rows at zero, one for each side;
- no Standings rows.

A knockout match is decided by its goals, and then by its shootout. Enter a match's goals first and set `IsFinished`
last: a finished match with no goals recorded reads 0-0.

A match is planned at 20:00 UTC on one of the 8 finals days: the group matches on days 1 to 3 by their matchday, the
round of 16 on day 4, the quarter-finals on day 5, the semi-finals on day 6, the third place on day 7 and the final on
day 8. A match is on its planned day when that day is at least 12 hours after the tick, at least 24 hours from every
other match of both nations this season, and after the matches before it, otherwise on the next day that is. The finals
use every day of the week, so that is usually the day after. The matches before a match are the matches of the matchday
before, for a group match; the last group match, for the round of 16; the two ties that lead to it, for a quarter-final
or a semi-final; the later semi-final, for the third place; and the later of the semi-finals and the third place, for
the final. So a late result moves the matches after it a day, or more. A date that no day within a year fits is a
safety stop that refuses the finals.

A written draw is frozen: once the national competition has a match with a stage from 11 to 16, the group step only
logs that the finals already have matches, so the groups are not drawn again; later changes to the qualifier
results, the nations or their players do not change the finals. The knockout step reads the group results
again at every tick.

The group step reads the league's roster and the confederations at every tick that passes the checks on the
matches and the calendar, and the players' ratings only on the tick that draws the groups, after the waits for the
qualifiers and for the continental finals. A tick that waits never costs the ratings.

The group step skips, and tries again at the next tick, when:

- the league has no national competition, or has no qualifier match of the season;
- the league matches are not written yet;
- the calendar has no World Cup finals at the divisions' size;
- the qualifier matches are not all finished: it logs how many are;
- a continental cup has matches but no final yet: it logs the cup;
- the finals already have matches, the groups or the knockouts: it logs that every tick;
- another run holds the season's lock.

The knockout step skips, and tries again at the next tick, when:

- the national competition has no finals group match yet: the group step writes them;
- the league matches are not written yet, or the calendar has no World Cup finals;
- the group matches are not all finished: it logs how many are, of 48;
- a continental cup has matches but no final yet;
- no match has become known since the last tick: it logs that the finals have no new knockout match, with the number of
  knockout matches written and decided, the third-place match counted: 16 and 16 once the finals are over;
- another run holds the season's lock.

The group step warns, and tries again at the next tick, when:

- the qualifiers had fewer than 32 nations, so the season plays without World Cup finals: this warning repeats at every
  tick, and no finals are written;
- a confederation has no qualifier group but two or more nations that can enter today: they played no qualifiers, so
  none of them has a place (a nation that was the confederation's only entrant at the qualifier draw loses its direct
  place this way); this warning repeats at every tick.

When nations that can enter today are in no group of a confederation that has groups, the step also logs one
information line that lists them, and they are left out of the finals.

The group step refuses the finals groups, which is logged as an error and tried again at every tick, when:

- the backend has no `dbo.GetLeagueNationalTeams`, `dbo.GetNationalTeamConfederations` or
  `dbo.GetNationalTeamPlayerRatings`: deploy the backend first; the ratings are only read on the tick that draws the
  groups;
- a qualifier match has no group number, or has no home or no away nation because a statistics row is missing, or has
  one nation on both sides: restore the match, or reset the World Cup (see "Writing a season again");
- a qualifier group has fewer than 2 or more than 5 nations, a nation is in two groups, or a pair did not meet as often
  as its group plays: restore the match, or reset the World Cup;
- a nation of a group is not on the league's roster, has no confederation, or its group's nations are not all of one
  confederation: restore the roster entry or the confederation, or reset the World Cup;
- the national team reads do not fit together, as the qualifier step lists them;
- a league match was moved by hand, or the season's matches do not fit the league's clubs or its calendar in another
  way;
- no day within a year fits a match: move the matches of the nations that are in the way, or reset the finals.

The knockout step refuses the knockouts, which is logged as an error and tried again at every tick, when:

- a finals match has no home or no away nation, or one nation on both sides: restore it, or reset the finals, or, for a
  knockout match, reset the knockouts from its stage;
- the group matches are not 8 groups, numbered 1 to 8, of 4 nations with each pair meeting once, 6 matches each, or a
  group match has a matchday outside 1 to 3 or a leg: restore the match, or reset the finals;
- a knockout match has a group number, a round number or a leg: delete it, or reset the knockouts from its stage;
- a group match is not finished while the knockout matches are written: finish it again with its result, or reset the
  knockouts;
- a knockout match is not a tie of the bracket, because it was made by hand or because a result was edited after the
  next round was written: reset the knockouts from the stage the line names; when the tie that leads to it is no longer
  decided, finish that tie again instead;
- a tie, or the third-place match, has two matches: delete one of them;
- the third-place match is written before both semi-finals are decided, or is not between the two semi-final losers:
  reset the knockouts from stage 15;
- a finished knockout match is level and has no shootout: fix its result;
- a league match was moved by hand, or the season's matches do not fit the league's clubs or its calendar in another
  way;
- no day within a year fits a match: move the matches of the nations that are in the way, or reset the finals.

A refused step writes nothing on that tick. Its log lines each start with the league and the season, and are:

- information, "the league matches are not written yet, so the World Cup finals groups wait for a later tick", and the
  same for "the World Cup knockouts";
- information, "the calendar has no World Cup finals at ... clubs per division, so no finals groups are written", and
  "... no knockout matches are written";
- information, "the World Cup finals wait for the qualifiers: n of n matches are finished";
- information, "the World Cup finals wait for the continental final of ...";
- information, "the World Cup finals already have matches, so the groups are not drawn again";
- information, "nations that can enter today but played no World Cup qualifier are left out of the finals: ...";
- warning, "... has n nations that can enter, but they played no World Cup qualifiers, so none of them has a place in
  the finals: ... A nation that was the confederation's only entrant at the qualifier draw loses its direct place this
  way";
- warning, "the season plays without World Cup finals: the qualifiers had n nations, and the finals need 32";
- information, "wrote the World Cup finals groups, 48 matches in 8 groups: AFC 2, CAF 6, ...; direct places: ...; ... to
  ... UTC", where the direct places are only there when a nation has one;
- information, "the World Cup finals wait for their groups: n of 48 matches are finished";
- information, "the World Cup finals have no new knockout match: n written and n decided", the counts taking in the
  third-place match, so they are 16 and 16 at the end;
- information, "wrote World Cup knockout matches, n in all: round of 16 (8 matches); ... to ... UTC", with the rounds
  written on that tick, such as "third-place match (1 match), final (1 match)";
- error, "the World Cup finals groups are refused: ... The next tick tries again";
- error, "the World Cup knockouts are refused: ... The next tick tries again";
- information, "another run holds this season's lock, so this run leaves the World Cup finals groups to it", and "...
  the World Cup knockouts to it".

To write the finals again, or only the knockouts from a round, see "Writing a season again".

## Promotion playoffs

The promotion playoff step runs after the World Cup steps and before the season rollover step. It acts on a division
whose `TeamsInPlayoffs` is above 0 and that has a match of the season; a League with no such division is left alone, and
the step then does nothing and logs nothing. From then on it reads the season again inside its own lock at every tick,
so it sees every result entered up to that moment. Each division with a playoff is handled on its own, the top division
first.

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
later semi-final. A match is at least 12 hours after the tick and at least 24 hours from every other match of both clubs
this season, otherwise it is on the next of those days that is. From 12 clubs per division a league has a World Cup, and
no playoff match is dated on or after the first day of the World Cup finals. Those finals start on the Monday eight days
after the last league round, so only the Tuesday, the Thursday and the Sunday between them are left: the semi-finals
take the Tuesday, the final the Thursday, and the Sunday is the one spare day. Semi-finals that move to the Thursday,
because a club has another match within 24 hours or the Standings settled too late for a tick 12 hours before the
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

## Season rollover

The season rollover step runs last. When the league's newest season has ended it moves the clubs between the divisions
and writes the next season, in one transaction.

A season ends when every match that decides it is finished:

- every league match of every division, and every playoff match of a division with a playoff;
- the domestic cup's final, when the season has cup matches; a season with no cup match at all is played without a cup;
- from 12 clubs per division, which is when a league has a World Cup: the World Cup final, when the season plays a
  World Cup, which is when the league's national competition has a match in it; a season that does not play one ends
  with the continental finals instead, the final of each continental cup that has a match in the season;
- friendlies never decide it, and the continental matches do not while a World Cup is played.

The step does nothing until the last league or playoff match of the divisions has kicked off, and then it logs
"the season runs until ... UTC, so it does not roll over yet" for as long as one has not. From then on it reads the
season again inside its own lock at every tick, so it sees every result entered up to that moment. It acts on the
newest season only: when the league already has a newer season, it logs that and writes nothing.

While the season is not over, the step logs one information line a tick, "the season is not over: it waits for ...",
and writes nothing. It waits for the first of these that is true, in this order:

- the season's league matches, which are not written yet;
- the league matches to be finished: n of m are;
- a division with a playoff: its league matches, its Standings to settle for an hour, or its playoff matches to be
  decided, the top division first;
- the domestic cup's final;
- the World Cup final, or the continental finals, as above.

It refuses the season, which is logged as an error with what to do and tried again at every tick, when:

- the divisions' promotion, relegation or playoff settings contradict each other or their clubs (see "Promotion
  playoffs"), or a division's `TeamsInPlayoffs` is above 0 and the season's playoff matches are not as that section
  describes;
- no division has Standings rows, or some divisions have them and another has none, or a Standings row is for a club
  that is a guest on the roster: add or delete the rows, give a tier that is gone Order 0, or make the next season by
  hand;
- a division's Standings disagree with its league matches: a division with a playoff needs its wins, draws, losses and
  goals to agree with the results, as the playoffs do, and every division needs a Standings row for each club of its
  matches, and each pair of clubs to have met once at home and once away; the divisions without a playoff are ranked
  from their matches, and their Standings numbers are not read;
- a league match lacks a club, because one of its two statistics rows is missing, a club is in two divisions, or a
  division has no league matches while the others have them: give a tier that is gone Order 0, or make the next season
  by hand;
- the league has two competitions with the domestic cup's shape, or two national competitions, so the final that ends
  the season cannot be told apart: keep one of them;
- a World Cup, continental or domestic cup final is finished and level with no shootout, a World Cup or continental
  final lacks a statistics row for one side, or the season has two finals of one cup: fix its result or its rows, or
  delete the extra one;
- the moved clubs cannot be laid out as a season, for example because too many clubs of one division share a stadium, or
  because an existing match is more than 365 days ahead.

Do not reset a season that has ended to clear a refusal: that would delete its results. The rollover's refusals say what
to fix by hand, or to make the next season by hand. The other steps can refuse the same problem in the same tick and say
to reset the season, which does not apply to a season that has ended. A refusal of the domestic cup can name the
cup-only reset, and a refusal of a promotion playoff can name deleting the division's playoff matches; each deletes
only those matches, not the season's results. A refusal ends only this league's rollover for the tick, and the other
leagues carry on.

The clubs move by the settings of the divisions, with P = `TeamsPromoted`, Q = `TeamsInPlayoffs` and
R = `TeamsRelegated` as in "Promotion playoffs", from the tables as they ended: the first P - 1 places of a division go
up, or the first P when Q is 0; with a playoff, its winner takes the last promotion place; the bottom R places go down.
Each division has the same number of clubs in the next season. A league of one division, or whose settings are all 0,
moves nobody.

It then writes, in one transaction:

- season N + 1 of the league, with the run's moment as its creation time;
- one Standings row at zero for each club of each division, on the division it plays in now;
- the season's league matches, with two MatchTeamStatistics rows each, laid out as the league matches step lays out a
  season.

It writes nothing else: the cups, the continental cups and the World Cup qualifiers of the new season are written by
their own steps on the next tick, and the roster, the squads, the contracts, the loans, the buybacks and the money do
not change. A competition that had league matches in the old season but has Order 0 now gets no Standings, and the step
logs it.

The first league Thursday is the first Thursday at least 7 days after the kickoff date of the last match that decided
the old season, or the first Thursday after the day of the run when that is later, and at least 24 hours after every
match that already exists, friendlies included.

Its log lines each start with the league and the season, and are:

- information, "the season had no league matches at the start of this run, so it cannot end yet";
- information, "the season runs until ... UTC, so it does not roll over yet";
- information, "the season is not over: it waits for ...";
- information, "season n (id n) exists already, so this season does not roll over";
- information, "the season does not exist any more, so it does not roll over";
- information, "season n was made while this run rolled the season over, so nothing is written";
- information, "rolled the season over: wrote season n (id n) with n clubs in n divisions and n league matches, ... to
  ... UTC; promoted ...; relegated ...", with each moved club as the team's name and its league team id, and then one
  line for each competition that has Order 0 now;
- error, "the season rollover is refused: ... The next tick tries again";
- information, "another run holds this season's lock, so this run leaves the season rollover to it".

A season that somebody makes by hand closes the old one: the step sees a newer season and does not roll the old one
over. A refused write that is tried at every tick can use up a season id each time; the ids then skip, which does no
harm.

A season whose World Cup qualifiers had fewer than 32 nations cannot play its finals (the finals group step logs a
warning at every tick), so it never ends: make the next season by hand instead.

To roll the season over again, delete the new season. Its Standings and matches are deleted with it, and the delete is
refused once a bid or a deal uses the season:

```sql
DELETE FROM dbo.Seasons WHERE Id = <new season id>;
```

The same delete is `DELETE api/seasons/{id}` on the backend. The next tick then rolls the old season over again, with
new fixtures. To stop that, make the next season by hand.

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

Delete the season's division, cup and national competition matches, and the next tick writes them again, the cup and
the World Cup qualifiers included. The national competition's matches are the qualifiers and the finals, so the finals
are deleted with them. A national competition has no Standings, so nothing else is deleted for it:

```sql
DELETE m FROM dbo.Matches m INNER JOIN dbo.Competitions c ON c.Id = m.CompetitionId WHERE m.SeasonId = <season id> AND (c.[Order] > 0 OR c.Id = <domestic cup id> OR c.IsNational = 1);
```

On a later season, writing the season again rewrites its league from the day of the run, with no gap of 7 days after
the season before: only the season rollover keeps that gap. A competition or a roster club that has matches or
Standings cannot be deleted, so delete those first.

To keep the league matches and draw the cup again, delete only the cup's matches. The next tick draws the cup again,
from today's squads:

```sql
DELETE FROM dbo.Matches WHERE SeasonId = <season id> AND CompetitionId = <domestic cup id>;
```

To keep the league matches and draw the World Cup qualifiers again, delete only the national competition's matches. The
next tick draws them again, from today's nations and players. This deletes the finals too, which are then drawn again
from the new qualifiers:

```sql
DELETE m FROM dbo.Matches m INNER JOIN dbo.Competitions c ON c.Id = m.CompetitionId WHERE m.SeasonId = <season id> AND c.IsNational = 1;
```

To keep the qualifiers and draw the World Cup finals again, find the national competition's id and delete its matches
from Stage 11, the finals groups, up. The next tick draws the finals again from the current qualifier results, once
the qualifiers are all finished:

```sql
SELECT Id, Name FROM dbo.Competitions WHERE LeagueId = <league id> AND [Order] = 0 AND IsNational = 1 AND MaxAgeAllowed IS NULL;
DELETE FROM dbo.Matches WHERE SeasonId = <season id> AND CompetitionId = <national competition id> AND Stage >= 11;
```

To keep the finals groups and write the knockouts again, delete the knockout matches from a stage. The next tick writes
them again from the current results, on the next free day:

```sql
DELETE FROM dbo.Matches WHERE SeasonId = <season id> AND CompetitionId = <national competition id> AND Stage >= <stage>;
```

Use `<stage>` 12 to delete every knockout match of the finals, or the stage that a refusal names to delete that round
and the rounds after it: 12 for the round of 16, 13 for the quarter-finals, 14 for the semi-finals, 15 for the third
place and the final, 16 for the final alone. A knockout match deleted alone is written again, unless a match after it is
written.

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
