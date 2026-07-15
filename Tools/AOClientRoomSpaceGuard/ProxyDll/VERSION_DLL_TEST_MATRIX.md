# version.dll validation matrix

Status: proposed reproducible validation plan. Codex does not launch AO; Mike
performs in-game actions and Codex analyzes completed logs/dumps/results.

## Required result vocabulary

Every run records exactly one or more of:

```text
crash prevented
bad object rejected
submission skipped
batch skipped and cleaned
frame skipped
renderer recovered
resource rejected
resource quarantined
false positive
visual corruption
performance regression
new crash family
no fault reproduced
```

“No crash” alone is not a pass. A run must also record visuals, logs, FPS, and
whether a mitigation actually fired.

## Build and configuration axes

| Axis | Required values |
| --- | --- |
| client | C/new graphics; D/old graphics |
| policy | forward-only; diagnostic-only; exact guards; each candidate feature alone; intended combined policy |
| renderer selector | AO original selection; plain HAL normalization, independently |
| proxy | absent baseline; installed diagnostic; installed mitigation |
| session | clean launch; relaunch; zone transition; long soak |

Do not compare an absent-proxy baseline only with a combined policy. Every
behavioral feature needs an isolated A/B run.

## Hardware and driver matrix

| GPU class | Minimum role | Driver evidence | Required clients | Priority |
| --- | --- | --- | --- | ---: |
| older GTX card that did not reproduce historically | compatibility/control | record exact installed driver | C and D | high |
| GTX 1660 Ti | Turing control | record exact installed driver | C and D | high |
| RTX 20 series, if available | Turing RTX | record exact installed driver | C and D | medium |
| RTX 30 series | Ampere | record exact installed driver | C and D | high |
| RTX 40 series, including RTX 4070 | Ada/reproduction target | NV-A `32.0.15.9186` and NV-B `32.0.16.1074` where installable | C and D | critical |
| Intel or AMD GPU, if available | non-NVIDIA control | record exact user-mode driver | C and D | medium |

For every machine record:

```text
GPU model and PCI id
Windows build
NVIDIA package and user-mode nvd3dum version
nvd3dum timestamp/size/checksum/hash
AO client path/profile and N3 hash
version.dll hash and feature policy
display mode/resolution/refresh
VSync/frame-limiter state, if any, and measurement method
```

Do not claim a driver guard works on a new driver merely because the reported
RVA is similar.

## Scenario matrix

| ID | Scenario | Minimum actions | Primary families | Required observations |
| --- | --- | --- | --- | --- |
| S01 | clean login | launch, select character, enter world twice | N3 login/Vehicle, RoomSpace | readiness, exact hook policy, login success, no `N3+0x15040` |
| S02 | repeated zoning | 20 transitions including prior bad location | RoomSpace, N3 | zone success, position/room result, crashes/dumps |
| S03 | old-client Subway | traverse repeated crash locations for 30 minutes | randy, GUI, NVIDIA | hook hits, skipped work, visuals, FPS, crash signature |
| S04 | crowded area | rotate/move through dense players/NPCs for 30 minutes | draw submission, object dispatch | rejection signatures, pop-in, missing geometry, FPS |
| S05 | GUI-heavy | inventory, character, skills, map, chat, inspect, resize/reopen | rectangle, tree, GUI VB | empty rectangles, missing panels, tree misses, batch cleanup |
| S06 | camera/geometry | continuous camera rotation/zoom and movement | coordinate-as-code, geometry traversal | invalid targets, geometry validation, visual corruption |
| S07 | texture churn | open inventory/inspect while moving through texture-rich area | randy color/material, resource | texture/material failures, black fallback, resource identity |
| S08 | alt-tab/device loss | windowed/fullscreen alt-tab cycles, minimize/restore | deferred flush, driver state | device status, lock/batch cleanup, recovery |
| S09 | vehicle/login | reproduce vehicle or login initialization path repeatedly | N3 `+0x15040`, Vehicle C++ | feature isolation, thrown exception class, object evidence |
| S10 | resource load | long-running zoning/texture/resource churn | BinaryStream, ResourceManager, heap | stream fields, resource IDs, temporal links |
| S11 | long idle | stand in the previous spontaneous-crash location for 2 hours | lifecycle/deferred renderer | crash/hit timestamps, resource churn, FPS |
| S12 | combat effects | normal attacks, nanos, particles, corpses and loot in dense area | renderer/resource | object rejection, missing effects, heap/stream events |
| S13 | client concurrency | repeat S03/S05 with one, two, then normal multi-client load | logging/thread/install races | per-PID manifest, FPS/frame time, independent counters/dumps |

