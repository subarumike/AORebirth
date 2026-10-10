# Current Task

Reconstruct observed mission destination-zone distributions on
codex/mission-destination-integration from the original retained offer journals.
Preserve working runtime repair 73fc97a75 and client verification c24456369.

Requested outputs: complete level/slider/zone, expected-QL/zone, zone/level
tables, overlapping-level comparisons, CSV/JSON datasets and readable reports
under docs/generated/missions/. Preserve source offer references and all
original capture conditions; mark missing combinations UNOBSERVED.

Validation must reconcile 93,185 retained offers, including 92,830 raw-backed
exact destinations and 355 unresolved offers, 812 identities and 22 destination
playfields. Read original capture records and verify original packet ownership;
do not substitute aggregate summaries as the source of observations.

Keep current established expected QL distinct from original capture-time labels
and unavailable live decoded QL. Separate terminal geography, secondary settings
and character metadata before interpreting overlap. Frequencies and observed
min/max ranges are not proven destination weights or eligibility restrictions.

Analysis only: no live captures, runtime mission generation, DAO/schema,
GameData, client or service changes. The working repair remains documented in
docs/evidence/MISSION_ROLL_ELIGIBILITY_REPAIR.md.

Original-source extraction and generation passed: 77 journals, 79,567 events,
18,642 requests, 18,638 cohorts and all 93,185 offers reconciled. The output
contains 66 observed level/slider/terminal combinations and 66 UNOBSERVED
cross-terminal combinations. Independent zone counts and observed QL sets match
all 22 retained catalog playfields. Independent CSV/JSON/ledger arithmetic and
hash review passed. Original-source --check regenerated all 23 output files
byte-for-byte. No runtime/GameData diff, compilation, gameplay suites, service
changes or new captures. Analysis complete.
Reports: docs/generated/missions/destination-zone-distribution/README.md.
