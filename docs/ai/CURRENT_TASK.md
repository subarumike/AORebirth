# Current Task

Completed the prebuilt Windows package Start/Stop and automatic database
provisioning repair for the package from public master
`6ec06bbd45e72496923275dcc8fd4e25fecace73`.

Mike authorized a precompiled package with the full release GameData, bundled
runtime and dedicated local MySQL, graphical first-run/account/play controls,
and a separate download of the fixed website client. Players must not need
Git, a compiler, developer tools, or terminal commands. Setup must prepare files
without starting services or requiring free ports. Only an explicit Start server
action may initialize the database and start local services.

Implementation is isolated under `Tools/WindowsLocalPackage`. Reuse the current
Windows project graph, configuration/GameData environment overrides, schema
tools, Account Broker, and engine headless/shutdown signals. Do not change
gameplay or existing engine behavior. Private GameData and binary packages
remain outside tracked source. No Linux work or deployment is authorized.

OBSERVED original failure: Start and Stop both reported "A required port belongs
to another process. No unrelated process was stopped." MySQL remained active
while the game database was absent. PROVEN by isolated reproduction: bundled
MySQL 8.4.11 starts a monitor
parent and a child that owns TCP 3308. The launcher tracks the parent, rejects its
own child's listener before DatabaseSetup runs, and repeats that rejection during
Stop.

The scoped launcher repair uses --no-monitor for the long-lived MySQL process.
Stop signals only an existing native shutdown event derived from the verified
held PID, supporting both the new process and an old monitor parent. It uses
bounded waits without TCP readiness, password probes, SQL shutdown or database
force-killing. Port errors identify the component, port and expected/actual PIDs.
The stateless Account Broker has no graceful hook; bounded termination of its
verified owned PID is expected and excluded from the abnormal-shutdown warning,
consistent with its existing exit-code handling. Other shutdown failures remain
visible. Engine source, database helper/schema semantics and GameData are unchanged.

Setup remains file-only. Start alone initializes the database and starts services;
account creation and Play require the already running server. Database-ready state
keeps its existing meaning. Original startup errors remain visible if cleanup also
fails. Earlier actual-handler checks proved setup works with TCP 7012 occupied,
creates no services/MySQL files/process logs, preserves settings and protected
credentials on repetition/reopening, and rejects Start port conflicts before any
service launch without touching their owners.

PROVEN: actual isolated launcher acceptance recovered the previous failed setup,
automatically initialized the schema, and completed Start, Stop, reopen, restart
and Stop. Subsequent current-machine acceptance through the actual launcher
controller began with a completely empty data directory and exited 0: setup
started no services, first Start became ready, first Stop completed, reopening
and restarting became ready, and second Stop completed. Both Stop statuses were
normal, without an abnormal-shutdown popup.

PROVEN: final package construction/integrity completed with exit 0 and
PACKAGE_INTEGRITY=PASS. The final staged launcher --verify-package returned 0
with PACKAGE_INVENTORY_AND_CONFIGURATION_VERIFIED. The full replacement and
overlay ZIPs passed their payload integrity checks. Exactly five payload files
changed; all 9,307 others, including all 8,090 GameData files, are preserved.
The fix is pushed as f0d1d575eeca3b12c952fbc639b18c6c03f66657 on
codex/windows-local-package. Binary packages, checksums and validation records
remain in private ignored local output and have not been published.
The repair is complete; distribution and live client acceptance are separate.

PUBLIC BASELINE OBSERVATION: empty-server Zone disposal logs an ObjectDisposed
InventoryFlushService warning while exiting with code 0. Core bytes are unchanged;
this warning is recorded, not repaired or promoted to gameplay acceptance.

UNVERIFIED: clean-machine prerequisites/UAC, account registration POST,
complete client download, and live login/play/restart. Mike performs
live client acceptance. Preserve existing databases and credentials; no automatic
reset or partial-database repair is authorized. No automated fixture/unit/
regression suites or AO client launch were performed for this repair.
