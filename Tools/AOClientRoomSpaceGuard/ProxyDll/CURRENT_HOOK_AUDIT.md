# Current `version.dll` hook audit

This is a source-to-artifact audit of the current working tree.  “Available in
source” does not mean “installed in the client that crashed.”  Runtime activity
requires the correct proxy build to be loaded, profile/module checks to pass,
and every preceding installer stage to succeed.

RVA convention: all hook sites below are actual PE image RVAs.  AO's crash text
normally reports `actual RVA - 0x1000`.

## Executive findings

1. The DLL is a 17-export system `version.dll` forwarder plus a detached
   compatibility worker.  The forwarding boundary is structurally separate,
   but behavioral features are not independently selectable.
2. Installation is serial and RoomSpace is mandatory.  A RoomSpace failure
   prevents every later guard.  On the old profile, a rectangle install can
   survive a later randy transaction failure.  This prevents clean A/B
   isolation.
3. The strongest current guards are exact AO-side, pre-mutation recoveries:
   Utils rectangle, randy low resource/state/color cases, GUI low-key not-found,
   and DrawIndexed pre-call validation.
4. Post-fault NVIDIA continuation and whole-GUI-helper continuation are not
   proven safe renderer recovery.  Driver locks/state, FPU/SSE state, and all
   native cleanup are not known.
5. The new-client two-call patch has no thread suspension and can leave a
   partial/active patch if post-write verification or protection restoration
   fails.
6. Successful hooks have process lifetime.  There is no detach unhook, no safe
   hot unload, and several handler handles are discarded.
7. `Log()` holds a process critical section and calls `FlushFileBuffers()` for
   every emitted line.  Calling it from fault recovery can stall the renderer or
   deadlock during heap/loader corruption.
8. Current optimized source removed normal-path `VirtualQuery` calls from the
   old draw/rectangle hot paths and removed the TnL HAL `2` to HAL `1` selector
   rewrite entirely. The remaining draw wrapper is not zero overhead, so live
   same-scene A/B attribution is still required.

## Entry, timing, and installation order

`DllMain` activates only when the process basename is `AnarchyOnline.exe`.  It
starts a detached worker; a `CreateThread` failure is silent.  Outside loader
lock, the worker performs:

1. initialize the file logger;
2. arm the dedicated exception-only `randy+0x25118` vector guard;
3. install the unhandled-exception dump filter;
4. wait up to 30 seconds for `N3.dll` only;
5. identify the N3 profile by exact SHA-256;
6. install RoomSpace on all profiles;
7. for C/new: install the two-site new-client GUI draw wrapper;
8. for D/old: install the Utils rectangle VEH, then the combined randy draw/
   batch/tree transaction.

GUI, Utils, randy31, D3DIM700, DDRAW, and NVIDIA are not waited for.  Their
presence at the instant their installer runs is an assumption.  A late module
load is treated as unsupported rather than retried.

Exact N3 profiles are:

| Profile | N3 SHA-256 | Graphics branch |
|---|---|---|
| C/new | `E242F4855DE93094161B619047CD838B6A3261BB53A5EB17065F60EDA5239168` | RoomSpace + new GUI wrapper |
| D/old | `8C019EFD...B0BB` | RoomSpace + rectangle + old renderer transaction |

Only N3 has a whole-file hash gate.  Other AO modules use exact local byte and
address checks.  NVIDIA is identified at fault time by module allocation base,
PE timestamp/size/checksum, exact RVA, instruction bytes, registers, and access
address; source does not verify NVIDIA's file path/hash/version resource.

## Hook inventory summary