## Performance protocol

The known control result is approximately 100 FPS with D-client
`version.dll` absent and approximately 20 FPS with the earlier hot-path query
build. Performance is therefore a release gate.

For each A/B pair:

1. same client, character, location, camera, resolution, and policy except the
   one tested feature;
2. two-minute warm-up;
3. five-minute sample;
4. record median and 1% frame time, average FPS, and frame-time spikes;
5. repeat three times in alternating order;
6. preserve log counters to prove whether a recovery fired.

Acceptance for an exact guard that does not fire: no material FPS or frame-time
regression versus diagnostic-only. Any repeatable loss greater than 5% is a
blocker pending explicit review. Hot paths must contain no normal
file I/O, `VirtualQuery`, module enumeration, or unbounded geometry scans.

## Family-specific validation

### Rectangle

- deterministic offline context tests for both exact fault instructions;
- repeat the former crash location/UI sequence;
- confirm no normal-path proxy call/query;
- confirm recovered rectangles are empty and do not poison later GUI draws;
- verify x87 state across repeated synthetic recoveries.

### Randy color/state

- one test per exact RVA and one near-miss register/address case;
- verify expected neutral black/missing sample or one-entry skip;
- ensure report image RVA `+0x25118` still fails closed;
- inspect texture/material artifacts after a hit.

### NVIDIA draw and deferred flush

- exact driver identity required for positive recovery test;
- include NV-A `32.0.15.9186` and NV-B `32.0.16.1074`; NV-B remains
  pass-through until its cleanup contract is proven;
- auxiliary NV-A `+0x170C4C6` is diagnostic/pass-through only; its offline
  exact and near-miss filters must prove no existing policy swallows it;
- other driver identity must continue normal search in the offline filter test;
- exercise old Subway/crowded/GUI/device-loss paths;
- record D3D result, skipped submission count, later device behavior, and
  visual integrity;
- distinguish draw skip from GUI batch cleanup.

### GUI null VB

- offline context matrix changes one predicate at a time;
- static and heap index paths;
- verify conditional VB cleanup, index free ownership, material reset, state
  reset, and fail-closed null GetVB;
- live GUI soak after any contained hit.

### GUI tree

- low key `0x8` maps to native sentinel result;
- normal high readable key matches original function byte-for-byte result;
- invalid tree/output fails closed;
- no claim for high unreadable keys.

### Invalid indirect target

- no live fault injection until a typed invoke shim exists;
- offline tests must prove initial target transfer using exact ESP/return;
- test target `0/2/5/8`, non-executable coordinate page, per-site approved
  target entry with matching ABI, registered trampoline, other code in the same
  approved module, and unknown executable mapping;
- unknown post-entry AV continues search.

### Graphics COM identity and closure

Isolated tests precede any AO deployment:

- stable `QueryInterface(IID_IUnknown)` identity across every used interface;
- successful/failed `QueryInterface`, `AddRef`, and `Release` counts including
  concurrent lookup/release and interface tear-offs;
- every used factory/create/lookup/getter method returns a registered wrapper;
- no raw underlying pointer escapes through any supported method;
- device generation invalidates every stale device-owned wrapper;
- teardown cannot race lookup, callback, or final release;
- C/new D3D9 and DirectDraw paths and both D/old DirectDraw origins are tested
  independently and together.

Any uncovered IID or raw-return path fails the complete proxy release gate.

### Frame transaction and device recovery

- prove exact frame-begin and Flip/Present owners for each client;
- inject failure at validated-unsubmitted, optional compatibility-queued,
  submitted-synchronous, driver-accepted, and presented transitions;
- prove only unsubmitted/locally queued work is described as rolled back;
- verify exact renderer-thread and unwind/FPU/SSE/nonvolatile state gates;
- prove device poison invalidates the generation and prevents further use;
- prove complete destroy/recreate/resource restore/first Present, or require a
  controlled restart;
