# Mob Loot Coverage Exporter

This tool expands the current CellAO mob loot seed data into CSV reports.

It reads:

- `CellAO\Libraries\Source\CellAO.Database\SqlTables\mobtemplate.sql`
- `CellAO\Libraries\Source\CellAO.Database\SqlTables\mobdroptable.sql`
- `itemnames` from `cellao_codex_clean`, using `CellAO\Config\Config.xml`, when MySQL is available

It writes:

- `CellAO\Documentation\MobLootCoverage\MobLootCoverage.csv`
- `CellAO\Documentation\MobLootCoverage\MobDropTableItems.csv`
- `CellAO\Documentation\MobLootCoverage\MobLootSummary.csv`

Run from the repo root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools-temp\mob-loot-coverage\Export-MobLootCoverage.ps1
```

Verify the parser and current expected counts without needing MySQL:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools-temp\mob-loot-coverage\Test-MobLootCoverage.ps1
```

The script only reads from MySQL, and it refuses to read any database except `cellao_codex_clean`.
