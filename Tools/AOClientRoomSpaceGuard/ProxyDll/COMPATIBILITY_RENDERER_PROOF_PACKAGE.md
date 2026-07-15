# compatibility renderer proof package

Date: 2026-07-15

Status: **proof only; renderer implementation is not authorized**

## Decision

The long-term direction remains a compatibility renderer boundary, but the
current evidence does not support building the full proxy yet. The two clients
do not expose one simple interchangeable renderer entry point, complete COM
closure has not been proven, neither client's true frame/Present owner has
been identified, and in-process device recreation has no proven contract.

`version.dll` remains the bootstrap, profile selector, policy owner, evidence
collector, and recovery coordinator. Graphics compatibility belongs in
dedicated modules that own every graphics interface visible to AO. The current
exact address guards remain temporary, independently gated mitigations until
an upstream boundary demonstrably prevents their crash families.

The proof also finds one concrete non-renderer root repair: the apparent
BinaryStream/heap/ResourceManager cluster is led by a Gamecode deserializer that
validates its 30-entry count only after writing the entries. That upstream loop,
not BinaryStream allocation and not a ResourceManager exception catch, is the
repair target once whole-object rejection semantics are proven.

Proof labels in this document mean:

- **proven**: supported by exact current-client imports, symbols, bytes, or a
  matching dump;
- **inferred**: consistent with the evidence but missing an ownership or
  lifecycle link;
- **unresolved**: required evidence is absent;
- **blocked**: unsafe to implement until the named evidence exists.

## Client identity and graphics API exposure

| Client | Current binary identity | Proven graphics exposure | Conclusion |
|---|---|---|---|
| C/new | `anarchyonline.exe` SHA-256 `20AA1DAA31DE191CC5498CEF34F6A95DF8667D2C0A01B9212888F71882E3D387`; `N3.dll` SHA-256 `E242F4855DE93094161B619047CD838B6A3261BB53A5EB17065F60EDA5239168` | `Cheetah.dll` imports `d3d9!Direct3DCreate9`, D3DPERF entry points, and D3DX9_43 surface/texture/shader helpers. `randy31.dll` also imports `DDRAW!DirectDrawCreateEx` and `DirectDrawEnumerateExA`. A matching dump loads Cheetah, D3D9, D3DX9_43, randy31, DDRAW, and NVIDIA's 32-bit user driver together. | **Proven dual-path exposure.** C/new is not coverable by a D3D9-only proxy. Which live graph issued each sampled draw remains unresolved. |
| D/old | `anarchyonline.exe` SHA-256 `370C0670CC9CB46626EF24692376AAF492BB1787BAD8A1125365A6BE4F663862`; `N3.dll` SHA-256 `8C019EFD72D547879A06585B69147AB1546B9617A2FCE090E5863791AEC8B0BB` | `randy31.dll` imports `DDRAW!DirectDrawCreateEx` and `DirectDrawEnumerateExA`; `DisplaySystem.dll` additionally imports `DDRAW!DirectDrawCreate`. Matching dumps load DDRAW, D3DIM700, randy31, and NVIDIA's 32-bit user driver, with no Cheetah/D3D9 path. | **Proven legacy DirectDraw/D3D7 path.** Both DirectDraw creation origins must be accounted for. |

Additional current module identities used by this proof are:

| Module | C/new SHA-256 | D/old SHA-256 |
|---|---|---|
| `randy31.dll` | `66906D654DFFA5183EBAF3DCAEC08192B8440321320BBF08D6602DB9D8619FCB` | `9D9E0DA25AC6F8C7ECDCE7A47A2409575945966A29E3B77850F7C62033A04BE3` |
| `DisplaySystem.dll` | `45E3D789BC8F2F864C8A3EB522B9ADE88DF3A75864C0E931F9B2B85D0DA171E1` | `D4DD19F3277D70FF5FC0146B5474E8C5ABDD55F45E81288FE018192669046C91` |
| `GUI.dll` | `ECAA2C686DB3E0E17032AC69B14A14F030BC3185C51A10E04BEEC18BA3AC5306` | `E485384721E2FE13972E840DFB6A9FE29B1BA4EB71B42CC049E5097A570B6DE1` |
| `Cheetah.dll` | `F2FD2BB9019F75A1F1A521678F8ED55B24FE0EFB47F7A196625E0504CFA8BB90` | not present in the proven D/old path |