- test last-good-frame only with a compatibility-owned retained backbuffer.

### Gamecode fixed-array deserialization

Diagnostic phase only:

- verify the four known dumps reproduce ESI as the caller output address at
  `BinaryStream+0x1B1D`;
- record Gamecode object, stream, decoded count, destination range, loop index,
  enclosing caller/message, and thread at `Gamecode+0x7A919`;
- test valid counts 0, 1, and 30 plus 31, truncated payload, byte-order-wrong,
  and very large counts;
- prove the diagnostic does not change parsing or publication;
- confirm whether paired E24/E25 and E29/E30 share PID/time identity.

Behavioral tests begin only after whole-object failure semantics are proven.
Verify rejection occurs before the first entry write, consumes/discards the
entire malformed object without desynchronizing the next decode, publishes no
partial state, and releases ownership exactly once. Never validate a clamp-and-
continue repair. BinaryStream capacity/growth tests are out of scope for this
family.

### ResourceManager worker

- first test whether the upstream Gamecode repair prevents the paired E29
  notifier failure;
- preserve the paired-address causal qualification unless common PID/time is
  confirmed;
- independently test construction/destruction/clear sequence, native failure
  callback, exact AddRef/Release ownership, queue-lock scope, publication,
  waiter notification/retry, cancellation race, and worker survival;
- never validate by returning from the faulting notifier instruction or
  skipping notification.

### N3 login/vehicle

- isolate RoomSpace off/on while retaining diagnostics;
- old and new profile separately;
- 20 clean logins and relevant vehicle transitions per configuration;
- full dump/module manifest for any recurrence;
- do not suppress `N3+0x15040` or C++ exceptions.

### Logging and handler concurrency

- force bounded counter saturation and worker backlog offline;
- prove overflow increments a drop count without blocking or allocating;
- race simultaneous rectangle/randy/resource evidence on separate threads;
- trigger an unknown exception while a summary write is active and prove the
  dump path does not deadlock on the logging lock;
- test handler order with AOSharp absent first; label any AOSharp compatibility
  run separately from official-client results.

## Soak plan

Promotion stages:

1. offline byte/ABI/context tests;
2. 30-minute single-feature manual reproduction;
3. two-hour render-heavy soak;
4. four-hour zoning/resource soak;
5. combined-policy two-hour soak;
6. four-hour mixed render/resource soak;
7. overnight soak after any experimental renderer recovery is enabled;
8. at least three clean launches on the next day.

A soak restarts from stage 1 after an ABI, cleanup, policy, or hook-site change.
Documentation/log-format-only changes do not reset hardware soak evidence.

## Run record template

| Field | Value |
| --- | --- |
| run id | |
| date/time/timezone | |
| machine/GPU/driver | |
| Windows build | |
| client/profile/path/hash | |
| proxy hash/policy/features | |
| scenario and duration | |
| reproduction count | |
| FPS average/1% low/spikes | |
| guard signatures/counts | |
| result vocabulary | |
| visual/resource side effects | |
| dump/log paths | |
| pass/fail and reason | |

## Release gates

- all emitted-x86/ABI tests pass;
- all patch transactions and rollback fault-injection tests pass;
- diagnostic kill switch works;
- unknown exceptions are not swallowed;
- no current exact-guard regression on both clients;
- no unexplained visual corruption;
- no meaningful performance regression;
- exact NVIDIA positives only on approved driver identity;
- no graphics proxy deployment until the used IID/method graph is complete and
  exhaustive tests prove no raw interface escape or identity/refcount drift;
- no frame recovery until exact frame/Present ownership, transition state,
  unwind, and mandatory Commit-tail preservation pass;
- no poisoned-device continuation until reset/recreate/rebind and first/subsequent
  Present pass; otherwise restart is required;
- no Gamecode behavioral repair until whole-object reject/consume/ref semantics
  pass without stream desynchronization;
- official live and AORebirth/private results remain separate evidence lanes;
- single-client and multi-client runs pass with AOSharp absent; any AOSharp
  compatibility run is labeled separately;
- at least one older GTX control and one RTX 40 reproduction machine pass;
- combined old-client policy passes Subway, GUI, zoning, and soak;
- new-client helper mitigation remains disabled until its cleanup/dispatch proof
  is complete.
