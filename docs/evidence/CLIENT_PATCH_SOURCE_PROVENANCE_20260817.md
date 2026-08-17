# AORebirth Client Patch Source Provenance - 2026-08-17

## 2026-08-17 reconciliation update

Reconciliation result: CLIENT PATCH AUTHORITATIVE SOURCE RECONCILED

The newer AORebirth private client patch source from
`origin/codex/linux-parallel-build` was reconciled into the authoritative
Windows `master` source tree at the canonical path:

`Tools\AOClientRoomSpaceGuard\ProxyDll`

The reconciled source revision is the client-patch state from:

`e1d60e74 Combine client key and crash repairs`

This keeps the existing project path and avoids a duplicate client-patch tree.
Generated package artifacts remain build outputs and are not tracked in Git.

### Reconciled source scope

The reconciled tracked changes are limited to the client patch/proxy tree:

- AORebirth launcher URL resources.
- AORebirth dimension URL resource.
- `setup_tool.cpp` and setup manifest.
- expanded `deploy_tool.cpp` and deploy manifest.
- `login_key_patch.cpp` / `login_key_patch.h`.
- combined `dllmain.cpp` startup path.
- crash dump, RoomSpace, GUI rectangle, renderer guard updates.
- package/install/uninstall/readme updates.

No Linux server source, production deployment files, website package, or
installed AO client files were changed.

### Source tree comparison

Current `master` before reconciliation was the older `AORoomSpaceFix` lineage.
The D-tree branch adds the AORebirth client patch lineage:

- RoomSpace/runtime guard changes: retained and updated through the branch's
  crash-repair commits.
- Login-key patch changes: added endpoint-aware login-key worker and tests.
- Endpoint/dimension coexistence: preserved; AORebirth endpoint arms the patch,
  official and unknown endpoints keep original/Funcom behavior.
- Installer changes: added setup EXE source and embedded resource payload.
- Launcher URL changes: added AORebirth `AnarchyLauncher.url` and
  `DimensionServer.url` resources.
- Deployment/package changes: deploy helper now validates, installs, backs up,
  repairs, and uninstalls the AORebirth package.
- Linux-build-only changes: not merged; reconciliation is limited to the
  client patch tree.

### Git history

Relevant source history exists on `origin/codex/linux-parallel-build`.
The most important commits in this lineage are:

- `a26f1912 Add distributable AO RoomSpace proxy repair`
- `490f00b9 Add AO client crash dump handler`
- `59c1d778 Guard old-client randy draw-resource crashes`
- `03ac9441 Publish refreshed AO crash-fix package folder`
- `582ca899 Fix client proxy renderer performance`
- `eff1a2a0 Prevent GUI rectangle crash before exception dispatch`
- `e1d60e74 Combine client key and crash repairs`

The final reconciliation uses the branch's tracked client patch tree state rather
than merging unrelated Linux branch work.

### Artifact lineage

Lineage A applies to the currently published and installed patch:

- Published installer:
  `E:\AORebirthWebsite\ao\downloads\AORebirthClientPatchSetup-v1.exe`
- Published installer SHA-256:
  `c1d1b66008298435c0b3cf8720da9ff5701edf61e20d2c8820b6e1e7c02a9ae8`
- Extracted published installer `version.dll` SHA-256:
  `fd3da14ae9d2584a7713b498a1b76ab7974a831d33f032c330e6ef47525de5a2`
- Installed client DLL:
  `D:\Funcom\Anarchy Online\version.dll`
- Installed client DLL SHA-256:
  `fd3da14ae9d2584a7713b498a1b76ab7974a831d33f032c330e6ef47525de5a2`

Conclusion: the website installer embeds the exact DLL currently installed in
the local AO client.

Lineage C applies to the reconciled authoritative source:

- Authoritative rebuilt `version.dll` SHA-256:
  `c79b3cca50e88c5ad0d900f38197381017e9256e8e20cbb983ea64561ea7e057`
- Authoritative rebuilt setup EXE SHA-256:
  `671a94f477ac1b1bccf6ac8027d0966b3a7a6c384aa552680c3bcde5361b78c9`
- Authoritative rebuilt package ZIP SHA-256:
  `2f79396f32546402b562767632fd7132a09953783b703f027b34e3da1f224ddc`
- Authoritative rebuilt deploy helper SHA-256:
  `39bfe20d12e3b029d896aef1b4559c8849c094505f42f61610fea8c322ef33c2`

Conclusion: the reconciled source is newer than the published/installed patch
and includes combined crash-repair plus endpoint-aware login-key behavior.

### Hash mismatch explanation

