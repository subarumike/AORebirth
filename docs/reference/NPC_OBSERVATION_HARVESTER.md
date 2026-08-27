# NPC Observation Harvester

`Tools\npc_observation_harvester.py` is the deterministic, database-wide join between retained AOSharp NPC observations and the governed official static-placement corpus. It preserves observable client evidence; it does not claim to reconstruct Funcom server-only behavior.

Run the governed Windows workflow with:

```cmd
cmd /d /c Tools\harvest_npc_observations.cmd
```

The default machine-readable output is ignored build evidence under `build-verify\npc-observation-harvester`. Raw captures and the accepted capture inventory are read-only inputs. The workflow has no capture-prune, inventory-prune, database-write, runtime-activation, or production-deployment operation.

## Current source paths

| Concern | Current source |
| --- | --- |
| Raw packet retention and live projection entry | `tools-temp/AOSharpLiveCapture/Main.cs` `LogPacket` |
| Raw SCFU decode | `tools-temp/AOSharpCaptureProtocol/RawSimpleCharFullUpdateDecoder.cs` |
| Live SCFU projection | `tools-temp/AOSharpLiveCapture/Main.cs` `DecodeAndExportRawSimpleCharFullUpdate` |
| Ordinary Stat raw decode | `tools-temp/AOSharpCaptureProtocol/RawStatDecoder.cs` |
| Live ordinary Stat projection | `tools-temp/AOSharpLiveCapture/Main.cs` `DecodeAndExportRawStat` |
| Offline SCFU and Stat replay | `tools-temp/AOSharpCaptureAnalyzer/Program.cs` |
| Dossier generation | `tools-temp/AOSharpLiveCapture/Main.cs` `WriteEnemyDossierJson` |
| Full-update CSV | `tools-temp/AOSharpLiveCapture/Main.cs` `ExportEnemyFullUpdate` |
| Capture finalization/integrity | `tools-temp/AOSharpLiveCapture/Main.cs` capture validation and `capture_info.json` generation |
| Capture discovery and accepted-history governance | `Tools/inventory_aosharp_captures.py` and `docs/generated/aosharp_capture_inventory.csv` |
| Combat/lifecycle/loot/respawn projections | `tools-temp/AOSharpLiveCapture/Main.cs` plus `tools-temp/AOSharpLiveCapture/decode_npc_lifecycle_capture.py` |
| Official placement loading | `docs/generated/playfields/official-placement-index.json` and its `placements/pf_*.json` shards |
| Official corpus generation/governance | `Tools/import_official_playfield_placements.py` |
| Existing runtime placement promotion | `Tools/generate_pf4582_placements.py` and the governed PF4582 catalogs |
| Database-wide observation/reconciliation/promotion candidates | `Tools/npc_observation_harvester.py` |

## Evidence schema

Every field has one evidence classification:

- `packet-observed`
- `client-state-observed`
- `sentinel/default`
- `not-observed`

Every field/category coverage result is one of:

- `captured`
- `partial`
- `not observed`
- `not protocol-exposed`
- `ambiguous`
- `conflict`

The AO unset value `1234567890` is recorded only as sentinel evidence with a null value. It is never copied into `authoritativeFields` in a promotion candidate. Legitimate zero remains an observed zero.

SCFU appearance retains `HeadMesh`, texture arrays, mesh arrays, texture overrides, `VisualFlags`, appearance/body values, breed, gender, race, side, active nanos, owner, opaque retained bytes, packet digest, capture identity, direction, sequence, global ordinal, and timestamp. An absent CATMesh is explicitly `not observed` and cannot replace proven texture/mesh evidence.

Client-state enumeration uses the checked AOSharp `Stat` enum in numeric order and calls the supported `SimpleChar.GetStat` surface without reflection. Unsupported access is `not protocol-exposed`, sentinel is `sentinel/default`, and a returned zero is `client-state-observed`.

## Outputs

| File | Purpose |
| --- | --- |
| `npc-observations.json` | Generic NPC observations with field provenance and category evidence |
| `npc-appearance-observations.json` | Exact SCFU appearance evidence |
| `npc-stat-observations.json` | Repeated packet/client-state stat observations and conflicts |
| `observation-placement-reconciliation.json` | Unique, ambiguous, and unmatched coordinate joins |
| `official-placement-field-coverage.json` | Field-level coverage for every official placement |
| `ambiguity-conflict-report.json` | Fail-closed ambiguity, conflict, and unmatched inventory |
| `npc-promotion-candidates.json` | Safe candidates and explicit promotion blockers |
| `capture-corpus.csv` | Accepted/current capture inclusion and replay status |
| `summary.json` | Measured corpus totals and acceptance facts |

## Identity and matching boundary

The current deterministic match requires the resource playfield plus exact IEEE-754 single-precision X/Y/Z coordinates. It does not use proximity, display name, appearance similarity, or ACGHash to choose a placement. Multiple official records at the same exact coordinate remain ambiguous. ACGHash remains an official packed tag, not a proven runtime NPC identity.

## Three independent gates

- Capture integrity answers whether the retained raw stream reconciled and decoded structurally.
- Observation coverage reports only the NPC fields/categories actually observed.
- Promotion readiness requires a unique placement join and no field/stat conflicts.

`processingAllowed` and `offlineDecodeRequired` remain capture-processing compatibility fields. Neither is used as evidence that an NPC field was observed.

Loot tables, AI, combat decision logic, respawn probability, and other server-only behavior can remain observational or `not protocol-exposed`; finite client observations do not prove complete Funcom server logic.
