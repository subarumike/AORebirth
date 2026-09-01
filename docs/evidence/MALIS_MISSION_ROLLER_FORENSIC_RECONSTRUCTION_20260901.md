# Malis Mission Roller 2.0 forensic reconstruction

Date: 2026-09-01

AORebirth starting SHA: `de61fa4cacb3626cb19155b9548c5325df6d8fd6`

Evidence branch: `codex/malis-mission-evidence`

Malis source: `https://gitlab.com/Pixelmania/malis-mission-roller-2.0.git`

Malis branch and commit: `main` at `3ac9943a4943b8cb80eda9e40359729e656686b0`

Retrieved: `2026-09-01T04:29:44Z`

## Outcome

Malis contains a client-maintained character-level/difficulty mission-QL table,
typed AOSharp mission-offer fields, five mission-icon filters, post-offer item
matching, and a 46-playfield destination filter. It does **not** contain a
rollability database, observations, occurrence counts, reward frequencies, or
generator weights. The central classification is `NO_EQUIVALENT_DATA`; its
architecture is `SERVER_OFFER_FILTERING`.

The retained source, full Git history, public metadata, exact AOSharp SDK
package, correlated AOSharp source, and separately published toolkit release
are normalized by deterministic offline tooling. No bundled executable or DLL
was run. No production mission code or data was changed.

## Evidence and provenance rules

- `PROVEN_FROM_SOURCE`: directly present in exact source or a retained diff.
- `PROVEN_FROM_STATIC_DATA`: directly present in a hashed data file.
- `OBSERVED_BEHAVIOR_DOCUMENTED`: a direct behavioral observation preserved by
  the source project. No new finding in this investigation reaches this class.
- `DERIVED_FROM_SOURCE`: a deterministic join, count, or implication from
  proven source fields.
- `HYPOTHESIS`: plausible, but not established as an AO server rule.
- `UNKNOWN`: no source bridge was found.

Item existence, mission eligibility, observed frequency, and true server
weight remain separate evidence classes throughout this report.

## Baseline and isolation

The primary worktree was clean on `codex/arpa3-mission-evidence` at
`de61fa4cacb3626cb19155b9548c5325df6d8fd6`. That SHA is the prior accepted
ARPA3/ClickSaver reconstruction. The ARPA3 evidence branch had not been merged
into the repository's default branch when this work began. This task was
therefore isolated in a separate worktree and branch, leaving the primary
worktree untouched.

The prior ARPA regression gate also exposed an accepted-evidence packaging
defect in that starting commit: Git had stored LF-normalized forms of eleven
retained text artifacts even though their governed manifests record the
original CRLF/mixed-line-ending bytes, and the documented ClickSaver log fixture
existed in the primary worktree but was omitted from Git by the global `*.log`
ignore. This branch restores only bytes that reproduce the already-recorded
lengths and SHA-256 values exactly, and force-tracks the byte-identical fixture.
It does not change any evidence value or conclusion. The ARPA offline write,
self-test, and stale check pass after this preservation repair.

## Acquisition and release provenance

The governed source manifest is
`docs/reference/missions/malis/source-manifest.json`. Important retained inputs
are:

| Evidence | Exact identity |
| --- | --- |
| Malis source tree | commit `3ac9943a4943b8cb80eda9e40359729e656686b0`; ZIP SHA-256 `c1dc1bf4c919193c0ea9b5ba3cc5419075becd5b94e1041391f0d9ebbae0074d` |
| Malis full history | bundle SHA-256 `286983b76670602b994be27851732962230ecf369c9511f0816f8236fcf6e75c` |
| Public toolkit release | `Malis-AO-Toolkit-27-01-26.zip`; SHA-256 `b9873762c86b7d0a2069c6d1bc830b5a96ed0dbc352466b2ddc410224efdf0ac` |
| Exact AOSharp dependency | `AOSharpSDK` 1.0.106; NuGet SHA-256 `4c2946f10aaa3d92a902be66149a09e4a24ca13bffd8110db37c5def4c578f22` |
| AOSharp source correlation | commit `b45b7a05f9ffd9676d37e620f2f7d481b82ed212`; ZIP SHA-256 `f668e29309ebba790dbac540df94666e6ce1803d8e169f21c7124e6a79777564` |

