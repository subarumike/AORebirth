# version.dll implementation plan

Status: prioritized plan produced from the 2026-07-15 investigation. Except for
the existing dirty worktree, no behavioral step below was implemented by this
planning task.

## Baseline

- Starting branch: `master`
- Starting commit: `8bb22776316c4d04145eaffc20d9d9efffcc6b38`
- Upstream: `origin/master`
- Starting divergence: `0 behind / 0 ahead`
- Working tree: dirty before this task. Existing Subway/server/capture changes
  and existing proxy changes in `README.md`, `PACKAGE-README.txt`,
  `src/gui_rect_fix.cpp`, and `src/randy_color_fix.cpp` are preserved.

## Implementation authorization order

The proof package supersedes any reading of the work-package numbers as
permission to build a whole renderer proxy. The mandatory dependency order is:

1. prove the true frame and Present owners for C/new and D/old;
2. inventory every created, queried, returned, retained, and released graphics
   interface/IID on both graphics paths;
3. build and unit-test the COM identity/lifetime registry in isolation;
4. proxy every used interface-returning method and prove that no raw interface
   can escape;
5. add typed pre-submit validation;
6. add a frame model only around compatibility-owned queued state;
7. prove device generation poison/destroy/recreate, or require restart;
8. repair the proven Gamecode count-before-loop bug only at a whole-object
   rejection/consumption boundary;
9. prove ResourceManager request/publication/waiter ownership before changing
   its worker path;
10. retain exact current guards until upstream prevention is demonstrated;
11. remove driver-RVA hooks only after the complete proxy prevents their exact
    reproductions in the hardware/driver soak matrix.

Current authorization is static proof, behavior-neutral instrumentation, and
isolated unit-test foundations only. A partial graphics proxy, catch-all SEH,
or guessed device reset is not authorized.

## Work packages

### P0 — Make current behavior auditable and fail-safe

#### P0.1 Feature policy and diagnostic kill switch

Goal: disable every behavioral mitigation while preserving forwarding, module
inventory, logs, and dumps.

Expected files:

```text
src/compatibility_policy.h       new
src/compatibility_policy.cpp     new
src/dllmain.cpp
Build-Package.cmd
README.md
PACKAGE-README.txt
```

Acceptance:

- `forward-only` starts no compatibility worker and performs no logging,
  dumping, hashing, or patching, providing a true zero-overhead A/B mode;
- malformed/missing policy fails to documented default;
- `diagnostic` installs no code hooks and no recovery VEH;
- dump handler and export forwarding remain active;
- one startup line lists policy and enabled feature IDs;
- N3 profile identification no longer depends on enabling RoomSpace;
- RoomSpace, renderer selector, rectangle, randy data guards, draw preflight,
  GUI batch, GUI tree, and new GUI helper are independently selectable;
- the coordinator waits for each enabled feature's modules and reports that
  feature ready/disabled independently; a late GUI/Utils/randy load cannot be
  mislabeled as whole-proxy success;
- RoomSpace defaults off for the `N3+0x15040` causality baseline;
- offline tests prove each feature gate independently.

Rollback: remove policy files and the coordinator calls; no client byte changes
occur unless a feature is explicitly enabled.

#### P0.2 Shared transactional hook installer

Goal: make every multi-site code edit use the already-proven RoomSpace/old
renderer transaction properties.

Expected files:

```text
src/hook_transaction.h           new
src/hook_transaction.cpp         new
src/roomspace_fix.cpp
src/gui_rect_fix.cpp
src/randy_color_fix.cpp
src/self_test.cpp
Build-Package.cmd
```

Required tests:

- whole-instruction byte verification;
- thread enumeration/suspension failure at every stage;
- IP-inside-edit rejection;
- page-protection failure;
- cache-flush failure;
- second-site write failure;
- complete verified rollback;
- fail-fast when rollback cannot be proven.

Acceptance: no path can return ordinary failure with one of a multi-site hook's
calls still redirected.

#### P0.3 Quarantine or repair the new-client GUI helper hook

Current hook sites:

```text
GUI.dll +0x14CC5F -> GUI.dll +0x14CA77
GUI.dll +0x157234 -> GUI.dll +0x14CA77
```

Current overwritten instruction at each site: one five-byte `E8 rel32` call.
Calling convention: inferred `__thiscall`, six stack DWORD arguments,
callee stack cleanup `ret 0x18`.