| ID | Hook | Module / actual RVA | Mechanism | Classification |
|---|---|---|---|---|
| H0 | VERSION forwarding | proxy exports | 17 lazy forwarded exports | correct core boundary |
| H1 | RoomSpace collision | N3, five profile RVAs | five `E8` replacements to generated RX wrapper | proactive mitigation; independent/suspect for F16 |
| H2 | new GUI draw helper | GUI `+0x14CC5F`, `+0x157234` | two `E8` replacements | broad containment; unsafe cleanup/rollback |
| H3 | rectangle add | Utils `+0x82EC/+0x82F1` | priority-first VEH, no normal-path patch | strong exact L1 recovery |
| H4 | renderer selector | removed | AO's original selector load remains untouched | retired FPS regression |
| H5 | DrawIndexedPrimitiveVB | randy `+0x219B4` | indirect call -> wrapper | L2 preflight; L4 driver containment |
| H6 | GUI render batch | GUI `+0x152E49` | call -> whole-batch wrapper | exact but experimental L4 cleanup |
| H7 | GUI tree find | GUI `+0x4F2EF` | prologue -> JMP/thunk/trampoline | strong narrow L1 prevention |
| H8 | randy draw resource | randy `+0x21A94` | VEH context unwind | strong exact L1 recovery |
| H9a | randy corrupt state vector | randy `+0x25118` | early process-lifetime VEH; resume `+0x25147` | strong exact L1 whole-vector recovery |
| H9b | randy impossible state id | randy `+0x2511A` | old-profile VEH; resume `+0x2512F` | strong exact L1 one-entry recovery |
| H10 | randy byte color | randy `+0x6C3A1` | VEH resume | strong exact L1 fallback |
| H11 | randy indirect color | randy `+0x6C476` | VEH resume | strong exact L1 missing-sample path |
| H12 | randy dword color | randy `+0x6C51D` | VEH resume | strong exact L1 fallback |
| H13 | crash dump filter | process-wide UEF | minidump then chain/search | diagnostic only |

## Detailed inventory

For template fields that do not apply: VEH-only H3/H8-H12 overwrite no bytes
and have no trampoline (`N/A`); H13 is a filter, not a callsite; compiled thunk
addresses are runtime-ASLR proxy addresses and are identified by symbol/RVA in
the inspected artifact; H1's generated wrapper address is chosen by
`VirtualAlloc` at runtime.

| Hook | Replacement/trampoline ownership |
|---|---|
| H1 | runtime 86-byte RX wrapper; no original trampoline because complete calls are redirected |
| H2 | compiled `NewClientGuiDrawHelperGuard`; original helper is invoked by the guarded body on its normal path; no relocated callsite trampoline |
| H3 | N/A, VEH redirects only an exact exception context to compiled recovery epilogues |
| H4 | removed; no proxy target or trampoline remains |
| H5 | compiled `GuardedDrawIndexedPrimitiveVb`; original target resolved from device vtable slot 0x20 |
| H6 | compiled `GuardedGuiRenderBatchThunk`; guarded body calls the verified original GUI target |
| H7 | compiled entry thunk plus `GuiTreeFindTrampoline`, which replays five overwritten bytes then jumps `GUI+0x4F2F4` |
| H8-H12 | N/A, VEH context transforms at exact fault sites |
| H13 | N/A, process UEF |

### H0 — VERSION proxy forwarding

| Field | Audit |
|---|---|
| Entry points | All 17 names/ordinals in `version_proxy.def` |
| Resolution | `InitOnce` loads `%SystemRoot%\System32\version.dll` and resolves all exports |
| ABI | Each wrapper uses the corresponding Windows API ABI; no behavioral hook is on the forwarding path |
| Failure | If one export fails resolution, the shared resolution state fails and wrappers cannot forward successfully |
| Lifetime | Real system VERSION stays loaded; no detach `FreeLibrary` |
| Validation | Package self-test checks 17 exports and four functional APIs, not hook ABIs |
| Risk | Forwarding can be called before logging initializes; no true forward-only policy currently bypasses the worker |

### H1 — RoomSpace collision wrapper

