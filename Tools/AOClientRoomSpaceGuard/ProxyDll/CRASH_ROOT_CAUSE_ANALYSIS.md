# AO client crash root-cause analysis

Status: investigation checkpoint, 2026-07-15. This document proposes no new
production mitigation. Exact event normalization is in
`CRASH_CORPUS_NORMALIZED.md`; hook safety is in `CURRENT_HOOK_AUDIT.md`.

## Conclusions

1. The corpus is not dozens of independent defects. Thirty-two of the 43
   supplied reports belong to repeated families.
2. The dominant group is the legacy render path:
   `GUI -> randy31 -> DisplaySystem -> DirectDraw/D3D7 -> nvd3dum`.
   At least 23 reports contain one or more components of this chain explicitly.
3. The NVIDIA faults occur downstream of the AO draw boundary. The driver
   consumes null/invalid internal or submitted state, but the AO object/resource
   that produced that state is not yet identified in most records.
4. The apparent `BinaryStream` family is a proven Gamecode fixed-array
   deserialization overwrite. In the paired reports, its overwrite ranges
   contain both the allocator state and ResourceManager request, giving strong
   conditional evidence that those two faults are downstream victims. Common
   PID/time confirmation remains missing; no link to renderer faults is proven.
5. EIP values `0`, `2`, `5`, `8`, `0x41C80000`, and `0x420C70A4` strongly
   indicate damaged control flow; E42's dump proves execute access. They do not
   identify whether the producer was a stack
   overwrite, use-after-free, bad object layout, or bad virtual/callback field.
6. Native exception `E06D7363` and managed exception `E0434352` must not be
   treated as access-violation pointer guards. The managed event is excluded
   because AOSharp was injected.

## Confidence labels

- **Proven:** repeated instruction, bytes, registers, call boundary, and safe
  cleanup or return behavior are all established.
- **High:** repeated module/RVA and call-chain evidence establish the consumer,
  but not the original producer.
- **Medium:** the relationship fits the available stack/register evidence but
  lacks the producer object or a complete dump.
- **Unresolved:** evidence does not establish a safe cause or recovery action.

## Family root-cause summary

| Family | Most likely cause | Likely producer / proven consumer | Confidence | Preferred AO boundary | Driver fallback |
|---|---|---|---|---|---|
| F01 | invalid Rect/Point operand, sometimes float-shaped | producer above GUI U / Utils helper proven | high consumer | Utils rectangle helper | none |
| F02 | low/missing color or sample pointer | resource/state lifetime U / exact randy reads proven | high consumer | exact randy setup/read boundary | none |
| F03 | low draw-resource object | GUI/resource owner U / randy first dereference proven | high | whole randy function entry | none |
| F04 | invalid primitive/material state lookup | state producer U / randy lookup proven | medium | state loop before mutation/device call | none |
| F05 | NV-A null internal draw object | upstream producer U / NV-A `+0x172776C` proven | high consumer | DrawIndexed preflight | exact NV-A catch Level 4 only |
| F06 | NV-A null submission object | upstream producer U / NV-A `+0x173A009` proven | high consumer | DrawIndexed preflight | exact NV-A catch Level 4 only |
| F07 | invalid deferred VB/flush state | earlier submission U / NV-A `+0x170C490` proven | medium | whole GUI batch | exact cleanup Level 4 only |
| F08 | invalid indirect target 5 | dispatch producer U / instruction fetch inferred | medium class | exact initial AO dispatch, not yet located | none |
| F09 | null DynamicVB lock/result followed by copy | randy DynamicVB result / GUI `rep movsd` proven | proven | whole GUI batch before/around VB use | none |
| F10 | invalid GUI tree/object key | low key observed / GUI comparator proven | medium | GUI tree entry | none |
| F11 | tiny invalid control target | callback/vtable/return producer U / CPU fetch | high class, origin U | exact typed dispatch TBD | none |
| F12 | coordinate-like data consumed as control target | C helper candidate; D producer U / CPU fetch | medium class | exact inner GUI/render dispatch TBD | none |
| F13 | caller-side fixed-array waypoint deserialization overrun; post-loop 30 comparison only controls zero-fill | Gamecode deserializer / `BinaryStream::operator>>(float*)` output initialization | high instruction, layout, and exact-caller Strategy D proof; malformed-count producer U | pre-write whole-message rejection after emitted/runtime gates | none |
| F14 | allocator is a secondary victim inside the Gamecode overwrite range in paired E24/E25 | Gamecode fixed-array overflow conditional on paired PID / ntdll allocator | strong paired-address evidence; pair PID U | repair F13 upstream; no allocator hook | none |
| F15 | ResourceManager request/list sentinel overwritten in paired E29/E30 | Gamecode fixed-array overflow conditional on paired PID / ResourceManager notifier | strong paired-address evidence; pair PID U | repair F13 upstream; retain notifier diagnostics only | none |
| F16 | N3 layout/argument confusion near RoomSpace site | proxy involvement unresolved / N3 `+0x15040` | medium correlation | RoomSpace off/on isolation, then typed init | none |
| F17 | native C++ precondition/throw in C client | N3/Gamecode / MSVC throw machinery | low without type | typed AO caller only after throw decode | none |
| F18 | repeated native C++ Gamecode/N3 throw in D | Gamecode/N3 / MSVC throw machinery | medium family | typed AO caller only after throw decode | none |
| F19 | native Vehicle/N3 throw | Vehicle initialization / MSVC throw machinery | low without type | typed vehicle init failure path | none |
| F20 | injected managed AOSharp exception | AOSharp/CLR / GUI boundary | proven external | excluded | none |
| F21 | NV-B invalid virtual-dispatch base `0x0A0A0000` | upstream producer U / NV-B `+0x154314F` | high consumer | same DrawIndexed submission boundary | none until cleanup proof |

