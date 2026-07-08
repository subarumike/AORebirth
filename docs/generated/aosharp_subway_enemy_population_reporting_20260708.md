# AOSharpLiveCapture Subway Enemy Population Reporting

Capture reviewed:

- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260708-180248`

Finding:

- Live Subway population was present in `events.log` as `CHAR-SEEN` rows.
- `enemy-dossier.json` stayed empty because the dossier only accepted combat/focused enemies.
- The Subway IDs in this capture have different meanings:
  - resource/playfield id: `127`
  - live runtime instance id: `1187842`
  - AOSharp capture object/output identity: `Playfield2:122002`

Tooling change:

- Dungeon NPC `CHAR-SEEN` rows now produce enemy population evidence in `enemy-dossier.json` even when no combat/focus event occurs.
- Combat/focus-derived enemy evidence remains preserved.
- Dossier output now includes resource, runtime, and capture object playfield fields so the three IDs are not conflated.
