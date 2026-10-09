# Mission destination duplicate and integrity audit

## Result and scope

**PROVEN for the retained September corpus:** accidental duplication does not
inflate the 93,185 offer records, 92,830 exact destination observations, 812
observed destination identities, or 2,242 client placement records. No source,
request, cohort or offer needs removal on the evidence examined. No parser
double emission was found. All historical evidence and destination assignments
remain intact.

**OBSERVED:** 2,678 raw-backed five-offer responses contain repeated destination
identities. Every repeated position has its own offer index and nonoverlapping
raw byte range. They are legitimate server results and must remain separate
observations.

**UNRESOLVED:** 355 offers still lack raw response bytes. Complete July versus
September overlap cannot be proved or disproved because July's original
journals and complete per-offer provenance are unavailable. Neither limitation
is converted into an accidental-duplicate claim.

This is an offline research audit only. It implements no destination selector,
runtime catalog, mission behavior, database change, new capture or server
deployment. Destination placement identity does not establish the unresolved
operational door binding, eligibility rules or selection weights.

## Source and branch provenance

Audit branch: `codex/mission-destination-duplicate-audit`.

Starting evidence SHA: `ab917a34011e02699a09798226af436ee3a54b5f`, retained at
`origin/codex/mission-capture-wave-planning` when the isolated audit branch was
created. This source contains the requested reconstruction and eligibility
corpus. It is research provenance, not a claim that the research branch is
current public-master runtime authority. No research history was merged into
master. The final audit commit is identified in the Git history and handoff;
it cannot be embedded as its own content hash.

The audit read both governing research reports completely:

- [ACGEntrance reconstruction](ACGENTRANCE_REGISTRY_AND_MISSION_DESTINATION_RECONSTRUCTION.md).
- [Observed destination eligibility](MISSION_DESTINATION_ELIGIBILITY_FROM_RESOLVED_CAPTURE_CORPUS.md).

The authoritative source manifest is
`docs/generated/missions/location-reconciliation/source-manifest.json`, checked
against
`docs/generated/missions/acgentrance-reconstruction/mission-location-capture-source-coverage.json`.
All 77 retained session journals were read, along with every located physical
copy. Every physical file is independently hashed. Selection prefers the
repository-retained journal where present, otherwise the original journal,
then the archive, and requires the authoritative SHA. Source variants and
conflicts remain visible instead of being silently overwritten.

Repository-owned paths in new artifacts are repository-relative. External
capture paths identify original/archive evidence. Private July GameData is
represented only by logical input labels, hashes and provenance metadata; no
private payloads or active worktree path are copied into the audit outputs.

## Physical sources, events and requests

**PROVEN:** 168 physical journals represent 77 distinct session/hash groups:

```text
168 physical files = 77 original + 77 archive + 14 repository-retained
77 logical sessions = 63 two-copy groups + 14 three-copy groups
91 extra physical copies = 168 - 77
77 selected sources = 63 originals + 14 repository-retained
```

The 91 extra physical files are `BENIGN_ARCHIVAL_COPY`. Only one authoritative
logical copy contributes per session. Files were not deleted. No conflicting
session hashes, identical files declaring different sessions, partial-prefix,
partial-suffix or overlapping-event-range findings were found between the
distinct source streams.

The logical journals contain 79,567 events. Exact serialized event hashes,
canonical event hashes, payload hashes, session/request/cohort identity,
event type, timestamp and physical source line are retained or compared.
Timestamp alone is never identity. There are zero duplicate event rows or
logical-event duplicate groups.

All 18,642 request identities are unique within their sessions. There are no
missing request headers, orphan responses, reused/conflicting request IDs or
multiple responses for one request. `request_started` and
`request_transmitted` are separate phases; sharing a request ID is expected.
The request ledger preserves all captured request phases, terminal identity/playfield, slider
values, expected QL, character metadata, response cohort and offer count.
Equal settings on different request IDs do not collapse independent rolls.

