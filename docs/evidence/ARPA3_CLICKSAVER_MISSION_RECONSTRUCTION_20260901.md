# ARPA3 and ClickSaver Rubi-Ka mission evidence reconstruction

Date: 2026-09-01

Repository: AORebirth

Scope: forensic evidence acquisition, normalization, and comparison only

Runtime mission logic changed: **NO**

## Outcome

The allowed public ARPA3 pages, ClickSaver release package, exact open-source snapshot, premade ClickSaver databases, landing pages, response headers, retrieval timestamps, byte lengths, and SHA-256 hashes are archived under `docs/reference/missions/`. The deterministic offline tools under `Tools/` verify those sources, decode the ClickSaver 3.x `All.cdb` and `Tiny.cdb` item/name catalogs, parse representative ARPA3 HTML and documented ClickSaver logs, project AORebirth's authoritative `items.dat`, perform fail-closed ID/group joins, and regenerate the normalized corpus under `docs/generated/missions/arpa3/`.

The full ARPA3 rollability database was **not** extracted. Both `arpa3.net/robots.txt` and `javierarpa.com/robots.txt` specify `Disallow: /cgi-bin` and a four-second crawl delay. The actual query handler is under `/cgi-bin`, so bulk or systematic extraction was stopped. One representative `contains=sunglasses` response observed before that policy boundary was discovered is retained only as a parser fixture. A failed or capped query is never treated as evidence that an item is not rollable.

ARPA3 and ClickSaver remain external historical evidence. No runtime dependency, database schema change, mission-generation change, or external binary execution was introduced.

## Evidence labels

| Label | Meaning |
|---|---|
| `EXACT_SOURCE_CODE` | Direct behavior or field layout in the archived ClickSaver source at exact commit `38f9347aca020ce2dd0e2e0b752829fc582b1532`. |
| `EXACT_ARCHIVE_STRUCTURE` | Byte-for-byte property proven by a retained archive/member and checked by SHA-256. |
| `OBSERVED_ARPA3_RESPONSE` | A value present in the single retained representative ARPA3 response fixture. |
| `DOCUMENTED_ARPA3` | A claim or example on a retained ARPA3/ClickSaver documentation page. |
| `DOCUMENTED_CLICKSAVER_LOG_SAMPLE` | A mission/log record copied exactly from retained ClickSaver release notes, not an independent player-log corpus. |
| `STRUCTURAL_INFERENCE` | A deterministic interpretation that fits every decoded record and is guarded by parser invariants, but whose 3.x source definition was not recovered. |
| `AO_REPOSITORY_EXACT_ID` | A ClickSaver template ID that exactly exists in the hashed AORebirth `items.dat` projection. |
| `UNKNOWN` | The available corpus cannot support the field or conclusion. `null` in normalized output has this meaning. |

## Source acquisition and provenance

The complete machine-readable manifest is `docs/reference/missions/source-manifest.json`. It contains 14 artifacts with requested/final URLs, request and response headers, status, retrieval time, byte length, and SHA-256. The manifest itself hashes to `41eb73cb0bac02d542dec566d227cd5fec4a05e98df04da285e9f43441ca1850`.

Primary retained artifacts:

| Artifact | Role | Bytes | SHA-256 |
|---|---:|---:|---|
| `arpa3/raw/rollability.html` | Rollability UI and public QL notes | 22,378 | `c678d72e9db5c7ab2a6ea4f0d3f01858c7f6f97c73eedca6eb615e6c27cea3ea` |
| `arpa3/raw/rollability-about.html` | Methodology and corpus limitations | 13,332 | `dcac4b855136354c89f32b78a76534468a8745c7f169a9c0e18994a62d77ec66` |
| `arpa3/raw/isitrollable.js` | Exact query construction | 3,459 | `2186aa148a73cc923683538d57f72224616be4b1c6495baedcf22ed155a7dc75` |
| `arpa3/raw/clicksaver.shtml` | ClickSaver release notes, log examples, filters, and database order | 50,045 | `6d809ff4ea516d7b32509b09168ab090f3340c2b3eb99157acb9d41e56441e0f` |
| `clicksaver/raw/cs310-v2.zip` | ClickSaver 3.1.0 binary release and `Tiny.cdb` | 2,229,895 | `d6aa6764b719dbe32f020f815bd877ae3f82f845de46eacd4a9b46f14c04d097` |
| `clicksaver/raw/clicksaver-source-38f9347aca020ce2dd0e2e0b752829fc582b1532.zip` | Exact source snapshot | 1,150,769 | `1d2d049a157711409b43da93717347e684febe200edd3f13ac2805cea455165a` |
| `clicksaver/raw/cs3-all-noicons-localdb-18-8-0.zip` | Patch 18.8.0 ClickSaver 3.x `All.cdb` | 1,262,206 | `a32fed2cbab9cb92bf03b92fd00a6c7739e2a095ea7203d19f35206dafa3ca77` |
| `clicksaver/raw/cs23-24-localdb-18-8-0.zip` | Patch 18.8.0 ClickSaver 2.3/2.4 Berkeley DB | 30,168,513 | `dfaebbdab8460befab00fc524af82b83dc6b3bdd4c86df300800471313f1afb0` |
| `AORebirth/Datafiles/items.dat` | AORebirth authoritative item templates | 2,466,207 | `4e5355f177a42fbd05b33b4a27083a53ecfee93f5fce982880f19e5461badf3c` |

