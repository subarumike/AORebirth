# AO client crash-fix proxy DLL

This is a narrowly scoped `version.dll` proxy for the approved Anarchy Online
client builds. Windows loads it through the existing dependency chain:

`AnarchyOnline.exe -> GUI.dll -> Awesomium.dll -> VERSION.dll`

The proxy forwards the complete 17-export Windows `version.dll` surface to the
real DLL under the 32-bit Windows system directory. A deferred worker then:

1. installs an unhandled-crash dump handler that writes minidumps under
   `%LOCALAPPDATA%\AORoomSpaceFix\Dumps` and chains back to the client's normal
   crash path;
2. waits for `N3.dll` outside loader lock;
3. hashes the exact file backing the loaded module;
4. selects only the approved new-client or old-client profile;
5. verifies all five original calls still target the expected `PosToRoom` RVA;
6. emits the same proven x86 checked-cast/negative-cell wrapper as the external
   `AOClientRoomSpaceGuard`;
7. requires repeated stable thread snapshots and aborts if any client thread
   cannot be opened, suspended, or completely enumerated;
8. applies and verifies all five calls as one transaction, requires every
   instruction-cache flush, and verifies page-protection restoration and thread
   resumption before reporting readiness;
9. verifies rollback before freeing the wrapper and retains that allocation if
   rollback or cache state cannot be proven safe;
10. retains the installed wrapper allocation until process exit.

For the approved new graphics client, the proxy also verifies the exact two
callers of `GUI.dll +0x14CA77`, a GUI draw helper associated with crashes where
the client jumps into coordinate data such as `0x41C80000`. Those two calls are
redirected through a guard that lets valid draw calls continue unchanged, but
skips that one draw helper call if it raises an access violation at a
non-executable address.

For the approved old live client, the proxy also verifies the exact GUI callsite
and `Utils!Rect::operator+(Point)` implementation associated with the recurring
`Utils.dll +0x72F1` crash, then replaces that one GUI import with a guard that
returns an empty rectangle when GUI supplies a null, unreadable, or nonsensical
rectangle or point address. A process exception fallback also contains access
violations from the exact verified `Utils!Rect::operator+(Point)` body. Valid
readable rectangle and point data continue through the original function
unchanged.

The old live renderer repair verifies the exact `randy31.dll +0x21A94`
draw-resource pointer read, `randy31.dll +0x2511A` render-state lookup,
`randy31.dll +0x6C3A1` byte-color read, `randy31.dll +0x6C476` indirect
color-sample read, and `randy31.dll +0x6C51D` packed-color read. If the draw
wrapper receives an invalid low resource pointer, the process-level guard
returns from that one draw call without submitting it. If a render-state entry
contains an impossible state id, the guard skips that one state entry and
continues the state-application loop. Invalid low color pointers use the
verified helper's existing missing-sample path or substitute black components
before resuming after the unsafe read.

The repair also byte-verifies the old renderer's single
`DrawIndexedPrimitiveVB` dispatch at `randy31.dll +0x219B4`. The fallback is
restricted to NVIDIA driver `32.0.15.9186`, its verified image identity, and
the two observed null-read instructions at driver RVAs `0x172776C` and
`0x173A009`. Only while that one triangle-batch call is active, a matching
read access violation is unwound to the AO-to-Direct3D call boundary and the
bad draw is skipped. Other NVIDIA versions or instructions, other renderer
calls, and exceptions that do not match that exact filter continue through
the normal client exception path unchanged. Driver recovery after an access
violation is containment, not a guarantee that NVIDIA's internal state remains
usable.

The separate observed NVIDIA RVA `0x170C490` occurs while
`IDirect3DVertexBuffer7::Lock` flushes earlier queued work. Randy discards the
Lock result and GUI immediately writes through the returned pointer, so turning
that exception into a null or failed Lock would only move the crash into GUI.
The proxy therefore does not intercept the Lock itself. It byte-verifies and
wraps the complete void GUI batch helper called at `GUI.dll +0x152E49`. The
exact NVIDIA `0x170C490`, `EAX=4`, read-from-`0x14` failure unwinds to that
boundary and skips the current GUI batch. The same boundary also contains the
verified `GUI.dll +0x150F22` null-lock result: randy returned a low destination
equal to `0x1C * baseVertex`, and GUI attempted `rep movsd` through it. Before
discarding either batch, the handler invokes AO's conditional native
`GetVB(0x144)` unlock and resets the viewport material and selected state blob.
The null-destination path additionally releases its heap index buffer when one
was allocated. The caller then advances normally. These scoped guards contain
the verified deferred failures without changing AO's selected renderer.