The published installer hash mismatch is expected because it is a different,
earlier artifact lineage. Static resource extraction proves the published
installer embeds the installed `fd3da14a...` DLL, not the newer `c79b3cca...`
DLL.

The setup EXE itself is not byte-reproducible across build locations. The
authoritative setup EXE hash differs from the D-tree setup EXE hash even though
the embedded payload resources are the expected files. The payload resources are
the stable provenance target:

- `AORebirthAnarchyLauncher.url`:
  `abc2e5fc40e30be4acbd3110250364c2ca27e6e6ca8738c3d8d9c39d4a3f7ddb`
- `AORebirthDimensionServer.url`:
  `6cb9844f770e9204c4fb07c63c2863824c44d22bfea01ac99d1a65fff277bcc7`
- embedded authoritative `version.dll`:
  `c79b3cca50e88c5ad0d900f38197381017e9256e8e20cbb983ea64561ea7e057`

PE timestamps are deterministic-linker values, not reliable wall-clock build
times.

### Behavior preservation

The reconciled build preserves endpoint-aware coexistence:

- AORebirth endpoint / port `7500`: login-key patch may arm.
- Rubi-Ka official endpoint: original/Funcom key behavior is retained.
- RK2019 official endpoint: original/Funcom key behavior is retained.
- Unknown endpoint: original behavior is retained.

The build ran the client patch self-tests, including endpoint parsing and
login-key policy cases, through `Build-Package.cmd`.

RoomSpace/proxy behavior remains covered by:

- offline wrapper self-test;
- proxy forwarding self-test;
- deployment helper self-test;
- package verification.

### Installer dry-run

Disposable target:

`C:\Users\Mike\AppData\Local\Temp\AORebirthClientPatchDryRun-20260817`

The dry-run target used copied `AnarchyOnline.exe` and `N3.dll` from the local
client folder plus disposable launcher URL files. The real installed client was
not modified.

Install result:

- URL files were backed up.
- root launcher URL files were patched.
- `cd_image\data\launcher` URL files were patched.
- `version.dll` was installed.
- ownership marker was written.
- installed temp `version.dll` hash:
  `c79b3cca50e88c5ad0d900f38197381017e9256e8e20cbb983ea64561ea7e057`

Uninstall result:

- temp `version.dll` was removed.
- original disposable root URL files were restored.
- original disposable `cd_image\data\launcher` URL files were restored.

The setup EXE itself is interactive in this shell context, so the dry-run used
the setup-built `AORebirthClientPatchDeploy.exe` helper directly against the
same package payload.

### Deployment recommendation

Do not replace the installed `D:\Funcom\Anarchy Online\version.dll` yet.

Do not replace the website
`E:\AORebirthWebsite\ao\downloads\AORebirthClientPatchSetup-v1.exe` yet.

The next deployment stage should intentionally decide whether to publish the
newer authoritative installer/package and replace the installed DLL. That stage
must be separate from this source reconciliation stage.

## Result before reconciliation

Provenance classification: LIKELY SOURCE - MORE EVIDENCE REQUIRED

The private AORebirth client patch source was found in the parallel Linux build
tree:

`D:\AO_Rebirth_Linux_Build\Tools\AOClientRoomSpaceGuard\ProxyDll`

The current `master` checkout at `C:\Users\Mike\Documents\AORebirth` contains an
older `Tools\AOClientRoomSpaceGuard\ProxyDll` source tree for the RoomSpace
package. Git history for the current repository also contains the later
AORebirth client patch files, including `setup_tool.cpp`, `login_key_patch.cpp`,
`AORebirthDimensionServer.url`, `AORebirthAnarchyLauncher.url`, and
`AORebirthClientPatch-PrivateTesterInstructions.txt`.

The parallel tree builds reproducibly from a temporary copy and produces the
same current D-tree artifacts. It does not match the currently installed client
DLL hash or the currently published website installer hash, so the exact source
snapshot for the deployed/public binary is not yet proven.

## Source discovery

Found source tree:

`D:\AO_Rebirth_Linux_Build\Tools\AOClientRoomSpaceGuard\ProxyDll`

Important source files:

- `Build-Package.cmd`
- `src\dllmain.cpp`
- `src\version_proxy.cpp`
- `src\version_proxy.def`
- `src\deploy_tool.cpp`
- `src\setup_tool.cpp`
- `src\login_key_patch.cpp`
- `src\login_key_patch.h`
- `AORebirthDimensionServer.url`
- `AORebirthAnarchyLauncher.url`
- `AORebirthClientPatch-PrivateTesterInstructions.txt`

Current `master` source tree:

`C:\Users\Mike\Documents\AORebirth\Tools\AOClientRoomSpaceGuard\ProxyDll`

