# Revenge — factions that hunt a vampire who kills too many of them

**Status:** in progress for 0.12.0 (docs/dod/revenge.md, audit docs/audits/revenge.md). Step 1 (the logic: trigger,
heat, gate, focus-only waves and hunts, lines, chat fields, info, wire api 7, and the final-wave bosses) and step 2's
services are built: the death read feeds the heat, the 5 s scan starts a due player's revenge, waves and hunts take the
target only, the alert and the private lines go out, and the end paths log and clear. The bandit-revenge template is in
the library, and spider-swarm and spider-hunting-grounds end on Ungoro. The in-game session (step 2) follows.

## Goal

Two pieces, both off until an admin enables an event that uses them:

- a **Revenge trigger**: a spawns definition with a `Revenge` trigger keeps, per player and in memory only, the kills of
  its factions within a window ("heat"). A V Blood kill of a listed faction counts `vbloodHeat` points, any other
  counted kill 1. A player whose heat climbs past `warnAt` values gets private warnings; at `heat` the player is due,
  and on the next 5 s scan, when eligible, the revenge starts: a server-wide alert that names no one, a private start
  line, and waves of the definition's SpawnWaves action around that player only, hunting only that player. Heat resets
  at the start and a `playerCooldownMinutes` cooldown begins.
- a **final-wave boss** (`"bosses": [ { "prefab": "CHAR_Spider_Queen_VBlood", "count": 1, "modifiers": {...} } ]` on any
  SpawnWaves action): the bosses join the event's last wave only, beside its regular units, and must die for "all
  waves defeated". Players feed on a boss's V Blood as on any V Blood.

## Design summary

- **Trigger:** `{ "type": "Revenge", "factions": [1-5], "heat": 3-500, "windowSeconds": 60-3600, "warnAt": [0-3 below
  heat, ascending], "vbloodHeat": 0-100 (10), "playerCooldownMinutes": 0-1440 (30), "scope": … }`; no `shared`.
- **Action:** SpawnWaves only, AroundPlayer location, Hunt behaviour, no fanOut, never in claimed territory. The tiers
  spawn 15-40 m from the target, because a spawned unit drops a chase about 45 m from where it spawned
  (hunting-grounds A5).
- **Heat:** Logic/Revenge.cs (`RevengeHeat`), rows per definition and player, bounded like the FactionKills counters
  (200 per definition, 2000 in all, least recently updated non-due row dropped first) but a separate table, so neither
  evicts the other. Cooldowns and warning states live outside the evictable rows (review 3 F3).
- **Gate:** due players start in the order they became due; held (territory, busy, target busy, ineligible, controls)
  with one log line per change of reason; a controls refusal keeps the heat, a conditions refusal resets it without a
  cooldown. An admin start focuses the admin, skips target-busy, resets no heat and sets no cooldown (review 3 F4).
- **Edits:** a heat or factions edit keeps a row's points; the next scan applies the new heat (review 3 F6).
- **Lines:** RevengeWarning, RevengeStart, RevengeEnd and RevengeAlertPool (Logic/Messages.cs), plain text, within
  480 bytes, naming no player. The end line goes on a natural end, all waves defeated and an admin stop; purge, restart
  and pillar off are silent.
- **Wire:** api 7, `trigger=revenge` on def and tpl rows; nothing else.
- **Bosses:** a boss is a SpawnTracker unit like any other: the caps cut the final wave's regular units before its
  bosses, a Region wave trades a boss into a group that found a spot, and every end path, the purge and the boot sweep
  remove it. A boss without modifiers takes the final wave's (growth's added levels included).
- **No chained events:** an event's or `.nyar spawn`'s V Blood never raises the VBloodKilled trigger (D30), so a spawned
  boss cannot start other events. A native V Blood still does.
- **Summons:** a boss's own summons (Ungoro's CHAR_Spiderling_Summon) belong to the game: they end on their own after
  120 s (DestroyAfterDuration) and count for no cap, clear or scoreboard.

## Open questions

- A revenge squad hunts only its focus player. When the focus moves farther than the Hunt range from a squad, the squad
  idles where it is until the focus comes back in range or the event ends (review 3 F5); the next wave is still
  centred on the focus. A look-and-feel point for the owner's test.
- Scaling unit counts and levels to the players in a region (design §9 D52) and replacing units that never engaged
  (D53) come after Halloween.

## Test results

Step 1 is logic only; its evidence is the unit tests (docs/audits/revenge.md › Post-audit › Step 1). The in-game
sessions (D18-D23, D32) come with step 2.
