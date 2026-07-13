# AO client crash-fix proxy DLL

This is a narrowly scoped `version.dll` proxy for the approved Anarchy Online
client builds. Windows loads it through the existing dependency chain:

`AnarchyOnline.exe -> GUI.dll -> Awesomium.dll -> VERSION.dll`

The proxy forwards the complete 17-export Windows `version.dll` surface to the
real DLL under the 32-bit Windows system directory. A deferred worker then:

1. waits for `N3.dll` outside loader lock;
2. hashes the exact file backing the loaded module;
3. selects only the approved new-client or old-client profile;
4. verifies all four original calls still target the expected `PosToRoom` RVA;
5. emits the same proven x86 checked-cast/negative-cell wrapper as the external
   `AOClientRoomSpaceGuard`;
6. requires repeated stable thread snapshots and aborts if any client thread
   cannot be opened, suspended, or completely enumerated;
7. applies and verifies all four calls as one transaction, requires every
   instruction-cache flush, and verifies page-protection restoration and thread
   resumption before reporting readiness;
8. verifies rollback before freeing the wrapper and retains that allocation if
   rollback or cache state cannot be proven safe;
9. retains the installed wrapper allocation until process exit.

For the approved old live client, the proxy also verifies the exact GUI callsite
and `Utils!Rect::operator+(Point)` implementation associated with the recurring
`Utils.dll +0x72F1` crash, then replaces that one GUI import with a guard that
returns an empty rectangle when GUI supplies a null, unreadable, or nonsensical
rectangle or point address. A process exception fallback also contains access
violations from the exact verified `Utils!Rect::operator+(Point)` body. Valid
readable rectangle and point data continue through the original function
unchanged.

The old live renderer repair verifies the exact `randy31.dll +0x6C3A1` byte-
color and `randy31.dll +0x6C51D` packed-color reads. If either instruction
receives an invalid low pointer, the process-level guard substitutes black
color components and resumes after the unsafe read. All other renderer
exceptions continue through the normal client exception path unchanged.

The old live Vehicle repair verifies the exact collision-query callsites
associated with the recurring `Vehicle.dll -> N3.dll -> MSVCR100.dll`
`E06D7363` C++ exception path. It wraps only those three verified virtual calls
and converts that exception into the original caller's normal `false` result so
the client uses its existing fallback vectors. Other exceptions continue
through the normal client exception path unchanged.

The proxy never modifies AO files after installation. It contains no
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
