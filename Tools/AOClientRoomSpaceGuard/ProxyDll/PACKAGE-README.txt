AO RoomSpace Fix - version.dll proxy
====================================

This package prevents several repeatable Anarchy Online client crashes through
byte-verified in-memory guards. It also writes an unhandled crash minidump for
new crash signatures before chaining back to AO's normal crash path. It does
not replace or modify AnarchyOnline.exe, N3.dll, XML, resources, or shortcuts.
The installed version.dll is loaded by AO's normal dependency chain and applies
the repair only in process memory.

INSTALL
-------

1. Close every AnarchyOnline.exe process.
2. Run:

   Install.cmd "C:\path\to\Anarchy Online"

3. Start AO with the same normal shortcut you already use.

The installer supports only the two approved N3.dll hashes. Unknown clients
are rejected without changing anything. It also refuses to overwrite an
existing version.dll, including a full AOReloaded installation.

LOG
---

%LOCALAPPDATA%\AORoomSpaceFix\AORoomSpaceFix.log
%LOCALAPPDATA%\AORoomSpaceFix\Dumps

For the new graphics client, look for:

PATCH PASS
READY RoomSpace and new-client GUI draw repairs active

The new-client GUI draw repair skips one bad GUI draw-helper call if the client
jumps into coordinate data or another non-executable address instead of code.

For the old graphics client, look for:

PATCH PASS
READY RoomSpace, GUI rectangle, and renderer repairs active

The old-client renderer repair skips one bad randy31 draw-resource call when
the client passes a low integer instead of a resource pointer. Color-pointer
guards remain limited to the verified randy31 color-read callsites, including
the indirect color-sample helper's existing missing-sample path. It also skips
impossible randy31 render-state entries, such as corrupted state ids that would
index outside the renderer's saved-state table.

The repair also guards the one verified old-client DrawIndexedPrimitiveVB call
that produced the repeated NVIDIA crashes. The fallback accepts only NVIDIA
driver 32.0.15.9186 and the two exact null-read instructions observed in the
dumps. During that exact triangle draw, a matching call is unwound and only
that bad draw is skipped. Other driver versions, instructions, calls, and
exceptions remain untouched. Because the driver already faulted, continued
driver operation cannot be guaranteed; the plain-HAL selection is the
pre-fault repair.

One separate NVIDIA failure can surface later while AO locks its next GUI
vertex buffer. AO does not check a failed/null Lock result, so the repair does
not replace that crash with an unsafe null pointer. Instead it wraps the whole
verified void GUI batch and skips that batch only for the exact NVIDIA
32.0.15.9186 read-from-0x14 instruction. The plain-HAL selection is the
preventive repair for that deferred-flush path.

The old GUI repair also contains the verified tree-lookup crash where GUI was
given pointer 0x8 instead of a four-byte key. Invalid or unreadable key pointers
use GUI's existing not-found result; valid keys use the original lookup.

For the verified old-client build, the repair automatically maps AO's existing
T&L HAL selection to its existing plain Direct3D HAL selection before creating
the persistent renderer device. This keeps GPU rasterization while the legacy
Direct3D runtime performs transform and lighting on the CPU. The exact selector
and both existing device GUIDs are verified first; this is not a new cap or
gameplay limit.

The dump handler does not suppress arbitrary access violations, C++
exceptions, arbitrary driver faults, stack corruption, or unknown callsite failures.
Only targeted, byte-verified repairs resume execution.

UNINSTALL
---------

Close AO, then run:

Uninstall.cmd "C:\path\to\Anarchy Online"

The uninstaller removes version.dll only when the ownership marker and current
DLL hash both match this package.

CONFLICTS
---------

Only one version.dll proxy can occupy the AO client directory. Do not combine
this package with AOReloaded or another version.dll proxy unless the RoomSpace
repair has been intentionally integrated into that proxy's source.

This is an independent community crash repair, not an official Funcom build.
See AOReloaded-MIT.txt for the third-party license retained by this derivative.
