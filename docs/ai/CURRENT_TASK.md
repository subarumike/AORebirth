# Current Task

The Windows SQL-access follow-up is complete, committed and pushed as
`8ccb7979f34412324fb615bf9a3e6aed709e585d` on
`codex/windows-local-package`. The package is based on public master
`6ec06bbd45e72496923275dcc8fd4e25fecace73`. Mike explicitly rejects random
passwords and a full-package upload; this delivery is a small overlay only.

The authorized local credentials are root / aorebirth and
aorebirth_local / aorebirth. Fresh installations use these defaults. Existing
owned databases update their saved/random credentials on the next explicit
Start server action without resetting data or changing schema. Existing
PackageController/PackageState and DatabaseSetup are reused; the helper's set-passwords
command retains owned-server, datadir and selected-port checks and parameterized
SQL. Engine core, schema definitions, GameData and gameplay are unchanged.

SQL login opens a read-only modal showing the local root endpoint and credentials
after the first Start has applied them. The password is masked initially, with
Show password and an explicit Copy password action. Never write credentials to
status/log output or copy them automatically. No custom-password controls are
requested.

Setup remains file-only and starts no services. Start alone provisions the
database and applies the credential update. Existing SQL servers stay untouched;
all package database consumers use the same saved private port, automatically
selected when 3308 is occupied. Existing data must never be reset.

PROVEN current-machine acceptance: launcher/helper compilation and secrets scan
passed. Rendered UI checks passed read-only fields, default masking, reveal/remask
and all 12 buttons fitting in three rows. The Copy handler is wired but was not
invoked; the clipboard was untouched.

PROVEN fresh and existing-ready acceptance (exit 0): Prepare started no services;
explicit Start provisioned the fresh database or applied the existing credential
update. Both known-password logins and Stop/reopen/restart/Stop passed while a
controlled 3308 listener remained unchanged. The existing database identity and
saved port were retained without first-time schema preparation. SQL-detail
display while stopped passed. Detailed evidence remains in PROJECT_STATE.md.

UNVERIFIED limits: existing pre-change SQL row counts were not probed, and no
nonempty player-data preservation case was tested. Partial-failure retry was
source-reviewed, not runtime-injected. An actual external SQL workload,
clean-machine prerequisites/UAC and live client behavior remain unverified.

PROVEN overlay closure: finalization exited 0 with PACKAGE_INTEGRITY=PASS and
clean provenance at the pushed source commit. Six payload files total 685,011
bytes; all 9,306 original files were preserved, including all 8,090 GameData
files. The staged launcher --verify-package exited 0 with
PACKAGE_INVENTORY_AND_CONFIGURATION_VERIFIED. The approved stop wrapper ran
after acceptance and engine ports are closed.

The overlay and validation records remain private, local and ignored. No full
ZIP was generated, rebuilt or uploaded. Distribution and Mike's live client
acceptance remain separate. No Linux work, deployment, automated
fixture/unit/regression suites or AO client launch was performed.

PUBLIC BASELINE OBSERVATION: the existing empty-server Zone disposal warning from
InventoryFlushService was recorded with exit 0 and unchanged core bytes; it is
not part of this repair. Clean-machine prerequisites/UAC, account registration
POST, complete client download and live gameplay remain UNVERIFIED.
