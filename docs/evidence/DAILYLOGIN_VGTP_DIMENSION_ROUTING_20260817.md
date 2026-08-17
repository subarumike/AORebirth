# DailyLogin VGTP dimension-aware routing investigation

Date: 2026-08-17

## 2026-08-17 correction: client patch source located

The earlier source-availability finding in this document is superseded by
`docs/evidence/CLIENT_PATCH_SOURCE_PROVENANCE_20260817.md`.

The private client patch source exists in the parallel Linux build tree:

`D:\AO_Rebirth_Linux_Build\Tools\AOClientRoomSpaceGuard\ProxyDll`

That tree contains the AORebirth-branded `version.dll` proxy source,
`setup_tool.cpp`, `deploy_tool.cpp`, `login_key_patch.cpp`, installer resource
embedding, and launcher URL patching. A temporary-copy build passed and produced
`AORebirthClientPatch-v1.zip`, `version.dll`, and
`AORebirthClientPatchSetup-v1.exe`.

The current `master` checkout still contains an older RoomSpace-branded proxy
tree. The exact source snapshot for the currently published website installer
and installed client DLL is not yet proven by hash. DailyLogin client routing is
therefore engineering-unblocked, but should not be implemented or deployed until
the D-tree client patch source is reconciled into the authoritative repository
branch.

## 2026-08-17 reconciliation update

The newer client patch source has now been reconciled into authoritative
Windows `master` at:

`Tools\AOClientRoomSpaceGuard\ProxyDll`

DailyLogin VGTP routing is still not implemented. The next routing change must
be made in this canonical client-patch path and must preserve the proven
dimension coexistence rule:

```text
AORebirth endpoint + DailyLogin VGTP host -> AORebirth routing
official/unknown endpoint -> original client behavior
```

The current published installer and local installed DLL remain the earlier
lineage and were not replaced during reconciliation.

## 2026-08-17 combined patch boundary

The authoritative client patch now builds a versioned v2 combined binary from
`b60b7ca6` that contains both crash-repair/RoomSpace protection and
endpoint-aware AORebirth login-key behavior. DailyLogin VGTP routing is still
not implemented and must wait until the combined v2 patch passes real
disposable-client acceptance.

Do not add DailyLogin routing to the older installed/published v1 lineage.

## Scope

This investigation covers the required routing contract:

```text
AORebirth + vgtp://uwg.daily.icc-rk/index.app
    -> AORebirth-hosted DailyLogin

Official Rubi-Ka / RK2019 / unknown dimension
    -> untouched original behavior
```

Global hosts-file changes, global DNS overrides, wildcard `*.icc-rk`
interception, and AORebirth-side proxying of official Funcom content are out of
scope and explicitly rejected.

## Files and evidence inspected

