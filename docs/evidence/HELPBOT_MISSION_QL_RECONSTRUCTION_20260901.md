# Helpbot Mission QL Reconstruction

Date: 2026-09-01

## Result

AORebirth now reproduces every published Helpbot mission-QL list for character
levels 1-149 exactly. The governed runtime remains a discrete 220-row by
11-detent lookup; no continuous mission-QL formula is used at runtime.

The previous AORebirth table disagreed with the reference on 41 character-level
rows. Reconciliation changed 43 detent cells: 37 hardest-detent values, the
level-90 ninth detent, and five lower-detent anomalies at levels 77, 112, and
142. Token counts and every row at levels 150-220 were retained byte-for-byte.

## Authoritative acquisition

- Source: `AOWiki Level Parameters`.
- Stable revision: `oldid=44808`.
- Retrieval time: `2026-09-01T11:41:39Z`.
- Raw wikitext SHA-256:
  `f8841253af7ed9b63aa2d9d1a2d48e487239b4f8e44e57b225cc7b3855c04488`.
- Tracked normalized artifact:
  `docs/evidence/data/helpbot-mission-ql-levels-1-149.json`.
- Tracked artifact SHA-256:
  `8a8e3a38f0328c96d0a372645e38ad14f7c892929bcd64ff9e9069e9c017fc35`.

The pinned page says its level data was obtained exclusively in game by asking
Helpbot for each level. Extraction accepts only rows matching the page's level
and mission-list grammar, requires exactly levels 1 through 149, rejects
duplicate/missing levels and malformed QLs, and rejects a raw snapshot whose
SHA-256 differs from the pinned acquisition.

The artifact contains all 149 published rows and all 1,579 distinct published
mission-QL entries. No row, repeated value behavior, or anomaly was silently
normalized away.

## Exact effective selection rule for levels 1-149

The mission request uses eleven one-based difficulty wire values, `1..11`.
AORebirth selects an exact row by bounded character level and then an exact
column by `wire - 1`.

For wires 1 through 10, the derived integer rule is:

`max(1, floor(characterLevel * percentage / 100))`

with percentages, in wire order:

`70, 75, 80, 85, 90, 100, 110, 120, 130, 150`.

Wire 11 is the last QL published for that character level. It is an irregular
authoritative lookup series, not one constant multiplier. A brute-force check
of constant multipliers from 1.7000 through 1.9000 found no exact conventional
floor, half-up, or ceiling fit; the best floor candidate still missed 37 of 149
levels.

The published Helpbot lists contain distinct values. The reconstructed detent
row retains all eleven positions, including adjacent duplicate QLs. Removing
only adjacent duplicates from every reconstructed row reproduces the published
list exactly for all 149 levels. There are 1,639 detent cells and 1,579 distinct
published entries, so the covered rows contain 60 duplicate detent occurrences.

Effective QL 250 capping is directly visible in the reference: wire 11 reaches
250 at character level 140 and remains 250 through level 149.

## Preserved anomalies

The ordinary integer percentages require these exact authoritative overrides:

| Character level | Wire | Formula value | Published value |
| ---: | ---: | ---: | ---: |
| 77 | 3 | 61 | 60 |
| 77 | 4 | 65 | 64 |
| 77 | 8 | 92 | 91 |
| 112 | 7 | 123 | 122 |
| 142 | 10 | 213 | 212 |

The hardest-detent series also contains a genuine cross-level decrease: level
102 publishes QL 186 and level 103 publishes QL 185. The old graph loader's
assumption that every difficulty column must increase between adjacent
character levels therefore rejected authoritative data and was removed. The
loader still requires nondecreasing QLs inside each row, exact neutral
`Q5 == character level`, bounded QLs, complete rows, canonical numeric tokens,
and immutable hash-checked publication.

Historical Malis corrections conflict with the newly declared authoritative
source at several hardest-detent cells, including levels 52-54, 60, and 80.
Those corrections had source-history provenance but no upstream or live-server
proof. They remain preserved in the dated Malis forensic report as conflicting
history and no longer govern AORebirth levels 1-149.

## Evidence status beyond level 149

| Behavior | Status | Boundary |
| --- | --- | --- |
| QL 250 exists as an effective cap at levels 140-149 | `PROVEN` | Directly published by the pinned Helpbot source. |
| Eleven detents, integer percentage pattern, neutral detent, and cap extension into levels 150-220 | `DERIVED` | Consistent with lower-level rows and retained runtime structure, but outside this reference. |
| Existing AORebirth values for levels 150-220 | `INFERRED` | Preserved unchanged from the prior canonical table and partially corroborated by legacy client data; not promoted as Helpbot proof. |
| Exact live AO mission-QL mapping for every level 150-220 detent | `UNKNOWN` | Requires a source or completed live evidence that directly covers these cells. |

No value at levels 150-220 was changed by this reconstruction.

## Runtime audit

The mission-terminal request path is:

1. `QuestAlternativeMessageHandler` receives the one-based difficulty wire.
2. `MissionLevelTable` validates wire `1..11`, clamps the character level to
   the supported `1..220` range, and resolves the discrete graph cell.
3. `MissionRollService` uses that mission QL for generated offers, rewards,
   cash, and XP.
4. Mission NPC level/health scaling is separate and occurs after mission QL
   selection through `MissionNpcDifficultyPolicy`; it is not the terminal
   selection formula.

The smallest correct runtime change was therefore to replace only the 43
incorrect source-table cells, regenerate the compiled graph, and remove the
invalid cross-level-monotonicity guard. No database schema, mission packet,
slider encoding, reward algorithm, location behavior, or NPC scaling rule was
changed.

## Deterministic validation

`Tools/helpbot_mission_ql_reference.cmd` provides two governed operations:

- extraction from a locally supplied pinned raw-wikitext snapshot, guarded by
  its exact SHA-256;
- exhaustive artifact/runtime parity verification for all 149 levels and all
  1,639 detent cells.

`Tools/generate_mission_level_graph.cmd` also imports the tracked reference and
fails before generation if any covered CSV cell diverges. C# graph tests retain
row/field/hash failure checks and now assert the lower-detent overrides, selected
hardest-detent corrections, QL 250 cap, and the level-102-to-103 decrease.

## Files inspected

- pinned AOWiki revision and its raw wikitext;
- `Mission_Tables_Level_Restrictions_Teaming_Levels.ods` provenance;
- `AORebirth/Server/ZoneEngine/XML Data/MissionLevels.csv`;
- `MissionLevelTable.cs`, `MissionLevelGraph.cs`, and generated graph data;
- `QuestAlternativeMessageHandler.cs`, `MissionRollService.cs`, and
  `MissionNpcDifficultyPolicy.cs`;
- mission graph and roll-semantics tests;
- Stage 1, Malis reconstruction, and modern capture-planner evidence reports;
- mission-level graph generator and approved Windows wrappers.
