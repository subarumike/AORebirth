# Live Data Collector

These scripts automate the passive AO data collection workflow.

Start a capture:

```powershell
powershell -NoProfile -File .\tools-temp\live-data-collector\Start-LiveDataCapture.ps1 -Mode All
```

Check whether a capture is running:

```powershell
powershell -NoProfile -File .\tools-temp\live-data-collector\Get-LiveDataCaptureStatus.ps1
```

Stop, decode, and export:

```powershell
powershell -NoProfile -File .\tools-temp\live-data-collector\Stop-LiveDataCapture.ps1
```

The stop script automatically writes:

- packet CSV/JSONL and flow summary
- client-to-server frame decode
- server-to-client frame decode
- packet-to-CellAO implementation coverage
- AOtomation message-model coverage for packet families without ZoneEngine handlers/builders
- capture-source and coverage-authority labels so official C2S-only captures are not mistaken for full server-response evidence
- combat/death/corpse/loot timeline with decoded attack targets, damage fields, health-damage fields, stop-fight fields, and loot move source containers
- corpse session summary with open, loot-window, item-move, and despawn timing
- loot body/drop observations
- quest observation CSVs and markdown, including focused quest reward/completion events

The default capture filter is host-only for `199.241.136.157`, so rolling AO game ports are handled without needing to know the next port.

Coverage authority labels:

- `official_live`: official AO live server capture.
- `private_server_199`: private server capture from `199.241.136.157`.
- `c2s_only_request_flow`: decoded client request flow only; do not infer missing or unused S2C packets from this capture.
- `private_server_response_flow`: rich S2C response shape/timing evidence, but keep it labeled as private-server evidence.
- `official_live_full_duplex`: official live capture with decoded C2S and S2C frames.

Quick truth table:

| Source label | Authority label | What it can prove | What it cannot prove |
| --- | --- | --- | --- |
| `official_live` | `c2s_only_request_flow` | Official request order/payloads | Any S2C absence/necessity claim |
| `private_server_199` | `private_server_response_flow` | Rich response shapes/timing and comparative choreography | Official-live parity by itself |
| `official_live` | `official_live_full_duplex` | Official request + response behavior | N/A (preferred parity evidence) |

Smoke test the offline pipeline:

```powershell
powershell -NoProfile -File .\tools-temp\live-data-collector\Test-LiveDataCollector.ps1
```
