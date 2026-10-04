# Changelog

Public beta. Event pillars and automatic announcements default off; admins opt in per feature.

## 0.8.0 (2026-10-04)

- **Events that start themselves.** Three new triggers: `Interval` (again every `minMinutes`–`maxMinutes`, kept
  across restarts), `RegionEntered` (a player walks into a region, with a per-player cooldown) and `FactionKills`
  (a player kills enough of a faction within a time window, or all players together with `shared`). An event started
  by a player spawns its `AroundPlayer` waves near that player.
- **Waves for several players.** `fanOut` on an `AroundPlayer` wave spawns one group near each of up to 10 players
  standing far enough apart. Each group gets the wave's full unit list; all groups together stay within
  `MaxUnitsPerWave` and `MaxTrackedUnits`.
- **Three new templates,** all off: `roaming-hunters`, `border-watch` and `bandit-reprisal`. All the new keys can be
  set in chat with `.nyar event set`; `event info` shows when an Interval event starts next.
- **Better ambush placement.** Wave units around a player now need a walkable line to them, so they no longer land
  behind walls or across ravines. Known limit: on a strip of land inside a pond, units without Hunt can stand across
  the water.
- Raphael api 6 (new trigger names). No new cfg keys. Rolling back to 0.7.0 disables the events that use the new
  triggers or `fanOut` until you change them.

<details>
<summary><b>Earlier releases</b> (0.1.0–0.7.0)</summary>

## 0.7.0 (2026-09-29)

- **Stronger waves.** A spawn event can set its units' level (`level` 1–120, or `levelDelta` −5 to +5 around their
  own) and multiply their max health, power, move speed and attack speed (×0.5–3.0). Only the event's own units
  change, and they are removed after the event as before.
- **Hunting waves.** `"behaviour": { "type": "Hunt", "range": 10–60 }` sets every unit on the nearest players (up to 5)
  within range of the wave, rechecked every 5 seconds.
- **Waves around players.** The `AroundPlayer` location centres each wave 10–80 m from a random online player, so a
  scheduled or triggered wave finds players wherever they are. With a regional scope, only players in those regions
  are picked.
- **Castles are off limits.** No wave spawns in claimed castle territory, and no player there or in PvP combat is
  picked or hunted (territory is read at each wave). A wave centred on a castle is skipped; `"allowTerritory": true`
  lifts this for one event.
- **Chance and loot.** Each unit can have a `chance` (0.05–1.0) of joining its wave. Event units still drop nothing
  unless the event sets `"loot": true`. All the new keys can be set in chat with `.nyar event set`, and
  `undead-rising` now ships at level +2 with ×1.2 health.
- No new cfg keys. Rolling back to 0.6.0 disables the events that use the new keys until you remove them.

## 0.6.0 (2026-09-28)

- **Regional events.** An event's trigger and action can be limited to named map regions (Farbane Woods, Dunley
  Farmlands, the Cursed Forest …) with the new `scope` key, or `.nyar event set <id> trigger.scope|action.scope`.
  A V Blood trigger then fires only for a kill in its regions, a timed or manual start needs a player there,
  empowerment reaches only NPCs standing there, and waves spawn only there.
- **New commands:** `.nyar region list` (the regions and how many events use each) and `.nyar region here`
  (the region you stand in). `event info` and `event list` show each event's scope; announcements can use `{region}`.
- Raphael api 5: `.nyar api regions` and a `region=` key on event rows.
- No new cfg keys; without `scope` every event behaves as in 0.5.2. Rolling back to 0.5.2 disables events that use
  `scope` or `{region}` until those are removed.

## 0.5.2 (2026-09-28)

- **Raphael admin actions (api 4).** Admins using the Raphael client panel get machine-readable versions of the
  event, template, pillar and purge commands, plus reads for templates, pillars and the kill switch. Each action
  answers one line, so the panel can show exactly what happened; at most five actions per admin per second. The reads
  answer their rows and an end line, and are not limited.
- Chat commands reply exactly as in 0.5.1.
- No new cfg keys; `events.json` and `state.json` are unchanged. Rolling back to 0.5.1 keeps everything.

## 0.5.1 (2026-09-28)

- **Fix: wave units no longer spawn in water.** Each wave checks its units' spawn points against the game's own
  walkable ground. A point in a pond, a river, a cliff face or a wall moves to the nearest walkable point on the same
  ring, then half the ring, then the centre; no unit is added or dropped. This fixes 0.5.0's known issue.
- The wave log line now shows how many units were moved (`(<m> moved, <u> unchecked)`). If the check cannot run,
  waves spawn as in 0.5.0, the log says `walk check unavailable` once, and `.nyar status` shows
  `spawns: walk check unavailable` until it works again.
- No new cfg keys; `events.json` and `state.json` are unchanged. Rolling back to 0.5.0 keeps everything.

## 0.5.0 (2026-09-28)

