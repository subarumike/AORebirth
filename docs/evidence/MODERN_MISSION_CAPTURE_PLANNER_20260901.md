# Modern AO Mission Capture Planner and Evidence Harvester

Date: 2026-09-01  
Scope: offline reachability planning and evidence acquisition only  
Runtime mission generation changed: **NO**

## Repository and evidence baseline

The primary AORebirth worktree was clean on `codex/arpa3-mission-evidence` at `de61fa4cacb3626cb19155b9548c5325df6d8fd6`. The work was isolated in `C:\Users\Mike\Documents\AORebirth-modern-mission-capture-planner` on `codex/modern-mission-capture-planner`, starting at Malis evidence commit `1cb8b18c2b3683114e947b0ff42b43cf035d0f23`. The ARPA commit is an ancestor of the Malis commit. Neither evidence branch was merged into `origin/master` at task start. No reset, clean, stash, or unrelated-work alteration occurred.

The authoritative planning input remains AORebirth's 220-row `MissionLevels.csv`. Malis is retained as an independent reconstructed client table, not promoted over AORebirth where it disagrees. ARPA supplies historical mission/reward observations but no character-level bridge for those observations.

## Reachability result

The graph contains 2,420 edges: character levels 1–220 times difficulty slots 1–11. Every edge retains both source values, source confidence, disagreement state, access status, special-case status, and live-validation status.

| Evidence state | Edge count | Meaning |
| --- | ---: | --- |
| `PROVEN_STATIC` | 2,420 | AORebirth canonical table contains an exact cell |
| `MULTI_SOURCE_SUPPORTED` | 2,352 | Malis contains the same value |
| `SOURCE_DISAGREEMENT` | 55 | Malis contains a different value; neither value was silently discarded |
| `MISSING` | 13 | Malis has no value; no interpolation was performed |
| `MODERN_ACCESS_BLOCKED` | 11 | All level-1 edges are blocked by the known modern starter-flow constraint |
| `LIVE_UNCONFIRMED` | 2,420 | No indexed modern live mission-offer session exists yet |

The canonical union from levels 2–220 contains every integer mission QL from 1 through 250. This is static selectable-target coverage, not live server proof. No QL in 1–250 is statically unreachable from the known levels-2-through-220 table. Modern live reachability of the full range remains unknown.

### QL1

Result: `QL1_MODERN_REACHABLE` in the known tables. Character level 2, difficulty slots 1–5, selects QL1 in both AORebirth and Malis. Level 1 remains `CHARACTER_LEVEL_1_CAPTURE_BLOCKED`; that access restriction is not evidence that QL1 missions do not exist. The level-2 path is still `LIVE_UNCONFIRMED` until Mike captures it at an ordinary mission terminal.

### Access model

- Level 1: ordinary Rubi-Ka mission-terminal access is unavailable in the known modern starter flow.
- Levels 2–220: `NOT_BLOCKED_BY_KNOWN_STARTER_RULE`, but actual possession of a character at that level and ordinary-terminal usability are live-unconfirmed.
- Character level and mission QL are separate fields throughout the graph and harvester.

## Coverage optimization

The static reachable universe is 250 QLs. Equal-cost set-cover dominance removes only levels 3 and 6; their QL sets are strictly dominated or duplicated. Level 1 is excluded because of the modern access block.

The deterministic branch-and-bound search reached its fixed 50,000-node limit. It found a 48-level, 100%-coverage solution and a simple cardinality lower bound of 23, but did not prove the mathematical minimum. The honest result is therefore `BEST_DETERMINISTIC_NEAR_EXACT`, bounded between 23 and 48 levels—not a falsely labeled exact minimum.

The 48-level full-domain solution is:

`2, 7, 17, 23, 31, 33, 38, 44, 48, 56, 63, 65, 73, 76, 94, 106, 110, 113, 124, 126, 128, 129, 132, 133, 134, 137, 139, 149, 157, 161, 163, 164, 166, 167, 168, 173, 175, 177, 179, 180, 183, 184, 201, 202, 203, 206, 208, 213`.

