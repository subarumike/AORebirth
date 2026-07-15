# unresolved evidence gaps

Status: fail-closed backlog from the 2026-07-15 investigation. An item remains
here until the listed evidence exists; plausibility is not promotion evidence.

## Corpus and provenance

### C01 — Complete raw reports are not all repository artifacts

Known: 43 complete reports were supplied in the discussion, plus the requested
dump-only event. Nineteen local dumps overlap the discussion and contain seven
additional unmatched exception records.

Needed:

- export each future report as a timestamped text file;
- record client PID/path/profile and proxy hash with the report;
- preserve the exact proxy log and dump together;
- assign event ID at collection time.

### C02 — Several pasted events lack time, PID, and loaded-module manifest

Needed: dump or crash-package metadata containing timestamp, PID, thread,
module bases/versions, client executable/N3 hash, proxy hash/policy, and driver
identity.

### C03 — The latest `0x420C70A4` event has no dump

Known: a later inspection found D `version.dll` absent, but runtime proxy state
at the event is unknown. EIP was not attributable to a loaded image in the
borrowed module map and EBP was unreadable. Several stack words would fall
within N3/Gamecode/dinput ranges under that map, but one N3-relative word is in
`.data`, proving the list is not a valid call chain.

Needed: a recurrence with diagnostic-only proxy loaded and a minidump. Do not
attach to or launch AO automatically; Mike reproduces and Codex analyzes the
completed evidence.

### C04 — Strict per-event raw register/stack normalization is incomplete

Known: the canonical matrix records the causal registers, stable stack class,
and every dump-backed module chain used in this investigation. The discussion
is not itself a repository artifact, some pasted stacks are truncated, and a
full appendix containing every raw register plus every resolvable stack word
for all 44 canonical events has not been generated.

Needed: export the 43 original exception blocks into immutable timestamped
files, preserve truncation markers, and run one normalizer against those files
and all 19 dumps. Output raw registers, module base/actual RVA/section for every
usable word, and `U` where the source is absent. Do not reconstruct missing
frames from neighboring events.

## Current hook proof

### H01 — New-client GUI helper cleanup is unknown

Sites: `GUI +0x14CC5F`, `GUI +0x157234`, helper `GUI +0x14CA77`.

Needed:

- exact inner indirect call/jump that receives coordinate-like data;
- saved ESP/return address before target transfer;
- helper-owned locks/allocations/state at each phase;
- native failure/cleanup path;
- emitted-x86 tests for a narrow invoke shim.

Until resolved: default this feature off. The current broad non-executable-EIP
SEH is not a proven recovery policy.

### H02 — New-client two-site patch is not a complete transaction

Needed: shared transaction tests proving thread safety, rollback after every
failure, cache/protection restoration, and no partial active hook.

### H03 — Exact randy `+0x25118` event

Known: the existing render-state guard is at `+0x2511A`, two bytes later.

Needed: instruction bytes, live register/context state, function boundary,
caller object, and safe resume/cleanup. Do not extend the existing RVA range.

### H04 — High unreadable GUI tree keys

Known: optimized source intercepts only keys below `0x10000`.

Needed: a matching crash showing a high stale key, owning tree/object lifecycle,
and whether native not-found remains valid for that lifetime failure.

### H05 — Handler ordering and lifetime

Needed:

- test with client, proxy, AOSharp, and other handlers installed in different
  orders;
- prove `SetUnhandledExceptionFilter` is not overwritten after startup or add a
  supported detection mechanism;
- define process-detach policy for VEH handles and wrapper memory, even if
  runtime unload remains unsupported.

### H06 — FPU/SSE and callee-saved state across every recovery

Known: rectangle x87 depth is now explicitly balanced. Other exception
boundaries rely on compiler/SEH and native cleanup.

Needed: emitted-x86 and context tests for x87 control/status/depth, MXCSR, XMM
state where used, EBX/ESI/EDI/EBP, ESP, return address, and EFLAGS requirements.

### H07 — RoomSpace cross-site ABI and positive-cell bounds

Known: all five calls per profile target the same native function and the
generated wrapper passes current byte-level self-tests.

Needed: caller-by-caller ABI proof, maximum valid cell/table extent, zone-table
readability/ownership, and positive out-of-range behavior. A shared callee does
not alone prove every caller's surrounding invariants.

### H08 — Late module loads and coupled readiness

Known: the worker waits only for N3; enabled GUI/Utils/randy/driver dependencies
must already be loaded. RoomSpace failure currently gates all later installs,
and rectangle can survive a later randy failure.

Needed: independent module wait/readiness tests, feature-scoped failure, and a
single manifest proving which modules installed. No READY line may imply an
uninstalled feature.

### H09 — CFG build claim

Known: the artifact advertises GUARD_CF metadata, while the build option
placement and raw indirect-call disassembly do not prove compiler CFG
instrumentation.

Needed: corrected compiler/link option placement, load-config inspection, and
disassembly/self-tests for guarded indirect calls, or removal of the CFG claim.

## Render-object and control-flow evidence

### R01 — AO render-object layout

Needed for each proposed typed validator:

- constructor/destructor or allocation/free evidence;
- object size and vtable field;
- vtable owning module/section;
- vertex/index pointer/count/stride offsets;
- material/texture/transform/bounds offsets;
- reference-count or ownership semantics;
- caller neutral result and cleanup.

Do not infer a layout from one crash register.

### R02 — Geometry format and limits

Needed:

- primitive type at the boundary;
- buffer allocation size;
- exact vertex format and stride;
- index element width;
- proof of triangle-list semantics before divisibility checks;
- format-specific finite fields;
- allocation-derived limits.

No arbitrary “sane maximum” is approved.

### R03 — Producer of randy low pointers