| Field | Audit |
|---|---|
| Sites | C/new N3 `+0x157BC,+0x16144,+0x168E2,+0x168F6,+0x16F98`; D/old `+0x13F2E,+0x148B6,+0x15054,+0x15068,+0x1570A` |
| Inferred function | five collision calls to `PosToRoom` |
| Original bytes | each complete five-byte `E8 rel32`, computed and verified to target C `N3+0xE095` or D `N3+0xC8AA` |
| Replacement | complete five-byte `E8 rel32` to one generated 86-byte wrapper |
| ABI | inferred x86 `thiscall`: ECX=`this`, two stack arguments, callee `ret 8`, EAX result |
| Wrapper logic | preserve EBP/ESI; copy ECX to ESI; dynamic-cast `[this+0x58]`; call `GetInsideCell(arg1)`; return EAX=0 for null cast/negative cell; otherwise call `GetZones(this)` and return `table[cell]` |
| Helper RVAs | C: dynamic cast `+0x3AAEA`, RTTI `+0x5F894/+0x5F8EC`, GetInsideCell `+0x154F8`, GetZones `+0xDEF4`; D: `+0x3894A`, `+0x5B80C/+0x5B864`, `+0x13C6A`, `+0xC709` |
| Register/flags state | ESI/EBP preserved; EBX/EDI untouched; EAX/ECX/EDX and flags caller-volatile; no x87/SSE instructions |
| Thread model | allocate 4 KiB RW, write, change RX, flush; suspend up to 256 other threads with stable repeated snapshots; recheck after pages are writable |
| Transaction | all five calls must match; patch all; verify/cache-flush; restore all on a confirmed failure |
| Failure edge | if patch succeeded but page-protection restore or thread resume cannot be confirmed, code remains active and installer returns false; wrapper is retained when safe free cannot be proven |
| Lifetime | generated RX page and five patches remain until process exit; no unhook |
| Matching crashes | no direct match in the 21 crash families; F16 occurs only 0x14 bytes before old patched site `+0x15054` and requires A/B isolation |
| False-positive/corruption risk | positive out-of-range cell and zone-table validity are not checked; identical ABI is assumed at all five calls; mandatory install can both cause and mask unrelated behavior |

This hook should be a separately controlled feature and default off during F16
causality work.  It must not gate unrelated crash guards.

### H2 — new-client GUI draw-helper wrapper

| Field | Audit |
|---|---|
| Sites | GUI `+0x14CC5F` and `+0x157234`, both calls to `GUI+0x14CA77` |
| Original bytes | `{E8 13 FE FF FF}` and `{E8 3E 58 FF FF}`; helper prefix, relocated SEH cookie, body bytes, and `ret 18` epilogue also verified |
| Replacement | each whole five-byte call becomes `E8 rel32` to `NewClientGuiDrawHelperGuard` |
| ABI | inferred `void __thiscall(ECX self, six DWORD args)`, callee `ret 0x18`; naked thunk creates EBP frame, forwards six args and ECX to a stdcall C++ body, restores ESP/EBP, then `ret 0x18` |
| Register/flags state | compiler wrapper is expected to preserve EBX/ESI/EDI/EBP; EAX/ECX/EDX and flags volatile; no explicit x87/SSE save or recovery |
| Predicate | catch any `C0000005` inside the helper when the exception EIP is not executable according to `VirtualQuery` |
| Action | skip the entire helper and return to its caller |
| Matching crash | C/new `0x41C80000`; the reported `GUI+0x14BC64` maps to actual `+0x14CC64`, exactly after the first patched call |
| Thread model | no thread suspension and no check that another thread is executing either site |
| Rollback | no complete transaction: post-write cache-flush, protection-restore, or verification failure returns false without guaranteed restoration of both original calls |
| Lifetime | no unhook |
| Risk | broad non-executable-EIP predicate can swallow an unrelated fault anywhere in a large helper; native cleanup, renderer locks, FPU/SSE state, and partial object mutations are unknown; every helper call pays wrapper+SEH cost |

This is containment-only and should be off by default until the exact inner
indirect call and a skip/cleanup contract are proven.  It is not evidence that
the D/old `0x420C70A4` event shares this site.

### H3 — old-client Utils rectangle VEH

| Field | Audit |
|---|---|
| Normal-path patch | none; GUI IAT slot `+0x1A83D0` remains pointed at `Utils+0x82E6` |
| Verified call/target | GUI caller prefix `+0x14C4A1`, indirect call operand at `+0x14C4AB`, and Utils bytes `{55 8B EC 8B 55 0C D9 02 8B 45 08 D8 01}` |
| ABI | inferred Rect pointer return in EAX, two stack args, frame pointer, callee `ret 8` |
| Fault sites | read AV only at `Utils+0x82EC` before `fld [edx]`, or `Utils+0x82F1` after the x87 load and at `fadd [ecx]` |
| Predicate | exact site/access=read; readable EBP frame; writable 16-byte result object |
| Action | write an empty rectangle; before-load path reconstructs epilogue; after-load path first executes `fstp st(0)` then reconstructs epilogue; EAX=result; `ret 8` |
| State preservation | x87 depth is explicitly balanced for the post-load site; caller-volatiles/flags are not restored; no SSE use |
| Matching crashes | all six F01 records, whose reported logical `+0x72F1` maps to actual `+0x82F1` |
| Handler/lifetime | priority-first process VEH; its handle is discarded, so it cannot be removed after later failure or at detach |
| Risk | `VirtualQuery`, result writes, and logging occur in exception context; handler reentrancy/deadlock remains possible; stale README claims of an IAT replacement are incorrect—the dormant normal wrapper is not installed |

