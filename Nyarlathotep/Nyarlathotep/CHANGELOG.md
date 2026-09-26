# Changelog

Public beta. Event pillars and automatic announcements default off; admins opt in per feature.

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
