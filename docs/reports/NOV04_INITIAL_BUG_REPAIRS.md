# November 4 initial bug repairs

Date: 2026-11-04. Starting authoritative public source:
`831d28ec8b325112769d73b7b4c2b29e3be4029a`.

## Scope and confidence

The authorized repair branch is `codex/nov04-bug-repairs`. Work is limited to
reports **152, 96, 92 and 73/95**, with separate repairs and focused regression
checks. Other open reports remain outside this implementation scope.

`PROVEN` below refers to inspected source/data or measured automated results.
`UNVERIFIED` includes original-client behavior and current production source and
GameData parity. Isolated development was explicitly authorized with those
production identities unverified; it does not establish release acceptance.

## Findings and validation

| Report | Proven finding and scoped change | Validation and remaining work |
| --- | --- | --- |
| **152: Basalis ring upgrading** | The selected item family omits the upper endpoint needed by the existing native upgrade operation. The isolated content candidate also preserves its existing initial-loot consumer using an already identical fixed-quality alias. | Original catalog: **1 passed / 8 failed**. Endpoint-only candidate: nine upgrade passes but **two added collateral failures**. Corrected candidate and committed rerun: **11/11**, including 48 deterministic native loot comparisons across all three branches and QL22–37. Original canonical content remains unchanged. |
| **96: starter Cyberdeck variant** | Starter selection could grant a sex-restricted variant without matching the new character. Optional data-driven selectors now apply after existing breed/gender normalization, with malformed/overlapping placement validation and the existing constructor interface preserved. | The three native eligibility rows initially produced **1 passed / 2 failed**. Committed focused cohort: **65/65**, including 168 breed/profession/requested-gender combinations, cache/metadata retention and old unconditional rows. End-to-end database creation and client behavior remain unverified. Existing characters are not updated. |
| **92: homogeneous team side** | Every team projected the Omni constant. Existing immutable roster metadata and PlayerCard snapshots now preserve an optional valid character Side; explicitly present zero means Neutral. Homogeneous teams project their common faction. | The Neutral and Clan regressions failed on the starting source. The committed focused cohort passes **22/22**. The full cohort is **98 total: 85 passed and 13 proven unchanged public-baseline failures**, with no new failures. Membership, zoning and reconnect contracts remain in place. |
| **73/95: mission chest combat** | Existing chest open and transfer permissions omitted the player's own combat state. Two guards now use that state and the existing refusal paths; the common interaction gate remains unchanged so closing is preserved. The transfer permission also protects existing chest deletion. | Original code: **5 passed / 6 failed**. Corrected and committed rerun: **11/11**. Actual transfer/deletion handlers reject incoming/outgoing combat without memory, persistence or success-acknowledgement changes; after combat, commits precede acknowledgements. Closing, actual teammate/pet-only combat and corpse opening remain supported. Exact client feedback remains unverified. |

Report 92 intentionally retains the existing Omni fallback for mixed or unknown
member factions; this is preservation of current behavior, not proof of intended
mixed-team policy. Local faction changes use the existing fenced owner dispatch.
Remote live faction changes become visible at existing normal team-snapshot,
join, zoning or reconnect refresh boundaries; no new World notification mechanism
was introduced. Team-level aging remains uninvestigated.

## Proven public-baseline exceptions

There are **18 known inherited assertion failures**, classified separately from
the repaired regressions:

| Subsystem | Exact starting-source reproduction | Treatment |
| --- | --- | --- |
| Team packet-contract assertions | **13 cases** reproduced with identical failure messages on exact public source `831d28ec8b325112769d73b7b4c2b29e3be4029a`, using the same minimal current-interface fixture adapters as the repair run. | Original assertions remain enabled and unchanged. The full post-repair cohort retains the same 13 signatures. Reuse this evidence while relevant inputs and signatures remain unchanged. |
| Account-creation field-count assertion | **3 data rows** reproduced on that exact public source: the historical assertion expects eight fields while the implementation supplies fourteen. | No account-creation behavior or historical expectation was changed for this repair. |
| Related inventory actions | **2 cases** reproduced with identical failure messages on that exact public source: `DeleteRetiresCorrectInstanceBeforeAcknowledgingAndDuplicateIsNoOp` and `UnsupportedOnUseAloneDoesNotCountAsExecuted`. Both repair and baseline runs are **25 total / 23 passed / 2 failed**. | Assertions and inventory implementation remain unchanged; record and retain the baseline evidence. |

The team cohort initially had eight fixture compilation errors from obsolete
chat/session interfaces. Only its required test doubles were adapted to the
current interfaces before the red comparison; this was not a product-behavior
repair or a relaxation of the inherited assertions.

These results must be described with their baseline exceptions; they are not a
plain all-green result for the broader cohorts. The four repair cohorts contain
109 passing cases; broader cohort exceptions remain visible above.

## Compilation and commits

Chat, Login, World and Zone compiled with **zero errors** on gameplay commit
`38facd0b2bafa09fcecf343ec3a0ecd25e704e2a`, through the existing feature helper
and the explicit projects selected by the current four-engine wrapper. The
wrapper itself aborted before compilation because inherited CMD PATH lacked
`findstr`/`powershell`; no engine cleanup executed. Its unconditional cleanup step
was not re-invoked. Compile-only feature validation is already supported; process
PATH and quoted MSBuild arguments were corrected without tooling changes. Logs
retain the failed wrapper and first argument-parsing attempt. These are local
compilation receipts, marked `DEPLOYABLE=NO` and `WINDOWS_USER_ACCEPTED=NO`.

| Public commit | Scoped purpose |
| --- | --- |
| `be0b804a6ff5b3b8dd004360d14dcb1a57fcaf33` | Native Basalis upgrade and boundary regressions. |
| `5bfc4c06f28f37dea0e7fd1a6fe1a01784867440` | Data-driven starter variant selection and regressions. |
| `4f13d6a42cee6d8d0c3ef22eae3c523aef338dff` | Homogeneous team-side publication and regressions. |
| `cdc5c85f1556f3a3b133dee812de4759857df90b` | Basalis initial-loot preservation regressions. |
| `38facd0b2bafa09fcecf343ec3a0ecd25e704e2a` | Chest own-combat permissions and regressions. |

The 71-report ledger, duplicate/overlap evidence, private candidate commits and
measured input/result/receipt identities are retained privately outside runtime
content. Protected solved reports 136/151 remain unchanged.

## Boundaries and remaining acceptance

- Original protected content is preserved; separate private content candidates
  require their governed review and reconciliation. No private catalog records,
  infrastructure or release identities are published in this report.
- No database/schema operation, service restart, deployment, AO client control
  or website status change is part of this repair run.
- Production source parity and production GameData parity remain **UNVERIFIED**.
- Starter source and its corresponding data candidate must be reconciled
  together. The ring's direct test-only starter consumer still requests QL1;
  its effective QL/NCU are preserved, while its minted endpoint pair normalizes
  to the repaired range. Static acquisition searches do not prove client behavior.
- Automated checks do not establish Mike's exact-input Windows acceptance or
  original-client verification. No Linux release or live deployment is implied.

Recommended next batch: verify report 43's exact low/high ring consumers, then
recover the precise recipe/skill/QL/tool-retention evidence for reports 145/112
and 128. No additional repairs were attempted in this run.