This is the best current example of a narrow recovery with an explicit neutral
value and correct x87 balance.

### H4 — old renderer selector normalization (removed)

| Field | Audit |
|---|---|
| Site/function | former randy `+0x43B99` selector load |
| Original bytes | left untouched |
| Replacement | none |
| ABI/state | not applicable |
| Predicate | not applicable |
| Action | AO uses the renderer selected in its launcher |
| Transaction | absent from the old-renderer transaction |
| Risk | no proxy selector overhead or global renderer-mode change |

The mutating selector path and its dormant helper code were deleted. A
same-scene no-proxy FPS baseline is still required for the remaining hooks.

### H5 — old DrawIndexedPrimitiveVB wrapper

| Field | Audit |
|---|---|
| Site | randy `+0x219B4` |
| Original bytes | complete six-byte `FF 91 80 00 00 00` (`call [ecx+0x80]`) |
| Replacement | five-byte `E8 rel32` to wrapper plus one `90` |
| ABI | `HRESULT WINAPI`/stdcall, eight DWORD arguments, callee `ret 0x20`; device vtable slot `[0x20]` corresponds to byte offset `0x80` |
| State | compiler ABI preserves EBX/ESI/EDI/EBP; EAX return; ECX/EDX/flags volatile; no explicit x87/SSE recovery |
| Pre-call predicates | reject low device/VB; integer overflow/invalid index span; unreadable device/vtable/VB or first/last index; low vtable/draw target; exact execute AV at the initial target |
| Pre-call action | skip one draw and return `S_OK` |
| Driver predicate | NV-A exact PE timestamp `0x696F2FCE`, image size `0x03C76000`, checksum `0x03D0ECBD`; exact instruction bytes and state at `+0x172776C` (EAX=0/read8) or `+0x173A009` (ESI=0/read8) |
| Driver action | catch the exact AV, return `S_OK` |
| Normal-path work | wrapper+SEH, direct probes, vtable resolution, first/last index reads; no normal-path `VirtualQuery` or logging |
| Transaction | exact bytes, protected write, cache flush, verify; reverse exact bytes on later old-transaction failure |
| Matching crashes | F05/F06 only for NV-A; F21 reaches this wrapper but NV-B correctly passes through |
| Risk | `S_OK` hides a missing draw; direct probes may reject unusual valid proxy objects; post-fault driver locks/device state can be poisoned; no native driver cleanup or FPU/SSE repair; hot-path overhead remains |

The AO-side preflight is a credible Level-2 boundary.  The post-driver AV catch
is experimental Level 4 and must not be broadened by RVA alone.

### H6 — old whole-GUI render-batch wrapper

| Field | Audit |
|---|---|
| Site/target | GUI `+0x152E49`, complete call to `GUI+0x150E17` |
| Original bytes | `{E8 C9 DF FF FF}` |
| Replacement | complete five-byte `E8 rel32` to `GuardedGuiRenderBatchThunk` |
| ABI | custom: ECX=batch object, EAX=positive batch span, one caller-cleaned stack argument; original returns `C3`; thunk forwards stack arg/EAX/ECX to stdcall body and returns `C3` without consuming caller arg |
| State | compiler preserves nonvolatiles; volatile registers/flags not promised; no explicit x87/SSE recovery |
| GUI predicate | exact write AV at `GUI+0x150F22`, bytes `F3 A5`, ECX=`0x1C`, EDX=0, EAX=batch, EBX positive, exact null-base/index/state/frame/viewport invariants and readable source |
| NVIDIA predicate | NV-A exact identity and actual `+0x170C490`, bytes `8B 80 10 00 00 00`, EAX=4/read14, plus validated batch/state invariants |
| Recovery | obtain replacement DynamicVB using private randy helpers; conditionally free the owned heap index buffer with `GUI+0x1739C6`; reset material and three state blobs using private randy helpers |
| Transaction | per-site exact rollback; third patch in the combined old transaction |
| Matching crashes | F09 and exact F07 |
| Risk | private helper ABIs and ownership are version-pinned/inferred; cleanup runs on the faulting renderer thread; driver state may already be corrupt; an exception during cleanup escapes; no explicit FPU/SSE repair; every GUI batch pays wrapper+SEH cost |

