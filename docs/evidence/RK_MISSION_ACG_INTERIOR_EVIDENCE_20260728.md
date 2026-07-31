# RK Mission ACG Interior Evidence — 2026-07-28

## Scope and result

This document records the evidence boundary for the first capture-backed RK
terminal-mission interior catalog.

The five finalized official-live captures now extract as
`complete_and_selectable`. Each produces a canonical
`ao-rebirth.mission-acg-layout` version 1 artifact with an exact generator
payload, entry correlation, physical layout slots, one evidence-bound objective,
and one decoded exit-boundary door. The deterministic catalog emitter converts
those five artifacts into five `CompleteSelectable` runtime bundle definitions.

This is a captured-layout catalog, not a reconstruction of AO's procedural
interior generator. It does not prove room-graph grammar, tile probabilities,
collision generation, or any interpretation of the opaque C79F generator
payload. Stage 2 persists the exact selected bundle and isolated PF2. Stage 3
now materializes that exact captured bundle into the bound live PF2 without
regenerating or substituting layout evidence.

No database schema or destructive database operation is part of this stage.

## Durable generated-mission token progress

The earlier `MissionTokenProgressTracker` kept generated-mission progress in
process-local character/playfield dictionaries. That state did not prove which
accepted quest or objective owned a callback, disappeared on restart, and could
not safely distinguish a duplicate callback from a new token event.

Generated missions therefore use a version-1 sidecar under
`mission-state/acg-token-progress`. Serialization has deterministic key ordering
where practical, a SHA-256 integrity field, and atomic temp-file replacement.
Unknown versions, malformed content, integrity mismatch, duplicate accepted
quest records, and ownership mismatches fail closed; no record is rerolled or
silently replaced.

Each progress record is bound to:

- the exact accepted quest identity, never the rolled offer identity;
- the explicit no-team owner and mission type;
- the exact persisted objective binding and allocated live PF2;
- the exact runtime death-source identity;
- the captured materializable `Ambient` slot and spawn generation;
- a deterministic token event identity derived from the accepted quest, live
  PF2, captured slot, spawn generation, and runtime source identity.

Objective roles such as `KillTarget` and `FindPerson` are excluded from token
progress. The frozen denominator is the set of captured materializable
`Ambient` slots for that accepted bundle. Legacy missions keep their existing
aggressive/non-grey tracker path; generated missions do not infer a grey
classification that Stage 5 capture evidence does not contain. They require
the exact persisted dead `Ambient` source and its existing production combat
registration instead. No ambient slot or token amount is fabricated.

The captured/code-owned progress calculation is retained:

```text
percent = floor(applied * 100 / total)
known exact total == 0 => 100 percent
```

This is progress accounting only. It does not change token item amounts,
probabilities, reward rules, reward formulas, accepted-QFU fields, mission
sliders, loot, or authored-quest token behavior.

### Exactly-once event phases and arbitration

The durable event journal distinguishes:

1. `NotObserved`
2. `Validated`
3. `DurablyApplied`
4. `ClientUpdatePending`
5. `ClientUpdateSent`
6. `TerminalFailure`

Validation first resolves the full accepted quest, solo owner, objective,
allocated PF2, exact dead runtime source, captured Ambient slot, and spawn
generation. The deterministic event identity is persisted at `Validated`.
Advancing to `DurablyApplied` changes the counter once. Later recovery may
resume an incomplete phase, but the same event identity cannot apply again.
`ClientUpdatePending` is durable before feedback is attempted.
`ClientUpdateSent` means only that the server performed the send; the protocol
does not provide a client acknowledgement, so reconnect recovery may safely
retry a still-pending update without changing the applied counter.

Token application, objective verification, completion, abandonment, and expiry
use mutually exclusive lifecycle claims against the exact current binding and
objective. New token events are rejected after completion, expiry, abandonment,
or cleanup begins. The same gate prevents completion from applying a second
token event when the objective death path already durably applied it, and gives
completion-versus-expiry one persisted lifecycle winner.

### Startup, migration, and cleanup

Startup restores exact sidecars before accepting new progress. Reconnect
reconstructs the frozen counter and resumes only pending feedback; it does not
reroll a denominator or replay a durably applied event.

Migration without an existing token sidecar is intentionally restricted:

- when every countable Ambient source is still alive, the exact active mission
  can safely initialize at zero applied events;
- when any countable Ambient source is already dead, prior progress is
  ambiguous, so an invalid terminal record is persisted and progress fails
  closed;
- when an exact sidecar already exists, a persisted dead source can be
  reconciled through the exact quest, PF2, runtime source, captured slot, and
  spawn generation while already-applied events remain unchanged.

Mission cleanup removes only transient token runtime registration. The durable
terminal/audit record remains available to reject a delayed or replayed
callback. Team distribution is not inferred: generated bindings currently have
authoritative explicit no-team ownership, so team-owned token behavior remains
deferred. Authored quests and unrelated token systems keep their existing
owners and persistence.

## Authoritative captures

The source directories are under
`tools-temp/AOSharpLiveCapture/bin/Debug/captures`.

Raw counts are packet observations inside the selected layout window. The SCFU
count includes the player. Physical counts are normalized, unique layout slots;
their NPC count excludes the player. Identity-only inventory/lifecycle records
remain ordered observations and are not physical slots.

| Capture | Mission / icon | Building / captured PF2 | Raw Door / Chest / SCFU | Physical Door / Chest / NPC / Objective | Generator payload SHA-256 |
|---|---|---|---:|---:|---|
| `20260728-001044` | Kill Person / `11330` | `00D734E2` / `0015F008` | 23 / 22 / 16 | 11 / 8 / 7 / 1 | `ffe4327ac8af0f0a41a04cff7fe53ecd40c55a027f10a2cda2cd2a8fc18f1269` |
| `20260728-003410` | Return Item / `11329` | `00D6FC77` / `0016C80E` | 64 / 44 / 59 | 21 / 15 / 21 / 1 | `f7f00e3344bd12f2d7d302761403c9c5b083fc8a181417c7f2c9748da501ff59` |
| `20260728-005042` | Find Item / `11337` | `00D6FC78` / `00169802` | 59 / 27 / 40 | 27 / 13 / 18 / 1 | `3cfe53d3a32b50679530bdfd5ff7572405eb8865f4ab0c13308c7bcd935bf431` |
| `20260728-010220` | Repair / `11342` | `00D734E5` / `0015F00F` | 26 / 15 / 29 | 17 / 12 / 19 / 1 | `e75f1326a72db6d42ddb5ebd72320338148193e6469e70b1c30b2d8a0f6d1926` |
| `20260728-012547` | Find Person / `11335` | `00D734E7` / `0016700C` | 56 / 39 / 46 | 14 / 11 / 14 / 1 | `d5413273f69b018b66fcd6fe31bfa7be15b338cb6cb8fd17d83f7e14c4e4be82` |