The archived ClickSaver source is the current public `pzychotic/ClickSaver` lineage at version 2.5.3, not Kimi's ClickSaver 3.1.0 source. The 3.1.0 package identifies Kimi's 31-Jul-2012 binary release, but the associated 3.x source was not published by the retained ARPA3 page. Conclusions about mission packets and legacy Berkeley DB fields are therefore `EXACT_SOURCE_CODE`; the custom 3.x CDB byte layout is `EXACT_ARCHIVE_STRUCTURE` plus `STRUCTURAL_INFERENCE`.

No downloaded executable or bundled legacy DLL was run.

## ARPA3 query interface and constraints

`isitrollable.js` constructs this HTML endpoint:

`https://javierarpa.com/cgi-bin/aorollq.cgi?cs=<mode>&name=<escaped query>`

Observed/documented modes:

| `cs` | Mode |
|---:|---|
| `0` | contains |
| `1` | ClickSaver-like search |
| `2` | exact |

The public page also accepts `name` and `cs`, embeds the result in an iframe, and provides no JSON endpoint or pagination control. A controlled broad query returned a “100+ items / refine” cap. This proves a result cap, not a database size and not negative rollability.

Bulk extraction status: **BLOCKED BY PUBLISHED ROBOTS POLICY**. No `/cgi-bin` acquisition was made by `Tools/acquire_arpa3_mission_evidence.py`; the tool explicitly refuses that path and enforces the published four-second delay for allowed ARPA-family pages.

## ClickSaver source reconstruction

The following are direct source-code observations from `ClickSaver/mission.c`, `ClickSaver/mission.h`, and `ClickSaver/localdb.c` in the exact source archive:

- A terminal request presents up to five mission offers. The ARPA3 page and ClickSaver UI/documentation independently describe five missions per request.
- Mission type codes are `0x2c4e` Repair, `0x2c41` Return Item, `0x2c47` Find Person, `0x2c49` Find Item, and `0x2c42` Kill Person.
- The mission parser locates marker `0xDAC3`; after the post-16.3 header it reads cash at `+0x0c`, XP at `+0x14`, mission QL at `+0x0c` following the reward-item array, mission type at `+0x28`, playfield ID at `+0xA8`, and coordinates at `+0xB4`/`+0xBC` in the parsed blocks.
- Reward entries contain low template ID, high template ID, QL, and padding, and terminate at `0x2d2d2d2d`.
- `cs-res.log` records mission QL/slot, playfield/coordinates/name, find item, and reward QL/name/low/high IDs. It does not record a server-side selection weight.
- ClickSaver chooses which endpoint name/icon to display by the endpoint closest to the rolled reward QL and linearly interpolates shop value. This is client display behavior, not evidence of the AO server's reward selection algorithm.
- Item-to-find identity is recovered by matching known description strings. A decoded name is not by itself a terminal server-template identity bridge.
- Legacy local DB code stores item name, value, QL, icon key, playfield data, and icons in Berkeley DB. That schema does not turn the local database into a server-side reward pool.

ClickSaver documents local lookup order as `Test.cdb`, then `All.cdb`, then `Tiny.cdb`. `Tiny.cdb` is curated for commonly useful rollables; `All.cdb` is the broad local item-name database. Presence in either CDB means ClickSaver can identify/search the template, not that ARPA3 observed it as rollable.

## Static database decoding

`All.cdb` and `Tiny.cdb` are not Daniel J. Bernstein CDB files. The retained 3.x files use a custom structure:

