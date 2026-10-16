# Reviews: wave-variety

## Review 1 · 2026-10-16 · subagent · plan uncommitted · plan 54495 B · 24 items · files 0 · e3b0c44298fc · prompt d99e98468b85
L1 · Considered · no finding
L2 · Considered · no finding
L3 · Considered · no finding
L4 · Considered · F1, F2, F5
L5 · Considered · F1
L6 · Considered · F4
L7 · Considered · F1
L8 · Considered · no finding
L9 · Considered · F6
L10 · Considered · no finding
L11 · Considered · no finding
L12 · Considered · no finding
L13 · Gap · F3
L14 · Considered · no finding
L15 · Considered · F6

F1 · blocking · blocks: outcome · D8 promises a pursuing run's arena no longer returns strays and units stay where they stand with no eligible player, but unit-watch's landing rule (Logic/UnitWatch.cs:970-975, Context.Allowed) confines landings to the arena radius, and the stall backstop (StallMinutes 10) marks a waiting run's leftovers; D8's fails-when catches neither. Fix: state where unit-watch lands a pursuer and whether the stall backstop applies; add both to D8's or D9's fails-when.
F2 · advisory · D14 "lists exactly the units D13 recorded allied" would drop bandits, CHAR_Legion_Guardian_DraculaMinion and the cursed _Spirit units existing templates use. Fix: GAME_ASSETS' allied rows (D37/D60 plus D13's); say what happens when D13 finds hostile a unit an existing template uses.
F3 · advisory · 13.1 quotes no recon measurement. Fix: quote a current timing-span figure as the baseline.
F4 · advisory · D18's 2-2 undead final roll needs a second allied undead V Blood. Fix: name a fallback (silverlight-onslaught).
F5 · advisory · bosses are distinct only within one roll; two entries can repeat a V Blood. Fix: state whether distinctness holds across a wave's boss list.
F6 · advisory · pursuit ignores action.scope, unstated; spider-swarm is the units form, so its variants must be written as a waveList. Fix: a Business rules line; note the conversion in step 3.
F7 · advisory · the 5-boss cap's reach over the action-level list is unclear. Fix: state it.

missing components: none
undeclared families: Admin, Point, AroundPlayer, Region
14/15 layers · 49/50 probes
VERDICT: REVISE

### Dispositions
- F1 · accepted · D8: unit-watch lands a pursuing unit anywhere outside claimed territory, never confined to the arena, and its stall backstop does not mark a pursuing run's units; both added to D8's fails-when; Business rules 6 and step 1 (Logic/UnitWatch.cs) say so, Paths walked lists UnitWatch.cs
- F2 · accepted · D14 reads GAME_ASSETS' allied rows (the D37 and D60 rows plus D13's); a unit D13 finds hostile that an existing template uses is dropped from that template by an amendment first
- F3 · accepted · 13.1 quotes apocalypse D29's span (10/10 windows under 5 ms, tracked >= 50, 2026-10-14) as the baseline of the same jump-and-seed machinery
- F4 · accepted · D18 names silverlight-onslaught as the fallback
- F5 · accepted · D3 and D5: a roll never draws a boss its wave already holds; only a fixed entry's own count gives copies
- F6 · accepted · Business rules 4: pursuit ignores action.scope; step 3: spider-swarm's variants are written out as waveLists
- F7 · accepted · D3: the action-level list is held to the same 5-boss cap
- UF1 · accepted · D8 covers the family (Admin, Point, AroundPlayer, Region): its area is the arena or Hunt range around the wave centre whatever the location type, and Business rules 4 says a Region-location or scoped run chases off its scope

## Review 2 · 2026-10-16 · subagent · plan uncommitted · plan 56499 B · 24 items · files 0 · e3b0c44298fc · prompt d49ed398ff55
L1 · Considered · no finding
L2 · Considered · F5
L3 · Considered · F2
L4 · Considered · F1, F4
L5 · Considered · F3
L6 · Considered · no finding
L7 · Considered · no finding
L8 · Considered · no finding
L9 · Considered · F2
L10 · Considered · F5
L11 · Considered · F6
L12 · Considered · no finding
L13 · Considered · F2
L14 · Gap · F1
L15 · Considered · no finding