The accepted QFU does not expose a defensible mission QL in these five
captures. The emitted bundles therefore document QL as unresolved. Allowing
these captured mission families at QL 1 through 250 is an explicit Rebirth
selection policy, not a captured AO fact.

## Objective correlation

An objective is selectable only when exactly one physical record correlates to
an exact identity field in the accepted QFU. The extractor does not select an
item or NPC merely because it appears in the same playfield.

| Capture | Accepted-QFU evidence | Correlated physical record | Result |
|---|---|---|---|
| `20260728-001044` | `QuestActions[0].UnknownId2 = C350:79A16B61` | SCFU `C350:79A16B61`, Pedro Peasley | Kill Person NPC objective |
| `20260728-003410` | `QuestActions[0].Action = C74A:2586CCB1` | WeaponItemFullUpdate `C74A:2586CCB1` | Return Item objective |
| `20260728-005042` | `QuestActions[0].Action = C73D:57AC07B0` | SimpleItemFullUpdate `C73D:57AC07B0` | Find Item objective |
| `20260728-010220` | `QuestActions[0].UnknownId1 = C73D:57A3C596` | SimpleItemFullUpdate `C73D:57A3C596` | Repair objective |
| `20260728-012547` | `QuestActions[0].UnknownId2 = C350:79A16EB9` | SCFU `C350:79A16EB9`, Emery Ratti | Find Person NPC objective |

For Repair, `C73D:57A3C596` is the QFU-correlated objective.
`C73D:57A3C597` is a separate observation and is not promoted.

Owned item-family packets that have no physical placement are preserved with
full raw provenance as
`non_layout_owned_item_inventory_or_lifecycle_observation`. They are excluded
from `layoutSlots.objectives`. A positional item with an encoded unexpected PF2
is a blocking error.

## Exit-boundary rule

Exit selection uses decoded wire fields and placement, not a guessed topology
grammar:

1. Normalize DoorFullUpdate observations by captured identity.
2. Require a fully decoded door with `Unknown6 = 2`,
   `Unknown7 = 0xFFFF0000`, and an empty undecoded tail.
3. Require that sentinel candidate to be unique.
4. Require it to be the unique sentinel door nearest the C79F interior spawn.

| Capture | Exit identity | Distance from interior spawn | Post-layout exit teleport |
|---|---|---:|---|
| `20260728-001044` | `C748:109AAC07` | `1.80007958` | not captured |
| `20260728-003410` | `C748:109AD151` | `1.80007958` | captured |
| `20260728-005042` | `C748:109AC391` | `1.800081` | captured |
| `20260728-010220` | `C748:109AB591` | `1.80007958` | captured |
| `20260728-012547` | `C748:109AACF8` | `1.80007386` | captured |

The Kill capture remains selectable because its decoded, unique boundary door
is complete evidence; the absent post-layout teleport is retained as
`exit_teleport_missing`, not fabricated.

## Canonical extraction and rejection rules

The canonical artifact preserves:

- accepted QFU type, icon, building, terminal, exterior entrance, mission key,
  action identities, scalar fields, and raw provenance;
- entry teleport, exact C79F packet fields, exact generator payload bytes and
  hash, building, PF2, and interior spawn;
- exact DoorFullUpdate stats and trailing fields;
- positioned chest/item stats and supported trailing fields;
- VendingMachineFullUpdate type, placement, PF2, stats, display string,
  optional identities, and trailing fields;
- SCFU level, health, health damage, monster data/scale, head mesh, textures,
  meshes, decode consumption, and tail;
- CharInPlay and mission lifecycle packets with full raw bytes;
- raw observation arrays, normalized physical layout slots, identity mappings,
  issues, completeness, and selectability.

Selection evidence fails closed when:

- required evidence lacks raw provenance or
  `PreservationStatus=raw_complete`;
- required evidence comes from another capture session;
- the PAF, teleport, QFU, payload building, or physical slot PF2/building
  correlations disagree;
- a critical record cannot be decoded;
- an NPC SCFU is not fully consumed or retains an undecoded tail;
- duplicate immutable records conflict;
- an NPC/objective identity overlay disagrees on PF2, parent, placement,
  heading, template/name, or decoded SCFU fields;
- the objective or exit correlation is missing, ambiguous, or conflicting;
- a float is non-finite; or
- the captured PF2 is the explicitly incomplete shape `1441804`.

Identity-only chest observations do not claim a captured PF2:
`capturedPf2` and `capturedPf2Hex` are `null`, their position is `null`, and
they are excluded from physical chest slots. An actually encoded off-PF
positional chest remains a blocking error.

## Full capture-corpus result

The full captures directory was analyzed in one pass:

```text
ACG corpus analyzed=97 extractionFailures=0 selectable=5
```

The canonical corpus contains:

- 97 capture artifacts;
- 0 extraction failures;
- 5 artifacts containing a generator C79F PAF;
- exactly those 5 artifacts at `complete_and_selectable`;
- 92 non-ACG or incomplete capture directories at
  `incomplete_and_non_selectable`; and
- no other selectable capture.

Final warnings are evidence annotations, not errors:

| Capture | Warnings |
|---|---|
| `20260728-001044` | `exit_teleport_missing`; two `chest_non_layout_identity_only_observation` |
| `20260728-003410` | one `chest_non_layout_identity_only_observation` |
| `20260728-005042` | none |
| `20260728-010220` | none |
| `20260728-012547` | none |

All five have zero extraction errors.

## Runtime catalog: generated five, legacy eight, and 1441804

`MissionAcgLegacyLayoutCatalogFactory.Create()` combines the generated captured
bundles with eight older structural layout families:

1. `1441800`
2. `1443840`
3. `1460226`
4. `1456133`
5. `1419310`
6. `1419335`
7. `1419382`
8. `1419349`

The eight legacy bundles retain generator payload, entry, door, chest, and
structured NPC shape evidence. They remain
`StructurallyCompleteObjectiveIncomplete` and are not selectable because they
lack the exact exit/objective/lifecycle correlation required by this contract.

PF2 `1441804` remains an explicit exclusion, not a bundle. It has partial NPC
shape evidence but no coherent matching generator, door, chest, objective,
exit, accepted-QFU, and teleport lifecycle set. No other payload or layout may
be substituted for it.

With the generated catalog present, the catalog therefore contains five
capture-backed selectable bundles plus eight audit-visible nonselectable legacy
bundles, and one explicit `1441804` exclusion.

## Model, selector, and binding boundaries

These three layers have different responsibilities:

