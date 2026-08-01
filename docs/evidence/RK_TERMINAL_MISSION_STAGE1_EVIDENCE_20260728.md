# RK Terminal Missions: Stage 1 Evidence

Date: 2026-07-28

## Scope

This document records the evidence boundary for Stage 1 of generated mission-terminal repair. Stage 1 covers only:

- mission type, icon, objective, and text compatibility;
- difficulty-to-mission-QL selection;
- request slider decoding and capture-supported roll selection;
- cash, XP, and item-reward semantics;
- roll-fee behavior already present in the server.

It does not claim that accepted missions are playable or completable. Mission acceptance, persistence, unique mission playfields, procedural interiors, objective tracking, and completion are deferred.

## Capture corpus

The finalized captures used for this stage are:

- `20260727-222650`: insufficient credits;
- `20260727-222946`: ten easy rolls;
- `20260727-223041`: seven rolls across difficulty/location combinations;
- `20260728-001044`: generated mission roll;
- `20260728-003410`: return-item mission lifecycle;
- `20260728-005042`: find-item mission lifecycle;
- `20260728-010220`: repair/rescue mission lifecycle, containing two roll responses;
- `20260728-012547`: find-person mission lifecycle.

All paths are below `tools-temp/AOSharpLiveCapture/bin/Debug/captures`.

The roll corpus contains 23 five-offer responses, or 115 observed offers. Repeated full evidence keys collapse to 108 unique cash/XP evidence records. Duplicate observations are not retained as probability weights.

The insufficient-credits capture contains no outbound mission-roll request. That result is client-local evidence and cannot prove a server response packet. Stage 1 therefore preserves the existing fee rule and validates its success and insufficient-credit branches as pure server logic.

## Captured protocol facts

### Type, icon, and objective compatibility

The decoded captures establish these official item-mission icon meanings:

- icon `11329` = Return Item;
- icon `11337` = Find Item.

The compatible objective action codes decoded from `QuestActions[0].Version` in captured offer templates are:

| Mission type | Objective action code |
| --- | ---: |
| Kill person | `1` |
| Find person | `16` |
| Find item | `15` |
| Return item | `8` |
| Repair machine | `8` |

Return-item and repair objectives share action code `8`; their icon, target identity, reward shape, and text keep them distinct. A generated offer is accepted only when its type, icon, objective action, target, reward shape, and text agree.

All 65 immutable fixture offers contain exactly one objective action, a `Playfield2` destination, reward descriptor version `6`, positive cash and XP, and one item-reward entry. Their objective identity-slot shapes are also stable: Kill and Find Person carry raw type `70099` in `Unknown2`; Find Item carries it in `Action`; Return Item carries `70099` in `Action` plus terminal type `56001` in `Unknown1`; Repair carries `70099` in both `Action` and `Unknown1`. In all 13 Return Item fixtures, that `Unknown1` identity equals the roll's mission-terminal identity, so generated Return Item offers retarget it to the issuing terminal. Template admission validates these shapes rather than guessing a type from prose.

### Difficulty

Difficulty is a one-based wire value. For a valid request `d` in `1..11`, the implementation selects mission-level-table array index `d - 1`. Wire value `0` and values above `11` are rejected rather than clamped.

Captured/table anchors are:

| Character level | Wire difficulty | Mission QL |
| ---: | ---: | ---: |
| 60 | `1` | `42` |
| 60 | `2` | `45` |
| 60 | `3` | `48` |
| 60 | `4` | `51` |
| 60 | `5` | `54` |
| 60 | `6` | `60` |
| 60 | `7` | `66` |
| 60 | `8` | `72` |
| 60 | `9` | `78` |
| 60 | `10` | `90` |
| 60 | `11` | `107` |
| 220 | `1` | `154` |
| 220 | `2` | `165` |
| 220 | `3` | `176` |
| 220 | `4` | `187` |
| 220 | `5` | `198` |
| 220 | `6` | `220` |
| 220 | `7` | `242` |
| 220 | `8..11` | `250` |

The character level used to select a table row is bounded to the table's supported range, `1..220`. This boundary behavior is an implementation safeguard; the captures directly exercise levels 60 and 220.

### Official mission-level graph ownership