F1 · blocking · blocks: outcome · 14.2: D3's 5-boss cap on the action-level `bosses` would refuse a 0.12.0 final wave of 6-9 fixed bosses (MaxBosses 3, MaxBossCount 3), against Compatibility and D3's own fails-when. Fix: keep 0.12.0's 3x3 for the fixed form, or cap all and say so.
F2 · blocking · blocks: outcome · 13.2: a 30-member pool does not fit one 512-byte chat line, so D10's `action.pools.<name> <list>` cannot set a valid pool. Fix: an append and remove form, a lower cap, or file-only, with a fails-when.
F3 · advisory · 5.3: Apocalypse shares ParseBosses and the bosses chat field; the roll form on an Apocalypse action is unspecified. Fix: refuse it with a reason.
F4 · advisory · 4.1: a roll of max = pool size cannot be met when a fixed boss of the wave is in the pool. Fix: bound max by the pool less those, or refuse the overlap.
F5 · advisory · 10.1/2.1: the admin-only `api event set` twin reaches the new fields, so "never reach the wire" is wrong. Fix: reword D11 and list the twin under 10.1.
F6 · advisory · 11.4/D17: a seeded unit may walk into the castle by game pathing. Fix: check none stands inside after 60 s, or record walk-in as accepted.

missing components: none
undeclared families: bosses on SpawnWaves, bosses on Apocalypse; bosses chat roll form, wave bosses chat roll form
14/15 layers · 49/50 probes
VERDICT: REVISE

### Dispositions
- F1 · accepted · D3: an action-level list of fixed bosses keeps 0.12.0's limit (1-3 entries of count 1-3); a wave's own bosses and an action-level list holding a roll count against 5; D3's fails-when gains "a 0.12.0 fixed list of 9 bosses is refused"
- F2 · accepted · D10: `+<prefab>` and `-<prefab>` add or remove one pool member; a 30-member pool is built by appends; D10's fails-when gains it
- F3 · accepted · D3: the roll form is refused on an Apocalypse action's bosses with its reason, in the fails-when
- F4 · accepted · D3: a roll's max is at most its pool's size less the wave's fixed bosses from that pool, in the fails-when
- F5 · accepted · D11's contract sentence: settable through the admin-only api twin, no row, push or read carries them; 10.1 lists the twin
- F6 · accepted · D17: no unit of the run stands inside the territory after 60 s; a unit following by the game's pathing is recorded
- UF1 · accepted · D3 covers action.bosses on SpawnWaves and on Apocalypse (the roll refused on Apocalypse)
- UF2 · accepted · D10 covers the chat roll form of action.bosses and action.waveList.<n>.bosses (both take `pool:<name>:<min>-<max>`)

## Review 3 · 2026-10-16 · subagent · plan uncommitted · plan 58281 B · 24 items · files 0 · e3b0c44298fc · prompt 7998e9e04bbb
L1 · Considered · no finding
L2 · Considered · no finding
L3 · Considered · F1
L4 · Considered · F3
L5 · Considered · no finding
L6 · Considered · no finding
L7 · Considered · F4, F5
L8 · Considered · no finding
L9 · Considered · F4
L10 · Considered · no finding
L11 · Considered · F2
L12 · Considered · no finding
L13 · Considered · no finding
L14 · Considered · F5
L15 · Considered · no finding

F1 · advisory · 3.1/D10: the editor writes and disables an invalid definition rather than refusing, so adding a pool before its entry passes through "action.pools.foo is not used". Fix: say a new pool leaves the definition disabled until an entry names it; test both orders to a valid, enabled definition.
F2 · advisory · 11.2/D6: a 30-member pool does not fit one 480-byte info line. Fix: the "… and <m> more" rule or wrapping for event and template info.
F3 · advisory · 4.1/D16: no whole-run bound for onslaught. Fix: add the 150-unit bound at the roll maxima, or accept waiting at the cap.
F4 · advisory · 7.2/9.3: how the 20 jumps per tick are shared between events is unstated. Fix: name the order.
F5 · advisory · 14.1/7.2: `action.pursuit none` does not stop a running pursuit. Fix: say so in Shipping; stop or purge ends it now.

EARLIER: all resolved
missing components: none
undeclared families: none
15/15 layers · 50/50 probes
VERDICT: READY

### Dispositions
- F1 · accepted · deferred to the Log (advice on a READY review): `note · deferred · Review 3 F1`
- F2 · accepted · deferred to the Log: `note · deferred · Review 3 F2`
- F3 · accepted · deferred to the Log: `note · deferred · Review 3 F3`
- F4 · accepted · deferred to the Log: `note · deferred · Review 3 F4`
- F5 · accepted · deferred to the Log: `note · deferred · Review 3 F5`