Proven audit problems:

1. the two writes do not use thread suspension;
2. failure after writing does not restore both original calls;
3. the filter accepts any helper AV whose EIP is non-executable, even after
   unknown partial helper state changes;
4. no renderer cleanup/postcondition is proven for the broad case.

Initial production action: feature defaults **off** until one of these is
proven:

- a naked invoke shim proves an initial invalid target before target code runs;
- a whole-helper cleanup contract proves safe unwind after partial execution.

Do not merely narrow by “coordinate-looking” EIP. That is diagnostic evidence,
not a cleanup proof.

#### P0.4 Remove synchronous work from recovery paths

Goal: no VEH/SEH recovery takes `LogLock`, allocates, or performs file I/O.
Retain fail-closed readable/writable mapping predicates until an equivalent
prevalidated catalog or bounded probe is proven; remove only hot normal-path
queries and diagnostic work from recovery.

Expected files:

```text
src/evidence_logger.h/.cpp        new
src/logging.cpp
src/gui_rect_fix.cpp
src/randy_color_fix.cpp
src/crash_dump.cpp
```

Implement fixed per-family atomic counters and bounded POD context snapshots.
A low-priority worker writes first-hit and periodic/power-of-two summaries.
Unknown exceptions still reach the diagnostic dump/filter chain. Acceptance is
zero normal-path/recovery-path file calls, no unbounded queue, and a visible
drop counter rather than blocking.

#### P0.5 Correct build and documentation claims

Expected files:

```text
Build-Package.cmd
README.md
PACKAGE-README.txt
src/self_test.cpp
```

Move compiler CFG instrumentation to the correct compiler option position and
verify emitted guarded indirect calls, or remove the unsupported CFG claim and
header expectation. Correct stale statements that the rectangle IAT is patched,
that high unreadable GUI-tree keys are guarded, and that current self-tests
cover GUI/randy ABI. Acceptance is source, package text, PE metadata,
disassembly, and tests describing the same artifact. Rollback is the prior
build flag plus removal of the incorrect claim; no client hook bytes change.

### P1 — Preserve and test current exact guards

#### P1.1 RoomSpace wrapper contract tests

Sites by approved N3 profile:

```text
new: +0x157BC, +0x16144, +0x168E2, +0x168F6, +0x16F98
old: +0x13F2E, +0x148B6, +0x15054, +0x15068, +0x1570A
```

Add emitted-x86 checks for 86-byte wrapper, `ECX/ESI`, callee-saved registers,
stack `ret 8`, both failure branches, and no FPU/SSE mutation.

#### P1.2 Rectangle exception-only guard tests

Site: original target `Utils.dll +0x82E6`; exact supported faults
`+0x82EC` and `+0x82F1`. There is no normal-path code patch.

Test:

- first input fault before x87 push;
- second input fault after `fld`, including `fstp` balance;
- writable 16-byte output requirement;
- invalid frame/output continues search;
- normal IAT remains original target;
- repeated recovery leaves x87 depth/control unchanged.

#### P1.3 Old-render exact VEH tests

Sites:

```text
randy31 +0x21A94
randy31 +0x25118
randy31 +0x2511A
randy31 +0x6C3A1
randy31 +0x6C476
randy31 +0x6C51D
```

Test every positive predicate and every one-field near miss. Keep report
`+0x24118` / image `+0x25118` as a separate whole-vector policy from the
one-entry `+0x2511A` policy; never share their resume address or context rewrite.

#### P1.4 Old draw and GUI-batch wrapper tests

Sites:

```text
randy31 +0x219B4  FF 91 80 00 00 00 -> E8 rel32 90
GUI.dll +0x152E49 E8 C9 DF FF FF    -> E8 rel32
```

Verify:

- draw wrapper ABI is eight stack DWORDs and `ret 0x20`;
- successful draw runs no `VirtualQuery`, logging, or file I/O;
- phase stores precede input probes, target resolve, and driver entry;
- driver phase catches only initial target execute failure or the two exact
  NVIDIA signatures;
- unknown driver exceptions continue search;
- GUI batch cleanup order and static/heap index ownership;
- null `GetVB(0x144)` recovery fails noncontinuably;
- material/state cleanup receives the proven viewport/blob.

#### P1.5 GUI tree contract tests

