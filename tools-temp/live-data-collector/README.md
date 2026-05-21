# Live Data Collector

These scripts automate the passive AO data collection workflow.

Start a capture:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools-temp\live-data-collector\Start-LiveDataCapture.ps1 -Mode All
```

Check whether a capture is running:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools-temp\live-data-collector\Get-LiveDataCaptureStatus.ps1
```

Stop, decode, and export:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools-temp\live-data-collector\Stop-LiveDataCapture.ps1
```

The stop script automatically writes:

- packet CSV/JSONL and flow summary
- client-to-server frame decode
- server-to-client frame decode
- packet-to-CellAO implementation coverage
- AOtomation message-model coverage for packet families without ZoneEngine handlers/builders
- combat/death/corpse/loot timeline with decoded attack targets, damage fields, health-damage fields, stop-fight fields, and loot move source containers
- corpse session summary with open, loot-window, item-move, and despawn timing
- loot body/drop observations
- quest observation CSVs and markdown, including focused quest reward/completion events

The default capture filter is host-only for `199.241.136.157`, so rolling AO game ports are handled without needing to know the next port.

Smoke test the offline pipeline:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools-temp\live-data-collector\Test-LiveDataCollector.ps1
```