This has the strongest available post-fault cleanup evidence, but it remains an
experimental recovery until subsequent-frame/device integrity is proven.

### H7 — old GUI tree-find entry hook

| Field | Audit |
|---|---|
| Site | GUI `+0x4F2EF` |
| Original bytes | complete prologue `{55 8B EC 51 56}` |
| Replacement | five-byte `E9 rel32` to thunk; compiled trampoline replays all five bytes and jumps to `GUI+0x4F2F4` |
| ABI | inferred thiscall ECX=tree, stack `(output,key)`, callee `ret 8` |
| Predicate | key `<0x10000`; tree+4 readable; output writable |
| Action | copy the native not-found sentinel into output and return not found; otherwise call the trampoline/original |
| State | normal compiler ABI preserves nonvolatiles; volatile registers/flags not promised; no x87/SSE change |
| Matching crash | prevents the observed key=8 path upstream of F10 |
| Risk | every valid lookup pays JMP/thunk/C++/trampoline overhead; high unreadable keys are not guarded; invalid low tree/output falls through and may still fault; README claims about arbitrary unreadable keys are stale |

### H8–H12 — exact randy VEH recoveries

A dedicated priority-first VEH is armed before dump setup, N3 hashing, profile
selection, and old-client patch installation. It handles only the exact
`randy+0x25118` read AV after dynamically verifying a committed MEM_IMAGE,
PE32/i386 headers, the fault RVA, bytes through the `+0x25147` resume boundary,
and native vector-loop provenance. It remains registered for process lifetime.
The later general randy VEH handles H8/H9b/H10-H12 and is removed if the combined
randy patch transaction fails; its handle is discarded after success, so no
detach removal is possible.

| Hook | Exact fault and verified boundary | Predicate | Context/action | Matching family | State/risk |
|---|---|---|---|---|---|
| H8 draw resource | `randy+0x21A94` | EAX `<0x10000`, access==EAX, readable EBP frame/return | EAX=0; EIP=caller return; ESP=EBP+0x20; EBP=prior; skip whole six-arg callee | F03 | exact bytes prove no preceding nonvolatile/FPU/SSE mutation; manual context unwind is safe only for this profile |
| H9a corrupt state vector | `randy+0x25118`, resume `+0x25147` | read access==EDI, `[ESP]=0x0A`, writable frame locals, readable ESI vector fields, `[EBP-8]=[EBP-4]*16`, `[ESI+0x14]+offset=EDI`, coherent next 20-byte-vector bounds | pop one pushed DWORD; EAX=0; skip the entire first 16-byte vector | F04 `+0x25118` | exception-only dynamic PE/fingerprint lookup; exact report proves offset `0x20`, index `2`, and access==EDI; upstream lifetime producer remains unresolved |
| H9b impossible state id | `randy+0x2511A`, resume `+0x2512F` | EAX>`0x400`, observed stack DWORD/argument `0x0A` | pop one pushed DWORD; EAX=0; resume native loop after one entry | F04 `+0x2511A` | integer path; the `0x400` value is an exact crash predicate, not a proven general table-size contract |
| H10 byte color | `randy+0x6C3A1`, resume `+0x6C3AC` | low EAX | EAX=EBX=EDI=0; resume after byte sample sequence | F02 | integer only; access-address equality not checked |
| H11 indirect color | `randy+0x6C476`, resume `+0x6C478` | nonzero low ECX, access==ECX, EDI nonzero power of two | ECX=0; enter native missing-sample branch | F02 | narrow and root-like for known sample state |
| H12 dword color | `randy+0x6C51D`, resume `+0x6C51F` | low ESI | ESI=0; resume before native alpha OR | F02 | integer only; access-address equality not checked |