- **Event templates.** Six ready-made events ship inside the mod, all off: `legion-weekend-surge`,
  `bandit-vengeance`, `undead-nightfall` and `militia-crackdown` (empowerment), `bandit-ambush` and `undead-rising`
  (spawn waves). `.nyar template list`, `.nyar template info <id>`, and `.nyar template use <id> [as <newId>]` copies
  one into your events, disabled, ready to adjust and enable.
- **Build events in chat.** `.nyar event new <id> <pillar>`, `.nyar event copy <id> <newId>` and
  `.nyar event delete <id>` (then `confirm`); `.nyar event set` now also sets the trigger (`trigger.type`, `days`,
  `times`, `phase`, `bosses`), `action.factions`, `action.units` (`CHAR_<name>[:<count>]`) and `location here`
  (your position, height included). Every chat change is a normal edit of `events.json` plus a reload, with one
  `.bak` kept.
- **Pillar switches in chat.** `.nyar pillar list` and `.nyar pillar <name> on|off`, saved to the cfg.
  `.nyar event list` shows why each event would or would not start (`ready`, `off (pillar)`, `full (cap)`, …).
- **Waves keep to their level.** A wave unit the game drops onto another terrain level than the event's centre
  (past a cliff or plateau edge) is moved back beside the centre.
- **Slow-tick warning.** A mod tick of 250 ms or more logs one warning naming the slowest part, at most once a minute.
- **Known issue:** a wave unit whose spawn point falls in deep water (a pond or river) stands stuck until its wave
  ends and is removed. Put event centres on open, dry ground; a fix is planned for 0.5.1.
- No new cfg keys; `events.json` is unchanged (SchemaVersion 1). Rolling back to 0.4.0 keeps your events.

## 0.4.0 (2026-09-26)

- **Faction empowerment.** A new event action, `Empower`, buffs every NPC of up to five factions for the event's
  duration: physical and spell power, max health, attack and move speed, each ×1.0–3.0. It is a timed buff that
  ends on its own, so no NPC stays changed, even if the mod is removed mid-event. NPCs that respawn or load in
  during the event are buffed within about 15 s; V Bloods are left alone unless you include them. One empowerment
  per faction at a time. Turn it on with `[Pillars] FactionEmpowerment = true`; the example `example-empowerment`
  (off by default) rallies the bandits for 10 minutes after any V Blood kill.
- **Admin.** `.nyar event set <id> action.stats.<stat> <value>`; `.nyar debug here` also shows nearby native NPCs
  and their buff; a start blocked by the purge cooldown says how many seconds are left. New cfg key
  `Limits.EmpowerBatchPerTick` (200) paces buffing over server ticks.
- **Fix.** The V Blood trigger no longer fires for gate bosses that are not true V Bloods.
- **Upgrading from 0.2/0.3:** an `example-empowerment` from an earlier release now loads disabled; change its
  pillar to `spawns`, or replace it with the template in the README. **Rolling back to 0.3.0:** run `.nyar purge
  confirm` first, or the buffs already applied stay until their event's time runs out.

## 0.3.0 (2026-09-26)

- **Raphael API 2.** The optional Raphael client can now read the active events (`.nyar api status`) and, for
  admins, the event definitions (`.nyar api events`), and subscribe to live updates (`.nyar api sub on`): event
  start and end, waves and wave warnings, the kill switch and definition changes. Updates never tell a player more
  than chat or `.nyar status` does and never carry a position. The Raphael panels arrive with a Raphael update.
- No config changes; 0.3.0 reads and writes the same files as 0.2.1, and rolling back to 0.2.1 keeps them.

## 0.2.1 (2026-09-25)

- **Paced removal for every spawned unit.** Units with their own lifetime (`unitLifetimeSeconds`) and `.nyar spawn`
  units now leave a few per tick through `MaxDespawnsPerTick`, like event units, instead of all at once when their
  timer ends. Their timer now only matters if the mod stops.
- **Safer spawning.** A new unit gets its cleanup marker before any other setup, and its timer even if marking
  fails, so a unit whose setup goes wrong is still removed after a restart or when its timer ends.

## 0.2.0 (2026-09-25)

First public beta: the event engine.

- **Spawn-wave events.** Waves of chosen units at a map point or around the admin, started on a schedule, at
  nightfall or daybreak, after a V Blood kill, or by `.nyar event start`. Conditions for minimum players,
  cooldown, chance and time window.
- **Safe by design.** Caps on units, waves and concurrent events; spawns and removals spread over server ticks;
  every unit carries a timer and is removed after its event, even across a restart.
- **Kill switch.** `.nyar purge` then `.nyar purge confirm` ends everything and pauses new events for the purge
  cooldown (one minute by default).
- **Announcements.** Wave warnings, start and end banners and a daily banner, each behind its own switch;
  admins can also broadcast with `.nyar announce`.
- **Admin tools.** `.nyar event list|info|start|stop|enable|disable|set|reload`, `.nyar spawn`, `.nyar debug here`,
  and `.nyar status`, which also names any disabled hook.
- **Config change.** `General.AnnounceEvents` is retired; use the new `[Announcements]` switches.

## 0.1.0 (2026-09-23)

- **Project scaffold.** The plugin loads on a dedicated server and answers `.nyar`. No events yet.

</details>