- `MissionAcgLayoutBundle` is the immutable evidence model. It carries the
  exact captured payload, layout slots, objective/exit evidence, provenance,
  compatibility, and completeness.
- `MissionAcgLayoutSelector` is a deterministic Rebirth selection policy. It
  filters to catalog-admitted `CompleteSelectable` bundles compatible with
  mission type and policy QL, then derives a stable choice from the seed,
  mission inputs, and owner/team identity. It is not the official AO layout
  RNG.
- `MissionAcgInstanceBinding` version 2 is the immutable durable identity for an
  accepted generated mission. Mutable lifecycle and cleanup state remain in
  `MissionAcgInstanceState`; no chest, NPC, door, or objective state is stored
  in the captured layout.

The generated `.g.cs` catalog is deterministic source material. It does not
randomly generate a new room graph and does not by itself make mission entry
operational.

## Stage 2 binding persistence and allocation

Generated terminal missions use sidecars under
`mission-state/acg-bindings`. Authored-quest persistence and database schemas
are unchanged. Each accepted mission has one
`<accepted-type>-<accepted-instance>.acg` record with:

- header `AORebirth-MissionAcgBinding`;
- `FormatVersion=2`;
- distinct accepted quest and original offer identities;
- exact owner plus either an exact team identity or `ExplicitNoTeam=true`;
- mission type, QL, deterministic seed, key, exterior playfield/entrance/XYZ,
  and issuing terminal;
- selected bundle ID, exact generator-payload SHA-256, ACG building, and
  allocated live PF2;
- accepted/expiry/update timestamps; and
- lifecycle, cleanup, and optional cleanup-start timestamp.

Fields are serialized in ordinal key order as invariant UTF-8 `key=value`
lines. `RecordSha256` covers the complete canonical field block. Writes use a
same-directory temporary file, flush it to disk, validate a full readback, and
then move or replace the target atomically. `.tmp` files are never loaded.
Unknown versions, malformed/truncated records, record-hash mismatch, missing
bundles, bundle-payload hash mismatch, building mismatch, duplicate accepted
IDs, and duplicate active PF2 ownership all fail closed. No invalid record
causes bundle reselection or replacement PF2 allocation.

The allocated live-PF2 range is inclusive `0x160000..0x16FFFF`. The allocator
excludes every captured catalog PF2 and legacy shared PF2 `1419349`. Captured
PF2 values remain provenance in `MissionAcgLayoutBundle`; only
`AllocatedLivePlayfield2` from the binding is used as live instance identity.
Reservations are restored from all non-cleaned sidecars before a new
allocation. Exhaustion fails acceptance without an accepted mission, key, or
binding. A PF2 is released only after lifecycle `Cleaned` and cleanup
`Completed`.

Current generated terminal missions use durable solo ownership because the
existing team IDs are process-local and cannot be safely persisted. A binding
therefore records explicit no-team state, and another character cannot resolve
it. No team identity is inferred. Durable team-owned generated missions remain
fail-closed until a stable team identity exists.

Acceptance is ordered as follows:

1. Validate the player, exact rolled offer, mission type/QL, issuing terminal,
   and exterior action.
2. Reserve a distinct accepted quest ID.
3. Derive the deterministic seed and select one complete compatible bundle.
4. Reserve a non-captured, non-shared live PF2.
5. Reserve the exact mission-key identity.
6. Construct and atomically persist a `Reserved` binding.
7. Atomically persist accepted mission state under the distinct accepted ID.
8. Grant only the reserved key identity and the repair tool when required.
9. Send the accepted QFU using the accepted ID, bound key, building, terminal,
   and exterior marker.
10. Atomically transition the binding to `Accepted`.

Selection, PF2, key-ID, or first-sidecar failures release every process
reservation before any player artifact exists. Accepted-state failure marks
the durable binding for cleanup and releases it after `Cleaned`. Key/tool or
QFU failure removes the exact accepted state and exact granted artifacts before
terminal cleanup. If the final acceptance transition itself cannot be
persisted, the durable `Reserved` record is retained for explicit startup
reconciliation and its PF2 is not reused.

Lifecycle values are `Reserved`, `Accepted`, `Active`,
`CompletionStarted`, `Completed`, `Abandoned`, `Expired`,
`CleanupPending`, `Cleaned`, and `Invalid`. Cleanup values are `None`,
`KeyRemovalPending`, `InstanceReleasePending`, `Completed`, and `Failed`.
Shutdown does not transition active bindings. Startup validates the complete
catalog relationship, restores all reservations, and moves expired accepted or
active records to `Expired/KeyRemovalPending`; their PF2 remains reserved until
exact key/instance cleanup completes.

Entry resolution has no newest-mission fallback for bound missions. Exact key
lookup is owner plus mission-key identity. Exterior use resolves a unique
active owner binding by exterior playfield and captured marker proximity; zero
or multiple matches fail closed. The exact key instance must still exist in
that owner's inventory. The resulting non-production `MissionAcgEntryPlan`
carries one accepted ID, bundle ID/hash/payload, ACG building, key, and
allocated live PF2. QFU resync, teleport identity helpers, and PAF
payload/building helpers use that same binding. Production entry logs the exact
plan and remains blocked until Stage 3 materialization is safe.

## Validation commands and observed output

Analyzer build:

```text
cmd /d /c MSBuild.exe tools-temp\AOSharpMissionCaptureAnalyzer\AOSharpMissionCaptureAnalyzer.csproj /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
PASS
```

Analyzer self-test:

```text
cmd /d /c tools-temp\AOSharpMissionCaptureAnalyzer\bin\Debug\AOSharpMissionCaptureAnalyzer.exe --self-test
AOSharp mission capture analyzer self-test PASS
```

The self-test covers the exact five building/PF2/hash/raw-count/slot-count
tuples, exact objective identity and QFU field including Repair `.596`, exit
sentinels, canonical determinism, and negative cases for building/PF2 mismatch,
off-PF physical records, cross-session mixing, missing provenance,
non-`raw_complete` evidence, missing payload, non-finite floats, and incomplete
NPC decoding.

Full corpus:

```text
cmd /d /c tools-temp\AOSharpMissionCaptureAnalyzer\bin\Debug\AOSharpMissionCaptureAnalyzer.exe --corpus tools-temp\AOSharpLiveCapture\bin\Debug\captures
ACG corpus analyzed=97 extractionFailures=0 selectable=5
```

Stage 3 deterministic runtime validation:

```text
MissionAcgRuntimeMaterializationTests: 6/6 PASS
All MissionAcg Stage 1/2/3 tests: 50/50 PASS
Mission-filtered regression suite: 93/93 PASS
ZoneEngine isolated Debug build: PASS
```