Flags are not restored, but each resume address was selected around the exact
verified integer sequence.  No x87/SSE repair is required by those verified
instructions. The early H9a lookup and byte checks run only after an AV, never
on the normal frame path. As process-wide first-chance handlers, the rectangle
and randy VEHs still inspect every AV; any logging or secondary memory fault
from inside a handler can recurse.

### H13 — diagnostic unhandled-exception filter

| Field | Audit |
|---|---|
| Mechanism | `SetUnhandledExceptionFilter`, retaining one previous filter |
| Output | one `MiniDumpNormal | MiniDumpWithDataSegs | MiniDumpWithIndirectlyReferencedMemory` under `%LOCALAPPDATA%\AORebirthClientPatch\Dumps` |
| Action | log code/address/access/dump path, invoke prior filter if present, otherwise `EXCEPTION_CONTINUE_SEARCH` |
| Containment | none; it does not suppress the exception |
| Lifetime | `DumpInProgress` never resets, so at most one dump/process; filter is not restored on detach; later code may replace it |
| Risk | in-process dbghelp load, dump writing, heap use, and synchronous logger can deadlock during loader/heap corruption |

## Old-renderer patch transaction and synchronization

The randy installer registers its VEH and then makes one snapshot of other
threads.  It requests `CONTEXT_CONTROL` and refuses to patch if a suspended EIP
is in renderer initialization, randy draw, GUI batch/tree, D3DIM, DDRAW, or
NVIDIA ranges. Patch order is DrawIndexed -> GUI batch -> GUI tree; rollback is
the reverse order.  If exact restoration cannot be proven, the code fails fast
rather than continuing with unknown bytes.

This transaction is materially stronger than the new GUI installer but weaker
than RoomSpace's repeated stable snapshots: a thread created after the single
snapshot can race.  RoomSpace, rectangle VEH, and the UEF are outside this
transaction and survive a randy failure.

## Register, stack, FPU/SSE, and lifetime safety review

| State/property | Finding |
|---|---|
| ESP / return addresses | No code patch splits an instruction.  H8 deliberately reconstructs ESP/EIP from a verified frame.  H2 can return after swallowing a deep helper fault without proving native unwind/cleanup; this is the largest stack-consistency risk. |
| ECX / `this` | RoomSpace copies ECX to saved ESI; new GUI and tree thunks explicitly forward ECX. Their ABIs are inferred from exact callsites but lack automated ABI tests. |
| Callee-saved registers | Generated/naked code has been manually disassembled and appears correct.  Compiler wrappers rely on x86 ABI.  Automated disassembly assertions are missing. |
| Flags | VEH resumes are selected at exact integer boundaries. Broad H2 and post-driver H5/H6 do not reconstruct flags from the interrupted native path. |
| x87 | H3 explicitly pops the value loaded before the `+0x82F1` fault.  Exact randy VEH sites are integer-only.  H2/H5/H6 cannot prove driver/helper x87 state after a contained fault. |
| SSE | No guard explicitly saves/restores SIMD state.  This is acceptable before mutation at exact integer sites, but unproven after helper/driver faults. |
| Exception context | Exact VEHs mutate only named registers/EIP/ESP.  Broad helper SEH skips unknown work; driver SEH returns success after unknown internal mutations. |
| Renderer ownership | H6 has explicit DynamicVB/index/material/state cleanup, but driver lock/device ownership remains unknown.  H5 has no cleanup. |
| Executable memory | RoomSpace owns one generated RX page for process lifetime.  Other thunks live in the proxy image.  Unloading the proxy while callbacks/patches exist would leave dangling executable targets. |

No current source path is proven to be the producer of the generic EIP
0/2/5/8/`0x420C70A4` faults.  It would be equally unsafe to assert that no hook
can produce them: H2's broad continuation and every custom ABI remain items to
test with exact synthetic ABI/disassembly coverage.

## Rollback, unhook, and partial-state audit

- There is no `DLL_PROCESS_DETACH` unhook.
- The generated RoomSpace wrapper, successful code patches, VEHs, UEF, proxy
  globals, and real system VERSION module are process-lifetime.
- RoomSpace has verified all-five rollback, but can retain an active patch if
  protection/thread restoration cannot be confirmed.
- The old renderer transaction rolls back in reverse and fail-fast terminates
  if restoration cannot be proved.
- The new GUI installer can leave one or both calls patched after a post-write
  failure.