## COM escape-surface proof

### Proven roots and retained raw interfaces

The current static evidence proves three creation origins that a future proxy
must account for:

1. C/new `Cheetah.dll` through `Direct3DCreate9`;
2. C/new and D/old `randy31.dll` through `DirectDrawCreateEx`;
3. D/old `DisplaySystem.dll` through `DirectDrawCreate`.

D/old DisplaySystem uses its DirectDraw object only as a capability probe: it
queries `IDirectDraw7` and releases both interfaces. The retained legacy roots
originate in randy. The proven retained graph is
`IDirectDraw7 -> QueryInterface(IDirect3D7) ->
CreateDevice(IDirect3DDevice7)`, with `IDirectDrawSurface7` and
`IDirect3DVertexBuffer7` descendants retained in AO wrappers/raw fields. C/new
randy contains the homologous DirectDraw7/Direct3D7 graph.
Embedded IID evidence is exact for `IDirectDraw7`
`{15E65EC0-3B9C-11D2-B92F-00609797EA5B}` and `IDirect3D7`
`{F5049E77-4861-11D2-A407-00A0C90629A8}` in both randy profiles.

| Origin | Exact static evidence | Retention conclusion |
|---|---|---|
| D/old randy | `DirectDrawCreateEx` thunk `+0x622C6`; retained-root path `+0x438C0 -> +0x20DBA`, stores `IDirectDraw7` at render object `+8`; QI at `+0x43B83` stores `IDirect3D7` at `+4`; CreateDevice at `+0x43BDF` stores `IDirect3DDevice7` at `+0` | proven retained legacy graph |
| C/new randy | `DirectDrawCreateEx` thunk `+0x7D482`; callsites `+0x6AF54/+0x6B1F9/+0x6C29C`; homologous IID graph | legacy graph present; exact live-instance graph needs tracing |
| D/old DisplaySystem | `DirectDrawCreate` thunk `+0x82886`, call `+0x7C30E`, QI `IDirectDraw7` `+0x7C32A`, releases `+0x7C35A/+0x7C363` | capability/enumeration probe, not retained root |
| C/new Cheetah | `Direct3DCreate9` thunk `+0x288ACA`, sole static imported call `+0x13236C`; CreateDevice vtable call `+0x13244F` | this exact call creates a D3DDEVTYPE_REF software device; production hardware-device origin remains unresolved |

Public symbols and current call paths expose raw `IDirectDrawSurface7`,
`IDirectDrawPalette`, device, vtable, and vertex-buffer pointers through AO's
DisplaySystem/randy/GUI objects. The old draw hook at `randy31+0x219B4` itself
reads a raw device/vtable/vertex-buffer set. These pointers survive individual
calls and therefore escape into AO-owned state. This is enough to disprove the
idea that wrapping only a factory call or one draw method provides complete
ownership.

The proven legacy interface/method surface includes:

| Interface | Proven returned/retained or used methods |
|---|---|
| `IDirectDraw7` | QI, Release, CreateClipper, CreatePalette, CreateSurface, GetCaps, SetCooperativeLevel, SetDisplayMode, GetDeviceIdentifier |
| `IDirect3D7` | EnumDevices, CreateDevice, CreateVertexBuffer |
| `IDirect3DDevice7` | GetCaps, SetRenderTarget, DrawIndexedPrimitiveVB, GetInfo, plus typed state/transform/light/material/texture wrappers |
| `IDirectDrawSurface7` | QI, Release, Blt, GetAttachedSurface, GetPixelFormat, Lock, Unlock |
| `IDirectDrawClipper` / `IDirectDrawPalette` | returned by creation methods; later used-method inventory incomplete |
| `IDirect3DVertexBuffer7` | created, retained, locked/unlocked/released, and passed raw to DrawIndexedPrimitiveVB |