## Rendering and GUI

### GUI rectangle construction

Reports: six. Crash-report logical `Utils.dll +0x72F1`; image RVA
`Utils.dll +0x82F1`. The instruction is the second floating-point input read in
the verified rectangle-plus-point helper. The caller is the old-client GUI
rectangle path at image RVA `GUI.dll +0x14C4AF`.

Root cause: GUI supplies an invalid rectangle or point operand. Null and
coordinate-like addresses recur. The exact producer above the GUI caller is
not present in the short stacks. Confidence in the consumer is **proven**;
confidence in the producer is **unresolved**.

Safe boundary: the helper itself, because the output pointer and ABI are known,
the failed read occurs before any external state mutation, and an empty
rectangle is a coherent neutral value. The current exception-only handler also
balances the x87 stack when the first `fld` succeeded before the second read
failed.

### randy draw-resource, render-state, and color sampling

Observed report logical offsets and image RVAs:

| Report offset | Image RVA | Observed failure | Interpretation |
| --- | --- | --- | --- |
| `+0x20A94` | `+0x21A94` | read `0x144` | low draw-resource object |
| `+0x24118` | `+0x25118` | wild read | unresolved predecessor to the guarded state instruction |
| `+0x2411A` | `+0x2511A` | wild read | impossible render-state entry |
| `+0x6B3A1` | `+0x6C3A1` | read `0x202` | low byte-color source |
| `+0x6B476` | `+0x6C476` | read `0x100` | low indirect color sample |
| `+0x6B51D` | `+0x6C51D` | read `0x100` | low packed-color source |

Root cause: invalid old-renderer scratch, resource, state, or color pointers are
consumed by randy. The repeated low values are structure-field offsets applied
to null or tiny bases, not legitimate resources. The exact fault consumers are
**proven**. Object allocation, release, and ownership history are not captured,
so the upstream lifetime producer remains **unresolved**.

The `+0x25118` event must not be assumed identical to `+0x2511A`; it is two
bytes earlier and is not covered by the current exact predicate.

### D3D7/NVIDIA submission

Observed driver image RVAs for NVIDIA driver identity
timestamp `0x696F2FCE`, image size `0x03C76000`, checksum `0x03D0ECBD`
(reported driver version `32.0.15.9186`):

| Report offset | Image RVA | Reports | Evidence |
| --- | --- | ---: | --- |
| `+0x172676C` | `+0x172776C` | 6 | `mov ebx,[eax+8]`, `EAX=0`, read `0x8` |
| `+0x1739009` | `+0x173A009` | 1 | `mov esi,[esi+8]`, `ESI=0`, read `0x8` |
| `+0x170B490` | `+0x170C490` | 1 | read `0x14` during deferred GUI/VB work |

F21 proves the driver family is not limited to that image. NVIDIA
`32.0.16.1074` faulted at actual `nvd3dum+0x154314F` while executing
`mov eax,[ecx]` with ECX/read address `0x0A0A0000`, through
`DDRAW -> D3DIM700 -> version.dll draw wrapper -> randy+0x219B9`.
The current NV-A filter correctly did not swallow it. The instruction/register
signature is proven; cleanup and post-fault device state are not.

For the first two signatures, the earliest currently proven AO boundary is the
one `DrawIndexedPrimitiveVB` dispatch at image RVA
`randy31.dll +0x219B4`. The wrapper can unwind the exception and discard that
one submission, but the driver has not returned normally. This is **exact
downstream containment with unproven recovery safety**, not proof that driver
locks, queues, or device state are coherent.

