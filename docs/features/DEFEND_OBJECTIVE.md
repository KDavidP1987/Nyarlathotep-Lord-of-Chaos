# Defend objective (backlog)

> **Status:** backlog, researched 2026-10-18. The owner asked for it on 2026-10-18. It has no dod plan yet. It enters the
> Epic as a `requested` amendment with its own child plan. **Nothing here is decided:** the open decisions below go to
> the owner (plan mode, then `/dod plan` or the claudex loop) before any code. Codex cross-read this research
> (read-only, 2026-10-18: REVISE, 7 high, 9 medium and 3 low findings). Every finding is folded in below.

## Goal

An event can spawn something for the players to **defend**: one NPC, or a group of them (a room of villagers). The
waves or the horde march go for them.

- If the attackers kill them, the event ends and the players **lose**.
- If the players fight the attackers off, or hold until the timer runs out, they **win**.

This gives waves a target that stays put, and gives players a reason to stay in the area: leaving it means losing.

## What the owner asked for

- Spawn one NPC or several (a villager, "a room of villagers") as the target of the waves or the horde.
- The waves go for them, not only for the nearest player or a spot on the map.
- A clear win or lose: objective killed = lose; attackers beaten = win.
- Not urgent: research it, backlog it, plan it.

## What we already have (recon, 2026-10-18)

Paths are under `Nyarlathotep/Nyarlathotep/` unless they start with `docs/`.

| Need | What exists | Where |
|---|---|---|
| Waves that walk to a fixed spot | The destination march: one front, stragglers moved up, ending as arrived, stalled or met | `Logic/Destination.cs`, `Logic/WaveMarches.cs`, `Services/WaveMarchAction.cs` |
| A win/lose result | The apocalypse's goal: Lost when a front holds the goal, Won when every horde is cleared, Survived at time-out ("the living held out"), plus the outcome line and scoreboard | `Logic/Apocalypse.cs:829-899`, `Services/EventRuntime.cs:400-421`, `Logic/Messages.cs:283-290` |
| Pointing attackers at a target | Hunt seeding writes an `AggroBuffer` entry on each unit every 5 s, for players only, with seeds kept per unit and per player | `Services/HuntAction.cs:326` |
| Knowing that a unit of ours died | The death hook reads the ledger entry, then calls `SpawnTracker.Died` with the killer | `Patches/DeathEventPatch.cs:64-86` |
| Undead fighting villagers | The D4 hostility probe: Bandit_Thug, Militia_Light, Forest_Wolf and Legion_Shadowkin_Lesser all fought undead soldiers 10 m away | `docs/features/UNDEAD_HORDES.md:109-120` |
| Units that keep running with no player near | Every unit of ours gets `CanPreventDisableWhenNoPlayersInRange` with CanDisable false. Only the game's own NPCs stop 80 m or more from every player (undead-hordes A8). | `Services/UnitSetup.cs:19-22` |
| Safe spawning and cleanup | Every unit tracked, swept at boot, despawned in stages; a spawn deny-list for unsafe prefab classes | `Services/SpawnTracker.cs`, `docs/DEV_REMINDERS.md:38-54` |

**What is missing:**

- a unit that is ours but stays put, cannot be fed on or charmed, and does not hunt;
- an attacker that targets an NPC;
- "this event's objectives fell" as an end rule;
- a lose outcome on a SpawnWaves event. Today those end only as natural, victory, stop or fault.

## How it could work (proposed, to be decided)

### 1. The objective

- **What it is.** One to 20 units from an **objective allow-list**, not any prefab. The allow-list starts with the
  villagers and their verified `_Servant` variants:
  - `CHAR_Farmlands_Villager_Male` 1887807944 and its servant 1426964824;
  - `_Female` 525027204 and its servant 1532829342;
  - the Church of Light and Gloomrot villagers and their servants.
  Every spawn already refuses the named deny-list and `DropInInventoryOnSpawn` units (`DEV_REMINDERS.md:38-40`). The
  objective allow-list adds its own checks on top: no bosses, summons, mounts, prisoners, MicroPOIs or multipart units.