The broader `PlayfieldLifecycleTraceTests` checkpoint remains `50/66`; its
sixteen existing combat, Arete, corpse, and session-architecture guardrail
failures are outside the mission ACG change. No Stage 3 assertion fails there.

Kill capture extraction and replay:

```text
20260728-001044: ACG extraction status=complete_and_selectable selectable=True
20260728-001044: mission replay decoded=1974 rawPreserved=505 skipped=1990 errors=0
```

## C79F evidence boundary

The extractor knows the exact C79F wire bytes, captured building identity,
captured PF2, interior spawn, payload bytes, payload-leading building identity,
and payload SHA-256 for each finalized capture.

Fields still named `Unknown*` and the remaining payload bytes remain opaque.
The implementation does not assign them room, corridor, door, population,
probability, or seed semantics. The payload is retained as immutable captured
evidence. Reproducing it is different from understanding or implementing AO's
procedural-layout algorithm.

## Stage 3 runtime materialization

`MissionAcgRuntimeMaterializer` consumes exactly one validated
`MissionAcgInstanceBinding` and its selected `MissionAcgLayoutBundle`. It
constructs an instance-local registry for captured doors, the exact exit,
chests, mission terminals, repair/static objective objects, objective NPC
placeholders, and ambient NPC placeholders. Raw captured wire is copied before
retargeting. Explicit Stage 1 retarget slots are used for door/chest/terminal
wire; captured NPC and objective packets replace only their exact captured
identity, captured PF2, and captured player instance values.

Runtime identities use:

```text
0x60000000 | ((allocatedLivePF2 - 0x160000) << 8) | localOrdinal
```

Captured identities are sorted by type then instance and assigned ordinals
`1..255`. Because active PF2 ownership is unique, the mapping is collision-free
between simultaneous instances, deterministic across restart, and reversible
to live PF2 plus local ordinal for diagnostics. The runtime identity retains
the captured identity type. A shared captured identity used by both an
objective overlay and its structural/NPC record receives one runtime identity.

Mutable state is separate from both immutable bundles and version-2 bindings.
Version-1 sidecars under `mission-state/acg-runtime` contain:

- accepted quest identity;
- selected bundle ID and exact payload SHA-256;
- building identity and allocated live PF2;
- the complete captured-to-runtime identity map;
- per-door open and lock state;
- per-chest open state; and
- last-update timestamp.

The format uses invariant, ordinal `key=value` fields, indexed identity/door/
chest records, a SHA-256 over the canonical field block, atomic same-directory
temporary write plus flush/readback validation, and atomic replace. Unknown,
truncated, hash-invalid, or binding/bundle-mismatched state fails closed.

Startup loads Stage 2 bindings first, restores PF2 reservations, validates any
runtime sidecar, and deterministically rematerializes accepted/active
instances. A missing runtime sidecar is created from the persisted binding and
immutable bundle; no bundle or PF2 is rerolled. Door/chest state and runtime
identities survive restart.

Entry now resolves the exact owner/marker/key binding, validates its plan,
ensures materialization, transitions `Accepted` to `Active`, and teleports to
the bundle's captured entry point in `AllocatedLivePlayfield2`. PAF emits the
same binding's exact generator payload and building identity. Bound
`ClientConnected` and `CharInPlay` paths emit only the materialized instance
packets; movement and login cannot invoke `MissionInstanceDoorReplay` for a
bound PF2. Empty bound payloads fail closed instead of using a legacy payload,
and PF `1419349` is never substituted.

Every runtime interaction lookup requires the active binding owner, allocated
live PF2, and exact runtime identity. Doors toggle only their own persisted
open state and respect lock state. Chests preserve isolated open state without
inventing loot. Terminals, repair/static objectives, and NPC placeholders are
owned and acknowledged by the correct instance; type-specific objective,
combat, loot, completion, and reward effects remain deferred. Abandoned,
expired, cleanup-pending, cleaned, or invalid bindings remove only their own
runtime objects, identity maps, registry entries, send state, and mutable
sidecar. Stage 2 lifecycle remains the sole owner of PF2 release.

## Stage 4 exact objectives and durable completion

Each accepted generated mission now owns one version-1 objective record under
`mission-state/acg-objectives`. The immutable portion binds the accepted quest,
owner or explicit team, live PF2, bundle ID and payload hash, building,
captured objective slot and identity, deterministic runtime identity, template,
name, required interaction, issuing terminal, and required mission item or
machine template. Mutable state separately records lifecycle, the exact
mission-item inventory identity, frozen rewards, stable claim IDs, per-reward
grant state, packet-send state, exact artifact cleanup, and runtime/mission
cleanup.

The objective sidecar uses sorted invariant `key=value` fields, Base64 text
fields, a SHA-256 over the canonical field block, and same-directory temporary
write, flush, readback validation, and atomic replacement. Unknown versions,
truncated files, integrity failures, duplicate accepted identities, missing
bundles, payload/building/slot mismatches, or runtime identities outside the
bound PF2 fail closed. Stage 2 binding format version 2 and Stage 3 runtime
format version 1 are unchanged.

The five explicit contracts are:

- Kill: exact owner, accepted quest, live PF2, runtime target, captured slot,
  template/name contract, and first target-death observation.
- Find Person: exact runtime person `InfoRequest`; accepted QFU action version
  `16` and `QuestIdentity` flag `64`. Proximity alone does not complete it.
- Find Item: exact static runtime object pickup; accepted QFU action version
  `15`. The item identity is persisted before completion and the generated
  character-shaped legacy cube path is not used.
- Return Item: exact persisted inventory instance delivered to the exact
  issuing terminal; accepted QFU action version `8`. Template-only delivery is
  rejected.
- Repair: exact persisted repair-kit inventory instance used on the exact
  runtime machine in the bound PF2. The semantic captured relationship remains
  component template `100348` to machine template `100358`; another kit or
  another mission machine is rejected.

Kill and Repair accepted QFUs use their explicit version-16 structured
builders. Every builder emits the accepted quest identity, persisted building,
runtime objective, mission item/tool where applicable, issuing terminal where
applicable, and accepted offer title, description, QL, and reward fields.
Generated acceptance no longer mutates a captured Kill QFU by byte offset.

Completion advances through the following durable phases:

```text
ObjectiveVerified
CompletionStarted
RewardCalculationFrozen
RewardClaimStarted
CreditsGranted
XpGranted
ItemRewardGrantedOrNone
MissionArtifactsRemoved
Action59Sent
QuestDeleteSent
ObjectiveCleanupCompleted
MissionCleanupCompleted
```

