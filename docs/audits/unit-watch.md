# Audit — unit-watch

## Pre-audit
### Step 1 · 2026-10-14 · 9d570f8
- git status: clean at 9d570f8 before the step began (the step's own dev tools and scenario were then added: tools/vrclient/NyarDevTools/DevFight.cs, Plugin.cs, scenarios/unit-watch-spike.vrs)
- compile: 0 errors, 0 warnings (`dotnet build Nyarlathotep.sln -c Release -p:VRisingServerPath=C:\__nodeploy__`)
- preflight: exit 0, PREFLIGHT OK, release tags 14/14
- dod status: 0/33 verified
- feature doc read: docs/features/UNIT_WATCH.md (Status: planned, step 1 next; no open questions blocking the spike)
- baseline boot (in-game steps): dev server booted the current DLL (6e0b901 code) with NyarDevTools; "push: ready" after 15 s; LogOutput.log checked, no [Error lines at boot; dev snapshot uw0 saved before

## Post-audit
### Step 1 · 2026-10-14 · (this commit)
- compile / preflight: mod 0 errors 0 warnings; NyarDevTools builds 0 errors (dev server only, never shipped); preflight OK
- /code-review: the step's diff is the dev-only NyarDevTools (.devfight diagnostic patches, .devstrand, .devground) and the
  spike scenario; read in full: the fight patches return at once unless `.devfight` runs, are unpatched on unload and log
  no names; `.devstrand` moves the nearest unit of the given prefab, native or ours, which is acceptable for a dev-server
  tool (step 3 scenarios use it only on our spawned units). No findings.
- Codex verdict: not run for this step (Halloween track D34: once per child, before its release, step 5)
- in-game: unit-watch-spike.vrs SCENARIO PASS 14/14; fight verdict apply (+ S-9's target clause for hits on a player);
  off-ground verdict not usable (3 false in 7) so S-12's fallback; purge 6/6 silent; strand under 30 m works. Recorded
  in docs/features/UNIT_WATCH.md › Test results › Spike (D19). -LogCheck 0 unhandled; only known warnings.
- dod status: 0/33 verified; D19 waits on the owner's acceptance (manual, rubric 3)
