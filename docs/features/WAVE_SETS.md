# Wave sets — per-wave units and stats, cleared waves, the event scoreboard

**Status:** step 1 (Logic) in progress (docs/dod/wave-sets.md, audit docs/audits/wave-sets.md). Ships in 0.9.0.

## Goal

A spawn event can have a shape. An optional `waveList` on a SpawnWaves action gives every wave its own units, every
unit entry its own level (or level offset) and stat multipliers, and every wave after the first its own start: after
`afterSeconds`, when the previous wave is cleared, or whichever comes first. When the last wave is beaten (and every
wave was fought), the event ends at once with "all waves defeated". With `scoreboard: true`, the end shows the top 3
players by kills, with their deaths, and the totals.

## Design summary

- **Schema** (D1, D2): `action.waveList` (1-10 waves, 1-10 entries each, `prefab`, `count`, `chance`, optional
  `modifiers`), `afterSeconds` 10-3600 and `whenCleared` on waves 2+; it replaces `units`, `waves`,
  `intervalSeconds` and `modifiers`. `action.scoreboard` is a boolean on either form.
- **Start rule** (design §9 D40): the earlier of the previous wave's decision + `afterSeconds` and the previous wave
  cleared; cleared alone when neither key is set. Cleared = decided and no pending order or tracked unit of that wave
  left. A skipped, zero-rolled or zero-queued wave is cleared at its decision but rules out the early end.
- **Victory** (D41): the last wave cleared and every wave fought → ended as a natural end, "all waves defeated".
- **Scoreboard** (D38, D39): kills of the event's units by players (familiars and summons credit their player),
  deaths of players to the event's units, admins left out unless `[Scoreboard] IncludeAdmins`. Shown at the natural
  end, victory and an admin stop; never on purge, restart, pillar off or a fault. Names only in those chat lines;
  nothing is persisted.
- The units form is unchanged.

## Open questions

- Whether the game raises a DeathEvent for a vampire's death with the killer set to the NPC (or its projectile):
  checked in Session 1 (D21).

## Test results

(none yet)