That is a mathematical planning result, not a recommendation to create 48 characters.

The practical validation set is 14 character-level states:

`2, 10, 12, 13, 52, 53, 54, 60, 80, 200, 201, 209, 219, 220`.

It covers 91/250 QLs (36.4%) while directly targeting the level-1/QL1 boundary, the first and densest source disagreements, historical correction levels, QL200, and above-200 behavior. It leaves 159 statically reachable QLs outside the initial practical set. Those are not claimed unreachable; the adaptive planner can add them when their information value exceeds the cost of another character-level state.

## High-value boundaries

Two QLs are unique in the canonical levels-2-through-220 graph:

- QL1: only character level 2, slots 1–5.
- QL221: only character level 201 in the known table.

There are 53 QLs represented by five or fewer character levels: QL1–4, QL185, QL190–191, QL194, QL197, QL199–201, QL203, QL206–210, QL212, QL215–219, and QL221–249 with gaps listed exactly in `mission-ql-reachability.json`.

Priority levels are:

1. `2`: QL1 modern-path validation.
2. `10, 12, 13`: earliest and densest Malis/canonical disagreements; levels 12 and 13 also contain missing Malis cells.
3. `52, 53, 54, 60`: historical difficulty-11 corrections.
4. `80`: historical difficulty-11 correction from QL144 to QL143.
5. `200, 201, 209, 219, 220`: QL200 control, the unique QL221 path, missing/disputed high cells, and upper QL250 boundary.

First-priority target QLs are QL1, QL21, QL23, QL60, QL93, QL94, QL96, QL107, QL143, QL200, QL221, and QL250. The complete disputed canonical QL list and every substitute edge are generated rather than abbreviated in the report.

## Evidence harvester architecture

`AOSharpMissionOfferHarvester` is a separate evidence-only plugin. It compiles against the exact retained AOSharp SDK 1.0.106 package after verifying SHA-256 `4c2946f10aaa3d92a902be66149a09e4a24ca13bffd8110db37c5def4c578f22`. It was not loaded into AO, and exact-runtime multi-plugin compatibility is not claimed.

The plugin follows the proven Malis transport path but listens to the full `QuestAlternativeMessage`, not only the derived `RollListChangedArgs`. It associates one controlled request with one terminal and one returned cohort. It never filters unwanted offers. It defaults to 2.0 seconds between requests, enforces a 1.5-second minimum, allows only one outstanding request, records 30-second timeouts, and adds no evasion or anti-detection behavior.

Each raw event is appended to JSONL and `Flush(true)` is called immediately. A crash can lose at most the not-yet-received server response, not the previously journaled session. Restart creates a new durable session without overwriting prior evidence. Request IDs are stable inside the journal. Same-request duplicate callbacks are fingerprinted deterministically; identical cohorts returned for different requests remain legitimate independent observations. Unmatched cohorts are retained inside raw error events rather than discarded.

### Exact captured fields

Session/request inputs:

- session ID, request ID, UTC timestamps, character surrogate and raw AO identity;
- character level, profession, breed, faction side;
- organization ID (`Clan`) and organization rank (`ClanLevel`) as distinct raw fields;
- organization side as explicit unavailable data rather than conflating it with the preceding fields;
- terminal identity, current terminal playfield, terminal coordinates;
- all seven sliders, difficulty slot, planned target mission QL, request sequence;
- requested count, configured/minimum cadence, one-outstanding-request contract;
- expected AOSharp package version, observed AOSharp assembly version, harvester version;
- client version as explicit unavailable data on the inspected API path.

Response envelope and offer fields:

- envelope `Unknown1`, `Unknown2`, scope, terminal identity, and returned sliders;
- offer index and complete cohort order;
- mission identity, title, description, terminal identity;
- reward descriptor version, credits, XP, `Unk1`;
- every reward `LowId`, `HighId`, reward QL, and reward `Unk`;
- mission icon, destination playfield identity, X/Y/Z;
- all six fixed unknown byte chunks as Base64 with original AOSharp property names.