- a 12-byte little-endian header;
- one named-record prefix containing playfield names and item metadata/names;
- an opaque per-resource payload region whose 3.x source definition was not recovered;
- an exact eight-byte resource table. Its named prefix contains type-1 playfields and type-2 items; remaining entries are retained only as structurally classified resource records.

The parser validates all boundaries, exact trailing table length, the named-record prefix count, every playfield/item value offset, positive unique identities, text decoding, and the fixed 12-byte metadata prefix on type-2 item names. Those three metadata words and the non-named resource payload semantics are preserved but not assigned an invented 3.x meaning.

| Property | `All.cdb` | `Tiny.cdb` |
|---|---:|---:|
| Member SHA-256 | `cb86badc24e4b2429fb8b7568aea4cb26d3c6e80149a7af56ae582572c949150` | `2f9a6fcff91f09f68575623aac5dc9b0c36d9884e5db03599f902693f58d1747` |
| Bytes | 6,299,800 | 2,951,144 |
| Decoded item identities | 34,559 | 14,235 |
| Item records with a missing-name sentinel | 8 | 0 |
| Decoded playfield identities | 616 | 610 |
| Total named records | 35,175 | 14,845 |
| Total resource-table records | 119,468 | 25,156 |
| Opaque payload bytes | 3,848,208 | 2,127,099 |

The cross-version union contains 34,560 item IDs. `Tiny.cdb` contributes one item ID absent from the patch-18.8.0 `All.cdb`; 108 overlapping item IDs have different names across the historical database versions. The playfield union contains 619 IDs with 21 cross-version name conflicts. Those differences are retained, not collapsed or silently overwritten.

Eight `All.cdb` item records use the format's missing-name sentinel. Their IDs and raw metadata remain in the catalog, while the ClickSaver name is explicit `null`; AORebirth names are not fabricated into that source field.

`AODatabase.bdb` is retained as a 96,807,936-byte archive member with SHA-256 `ead90992fa86250e034eb44d221b3dc0f5289497b11db67484ea796777111f69`. It is classified as an opaque Berkeley DB 4 artifact. Running the bundled legacy database DLLs was outside the safe evidence workflow, and an independent record-level Berkeley DB 4 decoder was not introduced. The exact source-code schema above is retained separately from this extraction gap.

## Normalized outputs

| Output | Contents |
|---|---|
| `clicksaver-item-catalog.csv` | Reviewable union of all decoded CDB item IDs/names, per-version provenance, AO item/group joins, and reward-catalog membership. |
| `clicksaver-item-catalog.jsonl.gz` | Deterministically compressed JSONL form of the same 34,560 rows. |
| `clicksaver-playfield-catalog.json` | The 619 decoded playfield IDs/names with per-version provenance and name-conflict flags. |
| `normalized-roll-observations.csv` / `.jsonl` | Twelve rows from the one representative ARPA3 response: ten mission-QL rows and two overall rows. Missing IDs, mission types, and locations remain explicit `null`. |
| `normalized-clicksaver-log-samples.jsonl` | Five exact documentation examples with mission QL/slot, locations, find names, and reward IDs/QLs. |
| `documented-ql-exceptions.json` | Seven public ARPA3 QL exception statements with exact versus approximate labels. |
| `archive-member-inventory.json` | Member-level hashes and extraction status for `All.cdb`, `Tiny.cdb`, and `AODatabase.bdb`. |
| `analysis-summary.json` | Counts, measured ranges, explicit unavailable analyses, and runtime-boundary assertions. |
| `evidence-manifest.json` | Hashes and byte lengths for every generated artifact and the AO source projection. |

CSV and compressed JSONL were generated. SQLite was intentionally omitted because it adds no evidence, is harder to review byte-for-byte, and would duplicate a 34,560-row deterministic corpus already available in two queryable forms.

## AORebirth item cross-reference

The exact C# projector loads `AORebirth/Datafiles/items.dat` through AORebirth's own `MessagePackZip`/`ItemTemplate` implementation and emits 120,842 templates sorted by ID with QL, `Relations`, item type, and flags. The normalized join uses ID before name and derives low/high group endpoints from the minimum/maximum QL among resolved relation members.

| Resolution | Count | Meaning |
|---|---:|---|
| `EXACT_ID_AND_REWARD_ENDPOINT` | 19,495 | Exact `items.dat` ID; also an endpoint in an existing mission-reward catalog row. |
| `EXACT_ID` | 15,046 | Exact `items.dat` ID; not an endpoint in the current mission-reward JSON catalogs. |
| `EXACT_ID_PARTIAL_RELATION` | 18 | Exact item ID, but one or more listed relation members are absent from the projected item corpus; group is fail-closed and labeled partial. |
| `UNRESOLVED` | 1 | CDB ID absent from the hashed AO item corpus and no unique repository reward-name candidate. |

