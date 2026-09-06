# Mission Capture Wave from Proven Coverage Gaps

Generated deterministically from the retained destination-eligibility corpus and canonical mission-level graph. This is a capture plan only.

## Outcome

- Current exact expected-QL coverage is **45/250**: `1-33, 35, 37-38, 40, 42, 44-45, 48, 52, 55, 62, 66`.
- Missing expected QLs are **205** values: `34, 36, 39, 41, 43, 46-47, 49-51, 53-54, 56-61, 63-65, 67-250`.
- The broad wave samples **52** missing QLs with **22** level-locked characters, 50 requests / 250 offered missions per target.
- The matched control wave reuses levels 25 and 37 at PF 655 and PF 800 for QLs 25 and 44: 240 requests total.
- All commands use explicit detents. The plan does not depend on treating the response-side QL candidate as authoritative.

| Wave | Variable isolated | QLs | Level-locked characters | Requests | Execution status |
| --- | --- | --- | ---: | ---: | --- |
| Broad QL discovery | expected QL | 52 missing values | 22 | 2600 | ready |
| Matched level/terminal control | level or terminal geography | 25, 44 | 2 reusable levels | 240 | ready if exact saved levels remain |
| Faction control | faction at fixed PF 800 terminal | 29 | 3 faction characters | 60 | conditional access gate |

## Proven model and boundaries

- Destination reconstruction baseline: `c09869d5028ad455569eef70c7a4abc86480b253`.
- Destination eligibility baseline: `ec5c2ac9600fcaebead785009e6fc5590f9bb848`.
- Terminal identity, terminal playfield, coordinates, side, level, detent, sliders, and complete five-offer cohorts are experimental provenance.
- Terminal instances are not separate backend loot tables unless matched controls prove a difference. No backend variation is assumed.
- Cross-playfield local-coordinate distances are not compared. Only within-playfield local distances remain valid.
- Offer frequencies, Jaccard overlap, saturation labels, and marginal discovery are diagnostics, never inferred Funcom weights.
- Duplicate offers in a five-offer cohort are preserved as evidence and require no special capture wave.

## 1. Exact conditions already captured

- Exact raw-backed destination offers: **92,830** in **77** sessions; unresolved offers: **355**.
- Character levels: `2, 7, 13, 25, 35, 37`. Expected QLs: `1-33, 35, 37-38, 40, 42, 44-45, 48, 52, 55, 62, 66`. Side: Omni only.
- Expected QL source: static mission-level graph. Captured secondary-slider inputs include centered, the `FIND_ITEM_PERSON_SUPPLEMENT` supplement, and the completed level-2 one-variable matrix; the 174-row coherent-condition artifact preserves every exact combination separately.
- Coherent condition groups: **174**. Aggregating them is allowed for coverage inventory, not for causal or probability claims.
- PF 655 (Andromeda), `Basic Individual Mission Terminal` identity `3221226127`, local XYZ `(3236.463, 35.11, 921.2086)`: 79,025 offers; instance provenance only.
- PF 800 (Borealis), `Individual Mission Terminal` identity `3221226272`, local XYZ `(632.6141, 72.80022, 545.5198)`: 13,805 offers; instance provenance only.

## 2. QL gap and saturation matrix

The authoritative 250-row matrix is `docs/generated/missions/capture-wave-plan/expected-ql-capture-gap-matrix.csv` (and JSON). It includes candidate levels/detents, exact counts, last-100/500/1000 discoveries, marginal discoveries per 1,000 offers, neighbor overlap, saturation, and priority.

- Captured QLs with nonzero observations: 45. Missing QLs: 205.
- Existing QLs marked saturated are saturated only for destination discovery under their coherent captured conditions; they are not probability-complete.

## 3. Character-level solutions