The canonical exact source table is
`AORebirth/Server/ZoneEngine/XML Data/MissionLevels.csv`. After normalizing
repository text line endings to LF, its SHA-256 is
`295ade2cac00ddfc975bbf1c3f0d7f953f3726e08cc21c0c1f32a5b5b30eb70f`.
It contains one exact header plus 220 level rows, eleven mission-quality
positions per row, and the existing token column.

`Mission_Tables_Level_Restrictions_Teaming_Levels.ods` remains upstream
provenance with SHA-256
`5efdba9a2e8310253246d82a9e733d90b32bb4b360a035c157f9d81832f4a0e7`.
Its expanded ODF cells match the canonical mission positions and token values
through level 133. The mission cells for levels 134–220 were coerced to
floating-point scientific notation and lost low-order digits. The ODS therefore
cannot reproduce the exact complete graph and is not a production or generation
dependency.

`tools/generate_mission_level_graph.cmd` validates the canonical CSV and emits
`Core/Missions/MissionLevelGraphData.g.cs`. A `--check` invocation performs a
byte-for-byte reproducibility check. The generated artifact embeds the source
path, canonical source/payload hash, ODS provenance hash and limitation, and
the complete canonical rows. Production does not read either spreadsheet file.

Before mission rolling can use a QL, the runtime loader requires:

- exactly levels `1..220`, with no missing, duplicate, conflicting, or extra
  level row;
- exactly unique difficulty positions `Q0..Q10`, in canonical order, with no
  missing, duplicate, out-of-range, malformed, or extra header cell;
- exactly `Level + 11 QL values + Tokens` for every row;
- canonical unsigned-decimal tokens without signs, whitespace, or leading
  zeroes;
- mission QLs in `1..250` and the unchanged token values in `1..9`;
- nondecreasing QLs within every row and down every difficulty column;
- the official neutral invariant `Q5 == level`;
- nondecreasing existing token values;
- exact payload SHA-256 and byte-identical canonical reserialization.

Only after every check passes does one immutable graph snapshot become visible
through an atomic reference exchange. Failed validation publishes nothing; a
failed test reload cannot replace an already valid snapshot. There is no partial
row admission, default row, interpolation, guessed value, runtime file search,
or legacy QL formula fallback. If no valid graph exists, the roll path emits a
specific diagnostic and player message and returns before credits are charged.

This hardening does not alter the official values, character-level clamp,
one-based difficulty wire mapping, location selection, slider behavior,
rewards, token progress, ACG layout selection, or authored quests.

### Continuous sliders

The six continuous sliders are decoded as signed bytes:

1. Good/Bad;
2. Order/Chaos;
3. Open/Hidden;
4. Physical/Mystical;
5. Head-on/Stealth;
6. Money/Experience.

The accepted semantic range is `-100..100`. Examples:

- raw `0` decodes to `0`, or center;
- raw `100` decodes to `+100`;
- raw `156` decodes to `-100`;
- raw `255` decodes to `-1`.

Raw values `101..155` decode outside the valid signed range and are rejected. Response-side slider bytes are not treated as authoritative because the captures show they are not a reliable echo of request semantics.

Only two complete outbound request profiles are proven by the finalized corpus:

- neutral: `[0, 0, 0, 0, 0, 0]`;
- combined-left: `[-100, -100, 0, 0, 0, -100]`, encoded as raw `[156, 156, 0, 0, 0, 156]`.

For level 60, difficulty wire `1`, the captured combined-left five-offer cohort is exactly:

1. Repair machine;
2. Repair machine;
3. Repair machine;
4. Kill person;
5. Find person.

No finalized capture isolates a partial Good/Bad, Order/Chaos, or Money/Experience change. No finalized capture varies Open/Hidden, Physical/Mystical, or Head-on/Stealth. Their individual effects and any official type probability weights remain unresolved.

Stage 1 therefore uses an explicit categorical slider distance:

- an exact combined-left request ranks combined-left evidence `0`, neutral evidence `1`, and any other profile `2`;
- neutral and every unresolved request rank neutral evidence `0`, combined-left evidence `1`, and any other profile `2`.

This deliberately gives unresolved combinations neutral behavior. It does not invent equal per-slider weights, independent slider effects, or official mission-type probabilities.

## Capture-backed generation behavior

### Five-offer type cohorts

The generator selects from exact five-offer type cohorts observed in the captures. It first minimizes, lexicographically:

1. absolute mission-QL distance;
2. absolute character-level distance;
3. absolute difficulty-wire distance;
4. the categorical slider distance above.

Equal-ranked captured cohorts are selected with the request's deterministic random source. Capture frequency is not interpreted as an official probability distribution.

The combined-left type cohort is enforced only at its exact captured level/difficulty/slider context. Stage 1 does not extrapolate a global per-type ban to other levels or difficulty detents; unsupported contexts use the ranked nearest cohort above.

### Cash and XP

Cash and XP are selected only from the 108 unique captured evidence records for the requested mission type. The selection rank is lexicographic:

1. absolute mission-QL distance;
2. absolute character-level distance;
3. absolute difficulty-wire distance;
4. categorical slider distance;
5. exact playfield match before a playfield mismatch.

An exact type/context match is therefore preferred. If the requested context was not captured, the nearest same-type record supplies its exact captured cash/XP pair. Equal-ranked records use the deterministic random source.

There is no reward formula, scaling, interpolation, or per-capture fitted constant in Stage 1. The corpus does not support a defensible decomposition into a common base reward plus mission-type modifiers, so Stage 1 does not invent one. Playfield is retained as an evidence-context tie-breaker, but the captures do not isolate it as a causal reward input. The official hidden reward calculation remains unresolved. The nearest-record path is an explicit inference for unsupported contexts, not a claim about the official formula.

Level-60, wire-`1`, QL-`42` anchors used in validation include:

- Find Person: `5206` cash / `2178` XP;
- Kill Person: `4500` cash / `2155` XP;
- Find Item (`11337`): `5741` cash / `2196` XP;
- Return Item (`11329`): `13007` cash / `1808` XP;
- Repair Machine: `5627` cash / `2124` XP.

These are individual captured pairs, not universal rewards for their mission types.

### Item reward and text

Generated item rewards are selected from the existing mission-reward catalog with QL-aware selection: non-nano rewards match mission QL and nano rewards may differ by at most 10 QL. If the catalog cannot supply such a reward, generation fails closed before the roll fee is charged. When location, QL, cash, XP, item reward, or icon changes, the offer's short and long text is rebuilt from the resulting structured fields so stale captured values cannot remain in the description.

An unchanged captured offer retains its exact captured text. Regenerated text names the mission type and carries the resulting target where the packet exposes one, coordinates, playfield, terminal where the captured family uses it, cash, and XP. All 65 fixture offers have an empty `CharInfos` target-name array, so Stage 1 uses a generic assigned-target phrase when no structured name exists instead of attempting to parse highly variable prose. It does not invent an item-reward name in the description because the capture corpus does not establish that text contract.

## Validation contract

Focused tests establish these Stage-1 invariants:

- all 13 captured roll bodies round-trip byte-for-byte and retain their golden SHA-256 hashes;
- capture-library byte and hex accessors return defensive copies;
- the official `11329`/`11337` icon meanings and objective action codes remain compatible;
- difficulty is one-based and level 60 wire `1` produces QL `42`;
- signed slider boundary and invalid-range handling is stable;
- the combined-left request produces the exact captured five-type cohort;
- every difficulty detent at levels 60 and 220 generates under neutral, combined-left, and unresolved slider profiles without extrapolated type bans;
- repeated generation with the same deterministic inputs produces the same response;
- generated five-offer responses remain type/icon/objective/text/reward compatible across multiple seeds;
- selected cash/XP values are exact captured pairs;
- item reward QL follows mission QL;
- unchanged capture text remains exact;
- fee deduction and insufficient-credit behavior do not mutate credits incorrectly.

These tests protect the observed packet and generation contract. They do not validate gameplay completion.

## Explicitly unresolved and deferred

The following are not implemented or claimed by Stage 1:

- official independent effects or probability weights for any continuous slider;
- an official mathematical cash/XP reward formula;
- mission acceptance persistence or atomic acceptance;
- accepted-mission QFU/state serialization;
- unique mission playfield or instance allocation;
- random/procedural interior layout generation;
- mission-object placement inside an interior;
- objective progress tracking;
- mission completion, reward payout, or cleanup;
- database schema changes.

No database schema change is required or authorized for this stage. The lifecycle captures are evidence for later stages, but they do not broaden this Stage-1 implementation boundary.