Cheetah RTTI proves compile-time visibility of `IDirect3D9`,
`IDirect3DDevice9`, resource/base-texture/2D-volume-cube-texture, surface,
swap-chain, vertex/index-buffer, vertex-declaration, vertex/pixel-shader,
state-block, query, and related child interfaces. That is type/use evidence, not
proof that every interface was live in the sampled process. No D3D9Ex evidence
was found.

Raw escape sites include the legacy render object fields at `+0/+4/+8`, randy
globals, `surface_t::GetSurfacePointer()` returning
`IDirectDrawSurface7**`, vertex-buffer/device pointers passed across modules and
frames, Cheetah's raw `IDirect3DVertexDeclaration9*` return, callback-returned
surfaces, and parent/container lookups.

### Required closure invariant

> No graphics COM interface visible to AO may bypass the proxy registry.

Factory interception is necessary but insufficient. Every method capable of
returning another interface, including `QueryInterface`, device/surface/buffer
creation, attached-surface lookup, parent-interface lookup, and equivalent D3D9
resource getters, must return a registered proxy rather than an underlying raw
pointer. Every accepted IID and every aggregation/identity case must be known.

Required legacy edges include DD7 create/duplicate/enumerate/get-surface calls,
D3D7 CreateDevice/CreateVertexBuffer, Device7 parent/render-target/texture
getters, and Surface7 QI/attached-surface enumeration/clipper/palette/DD parent
getters. Required D3D9 edges include root CreateDevice; every Device9
swap-chain/backbuffer/resource/shader/state/query create/get method; and child
GetDevice/GetContainer/GetSurfaceLevel/GetVolumeLevel/GetCubeMapSurface paths.
D3DX9 helpers can also return resources and must be audited. QI interception
cannot cover typed out-parameters that return an interface without an IID.

The registry contract is:

- one proxy identity per underlying COM identity, with stable
  `QueryInterface(IID_IUnknown)` identity;
- thread-safe lookup, insertion, reference accounting, and teardown;
- exact `QueryInterface`, `AddRef`, and `Release` behavior, including failed
  queries and interface tear-offs;
- no raw descendant returned from any proxied method;
- a device generation on every device-owned object;
- deterministic stale-wrapper rejection after reset/recreation;
- no registry entry removed while any exposed interface reference remains;
- no proxy callback into a destroyed underlying object.

Current verdict: **coverage is not proven**. The exact accepted IIDs, every
interface-returning method actually used by both clients, and all retained
storage sites have not yet been inventoried. A whole renderer proxy is blocked.
Every returned interface is interceptable in principle only if this complete
method graph is owned; the current `version.dll` owns none of that graph.

## Frame owner and submission proof

### Proven boundaries

| Candidate | Proven role | Why it is not yet the frame owner |
|---|---|---|
| D/old `randy31+0x219B4` | driver draw dispatch (`DrawIndexedPrimitiveVB` contract) | one draw submission, not a frame |
| D/old `GUI+0x150E17` | GUI batch helper/owner candidate | batch cleanup is not frame cleanup and driver state may already have changed |
| D/old `GUI+0x220E2..+0x22186` | recurring callback calls sprite FrameProcess then `DisplaySystem_t::Commit`; repeated stacks return through its epilogue at `GUI+0x22183` | one Commit caller, not proven sole frame/Present owner |
| C/new `GUI+0x14CA77` | helper called from patched sites `GUI+0x14CC5F` and `GUI+0x157234` | broad helper with unknown partial mutations; not a frame boundary |
| C/new `GUI+0x21DD5` | equivalent GUI path calls `DisplaySystem_t::Commit` at `+0x21E70` | AFCM can also call Commit directly, so this is not the sole owner |
| `DisplaySystem_t::Commit` | shared central export, actual C/new `+0x796F5`, D/old `+0x789BB` | central per-frame work boundary, but exact Flip/Present ownership and safe whole-function abort are not proven |
| D/old `AFCM+0x49C9/+0x4C2C` | recurring caller below the GUI/Display path | outer-frame candidacy is inferred; Present ownership is unproven |