Site: `GUI.dll +0x4F2EF`, first five-byte prologue
`55 8B EC 51 56` -> `E9 rel32`.

Verify trampoline replay, `thiscall`/`ret 8`, native sentinel result for low
key, and fail-closed behavior for invalid tree/output. Correct documentation to
state that only low keys are diverted in the optimized path.

### P2 — AO-side render provenance diagnostics

Goal: identify the producer before state enters D3D7/NVIDIA.

Do not add a second behavioral guard yet. Add cold, rate-limited diagnostic
records to the existing proven boundaries:

```text
randy31 +0x219B4 draw dispatch
GUI.dll +0x152E49 GUI batch call
GUI.dll +0x4F2EF tree entry, invalid-only
the exact old-render state/color guard sites, fault-only
```

Expected files:

```text
src/module_catalog.h/.cpp         new
src/evidence_logger.h/.cpp        new
src/render_evidence.h/.cpp        new
src/randy_color_fix.cpp
src/logging.cpp
Build-Package.cmd
```

Capture only fields whose offsets are already proven. Add stable signature
deduplication and process summary. No normal-frame file flush.

Acceptance:

- the same-scene observed no-proxy baseline of about 100 FPS is preserved
  within measurement noise; no frame-rate limit is added or changed;
- exact rejected/fault event contains producer/consumer RVA, object, vtable,
  D3D target, proven counts/pointers, action, and signature;
- no unproven pointer is dereferenced for logging.

### P2.5 — Graphics API closure and COM foundation

Proven creation origins are C/new `Cheetah -> Direct3DCreate9`, C/new and D/old
`randy31 -> DirectDrawCreateEx`, and D/old
`DisplaySystem -> DirectDrawCreate`. C/new has dual-path exposure, so neither a
D3D9-only nor a one-factory proxy is complete. The sole static Cheetah
Direct3DCreate9 site creates a REF device; the production hardware-device
root/dynamic path must be found before runtime proxy work.

Proof sequence:

1. inventory every accepted IID and every interface returned by factory,
   creation, `QueryInterface`, attached/parent lookup, and resource getter;
2. identify every AO storage/retention and release site;
3. prove COM aggregation/identity behavior used by each client;
4. define one thread-safe proxy identity per underlying COM identity;
5. define exact `QueryInterface`/`AddRef`/`Release`, teardown, generation, and
   stale-wrapper semantics;
6. unit-test the registry and interface wrappers without AO;
7. prove that no method can return an unregistered raw interface.

Only then may the factory interception and complete used-interface set be
enabled together. Partial runtime deployment is forbidden.

### P3 — Prove a central AO render-object/dispatch boundary

Static and dump work:

1. identify the actual AO indirect dispatch that precedes EIP `0/2/5/8` and
   coordinate-as-code events;
2. prove instruction boundaries and full call ABI;
3. identify object/vtable field and approved target image set;
4. prove whether failure occurs before any callee instruction;
5. prove caller neutral result and object/batch cleanup.

Only then propose a narrow invoke shim. Candidate validation is target low/null,
target executable section/registered trampoline, and target ownership. Do not
hook every indirect call or every virtual dispatch.

Acceptance before behavior:

- at least two matching dumps/captures identify the same AO-side site; or
- one full dump plus static bytes and a deterministic reproduction proves it;
- exact saved return/ESP and callee-saved register behavior are tested;
- rejected-call cleanup/postcondition is defined.

### P4 — Replace downstream NVIDIA dependence where possible

Use P2/P3 evidence to validate AO objects at `randy31 +0x219B4` or an earlier
typed construction boundary. Validate only proven fields and allocated spans.

Keep current driver guards as experimental level-4, version-specific fallbacks until the AO-side
validator prevents every reproduced exact signature across a soak test. Do not
translate the current RVAs to a different driver by offset similarity.

Post-driver NVIDIA recovery and GUI-batch recovery default off independently
until cleanup/postcondition testing passes. AO-side DrawIndexed preflight is a
separate Level-2 feature and must not imply that driver recovery is enabled.

Plain-HAL normalization (`randy31 +0x43B99`) remains independently gated. Test
it separately because it changes renderer selection rather than containing an
exception.

### P5 — Gamecode waypoint deserialization repair

