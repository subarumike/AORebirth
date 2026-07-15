# version.dll mitigation architecture

Status: proposed architecture. No new behavioral mitigation in this document is
implemented merely by being listed here.

## Design objective

Turn the existing proxy from a collection of exact guards into a versioned,
testable compatibility layer while preserving the central safety rule:

> Recover only at a boundary whose ABI, state ownership, cleanup, and neutral
> result are proven. Otherwise record evidence and continue normal exception
> search.

The existing `version.dll` forwarding model is structurally usable. A rewrite
is not required. The installer, exact N3 profile hashes, Windows export
forwarding, offline self-tests, and crash dumps are a sound base.

## Proposed components

```text
VersionProxy
  -> RuntimeCoordinator
       -> ModuleCatalog
       -> CompatibilityPolicy
       -> HookTransactionManager
       -> ExactGuardSet
       -> RenderValidation
       -> ControlFlowValidation
       -> StreamResourceDiagnostics
       -> RendererRecovery
       -> EvidenceLogger
       -> CrashReporter
```

### VersionProxy

Responsibilities:

- forward exactly the 17 supported Windows `version.dll` exports;
- resolve the real DLL from the system directory once;
- contain no AO mitigation policy in exported forwarding functions.

Existing `InitOnceExecuteOnce` resolution should remain.

### RuntimeCoordinator

Responsibilities:

- run outside loader lock;
- wait for every required module for the selected profile, not only `N3.dll`;
- collect immutable module identities and address ranges;
- load feature policy before installing behavioral hooks;
- install hooks in dependency order as transactions;
- publish one compact readiness record.

The coordinator must not report READY after a partial transaction or an
unconfirmed protection/thread cleanup.

### ModuleCatalog

Immutable per-process records:

```text
module name
loaded base
image size
PE timestamp
checksum
file SHA-256 where required
executable sections
approved profile id
```

Profiles must retain exact N3 hashes. GUI, randy, BinaryStream, ResourceManager,
DisplaySystem, D3DIM700, DDRAW, and the NVIDIA user-mode driver should have
explicit identity requirements before a hook depending on their layout is
enabled. Byte verification remains mandatory at each edit surface.

The catalog permits integer interval checks on hot paths. It avoids calling
`VirtualQuery` for every draw; an earlier hot-query proxy coincided with a drop
from the observed no-proxy baseline of about 100 FPS to about 20 FPS.

### CompatibilityPolicy

Proposed levels:

| Level | Name | Behavior |
| ---: | --- | --- |
| 0 | diagnostics | version forwarding, compact startup log, crash dumps only |
| 1 | exact guards | only existing, audited exact guards |
| 2 | AO render validation | proven AO-side object/submission validation |
| 3 | stream/resource containment | only after stream/resource contracts are proven |
| 4 | renderer recovery | explicit frame/batch/device recovery with proven cleanup |

Every mitigation is independently selectable. Level is a convenience default,
not a reason to enable an unproven feature.

Proposed feature identifiers:

```text
roomspace.checked-cast
oldgui.rect-empty-on-exact-read
oldgui.tree-low-key-not-found
oldrender.color-low-pointer
oldrender.render-state-entry
oldrender.draw-input-probe
oldrender.nvidia-exact-draw-fallback
oldrender.gui-batch-cleanup
oldrender.force-plain-hal
newgui.helper-control-flow
stream.observe
stream.reject-proven-overrun
resource.observe
renderer.recover-frame
```

### Kill switch

A single startup policy must disable all behavioral mitigations while retaining
version forwarding, module inventory, startup logging, and crash dumps.

Proposed semantics:

```text
AORF_POLICY=diagnostic
```

The exact configuration surface is an implementation decision, but it must be
read once at startup, logged, immutable for the process, and fail to
`diagnostic` on malformed input. No registry-wide or machine-wide setting is
required.

A second `forward-only` mode is required for zero-overhead A/B diagnosis. It
loads only the real system VERSION and forwards exports: no compatibility
worker, module hashing, file logger, dump filter, or behavioral hook. This is a
measurement mode, not the required diagnostic kill switch. `diagnostic`
continues to preserve the bounded startup log and crash dumps.

Profile identification must move out of RoomSpace installation. RoomSpace and
each old-renderer component are independent policy modules; a RoomSpace or
selector failure must not suppress rectangle, color, tree, or diagnostic
coverage.

### HookTransactionManager

One implementation should own every code mutation. Required sequence:

