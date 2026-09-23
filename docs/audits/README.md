# Audit records

One file per DoD child plan: `docs/audits/<slug>.md` (slug = the plan in `docs/dod/`). Every Build-plan
step gets a pre-audit before it and a post-audit after it (CLAUDE.md › Development procedure). Append;
never rewrite an earlier entry. `tools/preflight.ps1` checks that every **done** child has a record with
the three markers below (Epic D17).

## Template

```markdown
# Audit — <slug>

## Pre-audit
### Step <n> · YYYY-MM-DD · <commit>
- git status: clean | <what was pending>
- compile: 0 errors, 0 warnings
- preflight: exit 0 | <failures>
- dod status: <n>/<m> verified
- feature doc read: docs/features/<X>.md (Status: …)
- baseline boot (in-game steps): log clean | <errors>

## Post-audit
### Step <n> · YYYY-MM-DD · <commit>
- compile / preflight: …
- /code-review: <findings, dispositions>
- Codex verdict: APPROVED | REVISE — <one-line summary; findings and dispositions below>
- in-game: <what was run, what was observed>, recorded in docs/features/<X>.md › Test results
- dod status: <n>/<m> verified; evidence lines added for <Dn …>
```

The literal markers `## Pre-audit`, `## Post-audit` and `Codex verdict:` are what the preflight check
looks for.
