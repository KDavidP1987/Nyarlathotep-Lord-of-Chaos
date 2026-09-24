# Sieges (Pillar B1)

**Status:** designed, **highest risk**. Spike S1 found the movement lever (aggro chase, ≤ 60 m; Test results); D16 (2026-09-24) builds the MVP on it. D3 and D10 resolved 2026-09-23.

## Goal

NPC war parties march on player castles. **No public mod does this today** (RESEARCH_NOTES headline #1), so
this is the mod's most distinctive feature and its biggest unknown.

## What we know constrains it

1. **Movement:** no API moves an NPC to a coordinate. Spike S1 tried four levers. Only the aggro chase walks
   a unit: widen `AggroConsumer` and `AggroModifiers` ranges (TideOfWar) and put the target in `AggroBuffer`.
   It works from 60 m or less. Beyond about 86 m the game drops the target on every update. Follow links
   teleport the unit (and crashed the server twice); the leash override leaves it Idle.
2. **Walls:** NPC pathing can't pass walls or closed doors.
3. **Structure damage:** NPC hits deal 0 to stone unless `DealDamageEvent.MaterialModifiers.StoneStructure`
   is raised — possible only by hooking the unmanaged `DealDamageSystem` through **HookDOTS** (RaidForge).
4. **Server rules:** `CastleDamageMode` (Always/Never/TimeRestricted) and raid windows govern structure
   damage; **RaidForge** rewrites these at runtime — detect it and defer.
5. **Compliance:** attacking other players' property is sensitive. Off by default; pre-clear with the
   community admin (Raphael precedent).

## Proposed phasing (Decision D3)

**MVP — harassment raid (no structure damage):**
- Target selection (Decision D10, resolved 2026-09-23): **every rule is an admin setting.** Eligible when
  the heart is claimed, not sealed, not decaying (Faust `CastleService` heart state); the owner or a clan
  member is online **or was last online within `RecentlyOnlineHours`** (logging out must not dodge a
  siege); castle-heart level ≥ `MinCastleHeartLevel` when set (heart level, not gear level, which players
  can swap); the event's PvE/PvP availability allows it. Re-checked every tick; an ineligible target ends
  the siege and despawns its units.
- Spawn waves outside the walls, 40–50 m from an online defender (Decision D16); DyWorld spawns ~30 units from castle centre.
- Stuck units: a unit with no target for 15 s is re-targeted on the nearest defender, or despawned (S1 finding: a unit that loses its target freezes in Combat).
- Line of sight (S1 wall run): a unit drops a target it cannot see, so waves do not approach defenders hidden behind walls. They hold outside until a defender is visible within about 60 m. The sieges child plan decides whether that "waiting at the gates" is the MVP behaviour or whether waves get a visible objective.
- Behaviour `Assault`: anchor at the castle's outer perimeter; seed aggro on online defenders and
  servants in range; units that reach the wall and can't path fight whatever is exposed under vanilla rules.
- Announce to the castle owner's clan only ("A Legion war party approaches your castle!"), plus an optional
  global message without coordinates.
- End conditions: duration, all waves dead, or defenders leave (despawn after grace).

**Phase 2 — structural siege (opt-in, separate switch; after the MVP per D3):**
- A HookDOTS prefix on `DealDamageSystem` raises `StoneStructure` only when the attacker carries our raid
  marker, only during allowed windows, never when RaidForge is loaded (or via RaidForge's rules).
- Candidate siege units: golems (`GAME_ASSETS.md` §Units useful for waves), `SiegePower` stat via buff.
- Adds a hard dependency on HookDOTS.API — decide when we get there.

## Spike S1 (do this first)

On a test server: spawn 5 bandits 100 m from a marker. Try, in order:
1. `Follower.Followed` = an invisible anchor entity placed at the destination.
2. Anchor moved in 10 m steps each second along the path.
3. `PreCombatPosition` = destination + raised leash distances + `Return` override.
Record for each: do they arrive, how do they path, do they get stuck, do they engage on arrival, what
happens at a wall. Write results here.

## Test results

### S1 march spike · 2026-09-23 to 2026-09-24 · spikes step 3 sessions 1–3 and 5–8 (throwaway save)

Unit: CHAR_Bandit_Thug (-301730941), spawned `distance` m north of the admin with LifeTime 600, DestroyWhenDisabled and `CanPreventDisableWhenNoPlayersInRange.CanDisable = false`. Without that last setting, units spawned about 100 m from any player were disabled and deleted within 5 s (session 2). Open flat ground in Farbane.

- [x] variant 1 (Follower.Followed = a held-still CHAR_Critter_Rat (-2072914343) at the admin): arrived no. The unit stayed at the spawn point in state Follow, then teleported beside the anchor; it never walked. Engaged on arrival no. Session 1 aborted the server (see crashes)
- [x] variant 2 (the same rat anchor stepped 10 m/s from the spawn point to the admin): arrived no. The rat moved; the follower stayed at the spawn point (d=100.4) and jumped to about 4 m at 11 s and 51 s. Path none (teleport). Engaged on arrival no
- [x] variant 3 (PreCombatPosition = the admin's position, leash raised, BehaviourTreeState forced to Return): arrived no. Units stayed Idle for 60 s and more at 100, 40 and 20 m; the owner saw them mill in place. Stuck at the spawn point
- [x] variant 4 (aggro chase: AggroConsumer ProximityRadius and MaxDistanceFromPreCombatPosition and AggroModifiers radius factors = distance + 50, the admin added to AggroBuffer): walks by the game's own pathing at about 3 m/s and fights on arrival, but only from short range:
  - 30 m: 5/5 arrived in 13 s (Idle → Combat at 5 s), engaged yes
  - 60 m: 4/5 arrived in about 20 s; one stayed at the spawn point in state Combat
  - 80 m: 4/10 arrived in about 30 s and engaged; 6/10 took no target and stood frozen in state Combat at 80 m for 180 s (the owner saw 4–5, sweep counted 10)
  - 100 m (×7 runs) and 150 m: arrived no. The units enter Combat and close 6–15 m, then at about 86–94 m the game removes the admin from AggroBuffer (the admin sees a brief in-combat flag). The units then freeze in Combat or walk back to spawn. Re-adding the entry every second (A7) does not hold: the next second it is gone again, so the game clears far targets on its own update
  - the widened ranges are not reset after spawn (the probe read 130–200 on every tick)
- [x] wall run (session 8, `march 4 5 40`, group g4): the owner built a castle heart and a stone wall and stood behind it, with open ground to the north. Arrived no. Units closed 10 m in the first 4 s and then stopped at about 30 m in state Combat. The probe showed the admin pruned from AggroBuffer every second while the wall blocked line of sight, even at 20–40 m. When the owner teleported into view (t=21 s and t=47 s) they moved 3–6 m at once, then stopped again as the owner went back behind the wall. Behaviour at the wall: they neither path around it nor attack it; they wait in Combat, out of range, until a target is visible. Engaged no
- finding: the game drops an aggro target the unit cannot see, not only one beyond about 86 m. The chase needs line of sight from the start and at every step
- crashes: two server aborts ("The entity does not exist … AppendDestroyedEntityRecordError", Burst), both with follow-linked units and the rat anchor alive. Session 1 came during `march 2`; session 3 came as the owner picked up the rat after devouring a follow-linked thug. Variants 3 and 4 caused none. Follow links to non-player anchors are not used again
- finding: a unit that loses its target freezes in state Combat until its LifeTime ends. A siege spawner needs a stuck-unit rule
- S1 verdict: go — aggro chase: widen AggroConsumer ProximityRadius and MaxDistanceFromPreCombatPosition and AggroModifiers CircleRadiusFactor and ConeRadiusFactor, keep the unit enabled (CanPreventDisableWhenNoPlayersInRange.CanDisable = false), and put a visible target within about 60 m in AggroBuffer; re-seed it while visible. Follow links and the leash override are rejected. Range and line of sight bound the mechanism (D16)

## Open questions

- A long visible march (100–300 m): hidden relay hops, or hooking the system that drops far aggro targets (HookDOTS). Deferred by D16; revisit with the Phase 2 HookDOTS decision.

- Should clans get a warning lead time (e.g. 60 s) before the first wave?
- ~~Offline-raid protection~~ — resolved by D10: online or recently online (`RecentlyOnlineHours`), admin-configurable.
- Rewards for defenders? (loot off for waves by default; maybe a reward chest on victory)