Four requests have no retained response. One request has a valid empty raw
response. Both cases are preserved:

```text
18,642 requests = 18,637 five-offer responses + 1 empty response + 4 unanswered
18,638 cohorts = 18,637 five-offer cohorts + 1 empty cohort
18,567 raw cohorts = 18,566 raw five-offer cohorts + 1 raw empty cohort
71 cohorts lack raw bytes, each with five offers
93,185 offers = 18,637 * 5 = 92,830 raw-backed + 355 missing raw
92,830 exact observations -> 812 unique destination identities in 22 playfields
```

The empty response is session
`mission-20260903T030938856Z-32279304-2c007c1c`, request `00000001`, cohort event
line 5. Its 51-byte packet has SHA-256
`50276c5a1eafc50460bc91e3649d18750ac65faa2241bbfe0914b60ee0d9f9fe`.
It adds a cohort and no offers; it is not duplicate evidence.

## Cohorts, offers and independent destination resolution

The full ledger preserves the logical offer key:

```text
(session_id, source_line, request_id, cohort_id, offer_index)
```

All 93,185 keys are unique. The audit checks the captured packet length and
SHA-256, uses the existing schema-assisted `decode_cohort` verifier to recover
offer boundaries, and independently verifies unique offer indexes, contiguous
nonoverlapping ranges and complete packet coverage. Each raw-backed offer has
one exact byte slice and its SHA-256. A separate decoded hash excludes observer
provenance while retaining the captured decoded mission fields.

Cohort fingerprints retain the raw response SHA, ordered offer hashes, ordered
boundaries and complete decoded WorldPos values including their raw bytes.
Exact source-event hashes allow the audit to distinguish a copied event at a
different line from a new roll with the same content. A parser emitting the
same byte range under multiple indexes is reported separately.

The destination join is recomputed from decoded packet WorldPos, explicit
playfield and the three original binary32 coordinate patterns. The placement
side is independently read from the original little-endian transform bytes.
There is no rounding, distance tolerance, nearest-neighbor lookup or name-based
join. All 92,830 raw-backed offers resolve uniquely and agree with the existing
reconstruction and eligibility assignments. All 355 missing-raw offers retain
null destination identities.

Every observation keeps its source key, destination identity/name/playfield,
local XYZ values and bits, expected QL, character level, terminal, sliders and
mission type. Existing condition metadata is preserved from the eligibility
inventory; request metadata is independently retained in the request ledger.
The destination aggregate records observation, request, cohort, session and
QL counts for each of the 812 identities.

Three pairs of different response occurrences have identical decoded cohort
contents but different raw packets. Their distinct request/source provenance
is preserved as `LEGITIMATE_SERVER_RESPONSE_REPEAT`. Identical contents alone
do not prove repeated ingestion.

Those six responses contain 15 matching raw-offer groups, with two occurrences
per group, accounting for 15 reused mission identities. Inspection of the six
original journal rows confirms distinct request IDs and timestamps, identical
offer bytes from packet offset 51 onward and differences in packet/response
header bytes. These are not described as transport-only differences. All 30
offer occurrences remain; no decoded-offer/different-raw groups or duplicate
logical offer keys were found.

Only `ACCIDENTAL_DOUBLE_INGESTION` and `PARSER_DUPLICATION_BUG` may contribute
to the proposed count reduction. Proposed excess positions are unioned so
overlapping proof classes cannot remove the same occurrence twice. Conflicting
and unproven missing-raw evidence remain visible. No correction is applied.

## Same-cohort destination repeats

The independently recomputed distribution among the 18,566 raw-backed
five-offer cohorts is:

| Unique destination identities | Cohorts | Repeated offer positions |
| --- | ---: | ---: |
| 5 | 15,888 | 0 |
| 4 | 2,481 | 2,481 |
| 3 | 185 | 370 |
| 2 | 12 | 36 |
| **Total** | **18,566** | **2,887** |