The D/old recurring stack is
`GUI+0x15780A -> DisplaySystem+0x7BE26/+0x799B7/+0x79B6B ->
GUI+0x22183 -> AFCM+0x49C9/+0x4C2C`. It narrows the static search but does not
prove the true frame boundary. No equivalent complete C/new boundary is proven.

Whole-Commit recovery is unsafe. After its internal render call, C/new Commit
runs Cheetah timer update, resource loading, main-thread tasks, optional
graphics render, frame-rate tick, and memory-manager processing. Swallowing an
exception around the whole function would skip required maintenance. D/old
Commit resets render statistics, checks device loss, restores surfaces, opens/
processes/renders/closes the viewport, and resets DynamicVB. A recoverable
region must therefore be narrower than Commit and preserve its mandatory tail.

### Exact Flip/Present status

D/old DisplaySystem imports `Randy_t::Flip` at IAT RVA `+0x89944`; private
method `+0x1CDB` calls it at `+0x1CE0` and occupies display-vtable slot `+0x14`.
C/new retains the homologous legacy method at `+0x7982`, Flip call `+0x7987`,
IAT `+0x8CB78`, also vtable slot `+0x14`. In both clients, Commit instead calls
`[this+0x24]->vtable+0x04`; static evidence has not proven that dispatch reaches
the Flip method.

C/new Commit conditionally calls Cheetah export `Graphics_n::Render` actual
`+0xE6160`, but the exact `IDirect3DDevice9::Present` instruction inside Cheetah
has not been isolated. Therefore Flip implementations and the named Cheetah
render call are proven, while the true normal-frame Flip/Present owner remains
unresolved for both clients.

### Required submission state machine

A real frame transaction must track external side effects, not merely catch an
exception around an immediately forwarded call:

```text
validated-unsubmitted
    -> compatibility-queued (optional; still proxy-owned)
    -> submitted-synchronous
    -> driver-accepted
    -> presented
```

The state must advance only when the compatibility layer can prove the
transition. `__try/__except` around a forwarded draw does not roll the driver
back and therefore is not a transaction.

| Operation class | Safe pre-forward checks/defer rule | After forwarding | Recovery classification |
|---|---|---|---|
| AO object/list validation or compatibility-owned commands | typed bounds/lifetime/generation; defer only while all referenced data is owned/retained | not externally changed until underlying call runs | reject or discard local queue |
| state changes/transforms | validate state/resource/scene; defer only with exact query-compatible shadow state | device/driver state changed | poison on uncertainty |
| Lock | validate surface/rect/flags and conflicting lock; cannot generically defer because AO needs pointer/pitch | mapped pointer and runtime lock exist | never drop frame across outstanding lock |
| Unlock/upload | require matching tracked lock/generation/thread; cannot defer | consumes lock and may queue upload | irreversible; poison on fault |
| draw/indexed draw, Blt, Clear | validate resources/bounds/format/phase; defer only in complete ordered owned command list | work accepted/queued or destination changed | irreversible; poison on driver/unknown fault |
| Begin/EndScene | validate balanced phase/thread/generation; do not reorder | device phase changed | incomplete pair poisons generation |
| Flip/Present | validate generation, no outstanding lock, complete frame; defer only with owned final backbuffer | visible output and driver submission | irreversible; never claim rollback |
| resource/device creation | validate descriptors/limits; wrapper ready before exposure | identity/lifetime obligation exists | no fabricated success |
| QI/AddRef/Release | enforce registry identity/generation; never defer/reorder | reference/lifetime mutation; release-to-zero irreversible | exact COM semantics only |
| query/readback | validate output and flush/perfectly shadow queued state | synchronous observation exposed to AO | cannot replay a different history |

### Last-good-frame constraint

Repeating a last good image is possible only if the compatibility layer owns a
separate retained backbuffer and the present/copy path. Without that ownership,
an exact pre-Present AO/proxy failure with a proven-intact device may skip
Present and use a proven next-frame reinitialization path. Driver faults or
uncertain external state poison the device and require proven recovery or
restart; they may not continue into the next frame. The current proxy owns
neither facility.