Thus 34,559 of 34,560 recovered ClickSaver item IDs have an exact AORebirth template-ID bridge. Name-only candidates are never promoted to exact IDs. Ambiguous names remain unresolved; parser self-tests explicitly exercise this fail-closed rule.

The sole unresolved identity is Tiny-only template ID `89622`, `Pill with Fling Shot Proficiency`. It is absent from the retained patch-18.8.0 `All.cdb`, the hashed AORebirth `items.dat`, and the current reward-catalog endpoints. It remains historical evidence only.

The five existing mission-reward JSON files contain 23,994 rows. Membership in those catalogs demonstrates current AORebirth pool inclusion only; it does not prove live AO rollability or historical ARPA3 frequency.

## Quantitative rollability findings

The retained response fixture contains two QL1 sunglasses, each observed as a mission reward at mission QL1-5 plus an overall frequency row. For those ten rows the measured mission-minus-item QL delta is 0 through +4. The per-row ARPA3 “one in N items” denominator and “average (x5) rolls” are preserved exactly; `1/N` is included only as `DERIVED_ARITHMETIC_FROM_ONE_IN_N`.

This fixture is not a statistically representative sample and must not be used to estimate the full reward distribution.

The public ARPA3 page independently documents these QL exceptions:

| Item | Item QL | Mission QL | Delta | Evidence |
|---|---:|---:|---:|---|
| Nano Crystal (Anima of The Abomination) | 239 | 250 | +11 | exact documented |
| Nano Crystal (Sneaking Terror) | 70 | 90 | +20 | exact documented |
| Nano Crystal (A Clear Sense of Scheol) | 60 | 74 | +14 | exact documented |
| Nano Crystal (Mind Scream) | 37 | 60 | +23 | exact documented |
| Nano Crystal (Overview of Elysium) | 30 | 45 | +15 | exact documented |
| Nano Crystal (Greater Hold Victim) | 132 | around 80 | approximately -52 | approximate documented |
| Nano Crystal (Hold Victim) | 83 | around 130 | approximately +47 | approximate documented |

These statements prove that an absolute ±10 nano window is not universal. They do not provide full-corpus exception frequency, selection weight, or a replacement rule.

Unavailable from the lawful corpus, and therefore `null` in `analysis-summary.json`:

- reward composition and reward-type distributions;
- per-mission-QL eligible-item counts;
- full reward-QL delta distribution and outlier rate;
- repeated-item probability, streak behavior, or successive-roll correlation;
- mission-type weighting;
- server-side pool membership and weighting;
- mission layout, objective placement, mob population, or destination weighting.

## Repeatability analysis

ARPA3 publishes aggregate “found once every these items” and five-offer average values per item/mission QL. Those aggregates estimate marginal observation frequency in the contributing ClickSaver logs. They do not preserve roll order, seed, terminal, character, slider state beyond the displayed dimensions, or within-roll correlations.

ClickSaver logs individual successful matches and run counters, but the five retained documentation examples are not a repeated-roll corpus. No available artifact can determine whether successive terminal requests are independent, whether a reward can repeat inside one five-offer cohort, or whether server state changes weights. No synthetic stochastic law is proposed.

## Existing AORebirth runtime comparison

No runtime code was changed. The existing generator currently has these evidence boundaries:

- `MissionRollEvidenceCatalog.cs:7-8,14-34,54-78` selects one of captured five-offer type cohorts by nearest evidence and explicitly does not infer probability weights.
- `MissionRewardEvidenceModel.cs:9-16` selects exact captured cash/XP pairs by nearest evidence and does not calculate/interpolate rewards.
- `MissionRewardCatalog.cs:22,70-83,104-145` prefers exact-QL non-nanos, then nanos within ±10, then exact-QL fallback; nano reward QL is fixed to the catalog QL and scalable non-nanos clamp to their relation band.
- `MissionLevelGraph.cs:154-222` loads and validates the canonical mission-level graph with SHA-256 and deterministic line/order rules.

The ARPA3 exception list demonstrates a known divergence from treating ±10 as a universal nano rollability law. It does not safely specify how to alter `MissionRewardCatalog`. The current nearest-evidence type and cash/XP models also cannot be replaced by ARPA3 aggregate rollability because the latter lacks complete cohort, slider, destination, cash, XP, and ordering data.

