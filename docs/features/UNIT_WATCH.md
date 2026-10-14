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
   a player stood within 40 m of it or it held a player as its aggro target. A unit of a stationary design (a Point
   wave at its anchor, a waiting hunter) with no player near is not stuck merely for standing still.
2. **Off the ground:** on two checks at least 10 s apart, the walk check finds no free, grounded tile at its position
   at its own height level or a neighbouring one (under the map, inside a rock, on a roof the walk data does not
   cover), or its height is below WalkHeight.MinY (-100) or below the lowest grounded level at its x and z. This
   applies whether or not a player is near.
3. **Still at its spawn** (D34, the owner's fast check): SpawnStillSeconds (default 10 s) after it was spawned, or after
   one of our movers put it down (a hidden jump, an apocalypse hop, a region or arena return, a regroup, a recovery), it
   has moved no more than 0.5 m from that spot, has not fought, and in that time a player stood within 40 m of it or it
   held a player target: it is stuck at once (reason "still") and goes through the same tries and removal. With no
   player near and no target this check never marks. With Debug.VerboseLogging each event unit's first move logs
   `[nyar watch] first-move event=<id> prefab=<prefab> after=<s> player=<near|none>` (`after=none` after 120 s
   unmoved), Session 1's measurement for the default.

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
| SpawnStillSeconds | 10 | 5-60 | The still-at-spawn check's time after a spawn or a landing (D34); set from Session 1's first-move measurement. |

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