1. verify complete module/profile identity;
2. verify exact original bytes and whole-instruction boundaries;
3. build all replacement bytes and trampolines before suspension;
4. suspend and enumerate other threads to a stable snapshot;
5. reject installation if any instruction pointer is inside an edit or unsafe
   renderer range;
6. change protection for all pages;
7. reverify original bytes;
8. write all edits;
9. flush instruction cache;
10. verify all edits;
11. restore all page protections;
12. resume every suspended thread;
13. on any failure, restore every changed byte and verify rollback;
14. terminate rather than continue if active code or rollback state cannot be
    proven.

The current RoomSpace and old-render transaction logic are models. The current
new-client GUI helper patch does not yet meet this contract.

### ExactGuardSet

This component owns only audited Level-1 AO predicates: Utils rectangle,
randy low draw-resource/state/color variants, and GUI low-key not-found. Each
descriptor contains exact client/module identity, fault or entry RVA, original
bytes, ABI, context transform or neutral return, x87/SSE requirements,
near-miss tests, and an independent feature flag. RoomSpace and renderer-mode
selection are not members of this set. NVIDIA post-fault continuation and GUI
batch recovery are Level 4, not exact-guard defaults.

### MemoryProvenance

This service is not a generic “pointer is valid” oracle. It provides facts:

```text
low/null classification
checked pointer-end arithmetic
committed/readable/writable/executable mapping on cold/error paths
containing loaded image and section
known proxy trampoline ownership
known renderer buffer interval, only when registration is proven
```

Readability never proves object type or lifetime. Executability never proves a
valid target ABI. Those require a typed boundary and approved module/function
set.

### RenderValidation

Validation is attached to a proven AO-side construction or submission
boundary, not every driver instruction.

Candidate inputs, enabled only when offsets are established by code and dumps:

```text
object and vtable
approved vtable image
selected virtual function
vertex and index buffer pointers
vertex/index counts and stride
primitive type
material and texture references
transform and bounds
resource identity/owner
```

Geometry rules are conditional on a proven format:

- checked `count * stride` and buffer interval;
- triangle-list index count divisible by three;
- each index less than vertex count;
- finite positions, normals, UVs, matrices, and bounds;
- ordered minimum/maximum bounds;
- no registered freed/quarantined resource.

No “sane maximum count” may be invented. Limits must come from allocation size,
field width, API contract, or observed/profiled AO code.

### ControlFlowValidation

Only proven AO render/object dispatch sites are candidates. A target may be
accepted when it:

- is not below `0x10000`;
- matches a per-site approved target entry/range in an executable section and
  the declared call ABI; or
- is a registered executable proxy trampoline with a declared ABI.

An initial execute AV may be rejected only when the wrapper proves that no
target instruction executed and the saved return/stack state matches that
specific invoke shim.

Reject action is boundary-specific:

```text
discard object
skip one submission
mark batch invalid
run proven batch cleanup
return native not-found or neutral result
abort frame through an established frame owner
```

There is no generic “return S_OK” rule.

Current indirect-dispatch inventory:

| Site | Instruction/evidence | Status |
|---|---|---|
| randy `+0x219B4` | proven `call [ecx+0x80]`; public eight-argument DrawIndexed ABI; current wrapper resolves vtable slot 0x20 | proven narrow pre-entry boundary |
| N3 word resolving to `+0x5372` in the E43 stack | borrowed module map places it immediately after `call [eax+0x28]` | damaged stack candidate only; no proof E43 returned from this call |
| C/new GUI helper `+0x14CA77` | outer callers/ABI proven; exact inner non-executable target transfer not located | unresolved; outer catch defaults off |
| generic callback/deferred render sites for EIP 0/2/5/8 | no stable mapped caller | unresolved; no process-wide hook |

### StreamResourceDiagnostics and containment

Rendering and stream/resource code remain independent components.

Initial `stream.observe` is diagnostic-only. At the proven reserve/grow/write
boundary it records:

```text
stream pointer
buffer base
position
logical length
capacity
requested bytes
growth decision/result
caller RVA
resource identity and worker thread
```

Production containment is eligible only after the stream ABI and ownership are
established. Preferred actions:

1. invoke the native growth path before the proven write;
2. reject the malformed write with the caller's native failure result;
3. quarantine the identified resource;
4. skip resource consumption with complete ownership cleanup.

Guard pages are diagnostic evidence only.

### RendererRecovery

