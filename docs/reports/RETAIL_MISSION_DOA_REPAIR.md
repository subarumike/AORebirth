# Retail mission architecture repair

Feature validation starts from public master **cda38284540f45573424fb399a60c9ab7627a6f7**.
The protected chest policies were checked against their
approved private revision, and the exact private revision and selected GameData
manifest are recorded in the ignored feature evidence.

## Roll path and protocol

The baseline built each terminal response by decoding a recorded response,
decoding every retained cohort to find eligible shells, and decoding a selected
body for each of the five offers. It also scanned and allocated the list of
eligible reward rows for each offer. The new roller constructs AOtomation
messages from immutable definitions and uses the existing quality graph,
destination catalog, selection policy and DAO. It preindexes offer types and
reward item ranges. Eager initialization validates the catalog shape, type
coverage, reward references and supporting policy before handling rolls.

The catalog comes solely from previously public protocol fixtures. The native
codec reproduced all 13 historical responses and 65 offers byte for byte after
projection to typed fields. Unidentified fields keep their wire names and
observed values. The recorded bodies and template remain in test fixtures.
The runtime release manifest admits the two typed catalogs and excludes the
superseded recorded files. Frozen offers already persisted in the database
continue to use their existing codec and integrity checks.

The canonical offer catalog SHA256 is `65f279081eedb26969a4f234022ad6ca67ca716ec17597c40c49b464d2b19861`.
The initial Windows export differed only in its line endings. The publication
check repeated the projection and wire regression with the exact LF catalog
that Git distributes.

The generated wire SHA256 is `3d1aa7e35baa65d7669390c439d75311e12d249385c9e9e2eb304ea2fe834604`.

The latter digest covers 78 deterministic generated responses under the same two
captured request conditions before and after the change. The regression runs
each input twice and compares the resulting bytes.

## Runtime cost

Release configuration. The benchmark warmed both input conditions with 512 rolls
then ran three trials of 4,096 rolls and serialized each response. CPU is the
test process's CPU time. Allocation is the calling thread's allocated bytes.
Each trial produced 10,735,407 wire bytes. Medians follow.

| Metric per 4,096 rolls | Baseline | Candidate | Reduction |
| --- | ---: | ---: | ---: |
| CPU time | 1,718.75 ms | 125.00 ms | 92.7% |
| Allocated bytes | 4,071,802,656 | 221,689,472 | 94.6% |

These measurements cover warm offer generation and serialization on this host.
They do not measure startup, network, database or client latency.

## Content and compatibility

The mission key policy moves the existing key template, duplicator, key QL,
name prefix and copy cap to validated GameData. Current original-client item
data verifies the two item roles and the key QL. The cap remains AORebirth
policy. Previously accepted seed-only dungeons still use their versioned
reconstruction settings. The exact version 2 payload and spawn-coordinate bits
are compared with the checked-in golden fixture.

The three approved RK chest policy files were byte-identical to the published
private blobs. The catalog still contains **25,118 definitions and 13,269
approval bands**, covering **186 captured pairs and 291 observations**, including
Gift wrapped basket at QL 1. Chest placement, selection, inventory and
persistence code retain their existing ownership. The validation dataset was
an isolated copy of the complete private GameData plus the two public-derived
catalogs, the provisional coverage metadata and the removal of the two obsolete
runtime copies.

## Coverage limits

| Content | Verified coverage and remaining gap |
| --- | --- |
| Find Item and Return Item | Existing URSE and COSE bindings resolve. Other eligible items, QLs and weights need independent evidence. |
| Repair Machine | The observed SPPR and CRCH pair resolves. Additional pairs and their eligibility by mission QL are unproven. |
| Kill Person and Find Person | The MFPB family resolves in the current data. It remains a provisional single-family policy. The capture data do not establish a retail NPC-family pool. |
| Ordinary population | Six nonalien families are supported. Their known maximum authored QLs are 106, 47, and 85 for the other four. Broader retail families, upper QLs and weights remain unverified. |
| Procedural styles | Observations for style 320 lack mapped entrances and a door style. Evidence is insufficient to activate it. |
| Terminal rewards | All 43,076 distinct packaged item endpoint IDs resolve in the current item data. Five inherited reward bands disagree with current endpoint QLs. The published selection policy and seeded behavior are preserved pending separate evidence review. |
| Destinations and reward probabilities | Nearest-QL destination eligibility and selection weights outside recorded conditions remain provisional. Wire equality is not a proof of all retail distributions. |

The fixed pools are declared provisional in GameData. No extra selection records
or weights were inferred from observation frequency.

## Enforcement and validation

The existing NewEngine architecture guard now checks proposed changes against
the exact base in CI, with focused mutation tests for its rules. The private
reference validation is available when the authoritative data is supplied.
The public check covers tracked source and public contract shape. Inherited
lexical findings remain visible and are distinct from verified gameplay defects.

The baseline test build needed the session doubles updated to the current
transfer signature. Its mission run then recorded 107 passing cases and five
failures that assumed nearest-QL fallback was disabled. The candidate
rejection fixtures explicitly disable that option in their test data and still
exercise the unmodified runtime policy. The candidate passed all 137 focused
mission, protocol, compatibility, key and reward checks.

The architecture guard passed all **17 mutation tests**. It inspected 870
runtime C# files and reported zero introduced findings, unresolved project
inputs or private reference failures against the selected data. Eight inherited
lexical findings remain open. They concern DAO artifact columns, typed playfield
identity handling, pet creation, proxy masks, dungeon IDs and a quest catalog
diagnostic. The release admission check for the two typed catalogs and the
excluded raw inputs also passed.

The complete selected chest suite had **95 passes, three failures and one
skip**, identical to the same 99-case run on the exact public baseline with
the same GameData. The failures were the retail chest-use packet offset 16
mismatch, intermittent visibility publication of three of four chests in a
style 351 layout, and the absent original saved cave fixture. The missing
fixture also accounts for the skip. The same real layout passed in the
narrower control on both sources. The control needed only test-double signature
updates to compile against the current session interface.

The private catalog campaign generated **10,000 chest populations and 19,948
items** with zero unauthorized items, excluded items or durable preview
allocations. All **50 disposable MySQL chest checks** passed. The four Windows
engines compiled under feature validation.

The disposable TCP fixture first stopped at schema provisioning because the
account migration requires the baseline login table. Its setup now creates that
table from the repository's existing definition before migration. The retry
passed five-offer rolling and acceptance, key duplication and trade, chest
preview, chest access and loot transfer, authenticated reconnect, fresh-process
reconstruction, seasonal withholding and multiplayer access. The TCP run
selected a one-item chest for the first open. Partial reopen is covered by the
focused inventory test. All owned processes exited successfully and cleanup
found no disposable container or network residue.

## Remaining acceptance

The feature branch is a repair of the existing mission architecture. Original
client acceptance, broader retail content reconstruction and any future external
mission module API remain separate work. The explicit provisional content gaps
above limit claims of retail equivalence.