AOSharp 1.0.106 does not expose offer mission QL, typed mission template ID, objective identity/QL/type, token reward, destination entrance identity, faction requirements, or description identifiers. These fields remain explicit nulls with availability status. The planner's target QL is never relabeled as a directly observed server field. Mission type is normalized offline from the directly observed icon. Reward names and playfield names are joined offline while preserving raw identities first; unresolved historical item `89622` does not block new captures.

### Raw and normalized schema

Raw JSONL event hierarchy:

```text
session_started
  request_started (stable request_id)
    cohort_received
      offer 1
      offer 2
      offer 3
      offer 4
      offer 5
  request_timeout / duplicate_callback / error
session_stopped
```

Offline normalization emits three append-friendly relations:

- `capture_session.jsonl`
- `mission_request.jsonl`
- `mission_offer.jsonl`

The fixture proves five-offer retention, partial-request survival, deterministic duplicate handling, raw-to-normalized conversion, exact item identity joining, mission-icon and playfield joining, and unknown-field preservation.

## Adaptive planner and readiness

The planner consumes the graph, access constraints, the accepted session index, normalized request/offer files, existing offer counts, and confidence classifications. Each recommendation includes character level, difficulty slot, target QL, existing exact-edge/QL/level offer counts, requested additional offers and equivalent requests, broad-coverage versus hypothesis purpose, reasons, substitutes, access status, and readiness labels.

Readiness is emitted separately for every QL with request attempts, complete cohorts, offers, reward observations, unique raw reward pairs, mission icons, playfields, objectives, new reward identities in the last 1,000 reward observations, and half-split reward-frequency total variation once data is sufficient. Labels are `LOW_SAMPLE`, `EXPANDING`, `STABILIZING`, or `SATURATED_FOR_DISCOVERY`, always accompanied by `DISTRIBUTION_NOT_PROVEN`. Thresholds are campaign diagnostics, not proof thresholds. Observed output frequency is never converted to a Funcom RNG weight.

Controlled comparisons are modeled as hypotheses: same target QL across levels, same level/QL across terminals, and same level/QL across factions. Terminal and faction changes belong in separate sessions so only one experimental variable changes at a time.

## Answers to the 15 required questions

1. **Which character levels are represented?** AORebirth and Malis represent levels 1–220 and slots 1–11. Malis has 13 missing cells. ARPA observations do not supply a character-level mapping.
2. **Which levels are practically usable?** Level 1 is blocked. Levels 2–220 are candidates not blocked by that rule, but actual ordinary-terminal usability is live-unconfirmed per character.
3. **Which mission QLs are reachable?** Static canonical coverage from candidate levels 2–220 is QL1–250 with no gaps. Live modern full-range reachability is not proven.
4. **Can QL1 be generated by a usable level?** The known tables say yes: level 2, slots 1–5. Live validation is pending.
5. **Which QLs appear unreachable?** None in QL1–250 under the canonical static table. No statement beyond that range is made, and static reachability is not live proof.
6. **What is proven versus inferred?** Exact repository cells and source equality/disagreement are proven static facts. Level-1 access is a user-supplied modern constraint. Levels-2-through-220 terminal usability and every server distribution are unconfirmed. No edge is `LIVE_CONFIRMED` yet.
7. **What is the mathematical minimum set?** Exact cardinality is not proven. The deterministic result bounds it between 23 and 48 and supplies a 48-level full-cover solution.
8. **What is the practical set?** `2, 10, 12, 13, 52, 53, 54, 60, 80, 200, 201, 209, 219, 220`.
9. **Which QLs are unique or sparse?** QL1 and QL221 are unique; 53 QLs have five or fewer candidate levels. Exact candidate edges are in the generated QL artifact.
10. **Which disagreements should be tested first?** Levels 10, 12, and 13 first, then level 209 and the remaining generated disagreement queue. Missing cells at levels 12, 13, and 209–219 are retained separately from value disagreements.
11. **Which special cases need dedicated captures?** QL1/level2; levels52–54 and 60 slot11; level80 slot11/QL143; QL200 at levels200/201; unique QL221; level209 disagreements; level219 missing cell; level220/QL250.
12. **How many characters are realistically needed?** The first campaign needs access to 14 exact character-level states, not necessarily 14 newly created characters. Run it in stages and reuse existing characters. Do not create 48 characters for full static QL coverage.
13. **Which levels should be avoided?** Level1 cannot serve the normal-terminal task. Levels3 and6 add no equal-cost full-domain set-cover value. Do not create any other exact-level character solely for this project until the adaptive queue selects it; most add coverage but may not justify their burden yet.
14. **What should the first campaign contain?** The six generated waves below, totaling approximately 835 requests/cohorts and 4,175 offers if every response contains five offers.
15. **What holes remain?** All live server relationships remain unproven; AOSharp lacks direct offer mission QL and several semantic fields; 159 statically reachable QLs lie outside the 14-level practical set; terminal/faction/level confounders remain; the exact set-cover minimum is unproven; and the plugin still needs Mike-owned installed-runtime/live acceptance.

