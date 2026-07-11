AO RoomSpace Fix - version.dll proxy
====================================

This package prevents the repeatable Anarchy Online E06D7363 RoomSpace crash.
It does not replace or modify AnarchyOnline.exe, N3.dll, XML, resources, or
shortcuts. The installed version.dll is loaded by AO's normal dependency chain
and applies the repair only in process memory.
This build patches only the observed crashing RoomSpace callsite for the
approved client hash instead of touching the other audited collision callsites.

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

Look for both:

PATCH PASS
READY RoomSpace repair active

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