- Mathematical all-missing solution: **34** levels `46, 48, 67-68, 71, 76, 78, 94, 103, 119, 121, 124, 129, 139, 154, 158, 165, 184, 188-192, 194-197, 199, 201, 203-204, 212, 219-220`; `BEST_KNOWN_DETERMINISTIC_WITNESS_NODE_LIMIT_REACHED`.
- Practical evidence-preserving all-missing solution: **49** levels `42, 49, 51, 64, 68, 79, 87, 97, 105, 110, 112, 115, 118, 120-122, 124-125, 127-140, 142-144, 146-147, 149, 156, 163, 165, 177-178, 180, 185, 201-202, 208-209`; `DETERMINISTIC_BRANCH_AND_BOUND_EXHAUSTED`. Helpbot edges are required wherever available; local-table-only edges are used only for the 17 QLs without Helpbot proof.
- Executable broad-wave roster: **22** levels `36, 49, 56-57, 70, 81, 87, 89, 127, 135, 143, 163, 165, 177-178, 180, 185, 201-202, 208-209, 213`.
- Historical captured levels are observations, not proof those character snapshots still exist. Reuse an exact saved level only when Mike confirms it has not advanced.

### KEEP / SAFE_TO_LEVEL

- `KEEP`: broad-wave levels `36, 49, 56-57, 70, 81, 87, 89, 127, 135, 143, 163, 165, 177-178, 180, 185, 201-202, 208-209, 213` until every assigned QL completes.
- `KEEP`: the level-25 character and current level-37 character until the matched level/terminal controls complete.
- `SAFE_TO_LEVEL_FOR_THIS_PLAN`: historical levels 2, 7, and 13 after confirming they are not one of the level-locked broad-wave characters. Existing capture evidence remains valid if they advance.
- The level-35 observation and level-37 observation share one surrogate; the corpus proves that character advanced. Do not plan around a separate existing level-35 character.
- Do not accept or complete missions during offer capture. A level-locked character must not gain XP until all commands assigned to that level are complete.

## 4. Variable decisions

1. **Mission QL:** primary next-wave dimension. Easy/Hard is operationally the detent selecting expected QL; no independent Easy/Hard effect is claimed.
2. **Character level:** independently testable because 14 same-QL level comparisons exist, but none is proven causal. QLs 25 and 44 at reusable levels 25/37 are the next matched controls.
3. **Terminal geography:** current data has PF 655 and PF 800 but zero same-level/QL/side/slider multi-terminal groups. Matched PF 655/PF 800 controls are required.
4. **Faction:** every exact offer is Omni. Faction restriction/effect is unproven. A tiny three-faction PF 800 control is conditional on proving all three sides can use the exact same terminal; it is not part of the unconditional wave.
5. **Secondary sliders:** the 27-state level-2 discovery is complete. Money/XP has a definite credits/XP compensation effect; destination effects remain only possible at discovery scale. Keep all six sliders fixed at the supplement preset in this wave.
6. **Live mission QL:** AOSharp does not expose an authoritative field. `MissionInfo.UnkChunk3` bytes 16-19 are a strong candidate, with 67,405 matches, 10 mismatches, and 165 un-compared observations among 67,580 candidates; zero authoritative live decodes. Classification remains `STRONG_CANDIDATE_NOT_RUNTIME_PROMOTION`; no decoder change is justified.

## 5. Terminal-region plan

Use only the two terminal regions already proven by captured identity and playfield:

- PF 655 (Andromeda), `Basic Individual Mission Terminal` identity `3221226127`, local XYZ `(3236.463, 35.11, 921.2086)`: 79,025 offers; instance provenance only.
- PF 800 (Borealis), `Individual Mission Terminal` identity `3221226272`, local XYZ `(632.6141, 72.80022, 545.5198)`: 13,805 offers; instance provenance only.

No new named terminal is invented. A same-playfield second-terminal experiment remains blocked until repository/capture evidence identifies its exact identity and position. PF 800 is new to the level-37 matched control, not a newly asserted backend region.

## 6. Broad wave rationale

- Close every gap from QL 34 through 67, then sample 12 ten-QL-spaced points from 75 through 185, all 17 local-table-only high QLs, and QL 250. Total: 52 QLs.
- This wave spans the whole unseen range without chasing all 205 missing QLs or all 2,242 placement records.
- Each QL starts with 50 requests (normally 250 offers). That is a discovery sample, not an exhaustion or probability threshold.

