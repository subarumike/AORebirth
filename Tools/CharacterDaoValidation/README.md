# Character DAO isolated validation

From the isolated repository root, with local Docker already running:

```cmd
Tools\run_character_dao_validation.cmd
```

Run the same complete command twice for acceptance. The wrapper accepts no operands,
reads no application connection string, and supplies the exact disposable-run
acknowledgement required by the executable. It builds and executes the actual linked
production contracts/DAOs, legacy `CharacterDao`/generic DAO/entity dependencies,
and narrowly selected unchanged offline compatibility sources. It does not initialize
an engine, open a game client, exercise packets, integrate runtime consumers, or deploy.

## Fixture safety

The only SQL target is a freshly created MySQL fixture:

- Pinned image: `mysql@sha256:c592c15aaf4a1961e15d82eb31ea5987dda862d1c4b1e93424438c0e91dc1f8d`.
- Host binding: `127.0.0.1:33071`; database `aorebirth_character_dao_validation`.
- Container: `aorebirth-character-dao-validation`.
- Dedicated bridge: `aorebirth_character_dao_validation_internal` (the historical
  naming convention does not mean a Docker `--internal` network).
- Disposable volume: `aorebirth_character_dao_validation_data`.
- Per-run ownership label: `org.aorebirth.character-dao-run`.

The runner refuses pre-existing named resources or an occupied port. It does not
reuse another database or fall back to repository/runtime configuration. It creates
random credentials in a temporary hidden environment file; credentials, connection
strings, and Docker stderr are never printed. Startup is bounded to 120 seconds;
individual Docker subprocesses are bounded to 60 seconds. Failures print sanitized
case/type/error-number diagnostics, not SQL parameter or credential values.

Only the unchanged authoritative `AORebirth.Database/SqlTables/characters.sql` is
imported. Test rows are inserted/reset only in that owned fixture. There is no
application schema change and no migration. Cleanup verifies the per-run ownership
label before deleting its own container, volume, and bridge, then removes its own
temporary credential file. `CHARACTER_DAO_DISPOSABLE_CLEANUP=PASS` is required in
addition to the suite PASS. A terminated host/process can leave owned resources;
subsequent runs refuse them rather than deleting unverified resources automatically.

## Coverage and evidence boundaries

Every assertion emits `PASS [category] exact-case-name`; the final
`CHARACTER_DAO_CHECKS` marker is the complete assertion count. The acceptance handoff
records both complete run logs and a machine-readable assertion inventory.

The frozen candidate passes **529 assertions**: fixture 3, contract 11, directory
73, matched-rows 25, changed-rows 25, stale 29, ownership 42, faults 248, uncertain
32, concurrency 28, synthetic-defensive 5, and legacy-offline 8. The eight offline
host assertions invoke all 29 unchanged original cases (11 + 13 + 5); those are
reported separately, not added to 529 as a fabricated unique total. Final acceptance
requires two complete runs of the identical candidate and successful owned cleanup.

| Category | Evidence |
| --- | --- |
| fixture/contract | Canonical table identity/collation/key, eight-method neutral contract, lazy three-DAO factory, provider rejection. |
| directory | All seven fields; missing, null, empty, punctuation/wildcard/parameter-like inputs; real collation; exact account filtering; detached results; real legacy read parity. |
| matched-rows/changed-rows | Both `UseAffectedRows` modes, same/missing/null state writes, no unrelated-column changes, actual legacy raw counts and owned transactions. |
| stale | Serializable transaction, actual database comparison, ordered old values, exact bounded IDs, affected/count verification, empty read-only rollback, unchanged legacy store characterization. |
| ownership | Each of eight operations twice on the same DAO with distinct owned connections; reader/command/transaction disposal. This is resource ownership, not authenticated-session authorization. |
| faults | All eight factory/open/command paths; transactional begin/null/rollback paths; partial readers; four stale statements; affected/count mismatch; disposal failures; original/secondary exception identities. |
| uncertain | Failure before durable commit versus injected lost acknowledgement after a real MySQL commit, on all three writes; fresh-read/recovery reconciliation. |
| concurrency | Actual MySQL lock waits, captured and scanned-but-unselected writers, rollback lock release, two simultaneous recoveries, concurrent online/offline writes. |
| synthetic-defensive | Explicit invalid-reader/invalid-schema observations that the canonical schema cannot physically represent. |
| legacy-offline | All unchanged stale-online 11, login handoff 13, and hydration source-contract 5 cases, with all database acquisition forbidden. |

The canonical schema permits duplicate display names and empty name/account strings;
it rejects duplicate primary IDs and null display/account names. Nullable `Online`
is exercised in real MySQL. Synthetic readers separately test duplicate IDs,
duplicate ownership results, invalid Online mapping, and defensive null name/account
mapping. They are not claims about possible canonical database rows.

Ordinary query order remains unspecified. Collection parity is normalized only in
the test comparison. Compatibility comparisons deliberately project a nullable new
Online value through `?? 0` where the unchanged legacy `DBCharacter` loses the null
distinction; separate assertions prove the new DTO preserves factual null. A duplicate
name lookup promises a matching first row, not a universal ordering among matches.

Actual legacy `SetOnline`/`SetOffline` use generic DAO-owned transactions. Therefore
the durable-write acknowledgement test uses a real committed transaction, **not an
autocommit claim**. The ADO observer delegates real SQL, reads, writes, locks, commits,
rollbacks, and disposal to MySQL, injecting deterministic failures at stated boundaries.
Lost acknowledgement is injected after the real provider's `Commit` returns; it is
not a network-fault proxy. A thrown commit exception does not prove unchanged data.
Secondary rollback/disposal failures remain diagnostic details on the original error.

Stale recovery's post-update zero count describes its transaction's verified state,
not a guarantee that a later writer cannot mark a character online. The concurrent
writer tests observe actual `performance_schema.data_lock_waits`, then prove a fresh
recovery can capture a later committed state. The canonical unindexed predicate may
also lock nonmatching scanned rows; the test does not invent a matching-row-only lock
guarantee. Synchronization and completion waits are bounded.

The unchanged Stage8 stale and handoff test classes are linked without their original
Program, which would initialize ZoneEngine. The five unchanged hydration methods run
through a narrow test-only throwing assertion/attribute shim, not a full MSTest runner.
These 29 cases are source-only compatibility evidence, not full engine or gameplay
acceptance. Factory acquisition is forbidden and counted even if an old test catches
the failure. No legacy source file is rewritten or copied into an alternative DAO.

The project is intentionally source-isolated from unrelated full-repository baseline
build failures. It does not replace the required full Windows, source-inventory,
architecture, account (273), mission (202), and login-authentication gates. Their exact
results and baseline reproductions belong in
`docs/reports/CHARACTER_DAO_PARALLEL_HANDOFF.md`.
