# NPC Identity Bridge Capture

## Status

`NpcIdentityBridgeCapture` is a read-only evidence workflow. It captures a
runtime NPC and the client-visible world identities in one explicitly bounded
zone epoch. It does not authorize an official placement match and does not
change runtime NPC content.

The decisive client audit result is:

- `Playfield.Identity` directly exposes the current runtime playfield identity.
- `Playfield.ModelIdentity` exposes a native model identity whose live type is
  not guaranteed to be `1000014`. The value and state are recorded verbatim;
  only type `1000014` is retained as direct base-playfield evidence.
- AOSharp's checked-in Dynel/native surface does not expose an NPC-specific
  type-`1000014` template/model identity.
- A native zone/cell instance is readable for a Dynel. A stable official
  district ID and a cell-to-official-district relation are not exposed.
- World position and rotation are directly readable. No independent local-,
  district-, or cell-relative NPC position accessor or transform was found.

This means the workflow can directly bind an NPC runtime identity to the
official *playfield resource* identity in one epoch. It cannot by itself bind
that NPC to one `HashSpawnPoint_t` record because the checked-in client surface
does not expose an NPC-specific official placement identity or a proven
runtime-world to official-placement coordinate relation.

## Safety boundary

The mode only uses existing getters, packet callbacks, and capture-file writes.
It performs no client-memory writes, packet injection, executable patching,
database mutation, schema change, capture pruning, or runtime NPC-definition
change. Pointer addresses are diagnostic within one process lifetime only.

`ACGHash_t` is never accepted as runtime identity. Names, appearance,
`MonsterData`, `CATMesh`, mesh fields, and client stats are corroborating unless
a separate source proves stronger semantics. The AO unset value `1234567890`
is serialized as `sentinel/default` and never as authoritative state.

## Identity-source audit

| Value | Direct source | Lifetime and disposition |
| --- | --- | --- |
| NPC runtime type/instance | `Dynel.Identity` | Direct client state; keyed with `capture_id` and `zone_epoch_id`. Reuse in another epoch is a distinct entity key. |
| Dynel pointer | `Dynel.Pointer` | Diagnostic for recreation lineage inside one process; never a cross-run or authoritative key. |
| Runtime playfield | `Playfield.Identity` / `N3Playfield_t.GetIdentity` | Direct client state; re-sampled before and after a bounded snapshot. Never inherited across zoning. |
| Base/full playfield model | `Playfield.ModelIdentity` / `N3Playfield_t.GetModelID` | Direct client state. Accepted as the official playfield resource candidate only when type is exactly `1000014`. |
| NPC template/full model | Checked Dynel, SCFU, native import, model-loader, and resource lookup surfaces | `FULL_MODEL_ID_NOT_EXPOSED` for an NPC-specific type-`1000014` value. No appearance reverse lookup is permitted. |
| Zone/cell instance | `N3Dynel_t.GetZone` then `N3Zone_t.GetInstance` | Direct client state in the bounded snapshot. It is not treated as an official district ID. |
| District ID | No stable Dynel accessor found | `not-observed`. Native pointers or collection positions are not converted to IDs. |
| World position | `Dynel.Position` / vehicle position | Direct client state; retained separately from the packet SCFU position. |
| Rotation/orientation | `Dynel.Rotation` / vehicle rotation | Direct client-state quaternion; retained without deriving a placement heading. |
| SCFU identity, PF, position, heading, appearance, owner, `MonsterData`, `HeadMesh`, textures, meshes, visual flags | raw `SimpleCharFullUpdate` decoder | `packet-observed`, linked by direction, sequence, and global raw-packet ordinal. |
| Client-visible stats | `Dynel.GetStat` for the ten identity-relevant fields consumed by the bridge | `client-state-observed`, `sentinel/default`, or `not-observed` per field. Full 626-stat enumeration is intentionally skipped; a zero returned by the client stays zero and absence is null. |
| Stat packet updates | raw `Stat` decoder | `packet-observed`, linked by exact raw-packet key and only within a valid epoch. |
| Lifecycle/movement/zoning packets | raw packet envelope | Epoch-scoped references link to `raw-packets.csv`; they do not invent decoded semantics. |

The audited repository-local sources are:

- `tools-temp/external/aosharp-github/AOSharp.Core/Dynel/Dynel.cs`
- `tools-temp/external/aosharp-github/AOSharp.Core/Dynel/SimpleChar.cs`
- `tools-temp/external/aosharp-github/AOSharp.Core/Playfield/Playfield.cs`
- `tools-temp/external/aosharp-github/AOSharp.Core/Playfield/Zone.cs`
- `tools-temp/external/aosharp-github/AOSharp.Core/Playfield/Room.cs`
- `tools-temp/external/aosharp-github/AOSharp.Common/Unmanaged/Imports/N3/N3Dynel_t.cs`
- `tools-temp/external/aosharp-github/AOSharp.Common/Unmanaged/Imports/N3/N3Zone_t.cs`
- `tools-temp/external/aosharp-github/AOSharp.Common/Unmanaged/Imports/N3/N3Playfield_t.cs`
- `tools-temp/AOSharpCaptureProtocol/RawSimpleCharFullUpdateDecoder.cs`
- `tools-temp/AOSharpCaptureProtocol/RawStatDecoder.cs`

No new native offset or vtable wrapper was added: the existing read-only API
already exposes the direct playfield model identity, while no checked-in proof
supports an NPC-specific model offset.

## Zone epoch semantics

Each ID is deterministic within the capture:

```text
<capture_id>-zone-0001
<capture_id>-zone-0002
...
```

An epoch records inclusive raw-packet ordinal bounds. Adjacent bounds cannot
overlap. Every snapshot and packet projection carries the capture ID and epoch
ID, and its ordinal must fall inside that epoch. An epoch is evidence-valid
only after its inclusive end ordinal is finalized; an open epoch is published
as `pending`, and its snapshots and packets remain ineligible for replay or
resolver proof.

The tracker uses these transitions:

1. Capture start creates a pending epoch and accepts it after a double sample
   of local-player and runtime-playfield identity remains identical while
   `Game.IsZoning` is false. Model identity is independent epoch enrichment.
2. `TeleportStarted` closes the prior epoch. No live NPC snapshot is accepted
   while the transition is unresolved.
3. `PlayfieldInit` creates a new pending epoch with its runtime playfield hint.
   A conflicting stable runtime identity invalidates that pending epoch.
4. `TeleportEnded` or `TeleportFailed` requires a fresh stable world sample;
   the old epoch is never reopened.
5. A runtime-playfield or local-player identity replacement closes the epoch.
   Model identity is sampled at bounded intervals until direct type `1000014`
   is seen or the epoch closes; non-resource, default, late, and conflict states
   remain explicit.
6. NPC identity and runtime context are sampled before and after the ten-field
   snapshot. A change discards the bounded snapshot instead of combining state.

SCFU is evidence-eligible only in a stable current epoch when the packet itself
directly names the same runtime playfield. Stat packets do not name a playfield
and therefore cannot be attached during a transition. Pointer lineage and
runtime-instance reuse are keyed by epoch. Initial client discovery preserves
already-received direct same-epoch packets. A proven replacement/despawn
boundary clears the affected lineage and advances its evidence floor.

Capture is event-first. First discovery, SCFU, Stat, lifecycle activity, and
relevant client changes mark per-epoch identity state dirty. The two-second
nearby pass performs bounded retries and ten-second position refreshes; an
unchanged fingerprint is not serialized. Complete NPCs stop incomplete-field
retries. JSONL rewrites are throttled to 15 seconds and finalization forces the
last artifact and a coverage/performance summary.

## Live capture artifacts

The explicit launcher mode is:

```cmd
cmd /d /c tools-temp\start-aosharp-live-capture.cmd --npc-identity-bridge --title "Anarchy Online"
```

`--pid` may be used instead of `--title`. The mode is mutually exclusive with
`--loot-10` and `--pf127-geometry-only`, refuses reuse of an active
comprehensive capture, and uses a consumed request marker so normal captures
do not pay bridge-snapshot overhead. The launcher reports bridge-mode success
only after the deployed plugin consumes that marker and writes a non-empty
bridge JSONL artifact.

The mode adds `npc-identity-bridge-live.jsonl` and final
`npc-identity-bridge-summary.json` coverage/performance metrics to the normal
capture folder.
Schema version `1` record types are:

- `zone_epoch`: validity, state, trigger, inclusive packet bounds, timestamps,
  direct runtime/model playfield identities, and sampling error.
- `npc_snapshot`: the versioned candidate bridge observation required by the
  task, including independent position spaces and per-field provenance.
- `packet_scfu` and `packet_stat`: decoded packet projections tied to the raw
  packet triple `(direction, sequence, global_ordinal)`.
- `packet_event`: selected lifecycle, movement, and zoning raw-envelope
  references.

The snapshot `bridge_state` is one of `direct-candidate`, `partial`,
`not-exposed`, `conflict`, or `invalid-epoch`. `direct-candidate` means the
same-epoch direct inputs exist; it is not an official placement result.

## Offline replay and parity

Build/replay an already completed capture without starting or controlling AO:

```cmd
cmd /d /c MSBuild.exe tools-temp\AOSharpCaptureAnalyzer\AOSharpCaptureAnalyzer.csproj /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
cmd /d /c tools-temp\AOSharpCaptureAnalyzer\bin\Debug\AOSharpCaptureAnalyzer.exe "<capture-folder>"
cmd /d /c Tools\replay_npc_identity_bridge.cmd "<capture-folder>"
```

The analyzer first reconstructs `scfu-appearance.csv` and
`npc-stat-observations.csv` from preserved raw packets. The bridge replay then:

- validates capture/epoch identity and non-overlapping inclusive bounds;
- rejects snapshots outside their declared epoch;
- joins SCFU and Stat evidence only by the exact raw-packet triple;
- reconstructs an omitted live packet reference from a fully decoded raw
  packet only when epoch, direct runtime identity, ordinal, and lineage evidence
  window all match;
- retains failed or partial packet rows for audit but makes their keys
  ineligible for snapshot evidence;
- reproduces packet-derived values without timestamp fallback;
- keeps cached SCFU position, heading, level, and derived appearance fields
  separate from the later bounded Dynel position/rotation and client stats;
- leaves client-state-only fields classified as client-state-only;
- retains a unique bridge observation ID while emitting the exact
  `capture_id|(SimpleChar:INSTANCE_HEX)` harvested-observation join (uppercase,
  minimum width four) when directly observed;
- emits deterministic `npc-identity-bridge.json` with a canonical digest and
  explicit parity fields.

`--check` compares a newly reconstructed artifact with the existing output and
fails on drift. Replay refuses any output path that resolves to its live JSONL,
SCFU CSV, or Stat CSV source, so primary evidence cannot be overwritten.

## Resolver consumption

`NpcPlacementIdentityResolver` accepts an optional bridge artifact without
changing its default baseline. It rejects invalid/stale epochs, conflicts,
derived/sentinel/absent model values, any ACG-based identity claim, absent
direct base playfield, and unproven coordinate relations. Existing duplicate
placement ambiguity remains ambiguity. Replay `bridge_blockers` are preserved
for audit; advisory client-exposure limits do not replace or bypass the
resolver's independent critical-field proof checks.

The harvested observation is bound to `positions.packet_scfu` through the exact
SCFU source/direction/sequence/global-ordinal/raw-SHA provenance. A proven
relation whose target is `positions.world` is evaluated against the independent
client-world vector, never the consolidated harvester position. Repeated rows
for one harvested identity may deduplicate only inside the same nonempty epoch
and lifecycle lineage with identical critical proof; cross-epoch or
cross-lineage instance reuse is rejected.

A synthetic contract fixture may exercise the `unique-proven` code path only
when it supplies a valid epoch, direct type-`1000014` model/base identity, and
an explicitly proven coordinate relation that resolves to exactly one official
record. That test is not live evidence and cannot authorize production content.

## Live acceptance boundary

The preserved Arete sample `20260827-213046` proved the first live recorder was
functionally complete at the raw layer but polling-heavy and incomplete at the
first-discovery linkage boundary. Its audit is retained in
`docs/evidence/ARETE_NPC_IDENTITY_BRIDGE_FAILED_CAPTURE_20260827.md`. A later
user-operated capture remains separate acceptance and is not authorized by
offline fixture success.

```text
LIVE_BRIDGE_SAMPLE_ACQUIRED=YES_PRESERVED_FAILED_ACCEPTANCE_SAMPLE
LIVE_UNIQUE_PLACEMENT_PROVEN=NO
```
