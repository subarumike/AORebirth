# WebCore Bootstrap Security Evidence — 2026-08-02

This original asset-bootstrap phase is superseded for maintained-PHP
compatibility and final acceptance by
`WEBENGINE_PHP_COMPATIBILITY_20260802.md`. The immutable upstream pin and base
asset manifest remain authoritative.

## Scope

This reconciliation covers only the optional WebEngine WebCore asset supply,
integrity validation, and startup gate. It does not change gameplay, packets,
database schema, HTTP routing, or PHP execution behavior.

## Root cause

The historical WebEngine configuration pointed at the mutable URL
`https://github.com/CellAO/CellAO-WebCore/archive/master.zip`.
`Checks.CheckWebCore` downloaded that URL to `WebCore.zip`, extracted it into
`htdocs`, and exposed the operation through the manual `checkWebCore` console
command. This provided neither immutable upstream identity nor an offline
startup boundary.

No AORebirth-specific WebCore asset set or local WebCore asset tree existed to
serve as a stronger authority. The repair therefore adopts a reviewed exact
upstream snapshot through an offline-only operator import.

## Evidence inspected

- AORebirth history at commit `73492f145` (2014-03-30): mutable `WebCoreRepo`
  configuration, `Checks.CheckWebCore`, `WebCore.zip`, `htdocs`, and the manual
  `checkWebCore` command.
- AORebirth password implementation at `73492f145`: PBKDF2-HMAC-SHA1, 1,111
  iterations, 30-byte salt, and 30-byte hash.
- CellAO WebCore commit
  `765c3850767b63af1cd259bab7f2f7ca3e97adf9` (2014-04-01): matching login
  password contract.
- Current AORebirth configuration, WebEngine startup, HTTP/PHP hosting paths,
  build inputs, engine-management tests, and optional-engine workflow.
- Repository and local asset search: no AORebirth-specific or pre-existing local
  WebCore asset authority found.
- Broader repository bootstrap search: no other mutable archive bootstrap was
  found. An unrelated legacy HTML-fetching utility remains outside this scoped
  WebEngine/WebCore dependency path.
- Upstream snapshot review: no upstream license file found.

## Reconciled authority

| Property | Reconciled value |
| --- | --- |
| Upstream repository | `https://github.com/CellAO/CellAO-WebCore` |
| Upstream commit | `765c3850767b63af1cd259bab7f2f7ca3e97adf9` |
| Archive root | `CellAO-WebCore-765c3850767b63af1cd259bab7f2f7ca3e97adf9` |
| Archive SHA-256 | `ef297e623040b375e64c543568ca94e44ed7cc59de6fe826ed5e42db95c020ab` |
| Extracted file count | 7,140 |
| Extracted byte count | 26,648,501 |
| Manifest SHA-256 | `85c1515d274c2e4051013e89ca6d2a355365d5d01df7d621cc060dfa84e38463` |
| License status | Unresolved; no upstream license file |

The exact commit was selected because it immediately follows and matches the
AORebirth password-storage change. That proves a narrow contemporaneous login
contract, not broad runtime or production compatibility.

## Repair contract

- Removed the mutable WebCore URL from runtime configuration.
- Removed runtime WebCore acquisition; no other mutable archive bootstrap is
  permitted.
- Added `/import-webcore-assets <local-zip> <exact-version>` as the only asset
  provisioning path.
- Added a checked-in exact file manifest and copied runtime manifest beside
  `WebEngine.exe`.
- Added a production parse-only manifest check that pins the repository, full
  commit, archive identity, license marker, 7,140-file count, and 26,648,501-byte
  total independently of synthetic asset fixtures.
- Added `/validate-webcore-assets` for installed-tree validation and
  `/self-test-webcore-assets` for deterministic, dependency-free contract
  validation.
- Held an exclusive WebCore lease for the lifetime of a process after its
  listener starts, preventing offline import while legacy listener/request
  threads may still exist. Import holds the same lease through activation.
- Inserted WebCore validation after database, binary, and PHP checks and before
  ownership/port checks and launch.
- Added WebCore source/behavior contracts to mandatory gate stage 2 with network
  access denied.

## Preserved behavior

WebEngine remains the HTTP host and continues to execute configured PHP content
only after all optional-engine prerequisites pass. The repair changes how its
website files are supplied and trusted; it does not redefine the served content
or claim that the historical site is supported in production.

## Validation status

Production integration and validation are complete:

| Validation | Status |
| --- | --- |
| Deterministic WebEngine/WebCore security tests | Historical bootstrap phase: PHP 7/7; WebCore 36/36; production manifest and source contracts PASS |
| Engine-management ownership contracts | PASS: 22/22 |
| Approved debug build | PASS |
| Complete mandatory integration gate | Historical bootstrap phase: PASS 12/12 twice; current 13-stage results are recorded in the superseding evidence |
| Final secret scan and clean-worktree check | PASS |

The deterministic focused and complete-gate paths used no database credential,
live database, PHP runtime, production engine, AO client, capture tooling, or
network service. Common proxy-aware outbound paths were denied, while a source
contract separately proved that the asset manager contains no network API or
acquisition-command path; this was not an OS-level egress sandbox.

## Remaining risks

- The upstream snapshot contains no discovered license file. Redistribution and
  production use remain unresolved.
- The obsolete PHP/MySQL/mcrypt/config dependencies were reconciled
  deterministically against PHP 8.5.9; see the superseding evidence for the
  exact dispositions and remaining live-verification boundary.
- The local environment has no valid MySQL credential, so live database and
  WebEngine startup verification remain externally blocked.
- Same-volume directory renames provide exception-safe rollback but not a
  crash-atomic directory exchange. Interruption between the live-to-backup and
  staging-to-live renames leaves startup fail-closed with the validated prior
  tree retained under its backup name for operator recovery.
- WebEngine remains optional and is not production-safe. Exact asset integrity
  does not remove those support boundaries.
