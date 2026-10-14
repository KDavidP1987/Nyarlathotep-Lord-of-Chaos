# Unit watch — stuck units recovered, then removed, so a wave never hangs

**Status:** planned (docs/dod/unit-watch.md, draft; design §9 D54, owner 2026-10-14). Built after apocalypse's step 3
and before its release; released together with apocalypse as 0.13.0. Nothing is built yet.

## Goal

A wave that waits to be cleared spawns its next wave only when every unit of it is gone (Logic/SpawnLedger.cs
WaveCleared; Logic/Engine.cs Due and Defeated). One unit of ours that gets stuck where no player can reach it, after a
hidden jump, a regroup, an arena or region return, or a bad spawn spot, holds the whole event until its timer runs out.
The owner (design §9 D54): "if one of those units gets stuck in a place where it can't be attacked or where the players
can't reach it … the wave would be stuck until the timer ran down … either due to the teleporting process or due to the
spawning process."

The owner chose **recover, then remove**:

- a unit of ours that is stuck is first moved back, unseen, to walkable ground near its group or the nearest eligible
  player in its scope;
- still stuck after 2 tries, it is removed and counts as gone for its wave, with no kill credit;
- a wave that waits to be cleared and sees no kill and no hit for N minutes (default 10) sends its leftovers through
  the same recovery and removal;
- an admin can do it at once with `.nyar event unstick <id>` (and its Raphael api twin).

It applies to every event unit SpawnTracker holds: SpawnWaves waves (wave-sets, swarm, revenge, hunting-grounds) and
apocalypse roamers and hordes. A unit spawned with `.nyar spawn` belongs to no event and is never watched.

## Design summary

### Detection (every 5 s, the behaviour cadence)

A watched unit is **stuck** when either holds (distances are on x and z unless stated):

1. **Standing:** it has not moved more than 5 m and has not fought for StuckSeconds (default 90 s), and during that time
   it held a player as its aggro target (it is trying to reach a player). An idle unit is never stuck by standing,
   however near a player stands (a Point wave at its anchor, a waiting hunter, a unit that ignores an invulnerable
   admin) — design §9 D56, plan A2, after Session 1 saw idle spiders beside Chaos marked.
2. **Off the ground:** on two checks at least 10 s apart, the walk check finds no free, grounded tile at its position
   at its own height level or a neighbouring one (under the map, inside a rock, on a roof the walk data does not
   cover), or its height is below WalkHeight.MinY (-100) or below the lowest grounded level at its x and z. This
   applies whether or not a player is near.