- `AI_START_HERE.md`
- `docs/project/DEVELOPMENT_AUTHORITY.md`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/evidence/LOGIN_INVENTORY_DAILYLOGIN_FOLLOWUP_20260817.md`
- `docs/generated/arete_web_window_loading_future_work.md`
- `docs/reference/client-dll-function-map/ghidra/GUI.dll.ghidra_functions.csv`
- `docs/reference/client-dll-function-map/ao_client_dll_decorated_function_strings.csv`
- `docs/reference/client-dll-function-map/ao_client_dll_exports.csv`
- `docs/reference/client-dll-function-map/ao_client_dll_summary.csv`
- `E:\AORebirthWebsite\HANDOFF.md`
- `E:\AORebirthWebsite\ao\downloads\AORebirthClientPatch-PrivateTesterInstructions.txt`
- `E:\AORebirthWebsite\ao\client-patch-test.php`
- `E:\AORebirthWebsite\deploy\website\apache-site.conf`
- `E:\AORebirthWebsite\deploy\website\docker-compose.yml`
- `E:\AORebirthWebsite\ao\uwg.daily.icc-rk\index.app`
- `E:\AORebirthWebsite\ao\uwg.daily.icc-rk\claim.php`

The current AORebirth source tree does not contain the private client patch
source, the `version.dll` proxy source, or an installer project for
`AORebirthClientPatchSetup-v1.exe`. Search evidence found only the deployed
installer documentation and website test page.

## Current deployed server-side state

The AORebirth Linux website now hosts the extracted DailyLogin app under:

```text
/uwg.daily.icc-rk/index.app
/daily/
```

The public route:

```text
https://ao-rebirth.com/daily/claim.php
```

returns live DailyLogin board state from the ZoneEngine durable claim directory.
For the observed login, the response included:

```text
accountKey=subarumike
hasIdentity=true
claimedCount=0
taken=[]
nextDay=1
```

Therefore the blank in-game DailyLogin window is not caused by missing
AORebirth DailyLogin state. The remaining failure is client-side routing of:

```text
vgtp://uwg.daily.icc-rk/index.app
```

to the AORebirth-hosted web app only when the active dimension is AORebirth.

## Client patch architecture

The available client patch documentation says the private installer:

- installs an AORebirth `version.dll` proxy into the selected AO folder;
- updates `DimensionServer.url`;
- updates `AnarchyLauncher.url`;
- updates `cd_image\data\launcher\DimensionServer.url`;
- updates `cd_image\data\launcher\AnarchyLauncher.url`;
- backs up replaced files using `.AORebirthBackup`.

No documented client patch source, hook list, installer source, registry owner,
configuration schema, or network interception implementation exists in the
tracked AORebirth repository.

The current documentation does not claim that the installer modifies:

- hosts file;
- DNS;
- WinSock;
- WinInet;
- WinHTTP;
- Awesomium callbacks;
- AO browser URL routing.

No old global DailyLogin host override was found in the inspected source/docs.

## Dimension detection

The reliable active-dimension signal is not yet proven because the private
`version.dll` source is absent.

Known dimension identifiers from the deployed dimensions file are:

```text
AORebirth:
  displayname = AORebirth
  connect = 2.24.96.30
  ports = 7500

AORebirth Local:
  displayname = AORebirth Local
  connect = 127.0.0.1
  ports = 7500

Official Rubi-Ka:
  displayname = Rubi-Ka
  connect = cm.d1.funcom.com
  ports = 7505

Official RK2019:
  displayname = Rubi-Ka 2019
  connect = cm.d1.funcom.com
  ports = 7506
```

The preferred future signal is the selected dimension record or actual login
endpoint tuple, not UI text. The safe fail-open rule is:

```text
known AORebirth endpoint -> apply AORebirth DailyLogin override
anything else -> untouched original behavior
```

Do not implement:

```text
not official -> AORebirth
```

Unknown state must preserve original behavior.

## VGTP path evidence

Existing evidence records the AO client opening:

```text
vgtp://uwg.daily.icc-rk/index.app
```

for the Daily Login Rewards window.

Reverse-engineering references identify the relevant client browser module:

```text
GUI.dll BrowserModule_c::SlotDailyLoginWindowActivated
GUI.dll BrowserModule_c::OpenBrowserFor
GUI.dll BrowserModule_c::AweBeginNavigation
GUI.dll BrowserModule_c::AweBeginLoading
GUI.dll BrowserModule_c::AweTargetUrlChanged
Awesomium.dll awe_webview_load_url
Awesomium.dll awe_webview_set_callback_resource_request
Awesomium.dll awe_webview_set_callback_resource_response
```

The current evidence does not prove whether the `vgtp://` URL becomes HTTP
before Awesomium, inside Awesomium, or through an AO-owned resolver. It also
does not prove whether Windows DNS, AO custom host resolution, or an Awesomium
resource callback owns `uwg.daily.icc-rk` resolution.

Because that path is not fully proven, broad DNS or process-wide network hooks
must not be implemented from this evidence alone.

## Interception point ranking

### 1. AO browser/VGTP URL transformation hook

Preferred if the `version.dll` layer can patch or wrap the AO browser open/load
path.

Reason:

- most specific to the DailyLogin URL;
- can include active-dimension state;
- can include exact host and path checks;
- does not affect official dimensions;
- process-local by construction.

Required missing evidence:

- `version.dll` source or binary hook map;
- proof that the browser URL string can be rewritten before load.

### 2. Awesomium resource request callback

Acceptable if the patch can install or wrap
`awe_webview_set_callback_resource_request` for the AO process.

Reason:

- still browser-specific;
- can filter exact host/path;
- can fail open to original handling.

Risk:

- must not override unrelated Awesomium requests;
- callback ownership and call ordering must be proven.