- Cross-stage rollback does not exist: RoomSpace or rectangle can remain active
  when a later feature fails.
- Manual `FreeLibrary` or hot unpatch is unsafe.  The supported rollback is
  process exit, then replace/remove the proxy while all clients are closed.

## Logging and performance audit

Every `Log()` call takes a process `CRITICAL_SECTION`, writes the line, and
synchronously calls `FlushFileBuffers`.  Hit logs are throttled to the first 16
and then each hundredth occurrence, but a recovered fault still performs lock
and file I/O on the faulting thread.  Initialization is one-shot even if file
setup fails.

Normal-path costs are:

| Feature | Normal-path cost |
|---|---|
| Rect VEH | none until an exception |
| RoomSpace | wrapper at five collision calls |
| Renderer selector | none; AO's original selection is preserved |
| DrawIndexed | wrapper, SEH, integer checks, pointer/index probes, and vtable resolution for every draw |
| GUI batch | wrapper and SEH for every batch |
| GUI tree | JMP, thunk, C++ guard, and trampoline for every normal lookup |
| New GUI helper | wrapper and SEH for both hot callsites |
| VEHs | first-chance dispatch cost only when an exception occurs |

The current source has removed the earlier normal-path `VirtualQuery`/logging
burden from key old-client paths, but performance has not been measured on the
installed artifact after the optimization.  A 100-FPS no-proxy baseline versus
20 FPS with proxy is a real symptom, not permission to invent a cap. Each
remaining module must be measured independently.

## Artifact and build-system audit

The packaged artifact inspected is x86 PE with ASLR, NX, and GUARD_CF header
metadata.  Manual disassembly found the expected compiled thunks and wrapper.
The build command places `/guard:cf` after `/link`, so compiler-side CFG
instrumentation appears absent even though the image advertises GUARD_CF; a hot
indirect call remains raw.  This needs a build/test correction before CFG can be
claimed.

Current automated checks cover:

- the generated 86-byte RoomSpace wrapper for two profiles and five callsites;
- all 17 forwarding exports and four functional API calls;
- deploy helper behavior and PE/package integrity.

They do not cover GUI/randy thunk ABI, stack cleanup, register preservation,
VEH context transforms, exact/near-miss filters, native cleanup count, or
source-to-artifact semantic equivalence.  The inspected artifact SHA-256 is
`35C146A19D4948CAB69FCCAB35A1255D7CE10B995EBADAB2C2E2A96D53BFAEDD`.

There is currently one optimized package build. There is no separate diagnostic
binary and no implemented policy level that changes semantics while retaining
the same artifact. “Diagnostic versus optimized” is therefore not a current
build axis; it is a proposed runtime-policy design. The current artifact always
attempts the profile's serial install path.

## Guard classification

### Preserve as strong exact guards

- H3 rectangle empty-result recovery, including its x87-specific epilogue.
- H8 whole randy draw-resource rejection before nested mutation.
- H9a exact whole-vector skip at `+0x25118` and H9b exact one-entry skip at
  `+0x2511A`, without sharing resume contracts or generalizing H9b's `0x400`
  predicate into an invented table cap.
- H10–H12 exact low color/sample fallbacks.
- H7 low-key native not-found path, after its documentation is corrected.
- H5 pre-call numeric, endpoint, vtable, and initial-target validation, subject
  to hot-path performance tests.

### Separate or disable pending proof

- H1 RoomSpace: independent proactive feature; default off for F16 A/B.
- H4 selector: removed; keep absent.
- H2 new GUI outer catch: off by default until exact inner dispatch and cleanup
  are proven.
- H5 exact NVIDIA post-fault `S_OK`: experimental, driver-fragile, no cleanup.
- H6 GUI/NVIDIA batch recovery: best current cleanup evidence but still Level 4.

### Uncovered or too narrow

- NV-B `+0x154314F`, generic NVIDIA RVAs, and auxiliary NV-A `+0x170C4C6`.
- D/old `0x420C70A4` and generic invalid EIP 0/2/5/8.
- GUI `+0x4ED00` except the observed low-key producer path.
- BinaryStream, ResourceManager, heap, N3 login, and native C++ families.

The correct response to uncovered families is evidence collection and a proven
upstream boundary, not a broader process-wide exception filter.