## 7. Adaptive stopping

After the broad wave, regenerate this analysis before any extension:

- Stop a QL when its last 500 offers add no destinations and its last 1,000 add at most one, unless it is a deliberate control.
- Extend an `EXPANDING` QL by 50 requests only when it adds at least 4 destinations per latest 1,000 offers or opens a new destination playfield.
- Stop a character level when all assigned target QLs satisfy their initial 50 requests; do not roll its other detents just because the character exists.
- Stop terminal expansion after the PF 655/PF 800 matched cells unless destination/playfield support differs enough to justify a named follow-up hypothesis.
- Preserve every complete five-offer cohort and duplicate. Never convert these rules into server probability claims.

A cell is invalid for comparison if the character levels, the exact terminal identity/playfield, faction, detent, preset bytes, request/response linkage, or five-offer completeness differ from the plan. Retain invalid or partial raw evidence, but do not count it as a matched cell.

## 8. Decision register

1. Captured conditions: 174 coherent groups across levels `2, 7, 13, 25, 35, 37`, Omni side, and two proven terminals.
2. Captured/missing QLs: `1-33, 35, 37-38, 40, 42, 44-45, 48, 52, 55, 62, 66` / `34, 36, 39, 41, 43, 46-47, 49-51, 53-54, 56-61, 63-65, 67-250`.
3. Smallest mathematical witness found: 34 characters; optimum not claimed unless its proof status says exhausted.
4. Practical all-missing set: 49 characters with Helpbot edges preserved wherever available.
5. Next broad wave: 52 QLs, 22 characters, 2600 requests.
6. Terminal regions: proven PF 655 Andromeda and PF 800 Borealis only.
7. New terminal locations: none asserted; PF 800 is a new matched condition for level 37.
8. Terminal identity treatment: capture-instance provenance, not backend identity.
9. Character level: independently testable; levels 25/37 at QLs 25/44 isolate it.
10. Faction: unproven; only the gated three-side PF 800 control is recommended.
11. Secondary sliders: discovery complete; freeze them for this wave.
12. Easy/Hard: treat as expected-QL detent unless a future matched analysis proves an independent effect.
13. Live QL field: strong UnkChunk3 candidate remains unpromoted; no decoder change.
14. Per-QL saturation: recorded in the 250-row matrix with exact 100/500/1000 windows.
15. Neighboring-QL similarity: Jaccard diagnostics recorded for previous and next QL; no pool equivalence inferred.
16. Five-offer duplicates: preserve them; no extra duplicate-specific capture.
17. Missing-QL priority: P0 frontier and local-table-only validation, P1 broad band, P2 deferred.
18. New broad-wave characters required: up to 22; none of those levels is proven currently available.
19. Existing characters to keep: exact level 25 and current level 37 for controls; level 35 has already advanced in the corpus.
20. Runbooks: explicit commands are grouped below by level and control cell.
21. Initial sample: 50 requests per broad QL and 40 per matched control cell.
22. Stop/continue: apply the marginal-yield and new-playfield rules above only after regeneration.
23. Invalidating mismatches: level, terminal, side, detent, slider bytes, linkage, or cohort-size mismatch.
24. Shortest practical path selected: the proven-edge 22-character broad roster, followed by two reusable control levels.

## 9. Ready-to-execute commands - broad wave

These are AO chat commands for Mike to run. They are documented here and were not executed by Codex.

### Broad wave - character level 36

Use the proven PF 655 Andromeda terminal and keep every secondary slider fixed:

```text
# QL 36 (PROVEN_HELPBOT)
/missionharvest start 6 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 43 (PROVEN_HELPBOT)
/missionharvest start 8 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 46 (PROVEN_HELPBOT)
/missionharvest start 9 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 54 (PROVEN_HELPBOT)
/missionharvest start 10 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

### Broad wave - character level 49

Use the proven PF 655 Andromeda terminal and keep every secondary slider fixed:

```text
# QL 34 (PROVEN_HELPBOT)
/missionharvest start 1 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 41 (PROVEN_HELPBOT)
/missionharvest start 4 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 53 (PROVEN_HELPBOT)
/missionharvest start 7 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 58 (PROVEN_HELPBOT)
/missionharvest start 8 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

