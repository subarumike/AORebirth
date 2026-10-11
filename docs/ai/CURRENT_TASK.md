# Current Task

Repair the prebuilt Windows package from public master
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

OBSERVED: a player reports setup/stop errors and MySQL remaining active during
first-time setup. The exact popup and underlying database failure are unknown.
PROVEN: the original launcher started MySQL and required all service ports during
setup. Create account and Play implicitly started services; no Start button
existed. Setup/shutdown failures could mask the original startup error.

The scoped repair makes setup file-only, adds Start server, and moves database
initialization into that explicit action. Database-ready state keeps its existing
meaning. Account creation and Play require a running server. Startup errors remain
visible if cleanup also fails. Fixed MySQL probe/shutdown SQL uses --execute to
avoid writing into an already exited client's stdin. This is not proof of the
player's exact shutdown failure.

PROVEN: the revised launcher compiles. Manual invocation of its actual setup
handler against the full package in a path containing spaces completes with TCP
7012 already occupied. No owned services, MySQL files or process logs are created.
Repeating setup/reopening preserves protected credentials and configuration;
Create account refuses before Start. Explicit Start reports an existing 6996
conflict before launching any services. Existing port owners were untouched.
The full replacement ZIP and small overlay ZIP are complete. Final staged
launcher verification and every payload hash in both ZIPs succeeded. Exactly five
package files changed: launcher executable, player README, provenance, inventory
and manifest. All 9,307 other inventoried files match the original package,
including all 8,090 GameData files. Package tooling is clean commit 72f3a8148343517c50129c8b6dfc0e1f72ef1cc3,
pushed to codex/windows-local-package. Private packages, checksums, update
instructions and the validation receipt remain in ignored local output.
Preserve existing databases and credentials; no automatic reset or
partial-database recovery is authorized. No automated fixture/unit/regression
suites or live AO client launch are authorized.

UNVERIFIED: clean-machine prerequisites/UAC, fresh database initialization,
account creation, complete client download, and live login/play/restart. Mike
performs live client acceptance; do not describe compilation as playable
acceptance. Existing databases must never be reset by first-run setup.

The setup/start boundary repair and local packaging are complete. Distribution,
fresh database/server acceptance and live-client acceptance remain separate.