Thus 2,678 cohorts contain exact destination repetition. Every repeat report
lists different offer indexes, start/end offsets and raw offer hashes within
the same verified response packet. The distinct byte ranges prove these are
`LEGITIMATE_SERVER_COHORT_DESTINATION_REPEAT`, with severity
`LEGITIMATE_SERVER_RESPONSE_REPEAT`. No repeated destination observation is
removed. The 71 cohorts without raw bytes are excluded from this byte-backed
proof rather than assumed to have the same property.

## Client placement collisions

| Independently checked property | Result |
| --- | ---: |
| Client placement rows / unique complete identities | 2,242 / 2,242 |
| Duplicate complete identity groups | 0 |
| Duplicate identity-instance groups | 0 |
| Same-playfield exact XYZ bit collisions | 0 |
| Same-playfield numeric XYZ collisions | 0 |
| Equal numeric XYZ with different bits | 0 |
| Shared referenced-template groups | 5 |
| Shared complete-transform groups across playfields | 5 |
| Same-playfield complete-transform groups | 0 |
| Unique names | 371 |
| Names shared by multiple identities | 120 |
| Same-playfield shared-name groups | 152 |
| Names spanning playfields | 45 |

The authoritative coordinate key is playfield plus all three binary32 bit
patterns. Numeric equality is audited separately, including signed-zero
fixtures. Shared transforms across different playfields do not establish the
same placement. Shared templates and names are descriptive metadata, not
identity or evidence for deletion.

Largest name families remain unchanged: `a building` (188 identities), `Cave`
(160), `House in Omni-1 Entertainment` (87), `Slumhouse in Omni-1 Entertainment`
(79), and `a house` (75).

## Generated artifact primary keys

Every listed artifact has `row_count == unique_primary_keys` and zero duplicate
primary keys. The JSON audit records the exact key-field names and full
conflicting rows if any occur; this table abbreviates them for readability.

| Artifact | Intended key | Rows / unique keys |
| --- | --- | ---: |
| `acgentrance-records` | Complete placement identity | 2,242 |
| `mission-location-full-corpus-reconciliation` | Full offer provenance key | 93,185 |
| `location-reconciliation/all-offers` | Full offer provenance key | 93,185 |
| `mission-offer-analysis-inventory` | Full offer provenance key | 93,185 |
| `destination-condition-evidence-matrix` | Placement identity + complete experimental condition | 25,296 |
| `destination-ql-evidence-matrix` | Placement identity + mission QL | 560,500 |
| `observed-destination-frequency` | Condition + destination instance | 11,112 |
| `observed-playfield-frequency` | Condition + destination playfield | 544 |
| `character-level-destination-matrix` | Character level + destination identity | 1,495 |
| `character-level-playfield-matrix` | Character level + destination playfield | 44 |
| `terminal-destination-matrix` | Terminal identity + destination identity | 828 |
| `terminal-playfield-matrix` | Terminal identity + destination playfield | 24 |
| `mission-type-destination-matrix` | Mission type + destination identity | 2,679 |
| `mission-type-playfield-matrix` | Mission type + destination playfield | 100 |

Repeated destination identities across different conditions are
`GENERATED_ARTIFACT_DESIGN`, not duplicate rows. These three offer projections
are not added together as separate experiments.

## July comparison and unresolved evidence

The July stage-one report describes 23 responses and 115 offers across eight
session IDs. Available retained inputs supply 13 raw response bodies, one full
packet template and ten layout provenance packet hashes. The template body is
distinct from the 13 bodies, giving 14 available body hashes. None matches a
September body/full-packet hash. Available first-mission identities, layout
packet hashes and session IDs likewise have no exact September match.