## Recovery policy

| Failure state | Allowed action |
|---|---|
| pre-submit validation failure | abort the command/batch/frame at its proven owner |
| recoverable AO-side exception with intact unwind and known postcondition | abandon the frame and reset only fully tracked compatibility/AO state |
| driver exception or uncertain external submission state | poison the device generation and execute a proven destroy/recreate path |
| stack, heap, unwind, lock, or control-flow corruption | terminate and restart; do not continue |

SEH recovery is eligible only when all of these are proven for the exact site:

- the current thread is the captured renderer thread in one non-reentrant frame
  generation and a named supervised region;
- the exception is the exact approved AO-side AV, not stack overflow, fail-fast,
  heap corruption, C++/.NET, or a broad invalid-control-flow class;
- phase proves validation or compatibility-owned unsubmitted processing;
- no underlying COM/driver call is in flight or has begun mutation;
- lock depth is zero and no mapped pointer is exposed;
- Begin/EndScene and every tracked state machine are balanced;
- no creation, destruction, reference mutation, or pending release has begun;
- ESP/control records, nonvolatile registers, FPU/SSE state, and language
  cleanup can unwind exactly;
- the local command list/shadow state can be discarded deterministically;
- the mandatory Commit postlude remains executable.

Faults in NVIDIA, DDRAW, D3DIM700, D3D9, heap/runtime code, or during/after a
forwarded mutating COM method are never drop-frame faults. Invalid EIP is
eligible only when Windows reaches the boundary with intact unwind state and
the phase tracker proves pre-submit execution; arbitrary EIP/ESP rewriting is
not recovery.

The current C/new helper filter fails these gates: it accepts a broad AV class
based on non-executable EIP without proving phase, unwind, ownership, or device
state. It must remain off by default.

## Device recreation proof

C/new exposes native D3D9 reset orchestration: Cheetah exports device-reset
callback registration/removal (`+0xE1A20/+0xE1450`), `Graphics_n::Reset`
`+0xDF990`, Initialize `+0xE3060`, and Shutdown `+0xE2D50`; Reset reaches
preparation/internal paths `+0x149780 -> +0x15EFA0`. This proves an in-process
reset facility, not complete device destruction/recreation or proxy-safe
rebinding.

D/old exports Randy `IsDeviceLost +0x41DC0` and `RestoreData +0x41E9F`, and
DisplaySystem invokes RestoreAllSurfaces at `+0x79A66` when loss is detected.
`RestoreData` only advances a restore generation. Every statically proven
Device7 creation call remains inside Randy Initialize overloads; no D/old
in-process recreation entry is proven.

No current evidence proves complete device destruction/recreation and rebinding
for either client without restarting AO. Before implementation, the proof must
identify:

- the owner of the window, DirectDraw/D3D root, device, primary/back surfaces,
  depth buffers, palettes, textures, vertex/index buffers, and state caches;
- release order and all AO callbacks/registries that retain those objects;
- the lost/cooperative-level/reset or destroy/create path actually used by the
  client;
- generation invalidation for every retained wrapper;
- restoration of render state and content;
- first successful present and subsequent-frame integrity.

On a driver/runtime fault, poison is monotonic for that device generation:
stop forwarding, skip further Flip/Present, perform no Release/Reset/recreation
inside the exception filter, unwind first, and schedule recovery only from a
known outer control point with zero locks/in-flight calls and balanced scene
state. A later successful-looking call cannot clear poison.

Current verdict: **device poison is a valid policy state; C/new native reset is
a candidate recovery primitive; complete in-process recreation is blocked for
both clients.** Until full ownership and rebinding proof exists, poisoning ends
in a controlled client restart rather than guessed partial reset.

## BinaryStream/Gamecode proof correction

The recurring family previously described as a BinaryStream capacity/growth
failure is now statically and dump-proven to be a caller-side fixed-array
deserialization overrun.