Recovery policies are named and exact. Each defines:

```text
entry boundary
owned locks/allocations/state
cleanup order
neutral return contract
postcondition
allowed signatures
```

The existing GUI batch cleanup is a candidate policy. Arbitrary driver faults,
invalid return addresses, and unknown helper phases have no recovery policy.

Each recovery policy has an atomic circuit breaker. Repeated cleanup failure,
an invalid postcondition, or exhaustion of a small configured recovery budget
disables that policy and lets the next matching fault follow normal
dump/search behavior. The circuit breaker never broadens a predicate or
silently retries forever.

### EvidenceLogger

Normal frames must execute no file I/O, module queries, or unbounded scans.

VEH/SEH recovery must also execute no file I/O, allocation, or logging lock.
Existing fail-closed readable/writable predicates remain until an equivalent
prevalidated catalog or bounded probe replaces them; the prohibition is on
normal-path/per-draw mapping queries and diagnostic I/O, not on removing a
safety predicate. Each exact family owns fixed static atomic counters and a
bounded last-context POD record. A low-priority worker emits the first
occurrence and periodic/power-of-two summaries. If the worker cannot keep up,
it increments a drop counter; it never blocks the renderer or resource worker.

Each event has a stable signature hash over profile, hook, producer/consumer
RVA, failure reason, and selected object/type fields. Rate limiting is per
signature:

```text
first occurrence
next 3 occurrences
power-of-two counts
final process summary
```

This is more compact than “first 16 and every 100” while preserving recurrence
shape.

Render rejection record:

```text
time/thread/profile/module identities
hook and policy level
producer and consumer module+RVA
object/vtable/target and provenance
proven counts/stride/material/texture/transform summary
failure predicate
cleanup/action/postcondition
signature/count
```

Stream/resource record:

```text
time/thread/profile
stream/buffer/position/length/capacity/request
caller and resource identity
growth/rejection/quarantine decision
signature/count
```

Logging must not dereference unproven fields merely to improve diagnostics.

### CrashReporter

Retain unhandled dump generation and chaining to the prior/client handler.
Improvements should add a compact module/policy manifest to the log and, if
implemented with supported dump APIs, as a user stream. The crash reporter is
diagnostic and never returns `EXCEPTION_CONTINUE_EXECUTION`.

## Boundary feasibility matrix

This matrix is the gate between a recurring final fault and a production hook.
“Known” means demonstrated by exact bytes/dumps in the current profiles; it is
not inferred merely because a pointer was readable.

