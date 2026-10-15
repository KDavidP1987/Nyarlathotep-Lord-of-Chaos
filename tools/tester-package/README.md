# Tester package

The workbook and reading copy human testers get for a dev-server round, built to `_shared/TESTING-STANDARDS.md`.

```powershell
python tools/tester-package/build.py          # build + verify → REFERENCE DOCUMENTATION/TESTING DOCUMENTATION/
python tools/tester-package/build.py --prove  # break each invariant and confirm the build refuses it (9/9)
python tools/tester-package/build.py --check-published  # is testing/ built from the current rows.py and build.py?
```

- `rows.py` is the only source: the round, the cover blocks, the known limits and the rows. `build.py` renders the
  `.xlsx` and `.pdf` (send these two only) plus a `.csv` and `.html` for us, and refuses any row key it does not render.
- The output folder is gitignored: returned workbooks carry tester names and machine details. The generator is
  committed, and so is the **blank** package: every build copies the `.xlsx` and `.pdf` to `testing/` with a
  `source.json` stamp (the hash of `rows.py` and `build.py`). `TesterPackageTests` fails when `testing/` is older
  than the generator, so a row edit is never pushed without its rebuilt workbook. Filled-in workbooks never go there.
- Each round is published as a GitHub pre-release (`v<next>-rc.<n>`): the mod zip (the DLL built with
  `-p:Version=<next>-rc.<n>`, the csproj untouched) plus the blank `.xlsx` and `.pdf`; `ROUND["download"]` links it
  and the workbook's Start here sheet carries the install steps (`INSTALL`). Keep only the current round at the top of that folder; closed rounds go to
  `RESOLVED/round-NN-<what>-<date>/`, built-and-never-sent ones to `superseded-<what>/`.
- Rows are production setups: the shipped templates armed with real triggers (a schedule, nightfall, a V Blood kill,
  walking into a region, a killing spree, a revenge heat), edited from chat as an admin would, then played. Every
  setup command in the rows is typed by `tools/vrclient/scenarios/tester-setups.vrs`, which checks each reply; run it
  (on a dev-snapshot, restored after) whenever a row's commands change.

## Deviations from the standard (recorded, §11)

- **No Standing checks sheet** (round 1; it starts from round two with checks tied to real past failures).
- **No Security checks sheet**: the dev server holds only test data and is not a live product (§11).
- **What changed** names its middle columns "What was missing before" / "What it does now": in a first round every
  row is new, so there is no old fault to diagnose. From round two the columns carry the diagnosis and the remedy.
- **Tester details** ask for PC, V Rising version, connection and gear level instead of browser and phone: a game
  client on a PC is what varies here.

## Round log

- Round 1 republished 2026-10-14: install steps for the test server and the download link added; blank package
  committed under `testing/`; build `v0.13.0-rc.1` (pre-release with the mod zip, workbook and reading copy).

- Round 1 (`dev-round-01-production-setups`, 2026-10-14): 17 new rows, 2 explore, 1 check; 85 steps. Setup commands
  checked in game: tester-setups.vrs 57/58 + its last step re-run 1/1 (the list page), after one fix found by the
  run (N-13: a revenge's warnings must be set before a lower heat).