Needed: allocation/resource identity from object construction through randy
submission and release. Current guards identify consumers only.

### R04 — Stable AO invalid-dispatch site

Needed: at least two matching full dumps or one deterministic reproduction that
identifies the same `call reg`, `call [reg+offset]`, or virtual dispatch, plus
exact ABI and cleanup. EIP value alone is insufficient.

### R05 — Meaning of coordinate-like corruption

Needed: memory ownership for the buffer containing the coordinate values,
write history, and the target object/stack field it overwrote or replaced.
Float decoding is evidence of data shape, not proof of overwrite direction.

## NVIDIA and Direct3D

### N01 — Cross-driver producer identity

Known:

- exact null-read guards target driver `32.0.15.9186`;
- dump-only `nvd3dum +0x154314F`, read `0x0A0A0000`, occurred on
  `32.0.16.1074` through the same AO/D3D draw chain;
- an unmatched dump also records NVIDIA `+0x170C4C6`, write `0x3C`.

Needed: AO-side object/submission fields and resource identities immediately
before each driver failure. Do not derive new driver guards from relative RVA
similarity.

### N02 — Driver state after contained draw AV

Needed: documented or observed D3D/DDRAW state after SEH unwinds the driver,
plus long post-hit soak. A returned `S_OK` does not prove internal driver state
is coherent.

### N03 — Deferred-flush ownership

Needed: prove which earlier submission is flushed at `+0x170C490` and link it
to the later lock/batch object. Current H6 supplies exact local cleanup inputs
and call-sequence evidence; a successful postcondition and coherent driver
state remain unproven.

### N04 — GPU-generation and driver coverage

Needed: exact evidence on older GTX, GTX 1660 Ti, RTX 20, RTX 30, and RTX 40,
with both clients and exact driver module identities.

### N05 — NV-B `+0x154314F` recovery contract

Known: its instruction, ECX/access value, driver identity, and full path through
the existing DrawIndexed wrapper are proven. Driver lock/device state and AO
cleanup are not.

Needed before any driver interception: exact phase, owned driver/AO state,
native outer cleanup or device-quarantine postcondition, subsequent-frame
integrity, circuit-break behavior, and driver-specific exact/near-miss tests.
RVA/instruction repetition alone is insufficient.

## BinaryStream and resources

### S01 — BinaryStream report-logical `+0xB1D`, actual `+0x1B1D`, function and ABI

Needed:

- function start/end and faulting instruction;
- calling convention and arguments;
- five caller module+RVA contexts;
- return/error contract;
- overwritten-instruction/trampoline proof before any hook proposal.

### S02 — Stream object fields

Needed from static usage and dumps:

```text
buffer base
position
logical length
allocated capacity
requested write
growth-needed flag/result
terminator/alignment behavior
ownership transfer
```

### S03 — Why growth did not precede the write

Needed: native reserve/grow call path, integer arithmetic, allocation result,
and exact branch that reaches `+0xB1D`. Distinguish missing growth, failed
growth, integer overflow, stale cursor, and malformed request.

### S04 — Resource identity

Needed: packet/resource/template/file identity for each BinaryStream failure,
and the worker that consumes it. Without this, quarantine cannot be scoped.

### S05 — BinaryStream-to-heap causality

Needed: same process/allocation identity and temporal ordering linking the
overrun to the `ntdll` allocator event. Similar memory symptoms are not enough.

### S06 — BinaryStream-to-ResourceManager causality

Needed: same resource identity or buffer passed from the stream caller into the
ResourceManager worker before its null/stale read.

## N3, Vehicle, and C++ exceptions

### V01 — N3 login fault at actual `N3 +0x15040`

Known: it is 0x14 bytes before old-profile RoomSpace callsite `N3 +0x15054`,
and VERSION frames are present.

Needed:

- full dump with proxy hash/policy and emitted wrapper address;
- feature-isolated reproduction with RoomSpace off/on;
- object/argument values before `+0x15040`;
- static function boundary and caller contract.

Do not add an N3 exception guard there.

### V02 — Native exception types/messages

Needed: C++ exception object/type/message, throw-site module+RVA, and caller
contract for each `E06D7363` family. Do not catch by exception code alone.

### V03 — Vehicle initialization ownership

Needed: typed Vehicle/N3 object, lifecycle phase, and whether the exception is
an intentional fail-fast for invalid content/state.

## Logging, performance, and validation

### L01 — No compact module/policy manifest in dumps

Needed: a bounded manifest in startup log and optionally a minidump user
stream, including all module identities, policy/features, and proxy hash.

### L02 — Exception-path logging is not lock-free

Known: current logging takes a critical section and flushes the file on each
event. Needed: atomic signature counters and deferred worker emission; prove no
VEH/SEH file I/O or lock acquisition.

### L03 — Performance evidence for the optimized draw wrapper

Known: source/emitted-x86 removed 4–5 `VirtualQuery` calls per draw and normal
rectangle interception. Needed: live A/B FPS and frame-time data on D old
client before commit/promotion.

The A/B must attribute cost per feature: forward-only, diagnostic, RoomSpace,
rectangle, randy data VEH, DrawIndexed preflight, GUI batch, GUI tree, new GUI
helper, and especially plain-HAL selector normalization. A combined proxy
result cannot identify which feature caused the 100-to-20 FPS observation.

### L04 — Hardware soak evidence

Needed: completed run records from `VERSION_DLL_TEST_MATRIX.md`, including
visual integrity, performance, feature hits, false positives, and post-recovery
stability.

## Exit rule

An evidence gap is resolved only by adding the exact artifact and updating the
canonical corpus/hook audit. “It stopped crashing once” is validation evidence,
not structure, ABI, ownership, or cleanup proof.