Credit, XP, and item values are frozen before the first grant. Each has a
stable accepted-quest claim ID and `NotStarted`, `Pending`, `Granted`, or
`ExplicitNone` state. A retry after `Granted` never repeats that reward and a
later packet failure does not restart the reward set. The exact mission key,
Return Item instance, Repair component, objective registry/runtime state, and
accepted mission are cleaned independently; no template-only or newest-mission
fallback is used. PF2 release remains owned by the Stage 2 terminal lifecycle.

The remaining crash boundary is explicit: legacy character cash, XP, and
inventory persistence cannot atomically commit with the objective sidecar.
The journal therefore writes `Pending` before invoking an existing reward
owner and `Granted` afterward. A restart that finds `Pending` fails closed and
logs the accepted quest and sidecar for reconciliation instead of risking a
duplicate grant. This is durable server-controlled idempotency, not a
distributed transaction.

Captured completion ordering is preserved as server-controlled reward/artifact
work followed by action `59`, Quest Delete, objective cleanup, and mission
cleanup. `Action59Sent` and `QuestDeleteSent` mean that the server sent the
packet; the protocol evidence provides no client acknowledgement. Unsent
durable packet phases resume on reconnect without recalculating or repaying
rewards.

User-driven abandonment proves the exact accepted binding owner before sending
Quest Delete. It advances only that objective and binding, reuses the durable
exact-artifact remover for the bound key and mission item/tool, and never falls
back to a newest key, template-only inventory removal, character-wide token
cleanup, or character-wide Find Item cleanup. Unknown and authored quest
deletes remain outside this generated-terminal cleanup path.

Abandonment and expiry can win only before the durable reward-claim boundary.
Once reward claiming starts, completion owns the persisted race. Abandoned,
expired, cleanup-complete, and invalid objectives are excluded from restart
completion work, so cleanup cannot resume rewards. A binding cannot transition
to `Cleaned` or release its PF2 until spatial, operational, and materialized
runtime cleanup all report success and the objective journal durably records
both cleanup markers. Stale binding state versions are rejected, making the
binding transition the completion-versus-abandonment race owner instead of
allowing a later stale write to resurrect the loser. Failed cleanup remains
durable `CleanupPending` work for retry. Server shutdown preserves active or
in-progress records.

Live expiry, occupant evacuation, exact corpse retirement, and verified PF2
release are now implemented in the later live-expiry section below. The
inside-at-expiry policy remains provisional and must not be inferred from the
user-driven abandonment result.

Stage 2 currently persists generated missions as explicit solo ownership, so
the exact objective path rejects a team identity. The objective model and
validation preserve an explicit team identity when one exists, but durable
team reward distribution is not inferred from the current captures.

## Stage 5 operational NPC, corpse, container, and spatial state

Stage 5 uses a version-2 mutable sidecar under
`mission-state/acg-operational`. Each record binds the accepted quest, owner,
allocated live PF2, selected bundle and payload hash, and building identity to
indexed NPC and chest state. NPC records persist captured slot and identity,
deterministic runtime identity, captured position/rotation, template,
MonsterData, level, maximum health, current health, scale, optional head mesh,
name, objective role, combat/life state, instance-local corpse identity/state,
spawn generation, and cleanup state. Chest records persist captured/runtime
identity, explicit loot authority, open/exhausted state, transfer count, and
cleanup state.

The format uses sorted invariant `key=value` fields, Base64 text, SHA-256 over
the canonical field block, same-directory temporary write, strict readback,
and atomic replacement. Unknown, truncated, hash-invalid, duplicate, or
binding/bundle/building/PF2-mismatched records fail closed. Stage 2 binding
format version 2, Stage 3 runtime format version 1, Stage 4 objective format
version 1, and all Stage 1 payload hashes remain unchanged. Operational version
1 remains readable only as the legacy captured-difficulty format. Active
version-1 records are validated against their exact captured slots, atomically
migrated to deterministic mission-QL level/health, and retain proportional
current health plus life, combat, corpse, chest, and cleanup state. Invalid or
unwritable migrations fail closed.

The runtime owner is always:

```text
MissionAcgInstanceBinding
  -> allocated live PF2
  -> exact bundle NPC/chest slot
  -> Stage 3 deterministic runtime identity
  -> Stage 5 mutable state
```

Bound ACG PF2 values bypass the legacy `MissionInstanceSpawn` path, so that
path cannot add guessed NPCs, the character-shaped Mission Cube, global
newest-target registrations, or legacy random loot to an isolated instance.
Captured NPC `SimpleCharFullUpdate` replay is suppressed only after the Stage 5
owner is valid; real server `Character` objects are created with the same
restart-stable Stage 3 identities.

Capture-proven NPC facts are position, rotation, template, MonsterData, source
level, source health, scale, head mesh where present, name, textures, and
meshes. The existing production `BART` template is only the construction
shell. Exact captured identity, placement, appearance, and MonsterData fields
overwrite its visible state. Source level and health remain immutable catalog
provenance but are not replayed into an accepted mission of a different QL.
Live level and health reuse the pre-existing `MissionInstanceSpawn` QL policy,
seeded by the durable mission seed and captured slot so restart cannot reroll
them. The existing `MissionInstanceMobCombat` policy remains the production
owner for damage, weapon context, death action, and combat packets because the
five finalized mission captures do not contain a complete per-NPC attack
contract. Stage 5 adds no new combat or health formula.

Kill targets and ambient NPCs enter the normal attack, damage, aggro,
changed-stat, death, and corpse pipelines. A death is persisted before Stage 4
objective/reward hooks run; a persistence failure blocks completion. Only the
exact Kill runtime identity can complete its accepted mission, dead state is
not respawned after restart, and repeated death may only retry the idempotent
Stage 4 verification without repeating combat XP or reward claims. Find Person
uses the same captured NPC materialization but is passive
and combat-rejected; only its exact Stage 4 `InfoRequest` contract completes
it. Repair machines, static objectives, terminals, doors, and chests never
enter NPC combat registration.

Combat selection additionally validates owner, active lifecycle, allocated
PF2, exact runtime NPC identity, living state, and cleanup state. Runtime
identities remain collision-free between simultaneous PF2 instances. Existing
production combat registries are reused only after that instance boundary and
cannot select a mission target by template or mission type.

Corpse identity uses the exact dead NPC runtime instance with identity type
`Corpse`, making it deterministic, instance-isolated, and reversible for
diagnostics. A live corpse is created only when the existing production
MonsterData/CATMesh owner can build a proven visual. Corpse opening and loot
transfer require the exact accepted quest, solo owner, allocated PF2, runtime
NPC, corpse identity, active binding, durable `Available` state, finite
coordinates, and the existing melee-distance authority.

