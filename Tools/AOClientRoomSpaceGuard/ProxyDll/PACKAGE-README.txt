AORebirth Client Patch
=====================

This is the one-download player package for using the official Anarchy Online
client with AORebirth while still allowing the same client install to connect to
the official Funcom dimensions.

The package supports the current tested EP2/new-engine client and the tested
EP1/old-engine client builds.

The installer adds:

- our version.dll proxy binary, added to the AO folder and loaded by the normal
  AO client dependency chain. The official client does not ship with this file.
  It changes only the login key used for AORebirth login handlers and skips the
  key patch for the official Funcom login handlers. The same proxy also restores
  the byte-verified client crash repairs for the supported old and new clients.
- DimensionServer.url and cd_image\data\launcher\DimensionServer.url,
  redirected to the AORebirth-hosted dimensions list.
- AnarchyLauncher.url and cd_image\data\launcher\AnarchyLauncher.url,
  redirected to AORebirth launcher news/account/support pages.

It does not replace or modify AnarchyOnline.exe, N3.dll, patcher executables, or
resource archives. Runtime changes remain build-specific and fail closed when
the expected module identities, callsites, or instruction bytes do not match.

Windows SmartScreen
-------------------

Unsigned community builds show "Unknown publisher" and may trigger Microsoft
Defender SmartScreen. The correct release fix is Authenticode signing with a
trusted code-signing certificate. The build wrapper supports signed releases:

   set AO_REBIRTH_CODESIGN=1
   set AO_REBIRTH_CODESIGN_THUMBPRINT=<certificate thumbprint>
   Build-Package.cmd

or:

   set AO_REBIRTH_CODESIGN=1
   set AO_REBIRTH_CODESIGN_PFX=C:\path\to\certificate.pfx
   set AO_REBIRTH_CODESIGN_PFX_PASSWORD=<pfx password>
   Build-Package.cmd

Do not publish an unsigned installer as a final player release.

Private tester Windows Security allow
-------------------------------------

This private test build is unsigned and installs an AORebirth version.dll proxy
inside the selected Anarchy Online folder. Some consumer Windows installs may
flag the private test package as Trojan:Win32/Wacatac.B!ml or a potentially
unwanted app.

Only allow the file if all of these are true:

- You downloaded it from the private AORebirth test link.
- The SHA-256 hash matches the hash posted with the current test build.
- You are installing into a dedicated AO test client folder.

Preferred private-test allow method:

1. Create or choose a dedicated AO test install folder.
2. Open Windows Security.
3. Select Virus & threat protection.
4. Select Manage settings.
5. Under Exclusions, select Add or remove exclusions.
6. Select Add an exclusion, then Folder.
7. Choose the dedicated AO test install folder.
8. Run AORebirthClientPatchSetup-v2.exe and select that same AO folder.

Alternative exact-file allow method:

1. Open Windows Security.
2. Select Virus & threat protection.
3. Open Protection history.
4. Expand the blocked AORebirthClientPatchSetup-v2.exe item.
5. Confirm the detection and affected path match the current private test build.
6. Choose Allow on device or Restore/Allow, depending on the Windows version.
7. Run the installer again.

Do not turn off all antivirus protection. Do not add broad exclusions such as
Downloads, Desktop, the whole drive, or a user profile folder. Remove the
installer-file allow entry after testing if you used the exact-file method.

INSTALL
-------

1. Close every AnarchyOnline.exe process.
2. Run the one-file installer:

   AORebirthClientPatchSetup-v2.exe

   The installer opens a folder picker. Select the main Anarchy Online folder
   that contains AnarchyOnline.exe.

   If the installer cannot find AO automatically, run:

   AORebirthClientPatchSetup-v2.exe "C:\path\to\Anarchy Online"

The ZIP package is kept as a manual fallback. To install from the ZIP, extract
it and run:

   Install.cmd

If the ZIP installer cannot find AO automatically, run:

   Install.cmd "C:\path\to\Anarchy Online"

3. Start AO with the same normal shortcut you already use.

The installer supports only the two approved N3.dll hashes. Unknown clients
are rejected without changing anything. If an existing version.dll is present,
the installer backs it up as version.dll.AORebirthBackup and installs the
AORebirth version.dll. Invalid or stale AORebirth ownership markers are backed
up and replaced during repair.

The first install backs up the original launcher URL files beside the originals
using the .AORebirthBackup suffix. Reinstalling is idempotent when the same
package is already installed. If a launcher URL file was manually changed after
installation, uninstall fails closed instead of guessing which copy is correct.

LOG
---

%LOCALAPPDATA%\AORebirthClientPatch\AORebirthClientPatch.log
%LOCALAPPDATA%\AORebirthClientPatch\Dumps

Useful log markers:

LOGINKEY policy=Auto
LOGINKEY patch=applied
LOGINKEY patch=skipped reason=non_aorebirth_endpoint
READY loginKeyWorker=started RoomSpace and new-client GUI draw repairs active
READY loginKeyWorker=started RoomSpace, GUI rectangle, and renderer repairs active

UNINSTALL
---------

Close AO, then run:

Uninstall.cmd "C:\path\to\Anarchy Online"

The uninstaller removes version.dll only when the ownership marker and current
DLL hash both match this package. It restores the launcher URL backups when
they are still owned by this package.

CONFLICTS
---------

Only one version.dll proxy can occupy the AO client directory. Do not combine
this package with AOReloaded or another version.dll proxy unless the AORebirth
login-key patch has been intentionally integrated into that proxy's source.

This is an independent community crash repair, not an official Funcom build.
See AOReloaded-MIT.txt for the third-party license retained by this derivative.