These bounded comparisons do not prove all July observations are disjoint.
Original July journals, per-body session/request mapping and complete offer
boundaries are missing. Complete-offer byte comparison therefore remains
unavailable; mission-content similarity or different dates cannot substitute
for exact provenance. July remains separate and `UNRESOLVED`. No July records
are added to, or removed from, the 93,185-offer September count.

## Tooling, reproducibility and validation

New files are limited to:

- `Tools/mission_destination_duplicate_audit.py` and `.cmd`;
- `Tools/mission_duplicate_sources.py` and `Tools/mission_duplicate_catalog.py`;
- `Tools/test_mission_destination_duplicate_audit.py`;
- this report;
- the 17 artifacts under
  `docs/generated/missions/destination-duplicate-audit/`.

The output directory contains all 14 requested artifacts plus full request,
cohort and offer inventories as deterministic gzip JSONL. Gzip uses zero mtime,
an empty filename and sorted compact ASCII JSON with LF. The manifest records
all input/output hashes and the imported decoder/runtime-selector identities.
Input hashes are checked again before output is accepted. Git change-scope
validation restricts differences from the evidence base to the new audit files;
historical sources and generated artifacts are not rewritten.

The supported commands are `generate`, `test` and `generate --check`. The
wrapper uses the existing approved Python runtime selector. July comparison
uses `AO_REBIRTH_GAMEDATA_PATH`, or explicit `--july-data-root`, pointing to the
canonical GameData parent directory containing `Missions`. Use the same input
selection for generation and checking. The exact private file hashes are in
the manifest; the private files are not published. Omitting those inputs cannot
reproduce this complete cross-corpus report and will make its stale check fail.

Validation executed from the audit repository root:

```text
cmd /d /c Tools\acgentrance_reconstruction.cmd test
cmd /d /c Tools\acgentrance_reconstruction.cmd generate --check
cmd /d /c Tools\mission_destination_eligibility_analysis.cmd test
cmd /d /c Tools\mission_destination_eligibility_analysis.cmd generate --check
cmd /d /c Tools\mission_destination_duplicate_audit.cmd generate
cmd /d /c Tools\mission_destination_duplicate_audit.cmd test
cmd /d /c Tools\mission_destination_duplicate_audit.cmd generate --check
git diff --check
```

Reconstruction: 17 tests passed and stale check passed. Eligibility: seven
tests passed and stale check passed. New audit: 50 tests passed, including an
independent complete-journal decode and generated-ledger comparison, deliberate
event/offer/parser duplication fixtures, conflicts, byte ownership, placement
bit collisions, preserved repeated names/destinations, gzip determinism and
stale-check behavior. Full generation passed, the final `generate --check`
passed with byte-identical outputs, and Git whitespace checks passed.

An initial repeat check encountered an intermittent JSON decoding error in the
new audit reader. All historical generated-input hashes remained identical;
the exact rejected string immediately reparsed, and a fresh process parsed all
93,185 rows of that artifact. The new reader now owns its decoder per input
stream and retains contextual failures without retrying or suppressing errors.
Two complete standalone catalog runs then produced identical results with zero
issues. The underlying interpreter failure mechanism is unverified; no
historical input was repaired or a validation requirement waived.

Inspected evidence includes the two required reports, both authoritative source
manifests, all 77 logical journals and 168 located physical copies, all 14
primary-key-audited artifacts, the existing decoder and field map, and the July
stage-one report plus available private response/layout provenance. The full
input manifest identifies every machine-audited file by hash.

The proposed correction plan contains zero removable offer positions:

```text
LOGICAL_OFFERS_AFTER_PROVEN_DEDUPLICATION=93185
EXACT_OBSERVATIONS_AFTER_PROVEN_DEDUPLICATION=92830
UNIQUE_DESTINATIONS_AFTER_PROVEN_DEDUPLICATION=812
HISTORICAL_FILES_MODIFIED=NO
RUNTIME_MISSION_LOGIC_CHANGED=NO
DESTINATION_DATA_DEDUPLICATED=NO
```
