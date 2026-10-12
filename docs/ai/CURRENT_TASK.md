# Current Task

Add SQL login access and fixed local credentials to the Windows package based
on public master `6ec06bbd45e72496923275dcc8fd4e25fecace73`.
Mike explicitly rejects random passwords and a full-package upload. Deliver only
a small overlay update; do not rebuild or upload the full package.

The authorized local credentials are root / aorebirth and
aorebirth_local / aorebirth. Fresh installations use these defaults. Existing
owned databases update their saved/random credentials on the next explicit
Start server action, preserving accounts, characters and schema. Reuse existing
PackageController/PackageState and DatabaseSetup; the helper's set-passwords
command retains owned-server, datadir and selected-port checks and parameterized
SQL. Do not change engine core, schema definitions, GameData or gameplay.

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

PROVEN fresh retry (exit 0): Prepare started no services; Start created the
database and selected private port 46513 while a controlled 3308 listener stayed
held. Packaged mysql client SELECT checks succeeded for both known-password
logins. The database had 59 tables and zero login/character rows. Stop, reopen,
restart, Stop and SQL-detail display while stopped passed.

PROVEN existing-ready case (exit 0): the retained-state legacy-credential
assertion passed. There was no first-time schema preparation stage;
auto.cnf identity hash and saved port 64593 were retained. Both known-password
logins succeeded, and the 59-table/zero-login/zero-character snapshot after Start
was unchanged across restart. Stop/reopen/restart/Stop passed and the controlled
3308 listener remained unchanged. The initial runtime attempt refused other
active-checkout engines before launching; the approved wrapper stopped those
positively identified engines, and the retry passed.

UNVERIFIED limits: existing pre-change SQL row counts were not probed, and no
nonempty player-data preservation case was tested. Partial-failure retry was
source-reviewed, not runtime-injected. An actual external SQL workload,
clean-machine prerequisites/UAC and live client behavior remain unverified.

Small overlay construction is pending. No full ZIP was generated, rebuilt or
uploaded for this follow-up. Only the required launcher/helper payload is rebuilt;
private packages and validation records remain in ignored output. No Linux work,
deployment, automated fixture/unit/regression suites or AO client launch are
authorized. Prior package/coexistence results at
197dff8bc5b2acd55d271aa004d8333dee4a4ec0 remain historical in PROJECT_STATE.md.
Mike performs live client acceptance.

PUBLIC BASELINE OBSERVATION: the existing empty-server Zone disposal warning from
InventoryFlushService was recorded with exit 0 and unchanged core bytes; it is
not part of this repair. Clean-machine prerequisites/UAC, account registration
POST, complete client download and live gameplay remain UNVERIFIED.
