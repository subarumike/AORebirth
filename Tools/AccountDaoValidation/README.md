# Account DAO validation

Run from the chosen AORebirth checkout using the dedicated CMD wrapper:

```cmd
Tools\run_account_dao_validation.cmd
```

This suite always compiles the actual account contract/implementation and the
unchanged legacy LoginDataDao, CharacterDao, generic mapper and required
dependencies from this checkout. It does not replace the legacy DAO with a mock.
The actual DatabaseDaoFactory is linked; its mission dependencies are linked for
compilation only. No engine, service host, application configuration, packet
handler, password policy or runtime initialization is invoked.

The only substituted production infrastructure is a test-only Connector factory,
sanitized logging and the MySQL SQLType value needed by the legacy mapper.
The actual existing PasswordHash implementation is linked as a compatibility
assertion helper, not moved into persistence or modified.

## Disposable fixture safety

The wrapper supplies a dedicated acknowledgement. The executable additionally
requires the exact `--run-disposable` argument. There is no connection-string
argument or application-database fallback. In particular,
`AO_REBIRTH_MYSQL_CONNECTION` is never read.

The fixture uses the existing pinned MySQL image
`mysql@sha256:c592c15aaf4a1961e15d82eb31ea5987dda862d1c4b1e93424438c0e91dc1f8d`,
requires that image already exist, and refuses an existing named container,
network or volume or occupied loopback port. Resources are:

- container: `aorebirth-account-dao-validation`;
- network: `aorebirth_account_dao_validation_internal`;
- volume: `aorebirth_account_dao_validation_data`;
- published endpoint: `127.0.0.1:33070`;
- database: `aorebirth_account_dao_validation`.

The network name is historical naming consistency, not a Docker internal-mode
claim: it is an ordinary dedicated bridge. MySQL is published only on host
loopback, matching the proven mission fixture pattern. Docker internal mode on
this host suppressed the needed publication and was removed from the runner.
No broker HTTP/SMTP listener or game listener is started.

A fresh per-run label verifies ownership before removal. Generated fixture
credentials live in a hidden temporary environment file, not source or command
arguments, and are removed during cleanup. Provider messages, hashes and
connection strings are never printed. Docker stdout/stderr is captured internally
and not dumped. Cleanup is verified and reports
`ACCOUNT_DAO_DISPOSABLE_CLEANUP=PASS`; a cleanup failure fails the run and needs
review, never a broad Docker prune.

Only the unchanged canonical `login.sql` and `characters.sql` definitions are
loaded into the empty fixture. No source schema, migration, application table or
production row is changed. Fixture DML, including the deliberately unscoped
legacy SetGM characterization, is confined to those owned disposable tables.

## Coverage and interpretation

Every assertion emits a readable `PASS [category] case-name` line; failures emit
the case name or sanitized exception type/MySQL number, never raw credentials.

| Category | Evidence |
| --- | --- |
| contract | Exact eight-method API; neutral DTO shapes; no GM/logoff/identity API; lazy factory; null command/factory; no invented DTO defaults. |
| reads | Empty/single account reads, count and existence; actual legacy zero/one parity; null/empty/case/space names; all data/auth fields; raw signed integers; DATETIME kind; detached results. |
| create | Caller fields, zero/empty values, unchanged existing hash compatibility, local clock, actual legacy writer parity, quote/backslash/semicolon and parameter-like usernames, duplicate/not-null/length failures, date boundaries. |
| resolution | Missing character versus missing/empty owner versus orphan account versus found account; actual legacy read parity; preserved owner/account spelling; real NOT NULL constraint; two reads on one connection without a transaction. |
| matched-rows / changed-rows | Both MySqlConnector UseAffectedRows modes; changed/same/missing password and expansion counts; exact SQL parameter binding and password LIMIT 1; unrelated rows preserved; actual legacy writer and error behavior. |
| concurrency | Unique-name insert has one winner; password/expansion writes do not overwrite each other; concurrent password writes preserve a complete last-writer value. |
| faults | Each operation twice on one DAO obtains/disposes distinct owned connections; factory/open/command faults for all operations; reader, second-read and partial-buffer failures; mapping/resource errors; legacy error fallbacks; unsupported configured-provider rejection; already-open connections; lost autocommit acknowledgement reconciliation. |
| mock-defensive | Invalid-schema NULL owner, duplicate character/account rows and invalid numeric conversion are injected readers only. They do not alter or bypass real schema constraints. |

The actual legacy SetGM method is unchanged and deliberately has no target API.
Each observation records supplied-name category, affected-row mode, total fixture
rows, actual affected rows and the number of rows now at the requested level.
An existing name, nonexistent name and null name all exercise the same actual
unscoped SQL. Repeating the same value yields zero changed rows but matches all
rows under matched-row mode. This is characterization, not authorization to
use or reproduce that behavior in the new DAO.

Canonical login.Username is UNIQUE and NOT NULL. Therefore physical duplicate
account rows and nullable persisted login fields are not applicable fixtures;
uniqueness/not-null rejection is tested without changing constraints. The
multi-row invalid-schema tests execute only the new DAO against synthetic
readers. Actual legacy FirstOrDefault and Count()==1 behavior is source-inspected;
real-MySQL legacy/new parity covers zero/one matches. The password LIMIT 1 is
observed on the real command, not claimed proven by synthetic duplicate reads.

Account mutations are single-statement autocommits; the API has no transaction,
commit or rollback scope. Tests require zero BeginTransaction calls and verify
pre-execution failure produces no write. An injected error after real execution
demonstrates that a mutation may already be durable when an acknowledgement is
lost. There is no fabricated transaction rollback or cross-call atomicity claim.
Character-to-account resolution is two reads, not an atomic snapshot.

This is not full LoginEngine/ChatEngine/AccountBroker/unified-identity integration,
MSSQL/PostgreSQL parity, Windows legacy-project runtime acceptance or live login
protocol acceptance. Those remain separately governed validation lanes. The
source-isolated suite is not permission to remove or hide failures in full
project-reference tests.

## Source inventory

- `Program.cs`: real-MySQL contract, read/create/resolve/mutation/concurrency and legacy GM tests.
- `FailureChecks.cs`: owned-resource/failure matrix and explicit synthetic readers.
- `DisposableMySql.cs`: scoped Docker fixture and cleanup.
- `IsolatedHost.cs`: test-only infrastructure dependencies.
- `AccountDaoValidation.csproj`: exact production/legacy source links.
- `acceptance-evidence.json`: machine-recorded acceptance case inventory, baseline
  commands/diagnostics and retained log hashes; local raw logs remain under ignored build-verify.
- `../run_account_dao_validation.cmd`: sole run entry point.

The complete source link list is authoritative in the test project. Files outside
this test subtree are compiled unchanged, not copied or rewritten.
