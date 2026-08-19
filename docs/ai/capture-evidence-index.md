# AORebirth Capture Evidence Plan

Status date: 2026-08-16

## Source of Evidence
Historical retained capture artifacts are in:
C:\Users\Mike\Documents\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures

New captures created by the approved launcher are stored in:
C:\Users\Mike\Documents\AORebirth\Captures

New session folder names use `<area> [PF <resource id>] - <capture id>` so a person can identify the location immediately while analyzers retain the compact timestamp capture ID.

The keep set was narrowed to 36 folders and contains mission-backed and reference-backed sessions we can use for implementation and verification.

## Retained captures
| capture_id | priority | files | mission_flow | raw_packets | raw_status |
|---|---|---:|---|---|---|
| 20260531-023030 | P2 | 2 | no | no | low |
| 20260604-235530 | P2 | 2 | no | no | low |
| 20260607-202610 | P2 | 4 | no | no | low |
| 20260610-185759 | P2 | 5 | no | no | medium |
| 20260610-214233 | P2 | 5 | no | no | medium |
| 20260610-232330 | P2 | 5 | no | no | medium |
| 20260610-233143 | P2 | 5 | no | no | medium |
| 20260611-005202 | P2 | 5 | no | no | medium |
| 20260611-015456 | P2 | 5 | no | no | medium |
| 20260611-031405 | P2 | 5 | no | no | medium |
| 20260623-012720 | P2 | 13 | no | no | medium |
| 20260623-015602 | P2 | 12 | no | no | medium |
| 20260623-021643 | P2 | 12 | no | no | medium |
| 20260623-040642 | P2 | 12 | no | no | medium |
| 20260623-042326 | P2 | 12 | no | no | medium |
| 20260623-081344 | P2 | 12 | no | no | medium |
| 20260716-222007 | P2 | 32 | no | yes | medium |
| 20260717-214751 | P2 | 32 | no | yes | medium |
| 20260717-215250 | P2 | 31 | no | yes | medium |
| 20260717-220340 | P2 | 32 | no | yes | medium |
| 20260722-104809 | P1 | 30 | yes | yes | high |
| 20260722-152454 | P1 | 30 | yes | yes | high |
| 20260728-001044 | P1 | 31 | yes | yes | high |
| 20260728-003410 | P1 | 31 | yes | yes | high |
| 20260728-005042 | P1 | 31 | yes | yes | high |
| 20260728-010220 | P1 | 31 | yes | yes | high |
| 20260728-010846 | P1 | 29 | yes | yes | high |
| 20260728-012547 | P1 | 31 | yes | yes | high |
| 20260728-231938 | P1 | 31 | yes | yes | high |
| 20260728-233312 | P1 | 31 | yes | yes | high |
| 20260729-000735 | P1 | 28 | yes | yes | high |
| 20260731-030702 | P1 | 30 | yes | yes | high |
| 20260731-035230 | P1 | 30 | yes | yes | high |
| 20260814-014647 | P1 | 28 | yes | yes | high |
| 20260814-015856 | P1 | 28 | yes | yes | high |
| BrandonThornCapture | P1 | 28 | yes | yes | high |
## How to use this dataset
1. Start every behavior change with a capture-backed statement.
2. For each change, cite capture_id, event source files, and the expected state transition.
3. Validate by comparing post-change behavior against the same capture evidence type (mission flow if available, otherwise packet/event logs).
4. Only promote changes when one of these is satisfied:
   - mission transcript is reproducible (mission-flow.log + aw-packets.csv)
   - or same outcome appears in at least two non-mission captures with consistent events.

## Priority usage model
- P1: Use immediately for implementation and regression checks.
- P1-regression: use as hard regression lock.
- P2: use for targeted backfill and confirmation.

## Immediate action list (next 3 passes)
1. Mission system reconstruction: prioritize captures with replay artifacts (...010220, ...001044, ...003410, etc.).
2. Combat/NPC behavior validation: prioritize captures with high combat event density (...222007, ...214751, ...215250, ...220340).
3. Content/interaction validation: vendor/shop and dialog/state evidence from ...012720 and adjacent sessions.

## Retention confirmation
No file list below 36 captures is removed by this plan; it documents only. For cleanup decisions, use manifest in capture-evidence-index.csv.