- **How it is spawned.** Through SpawnTracker, marked as an objective of **its event instance** in the ledger.
- **Where it stands.** Within a small radius of a Point or the admin's position. Every objective needs a valid landing,
  using the same height and walk probes as wave spawns (no geometry, water or other height level). If the full set
  cannot be placed, or `MaxTrackedUnits` has no room for it, the start is refused. Objectives are never cut like wave
  units, so `loseAt` always means what the admin set. They count against `MaxTrackedUnits`, not `MaxUnitsPerWave`.
- **Keeping it in place and safe** (allowed: a unit we spawned, CLAUDE.md spawn safety):
  - zero its `AiMoveSpeeds` (as Bloodcraft does at `FamiliarBindingSystem.cs:881-887`);
  - turn off its own `AggroConsumer.Active`;
  - pin both `AggroConsumer.PreCombatPosition` and `SpawnTransform.Position`, and clear its path and velocity;
  - correct it back to its spot within a per-tick budget if knockback or a flee moves it;
  - remove its feed and charm components (`BloodConsumeSource`, `ServantConvertable`, `CharmSource`) and turn off
    `Interactable`, as the project rule requires for units that must not be charmed (`DEV_REMINDERS.md:51-54`);
  - raise its health by a multiplier;
  - keep its `Aggroable` **on**, or the attackers ignore it.

### 2. The attackers

Any wave with `objective` set uses the objective's spot as its destination (the march we already have). Once there:

- **Native hostility.** The D4 probe shows undead fight Militia natives.
- **Hunt seeding.** It adds the event's own living objectives to its `AggroBuffer` entries, alongside players. This is
  the write Bloodcraft uses (`Utilities/Familiars.cs:826-859`). It changes no faction, so the source test that forbids
  faction writes (`Nyarlathotep.Tests/SourceTests.cs:49`, undead-hordes D14) still holds. An attacker seeds only its
  own event's objectives, so two events at one spot stay apart.
- **Pairing.** A wave whose faction will not fight the objective (a Militia wave and a Militia villager, say) is
  refused. The refusal uses a table of attacker and objective pairs proven in game, not a faction match. Whether
  servants on `Faction_Players` are attacked by every faction is unproven until a spike shows it.

### 3. Lose

The event's **fallen count** reaches `loseAt`. The count includes:

- objectives killed by anything (attackers, natives, players, the sun);
- objectives that vanish for any reason other than our own cleanup.

Our own despawn at stop, purge or the event's end never counts. The outcome line names what killed the last one when
the game reports a killer.

**If players leave** (decision 4): our units keep running with no player near, so attackers will kill an unguarded
objective in a real fight. A separate "held unopposed" rule is a gameplay choice only. It is needed only if the spike
shows that unattended fights stall.

### 4. Win

Either:

- every wave is cleared while fewer than `loseAt` have fallen (**Won**); or
- the duration ends first. That maps to the apocalypse's `Survived` result, with the player line "the living held
  out", so no new result type is needed.

Once the outcome is decided it is final: a death after it changes nothing, and nothing is announced twice.

### 5. Cleanup, per end path

| End path | What happens to the objectives |
|---|---|
| Won, Survived, Lost | They despawn after the event's grace period, like its waves, and the outcome is already fixed. |
| Stop, fault, purge, pillar off, kill switch | They despawn at once with everything else of ours. No outcome is shown. |
| Restart, SpawnWaves | The event is cancelled at boot (`EventRuntime.cs:47-66`) and the boot sweep removes the objectives. |
| Restart, HordeMarch (an apocalypse-type run that resumes) | The run is kept and resumes after the drain gate (`Apocalypse.cs:883-895`). Decision 1 settles whether its objectives are rebuilt from the resume record with their fallen count kept. |

### 6. Commands and Raphael