The C/new `BinaryStream.dll` SHA-256 is
`FCA5131AE23D538BEF37A3A0656893620143731F4874336E41256C28F2A3B5F1`;
the C/new `Gamecode.dll` SHA-256 is
`60E5C2073FD488EC01579CD23BA7C87E3881228815EC037954D5CE3DBF64B5B4`.

`BinaryStream::operator>>(float*)` spans actual
`BinaryStream.dll+0x1B14..+0x1B37`. It receives `this` in ECX and one `float*`
stack argument, returns `this`, and executes `ret 4`. At `+0x1B1D` it executes
`fstp dword ptr [ESI]` after loading ESI from that argument: it writes `0.0f`
to the caller's output slot before invoking the underlying stream read. The
fault address is therefore the caller-supplied destination, not the stream
buffer cursor or capacity.

```text
+1B17 fldz
+1B1A mov esi,[ebp+8]
+1B1D fstp dword ptr [esi]   ; repeated fault, before stream read
+1B26 call +0x193F           ; underlying read
+1B30 fstp dword ptr [esi]   ; store decoded value
+1B35 ret 4
```

The owning C/new Gamecode deserializer spans actual
`Gamecode+0x7A41E..+0x7AAEE`; the nearest public symbol name is not reliable.
Its defective block around `Gamecode+0x7A90A..+0x7A962`:

1. deserializes a count into `object+0x19C`;
2. immediately loops that count;
3. writes three floats per entry to a destination beginning at
   `object+0x1A0`, stride `0x0C`;
4. checks the `0x1E` (30-entry) limit only after the loop.

PID 29984 ties the machine state to this loop: object `0x26D6A930`, decoded
count `[object+0x19C] = 0x5A000000`, loop index `0x1E06E`, current destination
`0x26ED2FFC`, and next attempted float destination `0x26ED3000`. The loop walks
past the fixed destination until a page boundary stops it.

Four dumps independently prove ESI equals the attempted caller output at the
same instruction:

| PID | ESI/attempted output | Gamecode object | Parsed count | Loop index |
|---:|---:|---:|---:|---:|
| 29984 | `0x26ED3000` | `0x26D6A930` | `0x5A000000` | `0x1E06E` |
| 24200 | `0x25634000` | `0x252CE990` | `0x5A000000` | `0x48711` |
| 33032 | `0x25B2A000` | `0x25AE4F28` | `0x1CB95` | `0x5BEF` |
| 34884 | `0x22AB0000` | `0x22AAB3E0` | `0x1CB95` | `0x635` |

Permanent repair boundary: validate the decoded count immediately after the
integer extraction and before the first entry write. The repair cannot simply
clamp to 30 and continue, because unread entry payload would remain in the
stream and desynchronize subsequent decoding. Required proof is the enclosing
object/message failure contract: reject/consume/quarantine the whole malformed
object without publishing partial state, and release it exactly once.

The following earlier proposal is withdrawn for this crash family: changing
BinaryStream capacity, growth, terminator, alignment, or allocation policy.
Those properties may be studied only for a separately proven stream-capacity
bug.

### Strong downstream-corruption links

Two interleaved pasted event pairs place later fault objects inside the exact
Gamecode overwrite range:

- E24/E25: object `0x26D6A930`, overwrite begins `0x26D6AAD0` and continues to
  attempted boundary `0x26ED3000`; the simultaneous ntdll allocator event has
  `EAX=0x26E90214`, inside that interval.
- E29/E30: object `0x25AE4F28`, overwrite begins `0x25AE50C8` and continues to
  attempted boundary `0x25B2A000`; the simultaneous ResourceManager notifier
  request is `0x25B287B0`, inside that interval, with its sentinel already zero.

The blocks are textually interleaved and their address spaces align exactly,
which is strong evidence that the allocator and ResourceManager faults are
secondary victims of the same overwrite. The original pasted records do not
preserve an independent PID/timestamp identity for both halves, so this remains
conditional rather than absolute proof. It is nevertheless strong enough to
reverse the earlier “no causal link” assessment and to prioritize the one
upstream Gamecode repair before any ResourceManager mitigation.