| Candidate boundary | Object/fields available | Type, size, lifetime proof | Side effects before decision | Safe reject/return | Cleanup/refcount/frame risk | Threading and decision |
|---|---|---|---|---|---|---|
| Utils rectangle `+0x82E6` | 16-byte output Rect; 8-byte Point; two float operands | shapes and ABI known; producer lifetime unknown | no external mutation at the two fault instructions | empty Rect, EAX=output, `ret 8` | x87 pop required only after `fld`; no refcount | render thread; existing exact L1 is eligible |
| randy draw-resource entry before `+0x21A94` | resource arg, six stack args, caller frame/return | entry bytes/ABI known; high-pointer object layout/lifetime unknown | none before verified low-EAX fault | skip whole call, EAX=0 | exact ESP/EBP unwind; no observed prior FPU/nonvolatile mutation | render thread; exact low case eligible |
| randy state loop before `+0x2511A` | state id EAX; device EBX; 16-byte entry EDI; observed pushed stack/argument value `0x0A` | exact local entry usage known; table extent/owner not proven | before entry `+8/+C` mutation and device call | skip one lookup using native loop resume | pop exact pushed DWORD; no refcount | render thread; exact case eligible, no generalized cap |
| randy color/sample sites | byte/dword source and native missing-sample branches | exact instruction sequences known; texture/sample object layout/lifetime unknown | before failed read; integer-only sequence | black/zero or native missing-sample path | no proven ownership change | render thread; exact low cases eligible |
| DrawIndexed dispatch `randy+0x219B4` | device, primitive, VB, start/count, WORD index span, flags, vtable slot | public call ABI and endpoint probes known; AO object owner and buffer allocations incomplete | preflight runs before driver entry | skip one submission and return `S_OK` only for the declared draw contract | visual omission; post-driver AV has unknown NVIDIA locks/state | render thread; L2 preflight eligible after performance tests, post-fault L4 only |
| GUI batch `GUI+0x152E49` | batch pointer/+0 source/+8 state index, span, viewport, three 0x84 state blobs, static/heap index path, DynamicVB helpers | profile-specific layout and cleanup helpers proven locally; broader lifetime/refcounts unknown | post-fault paths may have entered GUI/driver | discard one batch | exact owned heap-index free, VB/material/state cleanup; driver state still unknown | render thread; exact F07/F09 experimental L4 |
| GUI VB copy `GUI+0x150F22` | null-derived destination, source, span, stride `0x1C`, base vertex, batch/frame locals | exact F09 arithmetic and surrounding batch layout proven; DynamicVB lifetime beyond batch unknown | failed `rep movsd` follows null VB result | not safe at the copy instruction; reject at whole-batch owner | batch cleanup/free/reset required exactly once | render thread; consume only through the F09 batch policy |
| NVIDIA deferred flush `NV-A+0x170C490` | exact driver state plus validated AO batch/state context | driver object/lock owner and earlier queued submission unknown | driver has already entered deferred work | no safe Lock-only return | use exact GUI batch cleanup only; later device poisoning remains possible | render thread; driver-specific experimental L4 |
| GUI tree `GUI+0x4F2EF` | tree, output, key, native not-found sentinel | entry ABI/prologue known; high-key object lifetime unknown | before comparator dereference | native not-found, `ret 8` | no observed refcount | render thread; low-key L1 eligible |
| C/new outer helper `GUI+0x14CA77` | self and six args only | outer ABI known; inner target/object/phase and cleanup unknown | potentially large partial helper mutation | no proven neutral whole-helper return | locks, allocations, x87/SSE and renderer state unknown | render thread; current broad wrapper defaults off |
| proven inner render indirect dispatch | not yet located for F11/F12 | none | unknown | none | unknown stack/object/frame state | no hook until exact call, ABI, and cleanup are captured |
| geometry traversal | coordinate-like registers/targets and GUI/randy consumer sites only | vertex/transform/bounds object, size and lifetime not known | traversal phase unknown | no safe object/frame discard contract | could leave batch lists, refcounts or transform stack inconsistent | render thread I; evidence-only, hook RVA TBD |
| resource creation/release | only GUI heap/static index distinction and crash-time resource candidates | general resource owner/refcount/destructor not known | creation/release can mutate global caches and worker queues | no generic reject/drop | AddRef/Release, partial allocation and callback obligations unknown | render and worker threads; identity instrumentation only |
| Gamecode whole serialization caller `+0x7A945/+0x7A954` | caller operation and BinaryStream call boundary | stable chain known; stream fields, capacity/grow API and error contract unknown | earlier bytes/cursor may already be mutated | no safe partial skip; eventual whole-operation reject only | must prove no partial send/consume and exact ownership | main/client thread I; diagnostic-only first |
| ResourceManager observed worker frame report-logical `+0x201`, actual `+0x1201` | request/result candidates and ACE worker context | function role/boundary, object fields, queue lock, refcount and cancel/failure callback unknown | worker/queue state may already be held | no safe raw return/drop | leak, double-release, lock retention and race risk | worker thread; boundary TBD, diagnostic-only until native cancel path proven |
| N3 login/vehicle before `N3+0x15040` | current register/stack values only | object/type/owner/failure return unknown | initialization may be partially complete | none | vehicle/world initialization may be poisoned | main thread I; isolate RoomSpace, do not catch |

## Render-validation proof matrix

The following status applies to the current evidence, not the desired final
architecture.

