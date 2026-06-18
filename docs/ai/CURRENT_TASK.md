# Current Task

Generated: 2026-06-18

## Current Objective

Repair the Rex B18D Cargo Box static dynel row using packet identity evidence only.

Scope:

- Target identity: `Terminal:56D9B4AF`.
- Do not use rendered text as the lookup source.
- Do not use nearby static clutter as a substitute identity.
- Do not use rejected local smoke attempts:
  - `Terminal:57369E8E` `Junk` anchor.
  - Template `285300`.
  - Explicit `Mesh=18794`.
- Keep B18D behavior narrow:
  - exact-target `GenericCmd Action=Use` only;
  - captured B18D-to-B18E packet-window handoff only;
  - no rewards, inventory, XP/credits, persistence, schema change, or broad static dynel execution.

## Packet Evidence

- Capture `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-194454` proves B18D use target `Terminal:56D9B4AF`:
  - `events.log:6327`
  - `events.log:6333`
  - `events.log:7798`
- Capture `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-205724` proves exact `SimpleItemFullUpdate` for `Terminal:56D9B4AF`:
  - `events.log:5493-5494`
- Capture `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-214819` repeats the exact full-update:
  - `events.log:14605-14606`
  - `events.log:15738-15739`
  - `events.log:19521-19522`
- Capture `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-215831` repeats the exact full-update:
  - `events.log:10939-10940`
  - `events.log:11431-11432`
  - `packets.hex.log:9058-9059`
  - `packets.hex.log:9328-9329`

Exact captured row data:

- Position: `(3621.576, 51.745, 780.4768)`
- Rotation: `(0, -0.7101817, 0, 0.7040185)`
- Stats:
  - `Flags=139265`
  - `StaticInstance=297277`
  - `ACGItemLevel=1`
  - `ACGItemTemplateID=297277`
  - `ACGItemTemplateID2=297277`
  - `MultipleCount=1`
  - `AnimPlay=0`
  - `AnimPos=0`

## Current Status

- `tools-temp/sql-staging/arete_cargo_box_staticdynel.sql` has been corrected to the exact captured `Terminal:56D9B4AF` full-update position, rotation, and stats.
- Corrected SQL has been applied locally to `cellao_codex_clean`.
- DB verification shows one row for `Type=51005`, `Instance=1457108143`, `Playfield=6553` at `(3621.576, 51.745, 780.4768)` with rotation `(0, -0.7101817, 0, 0.7040185)`.
- DB stat decode matches the captured eight stat entries: `Flags=139265`, `StaticInstance=297277`, `ACGItemLevel=1`, `ACGItemTemplateID=297277`, `ACGItemTemplateID2=297277`, `MultipleCount=1`, `AnimPlay=0`, `AnimPos=0`.
- ZoneEngine has been restarted with the four Rex gates enabled so playfield `6553` can reload the corrected row.
- Capture folders remain read-only for this repair; `git status --short -- tools-temp/AOSharpLiveCapture` returned no modified capture files.
- Client smoke still needs to inspect the corrected object and exact-target B18D use behavior.

## Validation Plan

- Inspect corrected `Terminal:56D9B4AF` in the client after relog/rezoning to Arete Landing.
- Confirm whether exact-target use advances from B18D to B18E.
- Run `git diff --check`.