GitLab exposed 92 commits, 34 merge requests, no issues, no repository release
records, and no comments on either specifically requested historical commit.
The project description links a separate public MEGA toolkit folder; that is
the provenance of the retained release archive. The AOSharp package does not
embed a commit bridge, so its relationship to the same-day public AOSharp
source commit is `UNKNOWN`; the relevant source structures agree with exact
package metadata and are unchanged through the inspected source head.

The release's Mission Roller directory has 72 files. All 66 source static
files expected in the release are present and byte-identical. Its six
release-only files are compiled/runtime artifacts: `AOSharp.Common.dll`,
`AOSharp.Core.dll`, `Malis Mission Roller 2.dll`, its PDB, and Newtonsoft JSON
DLL/XML. It contains no release-only mission database, cache, or generated
rollability data. Inspection was archive- and metadata-only.

## Source history findings

### Character level 80

Commit `e19bb1ddc25e2647688c7996c8b09d50198fc486`, dated
2025-03-17, subject `Fixed level 80 mission table`, changed only
`JSON/MissionLevels.json`. Character level 80's final, difficulty-wire-11 cell
changed from QL 144 to QL 143. QL 143 agrees with AORebirth's independently
governed canonical table. No commit body, issue, comment, formula, upstream
reference, or explanation establishes why the old value was wrong or why
level 80 is special. This is `PROVEN_FROM_SOURCE`; its upstream provenance is
`UNKNOWN`.

Earlier point fixes show that the table was maintained empirically or manually,
not generated by a stable formula in this repository: levels 52/53/54 at
difficulty 11 changed 92→93, 95→94, and 97→96 in
`6543610386e87a99243d12282a7ba474995d710d`, and level 60 changed 108→107 in
`ec4416970f5f38854e5a547a90ae44f40acc6b63`. These irregular corrections and
the level-80 correction make a simple unqualified formula unsafe.

### Character levels above 200 and QL 200 items

Commit `7e5b921cebabee99051252a4883f324b38a519fc`, dated
2026-01-07, changed `MainWindow.cs`, `Malis Mission Roller 2.csproj`, and added
`RollEntryProcessor.cs`. Its exact client rule is:

1. the character level must be strictly greater than 200;
2. the selected search entry QL must equal 200;
3. its name must not contain `Nano Crystal` or `NanoCrystal`;
4. Malis chooses the first value in that character's table row which is at
   least 200, then sends its one-based position as the difficulty value.

For levels 201 through 220, the current table makes that first value the sixth
slot and the requested mission QL equal to the character level (201 through
220). Malis then searches returned reward data for QL 200. It does **not** lower
the requested mission to QL 200. It is a client search/filter feature, not proof
of a server generation rule. The source comment itself asks whether the
eligible items are those “which aren't nano crystals?”, so the server-side
meaning is explicitly uncertain. Nanos continue through Malis's separate
±10-Ql client search heuristic. No observation corpus proves either heuristic.

### Slider centering history

Commit `fb7ea4b7933f1b804eb924c5ba3a83996afe1f1a`, merged by MR
!32, maps UI value 0 to signed -1 encoded as byte 255 for Good/Bad,
Order/Chaos, Open/Hidden, Physical/Mystical, Head-on/Stealth, and Credits/XP.
Difficulty is deliberately left as its direct value. The MR title says this
matches AO's offset but gives no protocol citation; the code change is proven,
while the server rationale remains undocumented.

## Static-data inventory

The complete 87-file source inventory and every SHA-256 are in
`docs/generated/missions/malis/source-file-inventory.json`. Mission-relevant
datasets are normalized in `static-dataset-inventory.json`.

