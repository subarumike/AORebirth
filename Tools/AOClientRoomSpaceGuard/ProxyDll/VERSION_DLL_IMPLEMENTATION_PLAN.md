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

## Priority sequence

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
randy31 +0x2511A
randy31 +0x6C3A1
randy31 +0x6C476
randy31 +0x6C51D
```

Test every positive predicate and every one-field near miss. In particular,
keep report `+0x24118` / image `+0x25118` outside the `+0x2511A` policy.

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

### P5 — BinaryStream diagnostic proof

Target family: crash-report logical `BinaryStream.dll +0xB1D`.

Investigation sequence:

1. recover exact function start/end and disassemble the faulting instruction;
2. recover caller ABI and five caller contexts;
3. map stream object fields only through observed reads/writes;
4. identify buffer, position, logical length, capacity, request, and growth
   decision;
5. identify resource/message owner and native failure return;
6. instrument the whole stable Gamecode serialization operation first; move
   closer to reserve/grow/write only after that ABI is proven;
7. reproduce the same location repeatedly;
8. decide among native growth, clean rejection, or resource quarantine.

Expected future files, after proof:

```text
src/stream_diagnostics.h/.cpp     new
src/resource_diagnostics.h/.cpp  new
src/dllmain.cpp
Build-Package.cmd
```

Do not add count caps, unconditional reallocations, guard-page recovery, or
“write and continue” behavior.

### P6 — ResourceManager and heap correlation

Add resource/allocation identity to P5 events, then compare with the
ResourceManager worker and allocator dumps. Promote causality only when the same
resource or allocation is linked temporally. Otherwise keep independent. Use
PageHeap/Application Verifier only in a controlled lab run to find the first
corruptor; never catch allocator faults in production.

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

### P8 — Renderer frame recovery

Only after AO defines a frame owner and reset contract, add a level-4 policy
that can abort one frame and reset known state. Do not infer frame recovery from
the existing batch cleanup; batch and frame ownership are different. Add a
bounded per-policy circuit breaker so a failed cleanup disables recovery and
the next matching exception dumps normally.

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
| P3 control flow | new `control_flow_validation.*`, likely `gui_rect_fix.cpp` or `randy_color_fix.cpp`, `self_test.cpp`; hook RVA **TBD pending dump/static proof** | P2 manifest/evidence | generic target filter or wrong return/cleanup | exact call bytes/ABI, initial transfer proof, two matching records or deterministic reproduction | feature remains off until proven; exact transaction rollback afterward |
| P4 AO render validation | new `render_validation.*`, `randy_color_fix.cpp`, module catalog/self-tests; primary site randy `+0x219B4`, earlier typed site TBD | P1/P2; P3 if virtual dispatch used | false object rejection, visual loss, hot-path regression | allocation-derived checks only; exact/near-miss tests; cross-driver soak | disable L2 validator; retain diagnostic/exact L1 independently |
| P5 stream proof | new `stream_diagnostics.*`, `resource_diagnostics.*`, `dllmain.cpp`; stable Gamecode callers `+0x7A945/+0x7A954`, BinaryStream fault `+0x1B1D` | P0.4/module manifest | observing guessed fields changes memory or partial operation | function/ABI/fields/grow/error contract proven; repeated exact resource identity | diagnostic feature off; no stream behavior changed during proof |
| P6 resource/heap | `resource_diagnostics.*`, `evidence_logger.*`; ResourceManager request boundary **TBD**, fault `+0x3D84`; lab verifier configuration outside package | P5 identities | false causal merge, ref leak/double free, worker lock retention | same allocation/resource/time link or families remain separate; native cancel/refcount tests before behavior | disable diagnostics/cancel feature; never catch allocator fault |
| P7 N3 initialization | `dllmain.cpp`, `roomspace_fix.cpp`, `module_catalog.*`, evidence logger; fault `N3+0x15040`, nearby RoomSpace call `+0x15054` | P0.1 independent RoomSpace flag | catching intentional failure or misattributing proxy | repeated RoomSpace off/on, full manifest/object and native failure contract | RoomSpace off; no N3 catch installed |
| P8 frame recovery | new `renderer_recovery.*`, likely randy/GUI integration; exact frame-owner hook **TBD** | P2/P4 and proven owner/reset contract | corrupted driver/device/locks after continuation | cleanup postcondition, subsequent-frame integrity, circuit breaker, long soak | recovery off; next fault dumps normally; process restart for active patch changes |

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

- replacing AO's renderer;
- disabling client integrity checks;
- modifying NVIDIA/system DLLs;
- generic SEH/VEH crash swallowing;
- global vtable scanning;
- global heap interception;
- arbitrary geometry caps;
- combining stream and renderer recovery without identity evidence.