The `master` tree is older and still branded as `AORoomSpaceFix`. The D-tree is
the newer AORebirth client patch tree.

## Distributed patch evidence

Website installer:

- Path: `E:\AORebirthWebsite\ao\downloads\AORebirthClientPatchSetup-v1.exe`
- Size: `333,824`
- Timestamp: `2026-08-11 22:11`
- SHA-256: `c1d1b66008298435c0b3cf8720da9ff5701edf61e20d2c8820b6e1e7c02a9ae8`

D-tree current setup artifact:

- Path: `D:\AO_Rebirth_Linux_Build\Tools\AOClientRoomSpaceGuard\ProxyDll\artifacts\AORebirthClientPatchSetup-v1.exe`
- Size: `367,616`
- Timestamp: `2026-08-14 21:28`
- SHA-256: `370361e9a9299b990d117917e2f8fe8a8471ec9ada7bc52bc9c348d53c2765ec`

Temporary rebuild setup artifact:

- Path: `C:\Users\Mike\AppData\Local\Temp\AORebirthClientPatchBuild-20260816-2329\artifacts\AORebirthClientPatchSetup-v1.exe`
- Size: `367,616`
- Timestamp: `2026-08-16 23:28`
- SHA-256: `370361e9a9299b990d117917e2f8fe8a8471ec9ada7bc52bc9c348d53c2765ec`

Conclusion: the D-tree source reproduces the current D-tree setup artifact
exactly, but not the published website installer.

## Installed client evidence

Installed proxy:

- Path: `D:\Funcom\Anarchy Online\version.dll`
- Size: `139,776`
- Timestamp: `2026-08-11 21:51`
- SHA-256: `fd3da14ae9d2584a7713b498a1b76ab7974a831d33f032c330e6ef47525de5a2`
- PE machine: `0x014C` / PE32 x86

Installed ownership marker:

`D:\Funcom\Anarchy Online\AORebirthClientPatch.install`

Contents:

```text
Product=AORebirthClientPatch
Version=1
ProxySha256=FD3DA14AE9D2584A7713B498A1B76AB7974A831D33F032C330E6EF47525DE5A2
N3Sha256=8C019EFD72D547879A06585B69147AB1546B9617A2FCE090E5863791AEC8B0BB
```

Conclusion: the installed DLL is AORebirth-owned and marker-verified, but it
does not match the current D-tree rebuilt proxy.

## Current source build evidence

D-tree current package proxy:

- Path: `D:\AO_Rebirth_Linux_Build\Tools\AOClientRoomSpaceGuard\ProxyDll\artifacts\AORebirthClientPatch-v1\version.dll`
- Size: `173,568`
- Timestamp: `2026-08-14 21:28`
- SHA-256: `c79b3cca50e88c5ad0d900f38197381017e9256e8e20cbb983ea64561ea7e057`
- PE machine: `0x014C` / PE32 x86

Temporary rebuild proxy:

- Path: `C:\Users\Mike\AppData\Local\Temp\AORebirthClientPatchBuild-20260816-2329\artifacts\AORebirthClientPatch-v1\version.dll`
- Size: `173,568`
- Timestamp: `2026-08-16 23:28`
- SHA-256: `c79b3cca50e88c5ad0d900f38197381017e9256e8e20cbb983ea64561ea7e057`

D-tree current package ZIP:

- Path: `D:\AO_Rebirth_Linux_Build\Tools\AOClientRoomSpaceGuard\ProxyDll\artifacts\AORebirthClientPatch-v1.zip`
- Size: `188,138`
- Timestamp: `2026-08-14 21:28`
- SHA-256: `4a59a7cfa5aab0a24d46b5b4886bec52c041d57daf84149de25338d56c2cd26d`

Temporary rebuild package ZIP:

- Path: `C:\Users\Mike\AppData\Local\Temp\AORebirthClientPatchBuild-20260816-2329\artifacts\AORebirthClientPatch-v1.zip`
- Size: `188,138`
- Timestamp: `2026-08-16 23:28`
- SHA-256: `36e03032e7d5331433914189b743955c559643ab34bbc190df3b34ec4532df70`

The ZIP hash differs because archive metadata is not deterministic. The package
payload manifest matches, including:

```text
C79B3CCA50E88C5AD0D900F38197381017E9256E8E20CBB983EA64561EA7E057  version.dll
39BFE20D12E3B029D896AEF1B4559C8849C094505F42F61610FEA8C322EF33C2  AORebirthClientPatchDeploy.exe
ABC2E5FC40E30BE4ACBD3110250364C2CA27E6E6CA8738C3D8D9C39D4A3F7DDB  AORebirthAnarchyLauncher.url
6CB9844F770E9204C4FB07C63C2863824C44D22BFEA01AC99D1A65FFF277BCC7  AORebirthDimensionServer.url
```

