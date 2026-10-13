# Revenge — factions that hunt a vampire who kills too many of them

**Status:** in progress (docs/dod/revenge.md, audit docs/audits/revenge.md). Step 1 (the logic: trigger, heat, gate,
focus-only waves and hunts, lines, chat fields, info, wire api 7) is built; the services that feed and start it are
step 2, so a Revenge definition loads and shows in `.nyar event info` but nothing starts it in game yet.

## Goal

A spawns definition with a `Revenge` trigger keeps, per player and in memory only, the kills of its factions within a
window ("heat"). A V Blood kill of a listed faction counts `vbloodHeat` points, any other counted kill 1. A player
whose heat climbs past `warnAt` values gets private warnings; at `heat` the player is due, and on the next 5 s scan,
when eligible, the revenge starts: a server-wide alert that names no one, a private start line, and waves of the
definition's SpawnWaves action around that player only, hunting only that player. Heat resets at the start and a
`playerCooldownMinutes` cooldown begins.

## Design summary

- **Trigger:** `{ "type": "Revenge", "factions": [1-5], "heat": 3-500, "windowSeconds": 60-3600, "warnAt": [0-3 below
  heat, ascending], "vbloodHeat": 0-100 (10), "playerCooldownMinutes": 0-1440 (30), "scope": … }`; no `shared`.
- **Action:** SpawnWaves only, AroundPlayer location, Hunt behaviour, no fanOut, never in claimed territory.
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

## Open questions

- A revenge squad hunts only its focus player. When the focus moves farther than the Hunt range from a squad, the squad
  idles where it is until the focus comes back in range or the event ends (review 3 F5); the next wave is still
  centred on the focus.
- The plan names the release 0.11.0, which hunting-grounds took; this child's contract and pending controls use 0.12.0
  (an amendment of D15, D24 and D25 for the orchestrator).

## Test results

Step 1 is logic only; its evidence is the unit tests (docs/audits/revenge.md › Post-audit › Step 1). The in-game
sessions (D18-D23) come with step 2.
