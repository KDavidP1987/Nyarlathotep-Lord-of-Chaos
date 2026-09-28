# Changelog

Public beta. Event pillars and automatic announcements default off; admins opt in per feature.

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
