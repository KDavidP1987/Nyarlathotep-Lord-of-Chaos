# Tester package — current round

The two files a tester needs for the current test round, kept up to date with the generator in
`tools/tester-package/` (a unit test fails when they fall behind it):

- **`*.xlsx`** — the workbook: fill in your results and send it back to the owner.
- **`*.pdf`** — the reading copy of the same rows.

The workbook's first sheet, **Start here**, says how to install the test build on the test server (BepInEx,
VampireCommandFramework, `Nyarlathotep.dll`, making testers admins). The build itself, with these two files, is
attached to the round's GitHub pre-release; `source.json` names it.

Never commit a filled-in workbook here: returned workbooks carry tester names and machine details.
