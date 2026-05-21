# Mob Loot Data

This is the current reliable path for mob loot data in this CellAO repo.

## Source of Truth

The server-side loot schema is:

- `mobtemplate.DropHashes`: comma-delimited drop group expressions.
- `mobtemplate.DropSlots`: comma-delimited slot numbers matching `DropHashes`.
- `mobtemplate.DropRates`: comma-delimited rates matching `DropHashes`, stored as percent times 100. `10000` means 100.00 percent.
- `mobdroptable.Hash`: drop group hash referenced by `DropHashes`.
- `mobdroptable.LowId` / `HighId`: item template ids or range endpoints.
- `mobdroptable.MinQl` / `MaxQl`: quality bounds.
- `mobdroptable.RangeCheck`: whether the id/QL range should be treated as a range.

`DropHashes` can combine groups with `+`, for example `HASH01+HASH02,HASH03`.

Live captures are useful for validating the corpse and loot protocol, but they are not a reliable source for complete loot tables. They only show the random drops observed during that capture.

## Current Coverage

The checked-in seed data and the local `cellao_codex_clean` database currently agree:

- 168 mob templates exist.
- 0 mob templates have configured `DropHashes`, `DropSlots`, or `DropRates`.
- 25 `mobdroptable` rows exist.
- 15 distinct drop group hashes exist.
- All current `mobdroptable` item ids resolve through `itemnames` in `cellao_codex_clean`.

That means real DB-backed mob loot is modeled and now wired into the combat
corpse roller, but the checked-in mob templates are still not populated with
drop assignments. The working combat test path still uses deterministic
`CombatTestLootCatalog` entries first so corpse/loot behavior can be tested
without random data.

Runtime behavior:

- `CombatTestLootCatalog` remains the first match for Codex test mobs.
- If no deterministic test entry matches, the corpse roller falls back to
  parsed `mobtemplate`/`mobdroptable` entries.
- `DropRates` are interpreted as basis points (`10000` = 100.00%).
- A comma-delimited `DropHashes` slot may use `+` to form a candidate union;
  one candidate item from that slot is chosen when the slot roll succeeds.

## Reports

Generate the current loot coverage CSVs with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools-temp\mob-loot-coverage\Export-MobLootCoverage.ps1
```

Output files:

- `CellAO\Documentation\MobLootCoverage\MobLootCoverage.csv`
- `CellAO\Documentation\MobLootCoverage\MobDropTableItems.csv`
- `CellAO\Documentation\MobLootCoverage\MobLootSummary.csv`

The exporter reads item names from `cellao_codex_clean` when MySQL is available. It refuses to read any other database.

Generate review-only seed files from decoded passive loot captures with:

```powershell
python .\tools-temp\live-loot-observations\Export-ObservedMobLootSeed.py
```

Output files:

- `CellAO\Documentation\MobLootCoverage\ObservedLiveLootSeed.csv`
- `CellAO\Documentation\MobLootCoverage\ObservedLiveLootSeed.review.sql`

The SQL file is not applied automatically. It is only a starting point for
reviewing observed item ids, sample sizes, estimated rates, and enemy names that
do not yet map cleanly to `mobtemplate`.

## Next Implementation Step

Review the observed seed rows for low-level ICC Shuttleport mobs, choose a small
approved set of drop assignments, then update only `cellao_codex_clean` or the
checked-in SQL seed after explicit approval.