## Six-wave first capture campaign

Counts are operational recommendations, not statistical proof thresholds.

| Wave | Character levels | Planned QLs | Slots | Requests / offers | Purpose |
| --- | --- | --- | --- | ---: | --- |
| 1 — Reachability validation | 2 | 1, 2, 3 | 1–11 | 55 / 275 | Validate the level-2 ordinary-terminal path and QL1-targeting slots |
| 2 — Disputed low cells | 10, 12, 13 | 9–19, 21, 23 as generated | disputed slots | 180 / 900 | Resolve the first and densest Malis/canonical disagreements and missing cells |
| 3 — Corrected boundaries | 52, 53, 54, 60, 80 | 93, 94, 96, 107, 143 | 11 | 50 / 250 | Retest known historical corrections |
| 4 — QL200 and above | 200, 201, 209, 219, 220 | 200, 201, 209, 219–221, 229, 240–242, 250 | 6–11 where QL>=200 | 300 / 1,500 | Separate QL200 controls, >200 filtering, unique QL221, missing cells, and QL250 |
| 5 — Coarse practical sweep | all 14 practical levels | generated low/neutral/high QLs | 1, 6, 11 | 210 / 1,050 | Broad modest sampling before deepening |
| 6 — Level control | 60 and 80 | 60 | level60 slot6; level80 slot2 | 40 / 200 | Same target QL from two levels; terminal/faction controls remain separate sessions |

After each wave, regenerate recommendations. Continue with `BOUNDARY_REFINEMENT` where new identities or table conflicts persist, then `DISTRIBUTION_DEEPENING` only where discovery/stability diagnostics justify more rolls.

## Deliverable artifacts

- `docs/generated/missions/modern-capture/reachability-graph.jsonl`
- `docs/generated/missions/modern-capture/character-level-coverage.json`
- `docs/generated/missions/modern-capture/mission-ql-reachability.json`
- `docs/generated/missions/modern-capture/set-cover-solutions.json`
- `docs/generated/missions/modern-capture/high-value-targets.json`
- `docs/generated/missions/modern-capture/next-best-capture-targets.json`
- `docs/generated/missions/modern-capture/first-wave-campaign.json`
- `docs/generated/missions/modern-capture/statistical-readiness.json`
- `docs/generated/missions/modern-capture/harvester-schema.json`
- `docs/generated/missions/modern-capture/analysis-summary.json`
- `docs/generated/missions/modern-capture/evidence-manifest.json`

## Explicit summary

| Question | Result |
| --- | --- |
| Level 1 character usable at normal mission terminal | NO |
| QL1 reachable from another usable level | YES (static tables; live-unconfirmed) |
| Full modern mission-QL range known | NO |
| Minimum useful character set computed | YES (practical and near-exact; exact minimum unproven) |
| Live harvester implemented | YES (offline-built; live-unvalidated) |
| Five-offer cohort preserved | YES |
| Raw evidence preserved | YES |
| Reward probabilities inferred | NO |
| Runtime mission logic changed | NO |

`RUNTIME MISSION LOGIC CHANGED: NO`
