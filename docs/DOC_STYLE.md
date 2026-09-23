# Documentation style — changelogs, READMEs, Thunderstore page

Nyarlathotep ships six doc surfaces (see CLAUDE.md → "Release & changelog discipline"). Keep them clean,
scannable, and free of repetition. Inherited from Faust, where an admin called the drifted Thunderstore
page "too complicated / AI slop". `tools/preflight.ps1` and the release hook only remind you of these rules.

## Core principle: say it once

State a cross-cutting fact **one time per document**, never per section or per entry. Likely repeat
offenders for this mod:

- "Everything is off by default / admins opt in"
- "Pre-1.0 / in testing"
- "Server-side only, no client mod needed"
- The feedback/Discord link

## Changelogs

- Newest first. One heading per version — `## X.Y.Z (YYYY-MM-DD)` (package) / `## [X.Y.Z] - YYYY-MM-DD`
  (root). Every version gets an entry; no gaps.
- Optional one-line summary, then bullets of user-visible changes, each led by a bold 2–5 word label.
- **No per-entry boilerplate footer.** Testing status and the feedback link live once at the top.
- Describe only what changed in **that** version.
- **Package CHANGELOG** (`Nyarlathotep/Nyarlathotep/CHANGELOG.md`, Thunderstore): plain language for
  admins and players. No class names or internal refs. Aim for ≤ ~6 bullets per entry.
- **Root CHANGELOG** (`CHANGELOG.md`, GitHub): full technical detail is fine and expected.

## READMEs

Fixed section order:

- **Thunderstore** (`Nyarlathotep/Nyarlathotep/README.md`): cover → intro → what it does (one collapsible
  `<details>` per pillar, plus stats and announcements, each marked *in development* until its release) →
  screenshots (collapsible; captioned slots, images as absolute `raw.githubusercontent.com` URLs under
  `docs/img/screenshots/`) → installation (with dependency table) → quick start → commands (one
  collapsible table per group) → configuration → uninstall → feedback → acknowledgements & license.
- **GitHub** (`README.md`): intro → status → how it works → feature table → architecture → layout →
  building → release discipline → docs → license.

Rules:

- **Intro = one paragraph.** Then one short paragraph for the pre-1.0 / off-by-default note. Stop.
- **Group features** by the four pillars. Don't write one over-explained bullet per knob.
- **Keep reference tables** (commands, config, event-definition fields). Don't prose-duplicate them.
- **Status sections point to the changelog**; they don't recap it.

## Style

- Bold for genuine emphasis only.
- Active voice, short sentences. Avoid stacking em-dashes.
- Trust the reader; cut anything that restates a point already made.
