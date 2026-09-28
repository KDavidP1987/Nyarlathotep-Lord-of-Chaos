# Outbreak: undead and Dracul hordes (planned child)

> **Status:** planned. The owner asked for this on 2026-09-26, and it is in the Epic as child `outbreak`, after
> anti-farming and before sieges (docs/dod/nyarlathotep.md, A24, A30). **Nothing here is designed yet.** The questions below are for the
> child's dod planning, and the owner decides them in plan mode before any code is written.

## Goal

The ultimate event: a zombie apocalypse. The undead appear in one area and spread across the map. Each unit
they kill is replaced by one or two undead, and those undead go on hunting the living until the world is
theirs. The point is to rely on the player base to stop the horde.

## What the owner asked for (2026-09-26)

- **Scope:** map-wide, or limited to an area.
- **Spread:** it starts in one area; every kill by the horde replaces the victim with one or two undead.
- **Duration:** a long-running event that can last until the server is entirely undead.
- **Respawns:** optionally, respawning NPCs also come back undead.
- **Levels:** the undead scale to each region's NPC levels, and to what they can overpower there.
- **Players:** the event exists to make the player base fight back.

## Second variant: the Dracul horde (owner, 2026-09-26)

- **Source:** Dracula's forces instead of the undead. The horde emanates from where Dracula and his generals are
  (the main story's final boss and his castle), and every enemy it defeats is replaced by Dracul troops.
- **Goal:** the same as the undead outbreak; the players fight back against the horde.
- **Bosses in both variants:** some bosses of the horde's type join it as copies that follow the troops and help
  take over the map, in the undead outbreak and in the Dracul horde alike.
- **Planning consequence:** outbreak is one event type with a horde profile (which faction, which units, which
  bosses, where it starts), and undead and Dracul are its first two profiles, rather than two separate features.
  Boss copies are spawned units (tracked, capped, despawned like any other), never the world's own V Bloods.

## Spawning rules, horde waves and castle takeover (owner, 2026-09-28)

Settled in plan mode (design §9 D23, D24; Epic A30, A31). The child's dod plan turns these into items.

- **Where a horde starts:** at run time, from where that faction's native NPCs actually stand, grouped into
  hotspots. Undead hordes grow out of undead areas; the Draculin horde out of Dracula's castle and his generals'
  areas. The regions child filters them: global (a map-wide takeover) or listed regions only (for example the
  Cursed Forest).
- **Spread:** hotspots push outward on a timer and hordes march towards the nearest players and populated areas.
  Converting killed units stays an option for the child's planning (the open questions below).
- **Horde waves:** optional per event, every M minutes: 30-100 units spawn at one hotspot and fan out across the
  map, led by one or more empowered boss copies.
- **Boss-linked empowerment:** while a leading boss copy lives, its horde carries faction-empowerment's carrier
  buff; when that boss dies, the buff is removed from the horde.
- **Caps:** a 100-unit wave plus spread units needs MaxTrackedUnits above the default 150 (hard ceiling 500); the
  tick-budget test is repeated at horde size.
- **Taking over castles:** horde units may assault a castle and destroy its heart. This is the documented
  exception to "every change is reversible", so it is off unless the cfg switch AllowCastleDestruction (default
  false) and the event's own flag are both on; assaults run only inside the game's castle raid window, never on an
  offline-protected castle; the owning clan is warned 5 minutes before; the heart falls only through the game's
  own damage path; `.nyar purge` stops every assault. It is built in the sieges child, after outbreak.

## Constraints it inherits

- **Every change is reversible** (CLAUDE.md › Spawn & buff safety):
  - A converted native NPC must come back as itself on stop, purge, uninstall and restart.
  - Nothing may be left in the save.
- **Tracked and capped:** every spawned undead goes through SpawnTracker and stays inside the caps in
  Config/Settings.cs.
- **Kill switch:** `.nyar purge confirm` ends it and restores the world within the despawn budget.
- **Builds on:** event-spawns' waves and the stats child's counts, so it comes after both.

## Open questions (for the child's planning)

- How a conversion works: the native NPC is destroyed and an undead spawned in its place, or the native is
  hidden and restored. This decides whether the world can be put back.
- Spread rate and caps: the most undead at once, the most conversions per minute, per region.
- Level scaling: take the victim's level, or the region's, or a fixed table.
- Respawn conversion: which spawners, and how they are restored.
- Win and lose conditions: what the players must do to end it, what "the server is undead" means, and what
  happens then.
- What players see: announcements, a progress readout, and Raphael rows.
- Horde profiles: which Dracul units and generals the Dracul horde uses, and which bosses each profile may copy;
  boss copies must not count as V Blood kills or unlock V Blood rewards.
- Interaction with castles and sieges.
