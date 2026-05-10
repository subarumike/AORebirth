# AO Client RDB Enemy Hints

This tool extracts client-side enemy and zone hints from the AO `ResourceDatabase`.

It is intentionally evidence-only. The client data is good for answering questions like "what creature families are associated with this playfield or district?", but exact live spawn positions, counts, timers, and pathing still need server data or packet captures.

## Run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools-temp\ao-client-rdb-hints\Export-AOClientZoneEnemyHints.ps1
```

Default inputs:

- AO client: `C:\Funcom\Anarchy Online`
- AODB binaries: `C:\Users\Mike\Documents\AO programs\aodb-master\AODB\bin\Debug`

Default outputs:

- `CellAO\Documentation\ClientRdbZoneEnemyHints.csv`
- `CellAO\Documentation\ClientRdbNpcTemplateHints.csv`

## Record Types Used

- `1000001`: playfield name records.
- `1000014`: playfield district records. These contain the strongest zone/enemy hints, for example dynacamp and creature-family district names.
- `1000029`: area description records.
- `1000026`: statel/door/object text records.
- `1040023`: NPC/monster template-name records.