For `+0x170C490`, the failure occurs while a later vertex-buffer lock flushes
earlier queued work. Intercepting `Lock` and returning failure is not safe: AO
ignored the return and subsequently wrote through a null base. The current
whole-GUI-batch wrapper is the earliest evidenced AO cleanup boundary because
it can perform AO's conditional VB cleanup and material/state reset; successful
driver/device recovery remains unproven.

The driver is therefore most likely the consumer, not the original producer.
AO-side object and submission validation should replace these driver-specific
fallbacks when the required object layouts are proven.

### GUI null dynamic vertex buffer

Report logical `GUI.dll +0x14FF22`; image RVA `GUI.dll +0x150F22`.
The failed instruction is `rep movsd`. The destination `0xABF0` equals a null
lock base plus the proven vertex stride `0x1C` multiplied by base vertex
`0x624`. Randy returned a null lock result and GUI continued.

This is the strongest root-cause result in the corpus: unchecked null dynamic
VB output is **proven**. Current containment verifies the exact batch object,
frame locals, byte counts, state blob, viewport, index-buffer path, and native
cleanup functions before discarding the batch.

### GUI tree/object lookup

Report logical `GUI.dll +0x4DD00`; image RVA `GUI.dll +0x4ED00`.
The observed key is pointer `0x8`. The earliest stable boundary is the tree
entry at image RVA `GUI.dll +0x4F2EF`, before the comparator dereferences the
key. Returning the tree's own not-found sentinel reproduces the original
not-found contract.

Only the observed low-key family is proven. High but unreadable keys are not
validated on the optimized normal path.

## Invalid indirect control flow

### Tiny targets

Four reports place EIP at `0`, `2`, or `8`; one NVIDIA-backed report places EIP
at `5`. AO's text labels some execute AVs as writes, and E42's matching dump
records access type `8` (execute). E42 is proven; the other tiny-target records
are strong analogous inferences. These values are not module addresses and are
consistent with a null/tiny callback, vtable entry, or corrupted return address.

A global execute-AV handler cannot distinguish an initial bad indirect call
from a fault after the callee changed renderer or heap state. Generic resumption
would silently continue with unknown locks, allocations, registers, and object
lifetime. Root cause and safe action remain **unresolved**.

### Coordinate-like targets

The two observed EIP/target values are `0x41C80000` and `0x420C70A4`; execute
access is inferred because neither has a matching dump. The latter decodes
as approximately `35.11f`; other live registers and stack values decode as
plausible world or geometry values (`842`, `926.34375`, `1308.46887`, `3278`).
The frame chain is damaged and the report stops before a valid unwind.

The latest D-client event produced no minidump. A later filesystem inspection
found the D proxy absent, but proxy state at the event is unknown. Its stack
words can be classified conservatively against a borrowed D module map, but
they cannot prove the frame sequence or original indirect-call site. The
evidence supports **data-as-code/control-state corruption** with medium
confidence. It does not prove which object field was overwritten.

The existing new-client helper wrapper is only relevant when the fault occurs
inside one of its two verified callers. The latest report does not prove that
boundary.

## Gamecode deserialization, resources, and heap

### Gamecode fixed-array deserialization overrun

Five reports fault at crash-report logical `BinaryStream.dll +0xB1D`, actual
image RVA `BinaryStream.dll +0x1B1D`, while the recurring caller chain is
`Gamecode -> N3 -> Interfaces`.

Static bytes correct the earlier capacity hypothesis. The faulting function is
`BinaryStream::operator>>(float*)`, actual `+0x1B14..+0x1B37`. It initializes
the caller-supplied float destination to zero at `+0x1B1D` before invoking the
underlying read. The attempted address is the caller's output pointer, not a
BinaryStream buffer/cursor/capacity field.

The owning `SimpleCharFullUpdateIIR_t` waypoint loop first deserializes a signed
int32 count into `object+0x19C`, then writes three floats per entry beginning at
`object+0x1A0` with stride `0x0C`. The array has 30 entries and ends at
`object+0x307`. Its comparison with 30 after the loop only controls zero-fill;
it does not reject excessive counts. PID 29984 proves
object `0x26D6A930`, count `0x5A000000`, loop index `0x1E06E`, current
destination `0x26ED2FFC`, and next attempted destination `0x26ED3000`. This is
a fixed-array overwrite driven by an unvalidated or incorrectly decoded count.
PIDs 24200, 33032, and 34884 independently show the same instruction with ESI
equal to the attempted destination and the same runaway loop shape.

The same defect is statically present in D/old. The repair boundary is
immediately after count extraction and before the first entry write. A blind
clamp is not safe: leaving unread entry payload in the stream would
desynchronize subsequent decoding. The observed C/D N3 caller contract now
proves whole-message abort: failure deletes the partial object, returns null,
destroys the temporary stream, and stops parsing the supplied buffer. Exact
hook integration and runtime validation remain production blockers.
BinaryStream growth, capacity, terminator, alignment, and allocation changes
are not repairs for this family.