The five Stage 1 layout captures did not establish corpse contents. The
separately analyzed mission-trash capture `20260725-185432` does establish ten
corpse-open credit observations with an inclusive observed range of `21-87`
(currency amounts, not combat contribution or distance), including one
captured item observation. One item does not prove a pool or probability, so
generated-ACG corpses use only the captured credit range and keep item contents
explicitly unresolved-empty. The pre-existing legacy mission sparse/rare paths
remain separate and unchanged. Persistent dead/corpse identity survives
restart. A dead Kill target is eligible for restart reconciliation only when
the same accepted quest already has the durable Stage 4
`ObjectiveVerified` phase; dead operational state alone never synthesizes
credit. The narrow crash boundary after death persistence but before objective
verification therefore fails closed. The current corpse runtime does not
safely reconstruct an already-visible corpse after process loss, so it does
not fabricate that visual.

For a live credited Kill, the exact corpse is scheduled before completion
hooks. After rewards and completion packets are durably recorded, successful
runtime cleanup pauses only while the exact accepted-quest/PF2/runtime-NPC/
corpse lease is pending or available in the live playfield. Corpse access
remains owner-only during `CompletionStarted`. Exact corpse retirement queues
that one completion journal after corpse lifecycle traversal completes;
ambient corpse retirement cannot resume it. Missing-dead-NPC or failed
registration retires the same durable lease fail closed, preventing a stuck
PF2 without exposing a half-registered corpse. Grant phases are not replayed,
and the existing cleanup/PF2 release gates run unchanged. Abandonment and
expiry do not use this successful-completion lease exception.

The captures likewise do not prove chest inventories or transfers. Every
captured chest therefore has `UnresolvedEmpty` authority: isolated open and
exhausted state survives restart, the item count remains zero, and no generic
loot table or reroll runs. Stage 3 remains the packet/open-state owner while
Stage 5 mirrors durable container authority and cleanup.

All captured entry, exit, objective, dynel, and NPC coordinates are validated
as finite; duplicate NPC slots and missing objective runtime slots fail
closed. Door, chest, exit, and objective use require the same PF2/runtime
identity and the existing server melee-distance bound. Combat uses the
existing production range checks. No server room mesh, ACG collision graph,
line-of-sight result, or navigation topology is available from the opaque
payload. NPCs therefore receive no fabricated waypoint route and remain
stationary until the existing combat owner can engage them.

Startup restoration order is binding/PF2 reservation, Stage 3 identity and
interior state, Stage 4 objective/completion state, then Stage 5 mutable state.
Living NPCs rematerialize once, dead targets remain dead, opened/exhausted
chests remain consumed, and no bundle, PF2, identity, NPC attributes, corpse,
or loot is rerolled. Completion, abandonment, and expiry persist Stage 5
cleanup, remove only that PF2's NPC/combat/corpse/container ownership, delete
its operational sidecar, and leave PF2 release with the Stage 2 lifecycle.

## Stage 6 capture-backed interior spatial authority

Stage 6 adds one `MissionAcgSpatialRuntime` ownership layer:

```text
allocated live PF2
  -> active MissionAcgInstanceBinding
  -> exact immutable MissionAcgLayoutBundle
  -> exact instance runtime identity
  -> spatial decision
```

It never resolves by newest mission, mission type, captured PF2, building
identity alone, template, or cross-playfield proximity.

### Captured spatial envelope

Each selectable bundle derives an axis-aligned envelope from its immutable
captured interior spawn, exit, every extracted dynel, every NPC slot, and every
objective slot. Derivation requires at least three distinct finite captured
coordinates. Missing, null, NaN, infinite, or insufficient coordinates make
that bundle spatially non-operational without changing its evidence
selectability.

The envelope expands each minimum and maximum by exactly `2.0` coordinate
units. This is a bounded deterministic Rebirth tolerance for ordinary client
coordinate variance at a captured boundary. It is not a wall thickness,
interaction range, room polygon, floor, corridor, connectivity edge, or
navigation clearance. The envelope is rebuilt from the selected bundle and is
not persisted. All five selectable bundles derive finite envelopes; incomplete
shape `1441804` remains nonselectable and spatially non-operational.

An axis-aligned envelope can reject non-finite and obviously out-of-layout
positions. It cannot prove that a point between captured extrema is walkable.
No opaque `C79F` bytes are interpreted.

### Player movement and recovery

`CharDCMove` validates a mission player before `Controller.Move` accepts the
coordinate. The authority requires the exact owner, allocated PF2, active
lifecycle, finite coordinate, and membership in that bundle's envelope.
Rejected movement is broadcast at the last accepted exact-mission position, or
the captured interior spawn when no prior accepted position exists. It does not
disconnect the player.

The current server has no authenticated player-speed or packet-time movement
policy that can safely distinguish a fast legal move from an in-envelope
teleport. Stage 6 therefore does not invent a speed constant. The maximum
accepted delta is bounded by the complete derived envelope; production movement
semantics remain otherwise unchanged.

The last accepted position is kept in memory. A durable checkpoint is written
after `2.0` coordinate units or five seconds, and is always flushed before a
validated exit. A crash can therefore restore a position up to one checkpoint
old, but never a position from another accepted mission or PF2.

### Interaction, exit, and door authority

Doors, chests, mission terminals, static objectives, repair machines, Find
Person actors, and exits use the same central validation. Each use requires the
exact accepted binding, owner, allocated PF2, mapped runtime identity, captured
slot, active registration, finite player/target coordinates, envelope
membership, and the existing production melee interaction limit of `8.0`.
Objective contracts still add their Stage 4 item, terminal, component, machine,
template, and lifecycle requirements.

Door topology is not inferred. Stage 3 remains the durable open/closed-state
owner. Stage 6 adds only exact identity and range authority. Exit use resolves
the exact captured exit and exterior destination from the binding, flushes the
position checkpoint, and does not complete the mission.

### Combat, LOS, and NPC movement

Player attack start, special damage, repeating player damage, NPC aggro, NPC
combat ticks, and NPC damage require both participants to share the exact
active PF2. One participant must be the bound owner and the other an exact
operational mission NPC. Both coordinates must be finite and inside the same
envelope. Stale, cleaned, expired, cross-instance, cross-PF2, and passive Find
Person combat remains rejected. Existing production combat range remains
authoritative.

No collision geometry is registered for allocated generated-mission PF2s.
Stage 6 therefore distinguishes:

- range-and-ownership operations, which may proceed without claiming clear
  LOS; and
- operations that require authoritative geometry, which return explicit
  `UnresolvedGeometryUnavailable` and fail closed.

Distance is never described as clear LOS. PF127 collision data is not reused
for generated interiors.

The production chase system requires a supported navigation owner or otherwise
falls back to direct following. That fallback is unsafe inside opaque ACG
interiors. Generated mission NPCs therefore remain at their captured slots,
stop follow/pursuit state, and attack only when existing production range
permits. If an NPC is observed outside its envelope it is restored to its exact
captured slot before further combat. There is no random roaming, waypoint
inference, room traversal, or pathfinding claim.

