# Enemy Spawn Coverage

Builds checked-in coverage reports from three local sources:

- Client RDB zone enemy hints in `CellAO\Documentation\ClientRdbZoneEnemyHints.csv`
- CellAO `mobtemplate.sql`
- AO Model Viewer `CatMeshToMonsterData.txt`

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools-temp\enemy-spawn-coverage\Export-EnemySpawnCoverage.ps1
```

Outputs:

- `CellAO\Documentation\MonsterDataCorpseVisualHints.csv`
- `CellAO\Documentation\ClientHintedEnemyCoverage.csv`

These reports are advisory. They help pick safe next enemy families for the test catalog without touching SQL schemas or depending on live-client play tests.
Rows backed only by statel/object text are tracked as weak evidence so descriptive strings do not accidentally become spawn coverage.
