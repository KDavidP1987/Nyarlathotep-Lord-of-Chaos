# Audit — nyarlathotep

Epic-level Build plan steps (1, 2, 3, 11, 12). Child steps are audited in their own files.

## Pre-audit
### Step 2 · 2026-09-23 · f828a39
- git status: clean apart from the untracked draft docs/dod/spikes.md (a separate plan, not part of this step)
- compile: 0 errors, 0 warnings
- preflight: exit 0 (warning: icon.png is missing, which is what this step fixes)
- dod status: nyarlathotep 1/36 verified
- feature doc read: none (repository identity step; no feature doc)
- baseline boot: not applicable (no DLL change)

## Post-audit
### Step 2 · 2026-09-23 · (this commit)
- compile / preflight: 0 errors, 0 warnings; preflight exit 0, and the icon warning is gone
- /code-review: inline review only. The diff is two binary images and one README line, which is below what the code-review skill is for. No findings.
- Codex verdict: APPROVED — F1 no findings (checked: the icon is the whole 1254x1254 source scaled to 256x256, the PNG header says 256x256, the cover is 1024 px wide, the README link resolves, artwork/ stays ignored, and thunderstore.toml icon = "icon.png")
- in-game: not applicable (no DLL change)
- dod status: nyarlathotep 1/36. D19 (Thunderstore icon) cannot be verified yet: its evidence is the preflight line "icon: 256x256", which Build step 1 adds

## Pre-audit
### Step 1 · 2026-09-23 · 4761cec
- git status: clean apart from the untracked draft docs/dod/spikes.md (under Codex review; not part of this step)
- compile: 0 errors, 0 warnings
- preflight: exit 0, no warnings
- dod status: nyarlathotep 1/36 verified
- feature doc read: none (tooling step)
- baseline boot: not applicable (the only DLL-affecting change is the D8 guard on the init patch, which is booted in the post-audit)

## Post-audit
### Step 1 · 2026-09-23 · (this commit)
- compile / preflight: 0 errors, 0 warnings; `pwsh tools/preflight.ps1` exit 0 (16 checks); `-SelfTest` → "selftest: 16/16 checks, 3 fixtures each, 18 extra bad fixtures", with every bad fixture confirmed to fail for its planted reason (`-SelfTest -Verbose`); `-Paths` → "paths: 199 walked, all in manifest"
- staged/index secrets: a token-shaped line was staged and then edited out of the working copy; preflight reported "secrets: FOUND in tools/_stagetest.txt (index)"; the file was unstaged and deleted
- in-game boot: the init patch's try/catch was deployed to the local dedicated server (throwaway save save-data-nyarboot). The first boot generated a new world, and no plugin initializes on world generation. The second boot on that save logged "Nyarlathotep initialized via GameDataInitializedPatch (attempt #1). Prefab map has 14481 entries." There were no Nyarlathotep errors. The server was stopped, and save-data-nyarboot and both boot logs were deleted
- /code-review: inline review of the preflight diff, which is PowerShell tooling with no game code. Structural-call detection in an expression-bodied method conservatively reports "outside a method" and fails. Private Persistence helpers may take a file name, because D7 restricts only what the class exposes. No further findings
- Codex round 1: the sandbox rejected every read. That verdict was void and was rerun with the files pasted in
- Codex round 1 (pasted): REVISE with 7 blocking findings and 1 advisory:
  - D7: the fence file's rules were not checked
  - D4: template discovery was not proven
  - D6: any Has<Prefab> text counted as a guard
  - D8: only the strings were checked, and the init patch was not codified
  - D9: staged content was not scanned
  - D10: no inputs, no schema check
  - D18: the date was not bound to its section
  - D21 (advisory): the remote tag's SHA was not compared

  All were accepted, except two parts:
  - Requiring at least one template now is wrong: the foundation ships the first ones.
  - Zips outside dist/ and build/ are beyond D9's scope.

  The init patch exception became Epic amendment A1 (~D8)
- Codex round 2: REVISE with 3 blocking findings: Persistence destinations were textual, the Prefab guard could be switched off by `&& false` or nesting, and the index was scanned only for paths differing from HEAD. All applied: allowed literals only, the folder derived from Paths.ConfigPath, a top-level `||` guard term, and `git grep --cached` over every index blob
- Codex round 3 (the cap): REVISE with 2 blocking findings and 1 advisory: write destinations were not tied to the folder, the guard could test a different entity, and the Secrets inputs wording was wrong. All applied, with fixtures:
  - FileWrites bad-6 and bad-7
  - StructuralEdits bad-6 and bad-7
  - the manifest inputs corrected
- Codex verdict: REVISE at the 3-round cap. Every finding from the final round was applied and proven by a failing fixture. There is no further Codex round; the owner reviews this record
- dod status: see the Log lines dated 2026-09-23 in docs/dod/nyarlathotep.md (D4–D10, D17–D19, D21, D33, D34)