## Reconstructed mission-generation model

| Component | Supported reconstruction | Boundary |
|---|---|---|
| Offers per request | Five offers | `EXACT_SOURCE_CODE` plus `DOCUMENTED_ARPA3` |
| Mission types | Repair, Return Item, Find Person, Find Item, Kill Person with exact ClickSaver wire codes | Client parser classification; no server type weights |
| Mission QL | Exact value present in mission packet/log; AORebirth has an independently governed level graph | No ARPA3 server formula recovered |
| Reward identity | Low ID, high ID, reward QL, display name in ClickSaver packet/log; 34,559 exact AO ID joins | CDB presence is identification, not rollability |
| Item-to-find | Description-derived display identity | Not an exact template-ID bridge unless IDs are independently present |
| Cash and XP | Exact packet fields and AORebirth captured pairs | No ARPA3 formula or probability model |
| Location | Playfield ID/name and coordinates in source/log examples | No destination distribution or layout generation rule |
| Reward eligibility | ARPA3 observations demonstrate that named items were rolled at displayed mission QLs | Full RDB unavailable; no negative inference from missing results |
| Repeatability | Marginal one-in-N aggregate where present | No sequence/correlation evidence |
| Layout/objectives/mobs | Nothing recoverable from this corpus | `UNKNOWN` |

## What additional evidence is required

1. An owner-authorized ARPA3 database export or static dump, including all item/role/mission-QL frequency rows and corpus metadata, would unlock full distributions without violating `/cgi-bin` policy.
2. The exact ClickSaver 3.1.0 source would convert the custom CDB header/packing interpretation from structural inference to source-backed semantics.
3. A safe Berkeley DB 4 record exporter, run against a copy without executing bundled legacy binaries, would recover the historical 2.3/2.4 local DB records.
4. Ordered raw ClickSaver logs with terminal, character level, slider state, mission QL, five-offer grouping, and every success/failure are required for repeatability and cohort analyses.
5. Capture-backed AO mission packets remain required for any runtime implementation. This corpus alone does not authorize a generator change.

## Files inspected

Repository governance and architecture:

- `AI_START_HERE.md`
- `docs/project/DEVELOPMENT_AUTHORITY.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/KNOWN_DECISIONS.md`
- `docs/project/SUBSYSTEMS.md`
- `docs/project/ARCHITECTURE.md`
- `docs/ai/WORKFLOW.md`

Mission/item runtime and generation:

- `AORebirth/Server/ZoneEngine/Core/Missions/MissionRollEvidenceCatalog.cs`
- `AORebirth/Server/ZoneEngine/Core/Missions/MissionRewardEvidenceModel.cs`
- `AORebirth/Server/ZoneEngine/Core/Missions/MissionRewardCatalog.cs`
- `AORebirth/Server/ZoneEngine/Core/Missions/MissionLevelGraph.cs`
- `AORebirth/Libraries/Source/AORebirth.Core/Items/ItemTemplate.cs`
- `AORebirth/Libraries/Source/AORebirth.Core/Items/ItemLoader.cs`
- `AORebirth/Libraries/Source/Utility/MessagePackZip.cs`
- `AORebirth/Server/ZoneEngine/XML Data/MissionRewards/ItemDB_*.json`
- `Tools/generate_mission_level_graph.py`

External retained evidence:

- all 14 artifacts in `docs/reference/missions/source-manifest.json`
- `ClickSaver/mission.c`, `mission.h`, `localdb.c`, `AODB/*`, `Deploy/ReadMe.txt`, and `README.md` inside the exact source archive
- `ReadMe.txt` and `Tiny.cdb` inside the 3.1.0 package
- `All.cdb` and its readme inside the patch-18.8.0 3.x archive
- `AODatabase.bdb` and its readme inside the patch-18.8.0 2.3/2.4 archive

## Validation gates

- acquisition-manifest offline integrity check;
- parser self-tests for valid/malformed CDB records, offset corruption, duplicate IDs, valid/malformed ARPA3 rows, documented ClickSaver log fields, missing fields, and ambiguous name joins;
- deterministic regeneration check for every generated artifact;
- AORebirth item projection hash/length gate and sorted/unique-ID validation;
- exact source archive/member SHA-256 verification;
- generated-data manifest verification;
- mission-level graph generator check;
- Git whitespace/diff check and focused status review.

**RUNTIME MISSION LOGIC CHANGED: NO**