### ResourceManager worker

One worker-thread report reads null at crash-report logical
`ResourceManager.dll+0x2D84`, actual `+0x3D84`, in a notifier that dereferences
the request/list sentinel at `request+0`. Construction normally allocates a
0x18-byte self-linked sentinel. The worker has removed work under lock,
unlocked, and stored/AddRef'd the resolved resource into the request before
notification; this is request-local assignment, not proven global cache
publication. Skipping the notifier could strand waiters.

The interleaved E29/E30 pair supplies strong upstream evidence. The Gamecode
object is `0x25AE4F28`; its total waypoint write span begins at `0x25AE50C8`,
actual out-of-bounds corruption begins at `0x25AE5230`, and writes run to the
attempted boundary `0x25B2A000`. The ResourceManager request `0x25B287B0` lies
inside the OOB interval and its sentinel is zero. The two pasted
blocks lack an independently preserved common PID/timestamp, so the link is
conditional, but address-space and range agreement make the Gamecode overwrite
the leading cause of this observed ResourceManager fault.

### ntdll allocator

One worker-thread report faults in `ntdll` allocator code with
`MSVCR100 -> Interfaces -> Connection -> ACE` below it. In the interleaved
E24/E25 pair, Gamecode object `0x26D6A930` begins its total waypoint write span
at `0x26D6AAD0`; actual OOB corruption begins at `0x26D6AC38` and continues to
attempted boundary `0x26ED3000`. Allocator value `0x26E90214` lies inside that
OOB interval. Subject to the same missing paired PID, this is strong evidence
that the allocator is a secondary victim of F13, not a separate repair site.
Allocator exceptions must never be swallowed.

### N3 login/vehicle initialization

Two identical reports read `0x40000000` at crash-report logical
`N3.dll +0x14040`, image RVA `N3.dll +0x15040`, with proxy `VERSION.dll`,
`Vehicle`, N3, and Gamecode frames. The fault is only `0x14` bytes before the
old-profile RoomSpace-patched callsite at `N3.dll +0x15054`, so RoomSpace must
be independently disabled/enabled during reproduction before any other N3
mitigation is considered.
`0x40000000` is the bit pattern for `2.0f`, again suggesting a layout/type
confusion. The frames prove that unidentified code in the loaded VERSION module
was on the path; they do not identify a specific wrapper or prove it produced
the invalid value. No safe recovery contract has been established.

## Explicit exception families

Four `E06D7363` events are native C++ exceptions raised through `MSVCR100` from
Gamecode/N3 or Vehicle. Exception class/message and throw-site object are not
available in the short reports. They remain diagnostic-only; swallowing them
would bypass C++ unwinding and invariants.

The one `E0434352` event entered the CLR with AOSharp injection. It is excluded
from native-client compatibility decisions.

## Cross-family causality

No evidence currently places a renderer object inside a Gamecode overwrite
range, so renderer and Gamecode groups remain separate.

The paired non-renderer events are different. E24/E25 places allocator value
`0x26E90214` inside the out-of-bounds interval
`0x26D6AC38..0x26ED3000`; E29/E30
places ResourceManager request `0x25B287B0` inside interval
`0x25AE5230..0x25B2A000`. This is strong address-range causality conditional
on confirming that both halves of each interleaved paste share PID/time. It is
not a license to catch the downstream faults: repair and validate the Gamecode
producer first.

## Highest-value root-level boundaries

1. Proven AO GUI batch construction and dynamic-VB result handling.
2. Proven AO/randy draw-submission boundary before D3D7.
3. Proven GUI tree and rectangle construction entries.
4. A yet-to-be-proven AO render-object virtual/callback dispatch choke point.
5. The proven Gamecode count extraction and Strategy D owner boundary before
   the fixed-array loop, with unknown-caller and runtime promotion gates still
   required.
6. A diagnostic-only resource-worker consumption boundary with resource ID.

Driver RVAs remain fallbacks for one verified NVIDIA image. They are not a
portable compatibility architecture, and F21 is not a candidate for a new
driver catch until cleanup and post-fault state are proven.

## What this analysis does not justify

- process-wide access-violation suppression;
- process-wide indirect-call validation;
- arbitrary geometry count caps;
- clamping the Gamecode deserialization count while leaving unread payload;
- invented object layouts or reference-count rules;
- returning success after unknown driver or resource failures;
- globally enlarging BinaryStream allocations;
- treating C++ or CLR exceptions as pointer faults;
- claiming BinaryStream caused renderer corruption without identity evidence;
- claiming the latest `0x420C70A4` report passed through a current guard.
