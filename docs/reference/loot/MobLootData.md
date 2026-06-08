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

The checked-in SQL seed currently has:

- 168 mob templates exist.
- 0 mob templates with configured `DropHashes`, `DropSlots`, or `DropRates`.
- 25 `mobdroptable` rows.
- 15 distinct drop group hashes.
- All current `mobdroptable` item ids resolve through `itemnames` in `cellao_codex_clean`.

The local `cellao_codex_clean` database currently has one extra reviewed low-level
assignment:

- 168 mob templates.
- 1 mob template with configured drops: `A004` Beach Leet.
- 29 `mobdroptable` rows.
- 19 distinct drop group hashes.
- `A004` points to `OBSA00400,OBSA00401,OBSA00402,OBSA00403` at rates
  `1250,5000,2500,5000`.

That means real DB-backed mob loot is modeled and now wired into the combat
corpse roller. The checked-in mob templates are still not populated with drop
assignments, while the local DB can now exercise the fallback path through
`A004`. The working combat test path still uses deterministic
`CombatTestLootCatalog` entries first so corpse/loot behavior can be tested
without random data.

Runtime behavior:

- `CombatTestLootCatalog` remains the first match for Codex test mobs.
- If no deterministic test entry matches, the corpse roller falls back to
  parsed `mobtemplate`/`mobdroptable` entries.
- `/command spawn lootstatus` reports DB loot coverage and whether supported
  population mob hashes have configured drops.
- `DropRates` are interpreted as basis points (`10000` = 100.00%).
- A comma-delimited `DropHashes` slot may use `+` to form a candidate union;
  one candidate item from that slot is chosen when the slot roll succeeds.

## Reports

Generate the current loot coverage CSVs with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools-temp\mob-loot-coverage\Export-MobLootCoverage.ps1
```

Output files:

- `docs\reference\loot\mob-loot-coverage\MobLootCoverage.csv`
- `docs\reference\loot\mob-loot-coverage\MobDropTableItems.csv`
- `docs\reference\loot\mob-loot-coverage\MobLootSummary.csv`

The exporter reads item names from `cellao_codex_clean` when MySQL is available. It refuses to read any other database.

Generate review-only seed files from decoded passive loot captures with:

```powershell
python .\tools-temp\live-loot-observations\Export-ObservedMobLootSeed.py
```

Output files:

- `docs\reference\loot\mob-loot-coverage\ObservedLiveLootSeed.csv`
- `docs\reference\loot\mob-loot-coverage\ObservedLiveLootSeed.review.sql`

The SQL file is not applied automatically. It is only a starting point for
reviewing observed item ids, sample sizes, estimated rates, and enemy names that
do not yet map cleanly to `mobtemplate`.

## Next Implementation Step

Use `/command spawn lootstatus` before playtesting DB-backed loot. Then review
the remaining observed seed rows for low-level ICC Shuttleport mobs, choose a
small approved set of drop assignments, and update only `cellao_codex_clean` or
the checked-in SQL seed after explicit approval.