BinaryStream is not the root cause of this family. The root is Gamecode's
fixed-array waypoint deserialization. Validate this Gamecode corruption repair
before adding any downstream allocator or ResourceManager behavior. Renderer
virtualization remains independently necessary for the separately proven
GUI/randy/NVIDIA defects and is not replaced by P5.

Target family: crash-report logical `BinaryStream.dll+0xB1D`, now proven to be
`BinaryStream::operator>>(float*)` actual `+0x1B14..+0x1B37` initializing a
caller-supplied output pointer.

The object is `SimpleCharFullUpdateIIR_t`; flag `0x00010000` is HasWaypoints.
C/new `+0x7A913` and D/old `+0x79641` read a signed int32 count into
`object+0x19C`, then write three floats per 12-byte record at
`object+0x1A0`. The array has exactly 30 records. The post-loop comparison with
30 only controls zero-fill and never rejects an excessive count.

The observed N3 failure contract is proven for both profiles: deserializer
failure `1` makes Construct delete the partial object and return null;
AddNetworkMessage destroys the temporary stream and abandons the remaining
buffer before publication. The selected repair design is a five-byte direct
detour at C `+0x7A91D` / D `+0x7964B`, preserving counts `<=30` and returning
through the native failure tail for positive counts above 30. Exact caller
return must match C `N3+0xB735` / D `N3+0x9C18` before consume-nothing
rejection is allowed.

Remaining implementation sequence:

1. add isolated emitted-x86 tests for both exact Gamecode/N3 profiles;
2. prove caller-return near misses pass through and all accepted branches are
   native-equivalent;
3. integrate an all-or-nothing patch transaction with thread-IP exclusion and
   verified rollback;
4. add bounded deferred diagnostics, diagnostic-only mode, feature flag, and
   kill switch without hook-path I/O or lookup;
5. run the malformed, short-read, C/D, publication, retry, performance, and soak
   matrix in `GAMECODE_OVERFLOW_VALIDATION.md`;
6. implement production behavior only after every gate passes.

Expected future files, after proof:

```text
src/gamecode_deserialization.h/.cpp  new
src/dllmain.cpp
Build-Package.cmd
```

Current outcome is **B: repair design proven, implementation blocked**. Do not
clamp the count and continue, change BinaryStream capacity/growth,
unconditionally reallocate, use guard-page recovery, or “write and continue.”
Any independent BinaryStream capacity investigation requires a separate proven
crash family.

### P6 — ResourceManager publication/lifetime proof

The exact notifier at `ResourceManager+0x3D7B..+0x3DB4` dereferences a null
request/list sentinel at `+0x3D84`. Its worker caller `+0x3F97`, call
`+0x40F6`, has already removed work under lock, unlocked, and stored/AddRef'd
the resolved resource into the request before notification. This is
request-local assignment, not proven global cache publication. Construction
proves a self-linked 0x18-byte sentinel; no protecting request lock/ref
increment is visible across notification.

Prove the request destruction/clear site, reference owner, cache publication,
waiter notification/failure result, retry rule, and exception policy. A
lifetime race is plausible, not proven. Skipping the notification is forbidden
because it can strand waiters. Correlate this with P5 only if the same
allocation/resource identity and temporal order are demonstrated; otherwise
keep the families independent. Use PageHeap/Application Verifier only in a
controlled lab run to find a first corruptor; never catch allocator faults in
production.

### P7 — N3 login/vehicle initialization

Collect a full dump with module/policy manifest and identify the object present
around the VERSION module frames before report logical `N3 +0x14040`
(image RVA `N3 +0x15040`). This is only `0x14` bytes before old-profile
RoomSpace callsite `N3 +0x15054`; reproduce with RoomSpace independently off
and on. Prove whether
`0x40000000` comes from the caller, unidentified VERSION-module code, or a stale
object field. No
skip/retry is planned until the initialization owner and failure contract are
known.

### P8 — Renderer frame and device recovery

First prove the frame-begin and Flip/Present owners independently for C/new and
D/old. `DisplaySystem_t::Commit` is central at C/new `+0x796F5` and D/old
`+0x789BB`, but its virtual dispatch has not been tied to the exact legacy Flip
methods and Cheetah's exact Device9 Present remains unresolved. Commit also has
mandatory maintenance/reset tails and is not safely catchable whole. The C/new
GUI helper and D/old GUI batch are not frame owners.