3. **Still at its spawn** (D34, the owner's fast check): SpawnStillSeconds (default 10 s) after it was spawned, or after
   one of our movers put it down (a hidden jump, an apocalypse hop, a region or arena return, a regroup, a recovery), it
   has moved no more than 0.5 m from that spot, has not fought, and in that time it held a player as its aggro target:
   it is stuck at once (reason "still") and goes through the same tries and removal. With no player target this check
   never marks (A2). With Debug.VerboseLogging each event unit's first move logs
   `[nyar watch] first-move event=<id> prefab=<prefab> after=<s> player=<target|none>` (`after=none` after 120 s
   unmoved), the test weekend's measurement for the default (A3); a unit 5 m or more below its anchor logs
   `[nyar watch] low event=<id> prefab=<prefab> y=<y> anchor=<y> level=<n>` (Session 1's diagnostic: the game's height
   correction lifts a unit sunk by a dev write back to its level within a frame).

A unit that **fought** inside the window — it hit a player, or a player hit it — is never stuck by any of these rules. A
unit moved by another mover of ours (a hidden jump, an apocalypse hop, an arena or region return, a regroup) has
moved. "Fought" comes from one guarded read the 1.0 replacement rule (design §9 D53) will reuse; which game signal
answers it (a damage event, or a health sample with the aggro target) is decided by the step 1 spike; a signal
averaging 0.5 ms a tick or more is not usable. Without a usable signal, fought means the unit's own health fell in the
window or it holds a player target within 5 m, so a melee unit on a rock above a player is still stuck at about 90 s.

### Recovery (2 tries)

A stuck unit is moved to walkable ground near, in order: a live, not-stuck unit of its own group; else the nearest
eligible player in its scope, landing 50-60 m from them; else its anchor (spawn spot, regroup anchor, arena centre,
a horde's front, a roamer's hotspot). Every landing:

- is free and grounded by the walk check;
- lies 50 m or more from every online player, and the move happens only while no player is within 100 m of the unit
  (hunting-grounds' hidden-jump rules);
- is never inside claimed castle territory (hunting-grounds A6);
- stays inside the event's scope regions for a region-locked or scoped event (hunting-grounds), and inside the arena
  circle for a swarm arena; for revenge the only eligible player is its focus player.

A try that finds no landing, or no unseen moment within StuckSeconds, is a failed try. After a move the unit has
another StuckSeconds: a unit that fights, or is no longer stuck by either rule, is cleared and starts over with no
tries.

### Removal

A unit that failed 2 tries is removed through SpawnTracker's staged despawn under MaxDespawnsPerTick. It counts as
gone for its wave (the next wave comes; a waveList's last wave can end the event as defeated; an apocalypse horde
can be won). It gives **no kill credit**: no scoreboard kill, no V Blood trigger, no FactionKills or revenge heat, no
apocalypse spread. An apocalypse resume never rebuilds a removed unit.

### Wave stall backstop

A wave whose clear the event waits on with no timer to fall back on (a waveList wave followed by a wave that waits for
a clear with no `afterSeconds`, the last wave of a waveList event, and the hordes of an apocalypse whose last horde has
risen) that sees no kill of its units and no hit on or by them for StallMinutes (default 10, 0 = off) marks its
leftovers stuck; each then goes through the 2 tries and removal. A marked leftover's try fails unless it fights within
StuckSeconds of the move. Waves with an interval timer (the units form, growth and swarm) advance by their timer and
need no backstop; their units are still watched one by one.

### Admin

- `.nyar event unstick <id>` marks every live unit of the running instance that is not fighting as stuck now; the
  same hidden rules, tries and removal follow. The reply counts the units marked and those left alone because they
  were fighting. Refused when the event is not running or unit watch is off.
- `.nyar api event unstick <id>` is its wire twin (docs/RAPHAEL_INTEGRATION_CONTRACT.md §5a), api 7 → 8.
- `.nyar event info <id>` shows the watch counts of a running event: stuck now, recovered, removed.

### Config ([UnitWatch], global, in kdpen.Nyarlathotep.cfg)

| Key | Default | Range | Meaning |
|---|---|---|---|
| Enabled | true | true/false | A safety rail like the caps. False: nothing is marked, moved or removed, no backstop, `unstick` refuses. |
| StuckSeconds | 90 | 30-600 | The standing window, and the time a try may wait for an unseen moment. |
| StallMinutes | 10 | 0-60 | The backstop's quiet time; 0 turns the backstop off. |
| SpawnStillSeconds | 10 | 5-60 | The still-at-spawn check's time after a spawn or a landing (D34); set from the test weekend's first-move measurement (A3). |

RecoveryTries is fixed at 2. No events.json key is added.

### Logs (one greppable line per state change)

- `[nyar] unit-watch <event> stuck: <prefab> wave <k> (standing|off-ground|still|stall|admin)`
- `[nyar] unit-watch <event> recovered: <prefab> try <t>/2, <d> m (group|player|anchor)`
- `[nyar] unit-watch <event> try failed: <prefab> try <t>/2 (no landing|seen|still stuck)`
- `[nyar] unit-watch <event> removed: <prefab> wave <k> after 2 tries, no kill credit`, ending ` (in view)` when a player
  stood within 100 m at the removal
- `[nyar] unit-watch <event> cleared: <prefab> (fought|moving)`
- `[nyar] unit-watch <event> stall: wave <k> quiet <m> min, <n> leftovers marked`
- failures once per streak, with a health entry: `[nyar] unit-watch: read failed: <message>`,
  `[nyar] unit-watch: move failed: <message>`

No line names or locates a player; distances are our units' own.

### Safety

Only units SpawnTracker tracks for a running instance are read, moved or removed. The caps, purge, the pillar switch
and Enabled=false win over the watch. Purge and every end path drop the watch rows. The watch rows live in memory only;
a restart drops them (the boot sweep despawns our units, apocalypse's resumed units start with fresh rows).

## Open questions

- Which game signal answers "fought" cheaply: a damage-event read (StatChangeSystem or DealDamageSystem, as
  Learning Mods/Bloodcraft-main/Patches/StatChangeSystemPatch.cs and Learning Mods/BloodyCore show) or a health sample
  plus the aggro target. Decided by the step 1 spike (docs/dod/unit-watch.md D19).
- A unit stuck beside a player who never leaves can be moved only unseen; it waits at most StuckSeconds per try, then
  the try fails, so it is removed about 4-6 minutes after it stops (S-10 of the plan). The owner judges the feel at the
  test weekend.
- Whether a staged despawn of a tracked unit ever raises the game's death event (it should not); the spike checks it
  before the removal code.
- The walk data calls some real ground "not grounded" (hunting-grounds A1-A3, ravine walls); the spike counts false
  off-ground readings on hill, ravine and water spots before the rule is trusted.

## Test results

### Spike (D19) · 2026-10-14 · step 1 (the mod at 6e0b901 + NyarDevTools .devfight/.devstrand/.devground) · vrclient as Chaos on the local dev server

`tools/vrclient/scenarios/unit-watch-spike.vrs`: **SCENARIO PASS 14/14** (unit-watch-spike-20261014-082645.json). Logs in
%TEMP%\nyar-uw0-logs (spike-*). -LogCheck: 0 unhandled, 51 nyar lines, 0 orphan errors, 0 unity errors. Warnings: only the
known ones (Il2CppInterop substitute, Beelzebub baked writes, example-empowerment pillar, the purge line, and the 226
boot-time missing-prefab warnings every run since 0.11.0 has).

- (a) Fight signal, 75 s window, 6 CHAR_Spider_Forest beside Chaos (invulnerable), Chaos swinging:
  - **apply** (StatChangeSystem.ApplyStatChanges postfix, reason DealDamageSystem_0; Entity = the unit hit, Source = the
    attacker): 7 lines on our spiders `hit=npc by=player`, 0 between them; cost 0.083 ms a call averaged in the fight,
    0.008 ms idle. The line names the prefab, not the unit, so the per-unit split (n/6) is not readable.
  - **taken** (StatChangeSystem.OnUpdate, DamageTakenEvent): the same hits, but 54.8 ms a call as measured (the
    diagnostic's health sweep runs inside it, so its own cost is not isolated): not usable as built.
  - **deal** (DealDamageSystem.DealDamage postfix): 0 lines, not usable.
  - **health** sample: 9 lines, no attacker (by=-).
  - Hits **on** a player: none seen. Chaos is invulnerable, so no damage reaches the stat change, and the scenario had no
    vulnerable test target (plan step 1 named one). This direction is not observable this way.
- (b) Staged despawn: `.nyar purge confirm` queued 6 tracked units; during the 40 s window deal/taken/apply/health =
  0/0/0/0 and no death, spread or scoreboard line: **6/6 silent**.
- (c) Walk check at standing units: the pond shore (open, flat ground beside water) read 4 grounded and 3 none for the
  spiders' readings, and Chaos's own spot there read none; the ravine mark read grounded. **3 false off-ground readings in
  7**, far past S-12's 1 in 80, so the full four-spot, 80-unit count was not needed for the verdict.
- (d) `.devstrand -581295882 under 30` at the pond (ground y 0, so -30 stays above -100): moved=1 to y=-30.0, read
  ground=free (not grounded); `.nyar debug here 40` listed it at -1882,-1642 with the five other spiders.

**fight verdict: apply** (StatChangeSystem.ApplyStatChanges, DealDamageSystem reason) for a player hitting our unit,
under 0.5 ms; a hit on a player is not seen by it, so the fight read also counts S-9's fallback clause (a player target
within 5 m on x and z, or within the unit's attack range for a ranged unit, plan Review 2 F2).
**off-ground verdict: not usable**, so S-12's fallback: off-ground reads only a unit more than 10 m below the lowest
grounded level at its position, or with no grounded level at all.

### Session 1 · 2026-10-14 · step 3 (the mod at 5e4659f + A2, NyarDevTools .devaggro/.devbuff/.devperch/hold) · vrclient as Chaos on the local dev server

Logs in %TEMP%
yar-uw1-logs (a- to k-, one pair per boot, copied before each stop). Snapshot uw1 saved before the cfg
changes. Seven boots; cfg changes per run: VerboseLogging and TimingLog true, IncludeAdmins true, StallMinutes 1 for the
stall run, Enabled false for the watch-off run, all restored.

- **Run 1** (before A2, unit-watch.vrs as planned): 24/28. Every spider stood at its spawn spot beside Chaos and never moved
  (`first-move … after=none`, 16 of 16): NPCs do not target the invulnerable Chaos, so D34 marked every one "(still)" and
  the planned rock top does not exist in the walk data around the pond (unit-watch-perch.vrs: two height levels, no
  ground on rocks). The owner chose that only a unit holding a player target is stuck by standing or still (design §9
  D56, plan A2).
- **Forced target** (`.devaggro`, an AggroBuffer entry on Chaos): the game dropped it at once on every reading, 0 of 16
  held (`[DEV AGGRO READ] held=0`). The positive standing and still cases, and D34's first-move measurement, wait for
  the test weekend (A3, the owner's fallback).
- **Sunk units** (`.devstrand under 30`): the game's height correction lifts a sunk unit back to its level within a
  frame; the mod's diagnostic `[nyar watch] low` read it below its anchor on 4 of about 14 checks even with a per-frame
  hold (NyarDevTools DevHold.cs), so no off-ground mark can be made in game (A3). The off-ground rule is D3's unit test;
  the marks below come from `.nyar event unstick`.
- `unit-watch.vrs` (A2/A3, the negative side): **SCENARIO PASS 23/23** (unit-watch-20261014-130315.json): 4 and then 12
  idle spiders beside Chaos for 120 s and 20 s, no "stuck:" line of any reason (standing, still, off-ground);
  first-move 22 of 22 `after=none player=none` across the session.
- `unit-watch-remove.vrs` (D24): **SCENARIO PASS 23/23** (unit-watch-remove-20261014-125223.json): "stuck … (admin)",
  "recovered: … try 1/2, 95 m (player)", "try failed: … try 1/2 (still stuck)", "try failed: … try 2/2 (seen)" (it landed
  50-60 m from Chaos), "removed: … after 2 tries, no kill credit (in view)", "wave 1 cleared", "wave 2/2"; scoreboard 3
  kills (the `.devkill`s). An earlier run (12:37) was cleared instead by a Bandit Scout killing the recovered spider at
  its landing: a recovered unit rejoins the world's fights.
- `unit-watch-stall.vrs` (D25, StallMinutes 1): **SCENARIO PASS 25/25** (unit-watch-stall-20261014-115848.json): "stall:
  wave 1 quiet 1 min, 4 leftovers marked", four "(stall)" marks and recoveries 93-103 m (player); `unstick` 4 marked,
  the api twin, unknown and not-running replies.
- `unit-watch-ends.vrs` (D27): stop, `pillar spawns off` and `purge confirm` each "tracked units: 0" and no unit-watch line
  after the end (28/33, unit-watch-ends-20261014-121424.json; the 5 failures were the last block's start inside the
  purge's 240 s cooldown, now waited for in the scenario); the last block re-run after the cooldown **7/7**
  (ends-tail-20261014-121902.json). Restart with Enabled false: "event uw-wave cancelled by restart" and no unit-watch line;
  `unit-watch-ends-off.vrs` **SCENARIO PASS 10/10** (unit-watch-ends-off-20261014-122620.json).
- `unit-watch-apocalypse.vrs` (D26, before its part B was rewritten): 19/21 (unit-watch-apocalypse-20261014-124454.json):
  `unstick` marked the live units; Guardians and Ghost Militia "recovered … (player)" 509-937 m (every unit marked, so no
  unmarked mate to land beside), tries failed "(still stuck)" then "(seen)", 7 "removed: … no kill credit (in view)",
  0 "spread" lines. The two failures were the horde-front read after the marks (part B, since rewritten). apo-test then
  ended "lost (2 of 2 hordes risen)" before the restart, so the resume line was not read.
- `unit-watch-apocalypse.vrs` (D26, rewritten): 15/16 (unit-watch-apocalypse-20261014-142231.json): `unstick` 14 marked,
  "recovered: CHAR_Undead_Guardian try 1/2, 193 m (player)" and Ghost Militia 930 m (player), `debug here 70` at W listed
  them; the removals "removed: … after 2 tries, no kill credit (in view)" (5) came after the step's 240 s wait (two tries
  of 90 s plus the removal's wait in view), so its timeout is now 330 s; no "spread" line. The apocalypse's tally counts
  them as "despawned" (a staged despawn, as planned; its "removed" field is for units the game removed). Restart while it
  ran: "event apo-test kept by restart" and "resumed after restart (horde 2/2, 16 units to rebuild)", the 5 removed units
  not among them.
- `unit-watch-timing.vrs` (D28): run 1 (four events, b marked) 19/26, and run 2 25/27 (a fourth event uw-timing-d carrying
  the marks; apo-test's first horde reached its goal about 8 min in and ended it "lost" before the span, so no hunt
  targets). Run 3, with apo-test's hordes.firstAfterMinutes 15 and StallMinutes 30 for the run: **SCENARIO PASS 27/27**
  (unit-watch-timing-20261014-141448.json); `-TimingSpan … -Windows 10 -MinTracked 50` → "timing span: 10/10 windows
  under 5 ms, tracked >= 50, targets >= 1, 0 slow ticks"; window averages 0.53-1.57 ms with 78 then 60 tracked; uw-timing-d's
  18 marked spiders recovered and were removed inside the span and the event ended "all waves defeated". One tick's watch
  phase reached 7.5 ms (Session 1's earlier run: 14.6 ms); the promise is the window average.

### Test weekend · about 2026-10-24/25 · the owner with the players (D23, D29, D34's measurement)

**Sheet.** Unit watch rides apocalypse's test weekend (docs/features/APOCALYPSE.md › Test weekend): install one Release
build that holds both, commit 0f90351 or later (`Nyarlathotep.dll` from `Nyarlathotep/Nyarlathotep/bin/Release/net6.0/`),
with the server stopped. Before starting it, in `BepInEx/config/kdpen.Nyarlathotep.cfg` set `[Debug] VerboseLogging = true`
for the weekend (it writes the first-move lines below); leave `[UnitWatch]` at its defaults (Enabled true, StuckSeconds 90,
StallMinutes 10, SpawnStillSeconds 10). Add the `uw-wave` entry of `tools/vrclient/fixtures/uw-wave.json` to the
`events` array of `BepInEx/config/Nyarlathotep/events.json`, then start the server.

What a stuck unit is: a unit of ours that holds a player as its target but has not moved for 90 s (or has not left its
spawn spot within 10 s) and has not fought, or one under the ground or stuck in the air. It is moved out of sight up to
twice; if it still cannot reach anyone it is removed with no kill credit, and its wave counts it as cleared.

1. **Standing (D23).** Find a spot a spider cannot reach: the top of a tall rock or ledge, or a small island across deep
   water. Stand at its foot and type `.nyar event set uw-wave location here`, then `.nyar event start uw-wave`. As soon
   as the spiders appear, climb (or cross) to the unreachable spot and stay within about 20 m of them. Do not hit them:
   a fight resets the watch. PASS: within about 100 s the server log reads
   `[nyar] unit-watch uw-wave stuck: CHAR_Spider_Forest wave 1 (standing)` (or `(still)`) and
   `[nyar] unit-watch uw-wave waiting: …, a player near`, and no spider vanishes while you can see it.
2. **Recovery (D23).** Walk at least 150 m away. PASS: within about 10 s the log reads
   `[nyar] unit-watch uw-wave recovered: CHAR_Spider_Forest try 1/2, <m> m (<group|player|anchor>)`; go back, and the
   spider is where the line says (beside its mates, 50-60 m from a player, or at the event's spot), not where it was stuck.
3. **The admin view.** `.nyar event info uw-wave`. PASS: the reply's line reads `unit watch: <n> stuck now, <n> recovered,
   <n> removed`. Then kill the spiders; wave 2 comes; `.nyar event stop uw-wave` shows the scoreboard.
4. **Unstick by hand.** Start uw-wave again, walk 150 m away, then `.nyar event unstick uw-wave`. PASS: the reply reads
   `unstick uw-wave: <n> units marked, 0 fighting left alone`; within 10 s the "recovered" lines follow.
5. **Turning it off.** Any time: `[UnitWatch] Enabled = false` in the cfg and a restart. Start uw-wave, then
   `.nyar event unstick uw-wave`. PASS: it is refused with "unit watch is off: [UnitWatch] Enabled", `.nyar event info
   uw-wave` reads "unit watch: off", and no `[nyar] unit-watch` line appears. Set it back to true and restart.
6. **Normal play (D29).** Run the wave events and zombie-apocalypse as planned for the weekend. Watch for: a wave that
   never ends because a unit is out of reach; a unit seen vanishing, or appearing right beside a player (other than a
   removal after its wait); a recovered unit that does not rejoin the fight believably.

**After the weekend (Claude, from the server's `BepInEx/LogOutput.log`, sent by the owner):**
- D34's measurement: the `[nyar watch] first-move … after=<s> player=<name>` lines with a player target (at least 10);
  SpawnStillSeconds becomes the slowest `after` plus 5 s (5-60), as a dod Log note and a Config default change.
- D26's landing beside its horde: any `[nyar] unit-watch zombie-apocalypse recovered: … (group)` line.
- Every `[nyar] unit-watch` line with its reason, read against what the players saw.

Note for the verdict: whether the stuck rule (only a unit holding a player as its target, design §9 D56) catches the
units that really were stuck and leaves alone the ones that were not; the owner also accepts D19 (the Spike above) and
D24-D27 (Session 1 above) here, or names what to change.

Verdict: waiting on the owner.