## ResourceManager publication proof

The C/new `ResourceManager.dll` SHA-256 is
`EEAFB26FBAAEA634FC5D2CC97D75EE28934D741F90E6AFEFDB67CF4AAB3BEF50`
(PE timestamp `0x647A0C47`, image size `0x11000`, checksum `0x180E3`).

The observed function at actual `+0x3D7B..+0x3DB4` is a thiscall notifier
dispatcher (`ECX=request/list owner`, two stack arguments, `ret 8`). At
`+0x3D82` it loads `request+0`; the fault at `+0x3D84` dereferences that value,
which is null in the crash. Static construction at `+0x4D5B -> +0x42DD`
allocates a `0x18`-byte circular-list sentinel and initializes next/previous to
self; allocation failure throws. The notifier iterates nodes, checks a
weak/cancel token through `+0x346B`, and invokes each callback stored at
`node+0x10` with context at `node+0x14`.

The only proven caller is worker job method `+0x3F97`, call at `+0x40F6`
(return `+0x40FB`). The worker removes the request under its job lock at
`+0x400B/+0x4010`, unlocks, resolves the resource, stores/AddRefs it into the
request at `+0x40B9..+0x40BD`, then notifies. This proves request-local resource
assignment, not global cache publication. No lock or reference increment
protecting the request itself through notification is visible. A null sentinel
means the request was corrupted or was destroyed/cleared after assignment; a
lifetime race is plausible but not yet proven.

E29/E30 supplies strong paired-event evidence that this particular request lies
inside the Gamecode overwrite interval. That makes upstream memory corruption
the leading explanation for this observed null sentinel. It does not prove
that every future ResourceManager fault has the same producer, and the missing
independent PID/timestamp pair remains a provenance gap. Skipping notification
can strand waiters, so there is still no safe downstream recovery.

Required proof before containment is:

```text
job creation -> queue ownership -> worker consume -> allocation/load
-> result ownership -> publication/cache insertion -> waiter notification
-> success/failure release
```

For every edge, identify the lock, reference owner, destruction/clear site,
failure result, retry rule, and whether an exception may leave a waiter or cache
entry live. Raw-returning from the fault or swallowing the worker exception is
blocked because it can leak, double-release, race destruction, or leave waiters
permanently blocked.

## Implementation authorization gates

The finite order is:

1. prove the true frame owner and Present boundary for each client;
2. inventory every created, queried, returned, retained, and released graphics
   interface/IID;
3. implement and unit-test the COM identity/lifetime registry in isolation;
4. proxy all used interface-returning methods and prove the no-raw-interface
   invariant;
5. add pre-submit typed validation;
6. add a recoverable frame model only around compatibility-owned queued state;
7. prove device poison/destroy/recreate or fall back to controlled restart;
8. repair the proven Gamecode count-before-loop bug at a whole-object rejection
   boundary;
9. prove ResourceManager publication/failure ownership before changing it;
10. retain exact existing guards until upstream tests prevent each family;
11. remove driver-RVA hooks only after the proxy prevents their exact
    reproductions across the hardware/driver soak matrix.

No later step is authorized by evidence from an earlier crash family. In
particular, the Gamecode proof does not justify ResourceManager recovery, and a
wrapped draw call does not justify frame or device recovery.

## Proof still required

- exact C/new and D/old accepted IID and interface-return inventory;
- every raw interface storage/escape and release site;
- canonical COM identity behavior across all returned interfaces;
- true frame-begin, submission, and Present owners for both clients;
- reversible/irreversible classification at each actual graphics method used;
- native device-loss or destroy/recreate sequence;
- Gamecode enclosing object/message rejection and stream-consumption contract;
- ResourceManager job/publication/waiter/refcount state machine;
- deterministic tests and long hardware/driver soaks for each promoted repair.

Until these are complete, this package authorizes static analysis,
instrumentation that does not alter behavior, and isolated unit-test
foundations only. It does not authorize a whole renderer proxy or catch-all
exception continuation.
