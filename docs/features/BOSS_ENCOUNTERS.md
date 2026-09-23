# Boss encounters (backlog)

> **Status:** backlog. The owner asked for this on 2026-09-23. It is not in Epic v2.3 yet. It enters the
> Epic as a `requested` amendment, with a new child plan, after the event-spawns child is done.
> **Nothing here is decided.** The open questions below go to the owner in plan mode before any design work.

## Goal

A timed encounter spawns in an area. It can be:

- one random boss
- several bosses
- bosses with mobs

Players have a limited time to beat it, and beating it earns a special reward the admin chose.

## What the owner asked for

- **Composition:**
  - a random boss from a pool
  - several bosses
  - a boss plus mobs
- **Time limit:**
  - the encounter despawns when time runs out
  - the timer **pauses while the encounter is actively engaged**
- **Reward:** an admin-designated special reward appears when the encounter is beaten.
- **Placement:**
  - a fixed set of coordinates, or anywhere in a region
  - further spawn settings set by the admin

## How it fits the existing model

It is one more action in `events.json`: Trigger → `BossEncounter` → Duration. It reuses:

- Schedule and Manual triggers
- the event-spawns spawn pipeline: SpawnTracker registration, the marker, LifeTime, and staged despawns
- announcements
- Stats, which can count encounter wins

Prior art to learn from, never paste: BloodyBoss (boss stat scaling, adds, reward on kill), BloodyEncounters
(random encounters with a reward), and KindredArenas (circle zones).

## Constraints the design must meet

These come from the rules already in force (CLAUDE.md, `docs/DEV_REMINDERS.md`):

- **Tracking:** every boss and mob is a tracked spawn. Stat changes are allowed only on our own units.
- **Cleanup:** timeout, kill switch and restart all clean up through the staged despawn.
- **The paused timer cannot run forever.** It needs a hard ceiling, because a unit that is "engaged" forever
  would otherwise never despawn.
- **Rewards must not duplicate:** one grant per encounter, even when several players land the kill or
  the server restarts mid-encounter.
- **Rewards and loot:** a reward is separate from D7, which keeps event loot off by default. The reward is
  opt-in per event.
- **No positions:** announcements never broadcast player positions. Whether the encounter's own location
  is announced is an open question.

## Open questions (for the owner, later)

1. **What counts as "engaged":**
   - any encounter unit in combat
   - a player within N m
   - damage taken in the last N seconds
2. **Ceiling on the paused timer:** what the absolute maximum should be.
3. **Reward form:**
   - item(s) into the killer's inventory
   - a chest or drop at the spot
   - split among participants
4. **Reward eligibility:** who qualifies:
   - the last hit
   - everyone who dealt damage
   - everyone within range
5. **Region placement:**
   - which regions to offer
   - avoiding castle territory and unreachable points
6. **Random boss pool:**
   - allow V Bloods (which have progression side effects)
   - or only non-V-Blood elites
7. **Announcing the location:**
   - a region name only
   - a map marker, which is a later-phase item