### Broad wave - character level 56

Use the proven PF 655 Andromeda terminal and keep every secondary slider fixed:

```text
# QL 47 (PROVEN_HELPBOT)
/missionharvest start 4 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 50 (PROVEN_HELPBOT)
/missionharvest start 5 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 56 (PROVEN_HELPBOT)
/missionharvest start 6 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 61 (PROVEN_HELPBOT)
/missionharvest start 7 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 67 (PROVEN_HELPBOT)
/missionharvest start 8 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

### Broad wave - character level 57

Use the proven PF 655 Andromeda terminal and keep every secondary slider fixed:

```text
# QL 39 (PROVEN_HELPBOT)
/missionharvest start 1 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 51 (PROVEN_HELPBOT)
/missionharvest start 5 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 57 (PROVEN_HELPBOT)
/missionharvest start 6 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 85 (PROVEN_HELPBOT)
/missionharvest start 10 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

### Broad wave - character level 70

Use the proven PF 655 Andromeda terminal and keep every secondary slider fixed:

```text
# QL 49 (PROVEN_HELPBOT)
/missionharvest start 1 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 59 (PROVEN_HELPBOT)
/missionharvest start 4 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 63 (PROVEN_HELPBOT)
/missionharvest start 5 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 125 (PROVEN_HELPBOT)
/missionharvest start 11 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

### Broad wave - character level 81

Use the proven PF 655 Andromeda terminal and keep every secondary slider fixed:

```text
# QL 60 (PROVEN_HELPBOT)
/missionharvest start 2 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 64 (PROVEN_HELPBOT)
/missionharvest start 3 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 105 (PROVEN_HELPBOT)
/missionharvest start 9 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 145 (PROVEN_HELPBOT)
/missionharvest start 11 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

### Broad wave - character level 87

Use the proven PF 655 Andromeda terminal and keep every secondary slider fixed:

```text
# QL 65 (PROVEN_HELPBOT)
/missionharvest start 2 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 155 (PROVEN_HELPBOT)
/missionharvest start 11 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

### Broad wave - character level 89

Use the proven PF 655 Andromeda terminal and keep every secondary slider fixed:

```text
# QL 75 (PROVEN_HELPBOT)
/missionharvest start 4 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 115 (PROVEN_HELPBOT)
/missionharvest start 9 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

### Broad wave - character level 127

Use the proven PF 655 Andromeda terminal and keep every secondary slider fixed:

```text
# QL 95 (PROVEN_HELPBOT)
/missionharvest start 2 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 165 (PROVEN_HELPBOT)
/missionharvest start 9 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

### Broad wave - character level 135

Use the proven PF 655 Andromeda terminal and keep every secondary slider fixed:

```text
# QL 135 (PROVEN_HELPBOT)
/missionharvest start 6 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 175 (PROVEN_HELPBOT)
/missionharvest start 9 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

### Broad wave - character level 143

Use the proven PF 655 Andromeda terminal and keep every secondary slider fixed:

```text
# QL 185 (PROVEN_HELPBOT)
/missionharvest start 9 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 250 (PROVEN_HELPBOT)
/missionharvest start 11 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

### Broad wave - character level 163

Use the proven PF 655 Andromeda terminal and keep every secondary slider fixed:

```text
# QL 244 (LOCAL_TABLE_ONLY)
/missionharvest start 10 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

### Broad wave - character level 165

Use the proven PF 655 Andromeda terminal and keep every secondary slider fixed:

```text
# QL 247 (LOCAL_TABLE_ONLY)
/missionharvest start 10 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

### Broad wave - character level 177

Use the proven PF 655 Andromeda terminal and keep every secondary slider fixed:

```text
# QL 194 (LOCAL_TABLE_ONLY)
/missionharvest start 7 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 230 (LOCAL_TABLE_ONLY)
/missionharvest start 9 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