### Durable spatial state and cleanup

Version-1 sidecars under `mission-state/acg-spatial` contain only:

- accepted quest, owner, allocated live PF2, bundle ID and payload hash;
- building identity;
- whether a last valid player position exists and that exact position;
- cleanup state and UTC update time; and
- SHA-256 over deterministic UTF-8 `key=value` fields.

Writes use same-directory temporary files, strict readback, and atomic
replacement. Unknown versions, truncation, malformed floats, integrity
mismatch, binding mismatch, bundle mismatch, building mismatch, PF2 mismatch,
or an out-of-envelope restored position fail closed. Stage 2 binding format 2,
Stage 3 runtime format 1, Stage 4 objective format 1, and Stage 5 operational
format 1 remain unchanged and readable; no migration rewrites those records.

Startup restores binding/PF2 ownership, Stage 3 materialization, Stage 4
objective state, Stage 5 operational state, then Stage 6 spatial state.
Completion, abandonment, expiry, and cleanup delete only the exact mission's
spatial registration and sidecar. The envelope is never serialized or rerolled.

## Live generated-mission expiry and verified cleanup

### Deadline authority and scheduler

The accepted `MissionAcgInstanceBinding.ExpiryUtc` is the sole deadline
authority. It is stored as an absolute round-trip UTC timestamp, restored
without recomputation, and never extended by entry, combat, interaction,
logout, zoning, or server activity. The deadline comparison is inclusive:
`nowUtc >= ExpiryUtc` is expired.

`MissionAcgExpiryRuntime` owns one process-wide, non-reentrant one-second timer.
Startup restores Stage 2 through Stage 6 durable state, loads and validates all
expiry journals, reconstructs a conservative journal for a binding already
marked expired, and processes overdue missions before normal reconnect
completion recovery. Shutdown stops the timer, drains active expiry work before
client/zone teardown, and preserves unfinished work.

### Version-1 expiry journal

Each generated mission uses one
`mission-state/acg-expiry/<accepted-quest>.expiry` record. Format version 1
snapshots the exact accepted quest, original offer, owner/team state, mission
key, exterior and issuing-terminal identities, bundle ID and payload hash,
building, allocated live PF2, accepted/expiry/detection/update times, status,
retry count, last failure, and immutable `RequiresOwnerReconciliation`.

Canonical invariant UTF-8 `key=value` fields are deterministically ordered,
protected by `RecordSha256`, written through a same-directory temporary file,
strictly read back, and atomically replaced. Unknown versions, truncation,
integrity failures, duplicate accepted quests, orphan journals, or any binding,
bundle, building, hash, owner, entrance, key, terminal, timestamp, or PF2
mismatch fail closed.

Cleanup checkpoints are monotonic and dependency-validated:
`ExpiryDetected`, `CleanupStarted`, `InteractionsBlocked`,
`OccupantsEvacuated`, `NpcsRemoved`, `ObjectivesRemoved`,
`ContainersRemoved`, `CorpsesRemoved`, `InventoryArtifactsRemoved`,
`RuntimeRegistrationsRemoved`, `OperationalStateFinalized`,
`ClientMissionRemoved`, `ObjectiveCorpseCleanupVerified`,
`BindingReleaseReady`, `Pf2ReleaseAttempted`, `Pf2ReleaseConfirmed`, and
`CleanupComplete`. Journal status is `InProgress`, `RetryPending`,
`TerminalFailure`, or `Complete`. Identical retry failures are persisted and
logged at most once per 30 seconds so an offline owner does not cause
one-second disk churn.

### Race ownership and immediate blocking

Expiry and completion share one accepted-quest claim boundary. Expiry may win
for `Reserved`, `Accepted`, `Active`, or `CompletionStarted` work before
`RewardClaimStarted`. Once `RewardClaimStarted` is durably owned, completion
continues and expiry cannot take over. The winning state is persisted; stale
writes cannot resurrect the loser.

Abandonment uses the same accepted-quest owner gate. A pre-deadline
abandonment claim blocks a stale expiry snapshot until `Abandoned` is durable;
an expiry claim or journal blocks abandonment. The handler refreshes the exact
binding and objective after claiming, and a failed pre-persistence claim is
released. Durable abandonment remains cleanup-resumable after its original
deadline without allowing expiry to become a second terminal owner.

Completion takes a short process-local transition lease under that same gate
before changing the binding from `Accepted` or `Active`. The lease spans the
durable binding and objective `CompletionStarted` writes and is always released
afterward. Abandonment and expiry cannot claim inside that interval; if either
claimed first, the completion transition fails closed. Once the lease is
released, the persisted lifecycle remains the authority. If a crash separates
the two writes, restart recognizes only the exact binding
`CompletionStarted`/objective `ObjectiveVerified` tuple, reacquires the same
lease before the deadline, skips the already-durable binding write, and
persists the missing objective transition.

As soon as expiry is claimed, the exact objective becomes `Expired` and all
new mission entry, objective interaction, item pickup, terminal/machine use,
door/chest use, target selection, attacks, damage, corpse access, and
mission-token progress fail closed. A completion-owned objective-corpse lease
is permitted only after the durable reward-claim boundary; a pre-claim
`CompletionStarted` corpse is not an expiry bypass.

### Occupants, exact cleanup, and client state

Connected occupants in the allocated PF2 are evacuated before release. The
private-server policy uses the binding's finite, non-mission exterior
destination plus the existing 12-unit outdoor marker standoff. If that exact
destination is unsafe, the existing side-specific hub is used. Fighting and
selected targets are cleared before teleport. This is a deterministic safety
policy, not confirmed official inside-at-expiry behavior; an official capture
is still required to replace it.

Cleanup is scoped by accepted quest, owner, allocated PF2, runtime identity,
and persisted artifact identity. It removes only that mission's NPC/combat
registrations, objective, doors/chests/terminals/machines, corpse leases,
container state, spatial/materialized state, process-local routing/token state,
exact mission key, and exact Return Item or Repair artifact. Inventory writes
are rollback-safe: a failed owner inventory persistence restores the removed
instance to its original slot. Accepted-sidecar removal uses a strict
type-and-instance match and refuses malformed input rather than treating it as
an empty list. Template-only deletion, newest-mission fallback, character-wide
token clearing, and another mission's same-template artifacts are not used.

The exact accepted sidecar is atomically removed only after durable cleanup.
The server sends the exact Quest Delete and records that send; it does not send
completion Action 59 and grants no credits, XP, or item reward. “Sent” is not
described as client-acknowledged because no acknowledgement exists.