| Dataset | Records | Purpose and provenance |
| --- | ---: | --- |
| `ItemDB_Implants.json` | 8,474 | Searchable item templates; upstream unknown |
| `ItemDb_Refined.json` | 8,613 | Searchable item templates; upstream unknown |
| `ItemDb_Clusters.json` | 516 | Searchable item templates; upstream unknown |
| `ItemDb_Nanos.json` | 2,112 | Searchable item templates; upstream unknown |
| `ItemDb_Rest.json` | 4,279 | Searchable item templates; upstream unknown |
| `MissionLevels.json` | 220 rows | Client level+difficulty lookup; upstream unknown |
| `ModTags.json` | 175 keys | Browser search synonyms, stats, and profession text |
| `Default_Settings.json` | 7 sections | UI defaults, sliders, types, database flags, and 46 locations |
| hardcoded playfield IDs | 46 | Client allow/deny and optional X/Z bounds |
| hardcoded mission icons | 5 | Post-offer type filtering |
| hardcoded offer views | 5 | Ordered display/processing slots |

The item schema is an array of key/value entries where the key contains
`LowId`, `HighId`, `LowQl`, `HighQl`, `Tags`, and `Name`, and the value contains
item stats. There are 23,994 rows including one pseudo-entry, ID 297315,
`Reward + Item`; 23,993 are actual item rows, comprising 23,985 unique template
pairs and 43,075 endpoint identities. History documents manual additions and
corrections but names no ClickSaver, ARPA, AO-resource export, or other upstream
source. These are search catalogs, not reward pools.

Across the five actual-item catalogs, normalization finds two excess exact
duplicate records and eight excess repeated template-pair rows. Six repeated
pairs have conflicting record bodies and are preserved explicitly in
`item-comparison.json`; the generator neither silently drops nor resolves them.

## Character level and difficulty to requested mission QL

Malis uses a static 220-row table. It indexes the row with
`characterLevel - 1`. The Easy/Hard UI produces integer values 1..11, and the
value is passed unchanged as `MissionSliders.Difficulty` to
`MissionTerminal.RequestMissions`. Auto-selection finds a desired QL in the row
and sends `IndexOf(QL) + 1`; repeated QLs therefore select the first matching
slot. The table column index is difficulty wire value minus one. There is no
interpolation or AOSharp API that returns the available mission QLs.

The exact 220-row relationship is
`docs/generated/missions/malis/character-level-mission-ql.csv`. It is not a
complete 220×11 matrix: levels 12, 13, and 209 through 219 omit difficulty 11,
leaving 2,407 of 2,420 intended cells. Against AORebirth's canonical table,
2,352 cells match exactly, 55 present cells differ, and 13 cells are missing.
Malis therefore supplies partial corroboration, not a replacement authority.

This establishes `MALIS CLIENT-SIDE CALCULATION`. It does not establish
`PROVEN AO SERVER RULE` beyond the separate capture-backed facts already
governed by AORebirth.

## AOSharp mission data path and fields

The traced path is:

`AO server QuestAlternative message → AO client → AOSharp deserialization →
Network.OnQuestAlternative → Mission.OnRollListChanged → Malis
RollListChangedArgs.MissionDetails → display/filter each MissionInfo`.

The exact AOSharp 1.0.106 catalog, member ordinals, types, fixed-size chunks,
origins, and Malis consumers are in
`docs/generated/missions/malis/aosharp-mission-field-catalog.json`.

`QuestAlternativeMessage` exposes `Unknown1: byte`, `MissionSliders`,
`Unknown2: int`, `Scope: MissionScope`, `Terminal: Identity`, and
`MissionDetails: MissionInfo[]`. `MissionSliders` exposes the seven slider
bytes. `MissionInfo` exposes mission identity, title, description, terminal
identity, reward descriptor version, credits, XP, a reward array, mission icon,
playfield identity, location `Vector3`, and several opaque fixed chunks/fields.
`MissionItemReward` exposes `LowId`, `HighId`, `Ql`, and one unknown integer.

Field-by-field conclusions requested by this task:

| Requested field | Offer representation |
| --- | --- |
| mission identity | direct `MissionInfo.MissionIdentity` |
| mission QL | not exposed by `MissionInfo` in AOSharp 1.0.106 |
| reward low/high ID and QL | direct `MissionItemReward.LowId`, `HighId`, `Ql` |
| objective item / QL | no typed offer field; Malis only searches description text; no objective QL |
| mission type/template | direct icon; no separate typed offer template ID |
| destination | direct playfield identity and `Vector3` coordinates |
| credits / XP | direct fields |
| token reward / faction | not exposed |
| description / icon | direct fields |
| entrance/building identity | not exposed in the offer |
| terminal | direct message and mission fields |
| offer slot | derived from array order |
| cohort identity | one event array; no explicit cohort ID |

After acceptance, AOSharp `Mission.Actions` can expose action identities for
FindPerson, FindItem, UseItemOnItem, and KillPerson, plus fields such as source,
target, and playfield instance. Malis does not inspect accepted actions; it
only uses the offered mission identity for acceptance/map upload. Post-accept
fields therefore cannot be promoted into offer-time generation evidence.

## Mission types

Malis receives and filters five direct `MissionIcon` values:

| Icon | Malis display | ClickSaver code | AORebirth capture-backed type |
| ---: | --- | --- | --- |
| 11329 | Return Item | `0x2C41` | `FindItemReturn` |
| 11330 | Kill Target | `0x2C42` | `KillPerson` |
| 11335 | Find Target | `0x2C47` | `FindPerson` |
| 11337 | Find Item | `0x2C49` | `FindItem` |
| 11342 | Use Item | `0x2C4E` | `RepairMachine` |

The numeric identities are preserved; similar human descriptions are not
merged. Malis infers no type from text and supplies no generation weights or
cross-type constraints.

## Reward and objective matching

Malis does not know that an item is rollable before requesting missions. It
first selects a search entry judged compatible with its local QL heuristics,
requests a server cohort, then scans each returned offer.

Reward matching primarily requires returned reward `HighId` equal to the
search entry's `HighId` and exact reward QL. A secondary exact-template case
accepts matching `LowId` only when the returned reward has `LowId == HighId`,
again with exact QL. It also constructs dummy items from returned low/high/QL
for display/value calculation.

Objective/find-like and nano matching is not a separate typed channel. Malis
checks whether `MissionInfo.Description` contains the search entry's full name:
nano names use that string match without the `_missionLevel` equality check;
other description matches require the selected search QL to equal Malis's
chosen mission level. The same roll entry may therefore match either the reward
array or description text. This gives no item-by-channel eligibility matrix and
no objective QL.

Architecture: `SERVER_OFFER_FILTERING`.

## Five-offer cohort

AOSharp presents a single request as one `MissionInfo[]`. Malis preallocates
five views, retains array order, displays all entries, and scans each offer
individually. A disabled mission-type or location filter skips that matching
offer, not the entire cohort; rolling continues until a usable match or a stop
condition. No explicit cohort identifier, per-cohort server cost field,
duplicate constraint, independence guarantee, or cross-offer weighting rule is
exposed. The transport and five slots are structural evidence only.

## Destinations

Each offer supplies playfield identity and X/Y/Z location through AOSharp.
Malis converts the playfield ID to a name, filters against 46 hardcoded IDs
paired by position with 46 default names, and optionally applies X/Z bounds.
It does not expose an entrance identity, building/template identity, computed
distance, or a server destination-eligibility table.

Against 619 ClickSaver playfields, all 46 Malis IDs are present, so Malis adds
zero IDs and omits 573 historical IDs. Thirty-six have an exact ID/name match;
ten have at least one ID/name disagreement. Thirty-eight names match after
diagnostic normalization. The 46-entry list is a user filter, not proof that
other destinations are server-ineligible.

## Item-corpus join

All 43,075 distinct Malis low/high endpoint identities exist in the retained
AORebirth `items.dat` projection. All five Malis reward JSON catalogs are
semantically identical to AORebirth's current mission reward catalogs; four
are byte-identical, while the nano JSON differs only in serialization.

Against 34,560 ClickSaver identities:

- 19,513 are exact Malis endpoint identities;
- 15,047 ClickSaver identities have no Malis endpoint;
- 23,562 Malis endpoints are absent from ClickSaver;
- 15,954 ClickSaver rows have an exact Malis low/high template pair;
- no additional ClickSaver identity was promoted through interpolation merely
  from sharing a Malis endpoint pair.

