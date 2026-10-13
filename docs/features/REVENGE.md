# Revenge — factions that hunt a vampire who kills too many of them

**Status:** in progress for 0.12.0 (docs/dod/revenge.md, audit docs/audits/revenge.md). Step 1 (the logic: trigger,
heat, gate, focus-only waves and hunts, lines, chat fields, info, wire api 7, and the final-wave bosses) and step 2's
services are built: the death read feeds the heat, the 5 s scan starts a due player's revenge, waves and hunts take the
target only, the alert and the private lines go out, and the end paths log and clear. The bandit-revenge template is in
the library, and spider-swarm and spider-hunting-grounds end on Ungoro. Session 1 (2026-10-13) passed every
scenario in game; the owner's acceptance (D18-D21, D32) and second-player check (D22) and the 0.12.0 release (step 3) follow.

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
  evicts the other. Cooldowns and warning states live outside the evictable rows (review 3 F3). A row keeps at most 500
  entries (the largest `heat`; Codex 0.12.0 F1). A disable or invalid edit drops a definition's rows at the next scan,
  `.nyar pillar <name> off` at once (Codex 0.12.0 F2); cooldowns keep their own expiry.
- **Gate:** due players start in the order they became due; held (territory, busy, target busy, ineligible, controls)
  with one log line per change of reason; a controls refusal keeps the heat, a conditions refusal resets it without a
  cooldown. An admin start focuses the admin, skips target-busy, resets no heat and sets no cooldown (review 3 F4).
- **Edits:** a heat or factions edit keeps a row's points; the next scan applies the new heat (review 3 F6).
- **Lines:** RevengeWarning, RevengeStart, RevengeEnd and RevengeAlertPool (Logic/Messages.cs), plain text, within
  480 bytes, naming no player. The end line goes on a natural end, all waves defeated and an admin stop; purge, restart,
  pillar off and a fault end (three tick faults) are silent, though each still logs `revenge <id> ended: <path>`.
- **Restart line:** the boot writes `revenge <id> ended: restart` only when the definition in the catalogue is still a
  Revenge one; a definition deleted or retyped before the restart logs only `event <id> cancelled by restart`.
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

Step 1 is logic only; its evidence is the unit tests (docs/audits/revenge.md › Post-audit › Step 1).

### Session 1 · 2026-10-13 · step 2 DLL (b766a73 + 3e6ff70) · vrclient as Chaos on the local dev server

Setup: rv-test and rv-test0 as D18 describes, Scoreboard.IncludeAdmins true, VerboseLogging true. Native bandits found
with `.devunits` (worldwide, disabled chunks included): marks `camp2` (-1560, -1410) and `vbstalker` (-1670, -1400),
CHAR_Bandit_Stalker_VBlood's home.

- **D18, D19** `revenge.vrs --var CAMP=camp2 --var BANDIT=-301730941 --var BANDIT2=-1301144178 --var KR=120` →
  SCENARIO PASS 39/39. Two own `.nyar spawn` thugs killed: no revenge line. Native thug kills two at a time:
  "revenge rv-test: warning 1 of 2", "warning 2 of 2", each private line read in chat (OCR), then "revenge rv-test due".
  A purge armed first kept the start back ("held (controls)"); at home "held (territory)" and no start for 230 s; at
  the pond "event rv-test started by Revenge", "announced to 1: The Bandits swear revenge on a vampire.", "The Bandits
  come for you" in chat; `.nyar debug here 60` listed both tier 1 thugs about 20 m from Chaos; each tier killed brought
  the next (wave 2/3 and 3/3 "around a player"); "event rv-test ended: all waves defeated (3 of 3 waves)", "revenge
  rv-test ended: all waves defeated", the scoreboard (1 players, 9 kills, 0 deaths) and "revenge is spent" in chat;
  four hunter kills inside the 5 min cooldown logged no warning.
- **D20, D21 purge** `revenge-vblood.vrs --var VBMARK=vbstalker --var VB=1106149033` → SCENARIO PASS 20/20.
  `.devkill` of the Stalker V Blood: "trigger: VBloodKilled CHAR_Bandit_Stalker_VBlood", "revenge rv-test0: vblood
  kill +10 heat", "revenge rv-test0 due", the start at once. Purge in tier 2: "revenge rv-test0 ended: purge" before
  "purge: 1 events ended, 3 units queued", no private line, "tracked units: 0", and `.nyar event info rv-test0` read
  "revenge: 0 heat rows, 0 due, 0 running".
- **D21 restart** `revenge-restart.vrs` → SCENARIO PASS 8/8 (rv-test0 in tier 2, "tracked units: 3"); after an
  autosave the client closed and the server stopped and started: the boot logged "event rv-test0 cancelled by restart",
  "revenge rv-test0 ended: restart" and "boot marker sweep: 3 found, 3 queued for despawn (3 listed in state.json)";
  `revenge-boot2.vrs` → SCENARIO PASS 4/4: "tracked units: 0", "revenge: 0 heat rows, 0 due, 0 running", no revenge
  line after the boot.
- **D23** `revenge-timing.vrs` (rv-test0 at durationSeconds 1800, tier 3 alive, fk-test from the bandit-ambush template
  enabled, 8 `.nyar spawn` thugs refreshed every 300 s) → SCENARIO PASS 20/20; on the run's own log slice
  `preflight -TimingSpan <slice> -Windows 10 -MinTracked 10` → "timing span: 10/10 windows under 5 ms, tracked >= 10,
  targets >= 1, 0 slow ticks" (window averages 0.17-0.53 ms). An earlier run at 900 s ended by its natural end in
  window 6 ("revenge rv-test0 ended: natural end", the D8 natural-end line seen in game).
- **D32** `boss-wave.vrs` → SCENARIO PASS 13/13: no boss in wave 1; wave 2 "4 units queued" with "spawned
  CHAR_Spider_Queen_VBlood for bw-test"; killing Ungoro raised no VBloodKilled; "all waves defeated (2 of 2 waves)"; no
  CHAR_Spiderling_Summon left within 80 m. The owner's look-and-feel verdict is still open.
- **D30** `vblood-player-death.vrs` → SCENARIO PASS 9/9: Chaos killed by a tracked spider and a tracked Ungoro killed,
  neither raised VBloodKilled; the native Stalker V Blood above did.
- **Logs:** every [Warning] in LogOutput.log is a revenge state line (warning, due, held, vblood heat) or a purge line;
  no [Error]. NyarDev.log holds only the base game's boot-time "PrefabLookupMap … unknown state" warnings (226, the same
  count at every boot). `tools/dev-snapshot.ps1 -Save rv1` before the session; afterwards the world, plugins and
  config came back: "snapshot restored; hashes equal (rv1, …)".
- **Scenario lessons:** a due player outside the territory starts at once, so the hold is shown with the purge
  cooldown first; a camp's natives thin out over repeated runs (the heat top-ups use a second and third prefab); a mark
  at y 0 over a cliff dropped Chaos under the map (y -45) and the wave found no ground, so marks sit where natives
  stood.