If the owner is offline, server-owned runtime content may be removed, but exact
owner inventory/client reconciliation remains pending. The durable
`RequiresOwnerReconciliation` flag survives restart and the PF2 remains
reserved until the owner reconnects. Retryable absence or transient
persistence/teleport failure remains `RetryPending`; malformed identity,
ownership, or integrity state fails closed instead of releasing the PF2.
Process-local outdoor return stamps carry accepted-quest and allocated-live-PF2
ownership in addition to the exterior coordinate, so expiry cleanup for an
older same-marker mission cannot clear a newer mission's return route.

### PF2 release proof

The binding may become `Cleaned` and its allocator reservation may be released
only after all journal checkpoints are durable, no eligible occupant remains,
all Stage 3 through Stage 6 runtime owners report no residual state, exact
objective/corpse cleanup is verified, owner artifacts and client mission state
are reconciled, and allocator ownership still maps that live PF2 to the exact
accepted quest. Release is idempotent for the same accepted quest and rejects a
different owner. The journal records attempted release before the allocator
mutation and before the binding becomes `Cleaned`, verifies that both
reservation and reverse owner mapping are absent, then records
`Pf2ReleaseConfirmed` and `CleanupComplete`. A process-local allocator
tombstone prevents reuse between reservation removal and durable journal
confirmation. If restart occurs after the release mutation but before
confirmation, startup reconstructs that hold from the incomplete cleaned
journal before allocator access or new acceptance is exposed. Attempted intent
plus verified total allocator/index absence may then complete the journal; a
write failure retains the hold, and any reservation or live binding for that
PF2 fails closed. Server shutdown never releases active persistent bindings.

## Live validation repair: 2026-07-29

A level-4 neutral character at terminal PF `655` successfully rolled, accepted,
entered, found the exact static item, completed, received credits and the item
reward, and had the exact key removed. The accepted Find Item binding used live
PF2 `1441792`, the persisted `20260728-005042` bundle, and its exact building.

That run exposed two independent integration defects:

- The destination filter classified the neutral ICC terminal through character
  side, rejected all same-PF `655` markers because the marker zone is sided,
  and fell through to a 14-entry `nearRing` pool. The accepted offer therefore
  used PF `505`. Same-playfield markers now bypass marker-side exclusion before
  low-level same-zone selection; cross-playfield side filtering is unchanged.
- The selected bundle contains 18 captured NPC slots, but the content module
  excluded every bound ACG PF2 before `MissionAcgOperationalRuntime` could
  create server NPCs. Stage 3 then correctly suppressed all 18 raw captured NPC
  packets, producing the observed `59` objects / `41` packets and an empty
  interior. Bound mission PF2 values now enter the Stage 5 spawn hook; the
  existing bound/unbound branch still prevents legacy NPC generation.

The source capture's first Find Item NPC is level 38 with 1221 health. Those
source values are not safe live values for the accepted QL2 mission. The
existing mission QL scaling owner now supplies deterministic live level/health,
while captured position, identity, appearance, name, template, and MonsterData
remain unchanged. Operational format version 2 records those live values; an
exact atomic version-1 migration prevents active pre-repair missions from
becoming unenterable or retaining unsafe captured difficulty. A post-repair
live roll/entry/combat check remains pending.

Thirty-one focused Stage 6 tests cover deterministic bounds, finite and
insufficient evidence, tolerance, layout isolation, explicit LOS policy,
versioned persistence, SHA-256 rejection, exact identity/PF2 restoration,
cleanup state, shared-PF exclusion, and source-level integration guardrails for
movement, interaction, combat, stationary pursuit, startup, entry, and exit.

The exact-abandonment pass adds lifecycle-policy and production-wiring
regressions to the Stage 4 objective suite: `16/16` focused tests and the
`164/164` mission-filtered suite pass, together with the isolated Debug
AORebirth.Core and ZoneEngine build.

## Generated-mission Kill/corpse offline repair: 2026-07-29

The prior generated-ACG fallback used
`20 + (Math.Abs(salt) % 68)`. It therefore emitted `20-87`, and
`Math.Abs(int.MinValue)` could throw after signed multiplication had already
wrapped. Sparse captured-item selection repeated the same arithmetic hazard.

`MissionAcgCorpsePolicy` now owns the inclusive `21-87` corpse-credit range.
It validates an exact reversible runtime NPC identity and allocatable live PF2,
widens multiplication before retaining the established low 32-bit identity
mix, rejects the historical `int.MinValue` absolute-value case, and uses a
bounded modulo over 67 values. Normal legacy salt mapping is preserved; only
the invalid overflow edge fails closed. Non-finite, negative, or out-of-range
corpse distances fail closed.

Generated corpses are registered `OwnerOnly` with their accepted quest and
owner. Open, item transfer, item deletion, delayed credit scheduling, and
credit award all revalidate the complete durable ownership chain. Exact
dead-NPC routing replaces newest-corpse selection for generated actors.
Duplicate operational death callbacks suppress combat XP and corpse creation
while allowing the exact idempotent Stage 4 target-death contract to reconcile.
A restored dead Kill target is reconciled before mission re-entry. Failed
durable corpse availability is not exposed to the client and remains durably
`Pending` rather than being marked cleaned. Cleanup retires only corpses whose
runtime NPC identities belong to that accepted mission. Ordinary and authored
corpse behavior remains on the existing production path.

Focused operational tests cover the credit boundaries, deterministic range,
invalid/extreme identities, widened hashing, non-finite distance, exact
owner/quest/PF2/NPC/corpse matching, lifecycle rejection, restart identity,
cleanup wiring, and ordinary-path preservation. The private-client smoke test
(kill trash, open corpse, transfer credits once, and complete the mission)
remains deferred; no additional official capture is required.

## Deferred Stage 7 behavior

The following work remains intentionally deferred:

- obtain directly supported generated-interior collision or room geometry
  before enabling geometry-backed LOS or navigation;
- add an authenticated player-speed/timing authority before rejecting legal
  in-envelope movement by a guessed speed constant;
- reconcile a reward journal left `Pending` across the non-atomic legacy
  character-persistence boundary with operator evidence;
- obtain direct chest inventory transfer captures and any additional corpse
  item outcomes before enabling content beyond the existing proven observations;
- add safe restart reconstruction for an already-visible production corpse;
- emit additional door, chest, machine, or objective state packets only when
  direct capture proves their values and ordering;
- capture official inside-at-expiry behavior before replacing the documented
  provisional exterior-or-side-hub evacuation policy;
- implement durable team-owned generated missions and reward distribution
  after stable team persistence exists; and
- perform private-client end-to-end lifecycle validation.

Procedural ACG generation, speculative `C79F` topology, payload mutation,
reward-formula changes, and database-schema changes remain out of scope.
