# Sieges (Pillar B1)

**Status:** designed, **highest risk**. Blocked on spike S1 (NPC movement). D3 and D10 resolved 2026-09-23.

## Goal

NPC war parties march on player castles. **No public mod does this today** (RESEARCH_NOTES headline #1), so
this is the mod's most distinctive feature and its biggest unknown.

## What we know constrains it

1. **Movement:** no API moves an NPC to a coordinate. Levers: `Follower.Followed` to an anchor entity,
   `AggroConsumer.PreCombatPosition`, `AggroBuffer` targets, and raising `MaxDistanceFromPreCombatPosition`
   + `ProximityRadius` (TideOfWar), plus the `Return`→`Follow` leash override.
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
- Spawn waves at the territory edge (outside the walls) — DyWorld spawns ~30 units from castle centre.
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

## Open questions

- Should clans get a warning lead time (e.g. 60 s) before the first wave?
- ~~Offline-raid protection~~ — resolved by D10: online or recently online (`RecentlyOnlineHours`), admin-configurable.
- Rewards for defenders? (loot off for waves by default; maybe a reward chest on victory)