- **Commands:** `.nyar event set <id> action.objective here`, plus `.count`, `.prefab`, `.health`, `.loseAt`.
  `event info` and `.nyar status` show "objectives: <alive> of <n>, lost at <k>".
- **Raphael:** text only in api 8 at first. An objective count and an outcome on the event row would be api 9 (decision 7).

## Spikes before the plan is approved

Each is a vrclient run on the dev server with a `.dev*` helper, recorded as a `note · spike` in the plan's Log.

1. **Attack an NPC.** Ten undead are seeded toward one villager 30 m away, with Chaos 40 m off. They must:
   - reach it and keep attacking it;
   - retarget the next objective when it dies;
   - keep that up with a native and a player nearby.
   Also find out whether rehoming the attackers, or setting their `BehaviourTreeState` to Combat, is needed.
2. **Stay put and stay safe.** A pinned objective under attack for 2 minutes must not walk, flee, be knocked away for
   good, or return to its old spot. Chaos must be unable to feed on it or charm it.
3. **Servants.** Do undead, Militia and bandit waves attack a `_Servant` objective with no seeding, and with it?
   Can Chaos hurt one? This sets the default prefab and the pairing table.
4. **Unattended.** With Chaos 200 m away, do the attackers still kill the objective, and how long does it take?
5. **Placement.** Spawn a room of 10 inside a building, on a slope and near water. Every one must land on walkable
   ground. Also test claimed castle territory, which DEV_REMINDERS calls the biggest pathing unknown (`:89-93`).

The plan's scenarios then cover:

- the outcomes: won, lost to a kill, lost unattended, held;
- stop, purge and the kill switch mid-fight;
- a restart: SpawnWaves cancelled, and HordeMarch resumed by decision 1;
- two events at one spot;
- a start refused for capacity, placement or pairing.

## Open decisions (each with a recommendation)

| # | Decision | Options | Recommendation |
|---|---|---|---|
| 1 | Which events get an objective | SpawnWaves only / SpawnWaves plus HordeMarch. HordeMarch is an apocalypse-type run: one at a time, resumed across a restart, so its objectives would need rebuilding from the resume record. | SpawnWaves only first (destination waves, cancelled on restart, any number at once). HordeMarch and the apocalypse goal follow in a second step once the resume rules are planned. |
| 2 | What the objective is | Native villagers / their `_Servant` variants / both on the allow-list | Both on the allow-list; the default comes from spike 3. |
| 3 | When it is lost | Any one falls / a share falls (`loseAt` %) / every one falls | `loseAt` as a count or percent (rounded up), default "any one falls" for a single objective and half for a group, with the remaining count always shown. Retune after the owner's playtest. |
| 4 | If players leave | Attackers kill it in a real fight / an extra "held unopposed for 60 s" rule / the event pauses | A real fight (our units keep running). Add the extra rule only if spike 4 shows unattended fights stall. |
| 5 | Win condition | All waves cleared / timer runs out / both | Both: Won or Survived, with the outcome line saying which. |
| 6 | Reward or consequence | Announcement only / reward chest (SIEGES backlog) / a penalty event on loss | Announcement and scoreboard first; rewards and penalties are their own later item. |
| 7 | Raphael | Text only (api 8) / an objective count and outcome on the row (api 9) | Text only in the first release; api 9 when Raphael wants a progress bar. |
| 8 | Players hurting the objective | Allowed (a mistake can lose it) / a damage filter (players do no damage to objectives, a new hook) / a servant prefab if spike 3 shows players can't hurt it | Decide after spike 3; feeding and charming are blocked either way. |

## Size and order

- **Size:** about L:
  - one new action block with allow-list, placement and pairing checks;
  - an objective record per event instance;
  - an end rule and its cleanup per path;
  - hunt seeding that also takes the event's objectives;
  - five spikes;
  - the scenarios above.
- **Order:** it builds on the undead-hordes destination march (0.14.0), so it can follow any open child. The owner sets
  its place in the backlog.