This is deliberately not a process-wide "continue every exception" handler.
Unknown access violations retain the normal crash/dump path because an
arbitrary failure can occur while AO owns a lock, allocation, or partially
mutated state. A fault is contained only when its byte signature, live register
state, helper locals, batch object, viewport, and one of the three state blobs
prove that the matching native cleanup is safe.

The old-client guards do not query Windows virtual-memory metadata during a
successful draw or rectangle operation. Draw inputs are checked with bounded
integer arithmetic and direct first/last probes inside the existing exception
boundary; the expensive module and byte verification runs only after a fault.
The rectangle call remains pointed directly at AO's original Utils helper, so
its proxy handler also runs only when that helper raises the two exact verified
read faults.

The old GUI repair also verifies the map/tree `find` entry at
`GUI.dll +0x4F2EF`, its comparator path, and its native not-found tail. If a
caller supplies a null, low, or unreadable four-byte lookup key such as the
observed pointer `0x8`, the guard writes the tree's own sentinel to the output
and returns the output pointer exactly as the original not-found branch does.
Readable keys continue through a trampoline containing the original prologue.

For the verified old-client build, the proxy preserves AO's renderer selection.
Direct3D T&L HAL remains T&L HAL, so hardware transformation and lighting are not
silently moved into the legacy Direct3D software pipeline. The scoped draw guard
remains responsible for the verified NVIDIA rasterization faults without
changing the renderer chosen in AO's launcher.

The crash dump handler does not suppress arbitrary access violations, C++
exceptions, arbitrary driver faults, stack corruption, or unknown callsite failures.
Only the targeted, byte-verified repairs listed above resume execution. The
proxy never modifies AO files after installation. It contains no
LargeAddressAware patch, XML/settings changes, DValues, camera/input hooks,
other UI modifications, or Project Rubi-Ka-specific behavior.

## Build

From the AORebirth repository root:

```cmd
cmd /d /c Tools\AOClientRoomSpaceGuard\ProxyDll\Build-Package.cmd
```

The wrapper uses the installed Visual Studio x86 compiler, builds with the
static CRT, runs an offline byte/ABI self-test for both profiles, verifies the
PE machine, export surface, and runtime dependencies, and creates the ignored
artifact:

`artifacts\AORoomSpaceFix-v1.zip`

The build never launches AO and never installs into a client directory.

The packaged install/uninstall path has been smoke-tested against isolated copies of both
approved clients, including exact-name activation, ownership/hash verification, idempotent
install, and same-handle uninstall. The old live client at `D:\Funcom\Anarchy Online` has
the verified package installed without changing its EXE or `N3.dll`. AO was not launched;
normal-shortcut in-game stability and the runtime `PATCH PASS` log remain the next smoke.

## Installation

Close all AO clients and use the packaged `Install.cmd` with the client root.
The installer validates the package hash and supported `N3.dll` hash, refuses
an existing `version.dll`, stages and verifies the copy, and writes an ownership
marker for safe uninstall. Normal AO shortcuts remain unchanged.
Existing ownership files are opened without following reparse points, pinned by
handle, revalidated, and rejected unless they are ordinary files.

Runtime logs are written to:

`%LOCALAPPDATA%\AORoomSpaceFix\AORoomSpaceFix.log`

Do not run the external guarded launcher with this proxy installed. Both
repairs correctly refuse already-modified callsites rather than chaining.

## Upstream attribution

The proxy/export-forwarding and deferred initialization pattern is derived
from the MIT-licensed
[Inorien/AOReloaded](https://github.com/Inorien/AOReloaded) project at commit
`c830cb20972c3806ec44e68ddaa85d2abc3f8d10`. See
`THIRD_PARTY_NOTICES.md` and `LICENSES/AOReloaded-MIT.txt`.