Malis contains neither endpoint ID 89622, an exact template-pair bridge for it,
nor a normalized-name diagnostic match for ClickSaver's historical
`Pill with Fling Shot Proficiency`. Item ID 89622 remains `UNRESOLVED`.

## Direct answers to the 23 required questions

1. **What knowledge exists?** A client QL lookup, slider request mapping,
   AOSharp offer schema, five type icons, post-offer matching, ordered five-offer
   handling, and a 46-playfield client filter.
2. **ARPA-like data?** `NO_EQUIVALENT_DATA`.
3. **Item→mission QL eligibility?** No.
4. **Counts/frequencies?** No observations, occurrence counts, frequencies, or
   weights.
5. **Available mission QLs?** A static row indexed by character level; not a
   formula or server-returned set.
6. **Level 80 error?** Difficulty 11 was 144 and became 143; the rationale is
   unknown.
7. **>200/QL200?** Strictly level >200, QL200, non-nano client search; it requests
   the first table mission QL ≥200 (slot 6/current character QL) and filters for
   a QL200 reward. No server rule is proven.
8. **Difficulty mapping?** Wire 1..11 maps to row index 0..10; auto-selection is
   first `IndexOf(QL)+1`.
9. **Known rollable before rolling?** No; only local heuristics gate a search.
10. **Reward matching?** Returned IDs plus exact reward QL, or description string
    matching; see the exact rules above.
11. **AOSharp fields?** Cataloged above and in the generated field artifact;
    mission QL/objective QL/token/faction/entrance are absent at offer time.
12. **Types?** The five numeric icon mappings above.
13. **Objective information?** Description text only at offer time; typed action
    identities become available only after acceptance and are unused by Malis.
14. **Destination information?** Direct playfield and Vector3; Malis filters 46
    IDs and optional X/Z bounds.
15. **Five offers?** One ordered array, five views, individually processed; no
    explicit cohort identity or generation law.
16. **Static datasets?** The eight JSON files plus embedded playfield/type/slot
    lists inventoried above.
17. **Release additions?** Only compiled/runtime files; no additional mission
    dataset.
18. **Corpus comparison?** Exact counts are in the preceding item join; Malis's
    item catalogs equal AORebirth's five reward catalogs semantically.
19. **ID 89622?** No; it remains unresolved.
20. **Which ARPA gaps fill?** Only H and I partially; G, J, K, and L gain
    structural evidence.
21. **Which remain?** A–F remain unfilled; G and J–L remain generator-incomplete;
    H/I still need authoritative resolution of divergences/anomalies.
22. **Highest-value captures?** Listed below.
23. **Implementation readiness?** Broken down below; no reward/destination/cohort
    algorithm is ready from Malis alone.

## ARPA gap matrix

| Gap | Classification | Evidence |
| --- | --- | --- |
| A complete item→mission QL eligibility | `DOES_NOT_FILL` | Search catalogs only |
| B observed item→mission QL matrix | `DOES_NOT_FILL` | No roll observations |
| C reward vs objective eligibility | `DOES_NOT_FILL` | Post-offer matching, no matrix |
| D observation counts | `DOES_NOT_FILL` | No corpus/counters |
| E reward frequency | `DOES_NOT_FILL` | No frequencies |
| F generator weighting | `DOES_NOT_FILL` | Server offers are only filtered |
| G reward QL distribution | `STRUCTURAL_EVIDENCE_ONLY` | Reward QL exists; no distribution |
| H character level→mission QLs | `FILLS_PARTIALLY` | 220 rows, 13 missing cells, 55 divergences |
| I difficulty→mission QL | `FILLS_PARTIALLY` | Exact client mapping, not server algorithm |
| J type generation | `STRUCTURAL_EVIDENCE_ONLY` | Five icons, no selection law |
| K destination eligibility | `STRUCTURAL_EVIDENCE_ONLY` | Client allowlist, no server pool |
| L five-offer behavior | `STRUCTURAL_EVIDENCE_ONLY` | Ordered array, no cohort law |

## Highest-information targeted captures