## Exports

The proxy exports the 17 Windows `version.dll` functions declared in
`src\version_proxy.def`:

- `GetFileVersionInfoA`
- `GetFileVersionInfoByHandle`
- `GetFileVersionInfoExA`
- `GetFileVersionInfoExW`
- `GetFileVersionInfoSizeA`
- `GetFileVersionInfoSizeExA`
- `GetFileVersionInfoSizeExW`
- `GetFileVersionInfoSizeW`
- `GetFileVersionInfoW`
- `VerFindFileA`
- `VerFindFileW`
- `VerInstallFileA`
- `VerInstallFileW`
- `VerLanguageNameA`
- `VerLanguageNameW`
- `VerQueryValueA`
- `VerQueryValueW`

The package build also runs `ProxyForwardingSelfTest.exe`, which passed with
`exports=17 functional=4`.

## Dimension handling

The D-tree package includes `AORebirthDimensionServer.url`:

```text
ao-rebirth.com:80/new-dimensions/dimensions_v3.txt
```

The comments in that file state that the hosted dimension list includes both
official Funcom dimensions and AORebirth private test dimensions.

The installer/deploy helper replaces:

- `DimensionServer.url`
- `AnarchyLauncher.url`
- `cd_image\data\launcher\DimensionServer.url`
- `cd_image\data\launcher\AnarchyLauncher.url`

Installed client evidence confirms `D:\Funcom\Anarchy Online\DimensionServer.url`
points at the AORebirth dimensions endpoint.

## Hook infrastructure

The D-tree source contains:

- `version_proxy.cpp`: loads and forwards to the real system `version.dll`.
- `dllmain.cpp`: activates only for `AnarchyOnline.exe`.
- `crash_dump.cpp`: writes dumps under `%LOCALAPPDATA%\AORebirthClientPatch`.
- `roomspace_fix.cpp`, `gui_rect_fix.cpp`, `randy_color_fix.cpp`: crash repair
  hooks.
- `login_key_patch.cpp`: endpoint-aware login key patching.

The login-key patch reads the process command line and parses AO launch tokens
such as `IA`, `IP`, and `DU`. It arms only when the endpoint matches AORebirth
login IP handling and port `7500`. Official and unknown endpoints skip the patch
and retain the Funcom key.

This is the important compatibility guard for the split official/private
dimension workflow.

## Installer behavior

The D-tree installer source is `src\setup_tool.cpp`. The deploy helper is
`src\deploy_tool.cpp`.

The installer embeds:

- `AORebirthAnarchyLauncher.url`
- `AORebirthDimensionServer.url`
- `version.dll`

The deploy helper validates the client root, requires `AnarchyOnline.exe`, backs
up an existing `version.dll` as `version.dll.AORebirthBackup`, installs the
AORebirth proxy, writes the ownership marker, and patches launcher URL files.

## Build performed

Build command was run from a temporary copy:

```cmd
cmd.exe /d /c "cd /d C:\Users\Mike\AppData\Local\Temp\AORebirthClientPatchBuild-20260816-2329 && Build-Package.cmd"
```

Result: PASS.

The build produced:

- `artifacts\AORebirthClientPatch-v1.zip`
- `artifacts\AORebirthClientPatch-v1\version.dll`
- `artifacts\AORebirthClientPatchSetup-v1.exe`

The build ran:

- offline wrapper self-test
- proxy forwarding self-test
- deployment helper self-test
- package manifest generation
- package verification
- extracted package verification
- setup EXE build

The script reported:

```text
AO was not launched and no client directory was changed.
```

## Repository decision

Do not implement DailyLogin VGTP routing against `master` until the newer D-tree
client patch source is reconciled into the authoritative AORebirth repository.

Safe next repository action:

1. Compare `codex/linux-parallel-build` client patch files against current
   `master`.
2. Promote the D-tree `ProxyDll` source into the authoritative branch or merge
   the branch intentionally.
3. Preserve the endpoint-aware login-key guard.
4. Only then add DailyLogin process-local VGTP routing.

## Security

The current package is unsigned unless `AO_REBIRTH_CODESIGN=1` is configured.
The build script has signing support through certificate thumbprint or PFX, but
no signing was performed in this validation run.

Do not publish a new installer until the authoritative source branch and exact
deployment artifact are selected.

## Status

DAILYLOGIN CLIENT ROUTING UNBLOCKED

Meaning: the client patch source exists and can be built. Deployment remains
blocked until source reconciliation decides which branch/artifact becomes
authoritative.