### Broad wave - character level 178

Use the proven PF 655 Andromeda terminal and keep every secondary slider fixed:

```text
# QL 213 (LOCAL_TABLE_ONLY)
/missionharvest start 8 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 231 (LOCAL_TABLE_ONLY)
/missionharvest start 9 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

### Broad wave - character level 180

Use the proven PF 655 Andromeda terminal and keep every secondary slider fixed:

```text
# QL 233 (LOCAL_TABLE_ONLY)
/missionharvest start 9 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

### Broad wave - character level 185

Use the proven PF 655 Andromeda terminal and keep every secondary slider fixed:

```text
# QL 203 (LOCAL_TABLE_ONLY)
/missionharvest start 7 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 240 (LOCAL_TABLE_ONLY)
/missionharvest start 9 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

### Broad wave - character level 201

Use the proven PF 655 Andromeda terminal and keep every secondary slider fixed:

```text
# QL 221 (LOCAL_TABLE_ONLY)
/missionharvest start 7 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 241 (LOCAL_TABLE_ONLY)
/missionharvest start 8 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

### Broad wave - character level 202

Use the proven PF 655 Andromeda terminal and keep every secondary slider fixed:

```text
# QL 242 (LOCAL_TABLE_ONLY)
/missionharvest start 8 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

### Broad wave - character level 208

Use the proven PF 655 Andromeda terminal and keep every secondary slider fixed:

```text
# QL 228 (LOCAL_TABLE_ONLY)
/missionharvest start 7 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 249 (LOCAL_TABLE_ONLY)
/missionharvest start 8 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

### Broad wave - character level 209

Use the proven PF 655 Andromeda terminal and keep every secondary slider fixed:

```text
# QL 209 (LOCAL_TABLE_ONLY)
/missionharvest start 6 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# QL 229 (LOCAL_TABLE_ONLY)
/missionharvest start 7 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

### Broad wave - character level 213

Use the proven PF 655 Andromeda terminal and keep every secondary slider fixed:

```text
# QL 234 (LOCAL_TABLE_ONLY)
/missionharvest start 7 50 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

## 10. Ready-to-execute commands - matched controls

Select the exact recorded terminal before each block and hold side Omni plus every secondary slider fixed.

### Control - level 25, QL 25, PF 655

```text
/missionharvest start 6 40 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

### Control - level 25, QL 44, PF 655

```text
/missionharvest start 11 40 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

### Control - level 37, QL 25, PF 655

```text
/missionharvest start 1 40 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

### Control - level 37, QL 44, PF 655

```text
/missionharvest start 8 40 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

### Control - level 37, QL 25, PF 800

```text
/missionharvest start 1 40 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

### Control - level 37, QL 44, PF 800

```text
/missionharvest start 8 40 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

## 11. Conditional faction control

Run only after confirming the exact PF 800 terminal identity `3221226272` is usable by level-37 Omni, Clan, and Neutral characters. Otherwise skip; substituting different terminals would confound faction with geography.

```text
# Omni: QL 29
/missionharvest start 3 20 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# Clan: QL 29
/missionharvest start 3 20 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
# Neutral: QL 29
/missionharvest start 3 20 FIND_ITEM_PERSON_SUPPLEMENT 1.5
/missionharvest status
```

## 12. Deterministic offline validation

```cmd
cmd /d /c Tools\mission_capture_wave_planner.cmd --check
cmd /d /c Tools\test_mission_capture_wave_planner.cmd
cmd /d /c tools\generate_mission_level_graph.cmd --check
cmd /d /c Tools\mission_destination_eligibility_analysis.cmd generate --check
```

## 13. Required declarations

```text
LIVE_MISSION_CAPTURE_PERFORMED: NO
RUNTIME_MISSION_LOGIC_CHANGED: NO
TERMINAL_BACKEND_VARIATION_ASSUMED: NO
TERMINAL_GEOGRAPHIC_COVERAGE_REQUIRED: YES
DESTINATION_PROBABILITIES_INFERRED: NO
```