Track `validated-unsubmitted -> optional compatibility-queued ->
submitted-synchronous -> driver-accepted -> presented`. Recovery rules are:

- reject pre-submit at the proven owner;
- abandon a frame only after exact AO-side unwind and tracked-state reset;
- poison the device on driver/uncertain external state;
- terminate/restart on stack, heap, unwind, lock, or control-flow corruption.

Prove device destroy/recreate, generation invalidation, resource restoration,
and subsequent Present before attempting in-process recovery. C/new has a
native Cheetah reset/callback path but no proven complete rebind; D/old has
lost-surface restoration but no proven post-initialization Device7 recreation.
Without complete proof, a poisoned device requires restart. Last-good-frame
presentation is available only if the compatibility layer owns a retained
backbuffer.

Do not infer frame recovery from existing batch cleanup. Add a bounded
per-policy circuit breaker so a failed cleanup disables recovery and the next
matching exception dumps normally.

## Per-step engineering contract

This table makes dependencies, risk, acceptance, and rollback explicit. A
`TBD` hook site means the step is evidence collection only; no production edit
is authorized until the site is replaced by an exact profile RVA and byte/ABI
proof.

| Step | Exact files and sites | Dependencies | Principal risk | Acceptance | Rollback |
|---|---|---|---|---|---|
| P0.1 policy/coordinator | `src/dllmain.cpp`, new `compatibility_policy.*`; existing N3 profiles and all named feature dependencies | none beyond forwarder | policy error enables behavior or one feature gates another | forward-only/diagnostic/each-feature tests; independent readiness/module waits | select diagnostic; remove coordinator calls before any patch |
| P0.2 transactions | new `hook_transaction.*`; N3 five calls, GUI `+0x14CC5F/+0x157234`, randy `+0x43B99/+0x219B4`, GUI `+0x152E49/+0x4F2EF` | P0.1 module catalog/policy | suspended-thread race or partial patch | failure injection at every stage; byte/protection/thread rollback verified | verified restore all bytes; fail fast if unprovable |
| P0.3 new GUI | `src/gui_rect_fix.cpp`; GUI calls `+0x14CC5F/+0x157234`, helper `+0x14CA77` | P0.1/P0.2 | broad catch returns after unknown mutation | default off; exact inner dispatch or whole-helper cleanup tests | disable feature; transaction restores both calls before process continues |
| P0.4 logging | new `evidence_logger.*`; `logging.cpp`, `crash_dump.cpp`, exception paths in GUI/randy | P0.1 worker lifecycle | logger deadlock, queue growth, lost evidence | no lock/allocation/I/O in VEH/SEH; retain bounded fail-closed mapping probes; bounded drop accounting | diagnostic dump/search remains; disable summaries without changing guards |
| P0.5 build/docs | `Build-Package.cmd`, both READMEs, `self_test.cpp`; no AO site | current artifact/disassembly | false CFG/safety claim | compiler option, PE metadata, indirect-call disassembly, docs agree | restore build flag only with claim removed |
| P1 audited current features | `roomspace_fix.cpp`, `gui_rect_fix.cpp`, `randy_color_fix.cpp`, `self_test.cpp`; strong exact H3/H7/H8-H12, H5 preflight, plus separately disabled/experimental H5 driver and H6 `GUI+0x152E49` recovery; H1 RoomSpace remains independent | P0.1/P0.2/P0.4 | ABI/context near-miss, x87/stack damage, or post-driver poison | positive and one-field near-miss tests; emitted x86; both profiles; Level-4 recovery remains off until postcondition tests | disable one feature; exact transactional byte restore where patched |
| P2 render evidence | new `module_catalog.*`, `render_evidence.*`, existing `evidence_logger.*`, randy/GUI exact sites `+0x219B4/+0x152E49/+0x4F2EF` | P0.4, P1 | diagnostic dereferences bad object or costs FPS | proven fields only; bounded counters; same-scene performance gate | disable evidence feature; no behavior changes to roll back |
| P2.5 COM closure | future dedicated `graphics_compatibility/` modules and isolated tests; Cheetah D3D9 plus all randy/DisplaySystem DDRAW creation/return sites | complete IID/return/storage/release inventory | raw-interface escape, split COM identity, refcount/use-after-free, stale generation | stable IUnknown identity; exact QI/AddRef/Release; thread/race tests; no raw descendant in exhaustive used-method tests | do not deploy partial proxy; isolated code remains disconnected |
| P3 control flow | new `control_flow_validation.*`, likely `gui_rect_fix.cpp` or `randy_color_fix.cpp`, `self_test.cpp`; hook RVA **TBD pending dump/static proof** | P2 manifest/evidence | generic target filter or wrong return/cleanup | exact call bytes/ABI, initial transfer proof, two matching records or deterministic reproduction | feature remains off until proven; exact transaction rollback afterward |
| P4 AO render validation | new `render_validation.*`, `randy_color_fix.cpp`, module catalog/self-tests; primary site randy `+0x219B4`, earlier typed site TBD | P1/P2; P3 if virtual dispatch used | false object rejection, visual loss, hot-path regression | allocation-derived checks only; exact/near-miss tests; cross-driver soak | disable L2 validator; retain diagnostic/exact L1 independently |
| P5 Gamecode deserialize | future `gamecode_waypoint_guard.*`; C hook `+0x7A91D`, D hook `+0x7964B`; exact native failure tails and N3 caller returns | P0.4/module manifest; type/layout/Strategy D owner contract proven | wrong caller, partial transaction, ABI drift, hot-path logging, retry/publication regression | exact emitted C/D contexts, caller near misses, rollback injection, count/malformed stream, no-publication, runtime/FPS/soak matrix | diagnostic/behavior off until validation; never clamp or change BinaryStream capacity |
| P6 resource publication | `resource_diagnostics.*`, `evidence_logger.*`; notifier `ResourceManager+0x3D7B`, worker call `+0x40F6`; lab verifier outside package | request destruction/ref/publication proof; P5 identity only if actually linked | missed waiter, ref leak/double free, lifetime race, false causal merge | destruction/clear site and native waiter failure/retry/refcount tests; otherwise families remain separate | disable diagnostics; never skip notification or catch allocator fault |
| P7 N3 initialization | `dllmain.cpp`, `roomspace_fix.cpp`, `module_catalog.*`, evidence logger; fault `N3+0x15040`, nearby RoomSpace call `+0x15054` | P0.1 independent RoomSpace flag | catching intentional failure or misattributing proxy | repeated RoomSpace off/on, full manifest/object and native failure contract | RoomSpace off; no N3 catch installed |
| P8 frame/device recovery | future `frame_state.*`, `device_generation.*`, recovery coordinator; exact frame/Present owners **TBD** | P2.5 complete closure, P4 validation, proven owner/reset/recreate | false rollback after submit, corrupted driver/device/locks, stale wrappers | submission transition proof, exact unwind, device recreate/generation/resource restore, subsequent Present, long soak | recovery off; poisoned device restarts unless recreate is proven |

