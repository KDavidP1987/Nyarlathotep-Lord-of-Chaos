# Changelog

Public beta. Event pillars and automatic announcements default off; admins opt in per feature.

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
