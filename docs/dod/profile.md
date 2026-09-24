# DoD project profile — Nyarlathotep, Lord of Chaos

## Project-wide notes
- No automated test harness exists for V Rising server mods; in-game behaviour is verified by `manual`
  evidence on the local dedicated server, and static rules by `cmd` checks in `tools/preflight.ps1`.
- Layer 11 (Design & UX) is never N/A here: the chat command surface is the UX.
- Probe 12.3 (spikes report, 2026-09-24): every per-session check whose input the next boot overwrites (BepInEx/LogOutput.log) is run and recorded before the restart; a check recorded later is not evidence.
- Probe 6.1: the game's spawn, aggro and LifeTime behaviour has differed from the reference mods (spikes A5–A10); a plan that relies on one names the in-game test that confirms it.

## Audience
- who · project owner
- default · working
- asked · 2026-09-24
- BepInEx/Harmony · working
- C# · working
- IL2CPP interop · working
- PowerShell · working
- Thunderstore packaging · working
- Unity ECS · working
- VampireCommandFramework · new
- xUnit · new