## Files expected to remain unchanged initially

- server, database, packet, Subway, and capture gameplay code;
- AO client executables and original DLL files;
- system/NVIDIA DLLs;
- database schemas and content.

## Acceptance criteria across all implementation commits

- exact profile and original-byte verification;
- ABI and emitted-x86 test for every thunk;
- transactional install and verified rollback;
- unknown exceptions continue normal search;
- no production field/layout without static or dump proof;
- no normal-frame memory queries or file I/O;
- per-feature disable and global diagnostic kill switch;
- old and new client behavior tested independently;
- crash, visual corruption, false positive, and FPS results recorded separately;
- package build/self-tests pass;
- engines restarted after every build as required by the repository workflow;
- commit contains one intentional scope and is pushed.

## Rollback strategy

1. Runtime: select diagnostic-only policy before client startup.
2. Feature: disable one named mitigation without disabling dumps/logging.
3. Package: close AO and use ownership-verified packaged uninstall.
4. Source: revert only the responsible scoped commit; rebuild package and
   restart engines.
5. Hook transaction: if runtime byte rollback is not fully verified, terminate
   the process instead of continuing.

## Explicit non-goals

- rewriting/replacing AO's renderer (a proof-gated API compatibility boundary
  is not an engine rewrite);
- deploying a partial graphics COM proxy;
- disabling client integrity checks;
- modifying NVIDIA/system DLLs;
- generic SEH/VEH crash swallowing;
- global vtable scanning;
- global heap interception;
- arbitrary geometry caps;
- combining Gamecode/resource and renderer recovery without identity evidence.