### 3. Awesomium `awe_webview_load_url` hook

Possible if the `vgtp://` URL reaches Awesomium unchanged or after predictable
transformation.

Reason:

- still narrower than DNS;
- process-local.

Risk:

- if `vgtp://` is transformed before this API, this hook may be too late or see
  only an already-failed URL.

### 4. WinInet/WinHTTP interception

Lower preference.

Reason:

- may be narrower than WinSock if Awesomium uses those APIs;
- can filter host/path.

Risk:

- current evidence does not prove Awesomium uses WinInet/WinHTTP here;
- could affect unrelated embedded browser traffic.

### 5. WinSock `getaddrinfo` / DNS interception

Rejected unless all AO/Awesomium-specific routes are proven impossible.

Reason:

- process-local if implemented inside `version.dll`;
- could distinguish process/dimension.

Risk:

- host-only, not path-specific;
- affects every request in the process;
- easy to accidentally redirect official DailyLogin if dimension state is stale.

### 6. Hosts-file or machine DNS override

Rejected.

Reason:

- machine-global;
- not dimension-aware;
- breaks official Rubi-Ka/RK2019 coexistence;
- breaks multiple-client isolation.

## Preferred routing contract

The safest implementation contract is:

```text
if activeDimension == AORebirth
and scheme == vgtp
and host == uwg.daily.icc-rk
and path == /index.app
then load AORebirth DailyLogin target
else call original behavior unchanged
```

The AORebirth target should be configuration-driven where possible:

```text
https://ao-rebirth.com/daily/index.app
```

or, if the client browser cannot use HTTPS safely:

```text
http://ao-rebirth.com/daily/index.app
```

Do not hardcode `2.24.96.30` into the DLL while a stable hostname is available.
Do not disable TLS validation.

## Dimension switching

The redirect state must be process-local and recomputed from current dimension
state.

Required behavior:

```text
AORebirth -> official:
  clear AORebirth override immediately or fail open to original behavior

official -> AORebirth:
  activate override only after AORebirth dimension is selected/proven

unknown / no active dimension:
  original behavior
```

No installed file, hosts entry, registry value, or machine-global DNS state may
be used as the active routing switch.

## Multiple clients

The routing decision must live inside each AO process. One AORebirth client
process must not redirect an official client process.

This requirement rejects hosts-file mutation and any machine-global DNS or proxy
state.

## Security constraints

If configuration is added:

- allow only the known AORebirth DailyLogin target;
- validate hostname and scheme;
- reject command-line or shell-style values;
- do not create arbitrary proxy behavior;
- do not disable TLS or certificate validation;
- do not intercept official content.

## Implementation status

No code implementation was performed.

Reason:

```text
BLOCKED: The private client patch / version.dll source and installer project
are absent from the authoritative AORebirth tree, and the exact VGTP resolution
owner is not yet proven.
```

Implementing a hook without that evidence would risk a broad process-wide
network change or an official-dimension regression.

## Required next evidence

Before implementation:

1. Reconcile the located private client patch source from the parallel Linux
   build tree into the authoritative AORebirth workflow.
2. Identify how `version.dll` records or observes the selected dimension.
3. Prove the URL stage where `vgtp://uwg.daily.icc-rk/index.app` can be
   transformed or intercepted.
4. Add focused unit tests for AORebirth, official Rubi-Ka, RK2019, unknown
   dimension, different host, and dimension switching.
5. Validate with the actual patched client without hosts-file or DNS mutation.

## Proposed implementation plan after evidence is supplied

1. Add a process-local active-dimension state owner to the client patch if one
   does not already exist.
2. Populate that state from the selected dimension record or login endpoint
   tuple.
3. Add the narrowest proven browser/VGTP interception point.
4. Apply only this rule:

   ```text
   AORebirth + vgtp://uwg.daily.icc-rk/index.app
       -> AORebirth DailyLogin target
   ```

5. Call original behavior for every other dimension, host, path, or unknown
   state.
6. Reset state on launcher return, logout-to-dimension-select, client shutdown,
   and failed/unknown dimension selection.
7. Keep the installer free of hosts/DNS mutation.
8. Run the focused routing test matrix.
9. Validate actual client behavior against AORebirth and at least one official
   dimension.