1. At character levels 200, 201, and 220, request every difficulty detent while
   targeting the same scalable non-nano QL200 item; preserve all five offers and
   every reward low/high ID and QL.
2. Repeat above level 200 for a QL200 nano and a scalable item with distinct
   low/high endpoints to separate cap, nano, and interpolation behavior.
3. Capture level 80 difficulty 11 and every current divergent/missing Malis
   boundary, especially levels 12, 13, and 209–219 difficulty 11.
4. For exact target items, retain complete offer cohorts and then accepted
   mission actions so reward-array and objective-description channels can be
   bridged without guessing.
5. If probabilities become a target, capture ordered, unscreened cohorts with
   character, terminal, sliders, cost, and all five offers over repeated
   requests. A matches-only sample cannot establish frequency or weight.

These are proposals for a later user-operated live capture task. No client or
capture workflow was launched here.

## Generator readiness by subsystem

| Subsystem | Status | Basis |
| --- | --- | --- |
| character level→mission QL | `READY` | AORebirth already has a governed exact table; Malis only corroborates it partially |
| difficulty slider mapping | `READY` | One-based request mapping is source- and capture-backed |
| mission type representation | `READY` | Exact icons/codes/actions bridge to existing capture evidence |
| AOSharp offer representation | `READY` | Exact package metadata plus correlated source |
| QL200 reward behavior above 200 | `NOT_READY` | Client assumption, no offer corpus/server bridge |
| reward eligibility | `NOT_READY` | No complete eligibility matrix |
| reward weighting | `NOT_READY` | No frequency or weight evidence |
| objective-item selection | `NOT_READY` | Description filtering is not generation evidence |
| destination selection | `NOT_READY` | Client allowlist is not the server pool |
| five-offer generation | `NOT_READY` | Representation known; constraints and selection law unknown |

The justified next task is targeted capture and reconciliation, not production
generator implementation.

## Deterministic artifacts and files inspected

Retained external inputs are under `docs/reference/missions/malis/`; the source
manifest lists every origin, byte length, and SHA-256. Generated outputs are:

- `analysis-summary.json`
- `aosharp-mission-field-catalog.json`
- `arpa-gap-matrix.json`
- `character-level-mission-ql.csv`
- `evidence-manifest.json`
- `item-comparison.json`
- `mission-level-comparison.json`
- `mission-type-catalog.json`
- `playfield-comparison.json`
- `release-comparison.json`
- `source-file-inventory.json`
- `source-history-findings.json`
- `static-dataset-inventory.json`

Inspection covered the complete exact Malis source inventory and Git history,
the two requested historical patches, all 34 public merge-request records,
public project/releases/issues/commit-comment metadata, all eight JSON files,
mission-relevant C# hardcoded arrays and request/filter paths, the entire
Mission Roller release subtree, exact AOSharp package metadata, the relevant
AOSharp message/game-data/event/action source, and the accepted ARPA3,
ClickSaver, AORebirth item, playfield, type, and mission-level evidence used by
the joins. `source-file-inventory.json` is the exhaustive per-file record;
the catalogs above are the exhaustive mission-relevant records.

## Validation scope

The offline acquisition tool preserves exact source/history/release/package
artifacts. The generator verifies retained hashes before parsing, fails closed
on malformed schemas and unexpected archive roots, normalizes all joins, and
regenerates artifacts deterministically. Its self-tests cover item schema
failure, duplicate/conflict accounting, short mission-level rows, playfield
and item-ID joins, mission-icon parsing, provenance retention, historical table
comparison, and the >200/QL200 special case. Acquisition, Malis self-test,
Malis deterministic stale check, ARPA self-test/write/stale check, mission-level
graph reproducibility, and Git whitespace validation all passed.

The accepted ARPA preservation repair modifies only
`aorebirth-item-templates.jsonl`, the eight retained ARPA text responses, the
two retained MediaFire HTML responses, and adds
`Tools/tests/fixtures/clicksaver-cs-res-sample.log`. Every restored raw file now
matches the SHA-256 already present in the prior acquisition/evidence manifest.

RUNTIME MISSION LOGIC CHANGED: NO