| Proposed check | Current proof | Eligible use now | Evidence still required |
|---|---|---|---|
| low/null pointer | exact low predicates at named randy/GUI sites | named L1 sites only | typed meaning for any new site |
| readable/writable mapping | cold/error probes for exact outputs/arguments | fail closed at existing boundary | never treat mapping as type/lifetime proof |
| allocation/live state | none generically | no | allocator/resource registration and free history |
| object size/type | Rect/Point; GUI batch/state blobs; public D3D call arguments only | those exact structures only | constructor/destructor and field-use proof for render objects |
| vtable readability | exact DrawIndexed device resolve under SEH | that dispatch preflight | typed object and owning allocation |
| vtable/function executability | low target and module ranges partly known; immutable executable-section catalog proposed | exact initial DrawIndexed target after catalog/perf tests | approved module/function set and ABI at other dispatches |
| vertex pointer/count/stride | public DrawIndexed start/count plus GUI null-VB stride `0x1C` for F09 | checked arithmetic/probes at those exact paths | allocation size and full AO vertex format |
| index pointer/count | WORD span and first/last probes at DrawIndexed; static/heap GUI index ownership | checked overflow/endpoints at exact call | proof of every index requires bounded buffer size/format |
| primitive type | present at DrawIndexed API | log/validate only by API contract | AO construction semantics for object rejection |
| triangle-list divisibility | primitive value available, format not established for every call | no general rule | prove primitive is triangle list at target boundary |
| material/texture | cleanup helper and selected color pointers only | exact F02/F09 cleanup/fallback | typed fields, ownership, AddRef/Release contract |
| transform/matrix | coordinate-like data observed, no field mapping | no | exact object offsets, format and producer history |
| finite positions/normals/UV | no complete vertex format/allocation | no | format, stride, count and bounded buffer |
| finite ordered bounds | no proven bounds object | no | exact offsets/semantics and producer |
| resource ownership/refcount | GUI heap/static index distinction only | exact conditional free only | constructors, owners, native failure/cancel path |
| freed/quarantined interval | no registry | no | allocation/release instrumentation with stable resource ID |

Thus Level 2 begins with checked arithmetic, exact endpoint probes, and target
resolution at the existing DrawIndexed boundary. It does not begin with a
generic “all render pointers are valid” scanner.

## Interception-point policy

| Family | Preferred upstream boundary | Current fallback | Required proof before expansion |
| --- | --- | --- | --- |
| rectangle | GUI/Utils rectangle helper | exact VEH recovery | producer caller/object |
| randy color/state | AO/randy state setup | exact instruction VEH | owning object and lifecycle |
| NVIDIA draw | AO draw submission | exact driver signatures | D3D object/vertex/index layout |
| deferred GUI/VB | whole GUI batch | exact driver/GUI cleanup | native batch owner/postcondition |
| GUI tree | tree entry | low key to native not-found | high-key lifetime/provenance |
| invalid EIP | proven render dispatch shim | none generically | exact dispatch site/ABI/cleanup |
| BinaryStream | whole Gamecode serialization first; reserve/grow/write after ABI proof | crash dump only | stream fields and caller result |
| ResourceManager | observed worker frame; consume/cancel boundary TBD | crash dump only | function role, resource identity/ref ownership |
| N3 login/vehicle | typed initialization boundary | crash dump only | object type/layout and failure result |

## Threading model

- Installation is single-shot and transactional.
- Immutable module/policy data is published before hooks become reachable.
- Hot render validation uses stack locals and immutable catalogs.
- Counters and dedup tables use bounded atomic operations; they must not hold a
  lock across AO or driver calls.
- Resource diagnostics must support worker threads and identify the thread.
- Renderer recovery runs only on the owning render thread proven by the
  boundary; it must not attempt cross-thread reset.
- No handler may suspend threads during exception recovery.

## Failure behavior

- Unsupported profile or byte mismatch: diagnostic-only; install no behavioral
  hooks.
- Required module absent by the documented deadline: diagnostic-only and one
  explicit readiness failure.
- Patch failure with verified rollback: diagnostic-only.
- Patch failure without verified rollback or with an IP in changed code:
  fail fast; do not continue a partially patched process.
- Invalid object without a proven neutral contract: record and follow the
  original failure path.
- Unknown exception: dump and continue normal exception search.

## Runtime ownership and unhook

Compiled thunks live in the proxy image. Dynamically emitted wrappers must be
allocated RW, written, changed to RX, cache-flushed, and retained for process
lifetime. The proxy is not designed for runtime unload. Package uninstall must
occur with AO closed; no in-process unhook is required for the initial design.

If runtime feature toggling is added later, it must change immutable policy at
a frame-safe boundary or use preinstalled branch gates. It must not rewrite
live callsites ad hoc.

## Optimized and diagnostic semantics

There should be one codebase and the same recovery predicates in every build.
“Diagnostic” is a policy level, not a compiler variant. Optimized builds may
remove normal-path diagnostics but must not broaden recovery. Tests must verify
that configuration changes only enable/disable named policies.

## Driver-side interception policy

Keep the three current exact NVIDIA signatures only as experimental level-4
fallbacks for the one verified driver image. Do not generalize by RVA across
driver versions or GPU generations. Each new driver requires its own module
identity, instruction/register proof, cleanup policy, and post-fault device
validation until AO-side validation makes the driver fallback unnecessary.
