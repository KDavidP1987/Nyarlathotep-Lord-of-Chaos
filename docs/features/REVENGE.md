# Revenge — a faction strikes back at the player who keeps killing it

**Status:** in progress for 0.12.0 (docs/dod/revenge.md, audit docs/audits/revenge.md). Step 1 (logic) is being built.
The final-wave bosses (design §9 D51) ship with this child.

## Goal

Two pieces, both off until an admin enables an event that uses them:

- a **Revenge trigger** (`"trigger": { "type": "Revenge", "factions": [...], "heat": 30, ... }`): every player builds
  heat with a faction by killing its units (a V Blood kill counts `vbloodHeat`). Heat decays after `windowSeconds`.
  The player gets private warnings at the `warnAt` thresholds. At `heat` the faction strikes: the server hears an
  alert naming the faction only, and growing ambush tiers (the event's own waves) hunt that player alone, never in
  claimed territory. A cooldown follows.
- a **final-wave boss** (`"bosses": [ { "prefab": "CHAR_Spider_Queen_VBlood", "count": 1, "modifiers": {...} } ]` on any
  SpawnWaves action): the bosses join the event's last wave only, beside its regular units, and must die for "all
  waves defeated". Players feed on a boss's V Blood as on any V Blood.

## Design summary

- Heat, warnings, due players and cooldowns live in memory only and are lost on every boot (design §9 D43).
- One revenge per definition and per player at a time. The tiers spawn 15-40 m from the target, because a spawned unit
  drops a chase about 45 m from where it spawned (hunting-grounds A5).
- No log line, reply, push or file names or locates a player. The scoreboard follows wave-sets D38.
- A boss is a SpawnTracker unit like any other: the caps cut the final wave's regular units before its bosses, and
  every end path, the purge and the boot sweep remove it.
- An event's or `.nyar spawn`'s V Blood never raises the VBloodKilled trigger (D30), so a spawned boss cannot chain
  other events. A native V Blood still does.
- A boss's own summons (Ungoro's CHAR_Spiderling_Summon) belong to the game: they end on their own after 120 s
  (DestroyAfterDuration) and count for no cap, clear or scoreboard.

## Open questions

- A spawned squad whose target moves more than `range` from its wave centre idles or leashes until the event ends or
  the wave is cleared (Review 3 F5). A look-and-feel point for the owner's test.
- Scaling unit counts and levels to the players in a region (design §9 D52) and replacing units that never engaged
  (D53) come after Halloween.

## Test results

_None yet. Session 1 (vrclient) runs in step 2._
