# Rex B18D Cargo Box Static Dynel Result

Generated: 2026-06-18

## Goal

Correct the Rex B18D static dynel row using captured packet identity evidence only.

This pass is placement/identity data only. It does not implement rewards, inventory effects, XP/credits, persistence, schema changes, or broad static-dynel event execution.

## Evidence Used

- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-194454/events.log`
  - `6327`, `6333`, `7798`: B18D `GenericCmd Action=Use` targets `Terminal:56D9B4AF`.
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-205724/events.log`
  - `5493-5494`: `SimpleItemFullUpdate identity=(Terminal:56D9B4AF)`.
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-214819/events.log`
  - `14605-14606`, `15738-15739`, `19521-19522`: repeated `SimpleItemFullUpdate identity=(Terminal:56D9B4AF)`.
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-215831/events.log`
  - `10939-10940`, `11431-11432`: repeated `SimpleItemFullUpdate identity=(Terminal:56D9B4AF)`.
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-215831/packets.hex.log`
  - `9058-9059`, `9328-9329`: raw packet evidence for the same full update.

Important correction: rendered text such as `Cargo Box` is not the lookup source. The packet identity `Terminal:56D9B4AF` and its captured `SimpleItemFullUpdate` are the source of truth.

## Corrected Static Dynel Data

- `Type`: `51005`
- `Instance`: `1457108143` (`0x56D9B4AF`)
- `Playfield`: `6553`
- `Position`: `(3621.576, 51.745, 780.4768)`
- `Rotation`: `(0, -0.7101817, 0, 0.7040185)`
- Captured stats:
  - `Flags=139265`
  - `StaticInstance=297277`
  - `ACGItemLevel=1`
  - `ACGItemTemplateID=297277`
  - `ACGItemTemplateID2=297277`
  - `MultipleCount=1`
  - `AnimPlay=0`
  - `AnimPos=0`

Rejected local smoke attempts are explicitly not used in the corrected row:

- Nearby `Terminal:57369E8E` `Junk` anchor.
- Template `285300`.
- Explicit `Mesh=18794`.

## Files Inspected

- `AI_START_HERE.md`
- `AGENTS.md`
- `docs/ai/AI_START.md`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/project/KNOWN_DECISIONS.md`
- `docs/project/ARCHITECTURE.md`
- `docs/ai/WORKFLOW.md`
- `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs`
- `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/staticdynels.sql`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-194454/events.log`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-205724/events.log`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-214819/events.log`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-215831/events.log`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-215831/packets.hex.log`

## Files Changed

- `tools-temp/sql-staging/arete_cargo_box_staticdynel.sql`
- `docs/generated/rex_b18d_cargo_box_staticdynel_result.md`
- `docs/generated/rex_b18d_box_progress_result.md`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`

## SQL Patch

Path:

`tools-temp/sql-staging/arete_cargo_box_staticdynel.sql`

The SQL remains idempotent and scoped to one row:

- `Type=51005`
- `Instance=1457108143`
- `Playfield=6553`

## Local Apply Result

Applied corrected SQL locally to `cellao_codex_clean`.

DB verification found exactly one scoped row:

| Id | Type | Instance | Playfield | X | Y | Z | HeadingX | HeadingY | HeadingZ | HeadingW |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 295 | 51005 | 1457108143 | 6553 | 3621.58 | 51.745 | 780.477 | 0 | -0.710182 | 0 | 0.704018 |

Decoded DB stats:

- `Flags=139265`
- `StaticInstance=297277`
- `ACGItemLevel=1`
- `ACGItemTemplateID=297277`
- `ACGItemTemplateID2=297277`
- `MultipleCount=1`
- `AnimPlay=0`
- `AnimPos=0`

ZoneEngine was restarted after the apply so playfield `6553` can reload the corrected row.

## Validation Status

- Capture files were checked and remain unmodified: `git status --short -- tools-temp/AOSharpLiveCapture` returned no output.
- Corrected `MessagePackZip` stat blob was generated from the repo's own built messaging/utility assemblies.
- Corrected SQL applied successfully to `cellao_codex_clean`.
- Local DB row and decoded stats match the captured full-update evidence above.
- `ChatEngine`, `LoginEngine`, and restarted `ZoneEngine` are running after the apply.
- Live client smoke still needs to be rerun after this correction.
- Build was not required for this correction because no runtime code or project files changed.
