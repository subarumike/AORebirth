# Observed mission destination eligibility from the resolved capture corpus

## Outcome and evidence boundary

The fixed 77-session corpus contains 93,185 offers. Exactly 92,830 offers retain
raw packets and resolve to one ACGEntrance placement; 355 offers lack raw packets
and remain unassigned. The exact population observed 812 of the 2,242 client
placements across 22 of 202 client-catalog playfields.

Every positive row means only
`OBSERVED_ELIGIBLE_UNDER_CAPTURED_CONDITIONS`. A zero means
`NOT_YET_OBSERVED` when that expected mission QL has capture coverage, or
`NO_CAPTURE_COVERAGE` when it does not. Neither classification means
ineligible. Observed proportions and Wilson intervals are descriptive
`OBSERVED_FREQUENCY`, not generator weights or probabilities.

No client was launched, no live capture was performed, no database was used,
and no runtime mission source was changed.

## Repository isolation and provenance

The primary worktree was inspected and not modified:

- path: `C:\Users\Mike\Documents\AORebirth`;
- branch: `master`;
- HEAD and `origin/master`: `cf1e12b894b1247b34f96f832b217c1cfb828213`;
- status: one pre-existing untracked `quest example from PRK.txt`.

The analysis worktree is
`C:\Users\Mike\Documents\AORebirth-mission-destination-eligibility-analysis`
on `codex/mission-destination-eligibility-analysis`. Its exact base is
`c09869d5028ad455569eef70c7a4abc86480b253`. The same full SHA was present at
`origin/codex/acgentrance-registry-reconstruction`. No developer branch was
merged, reset, cleaned, stashed, or modified.

Registered worktrees at the initial safety inspection were:

| Path | Branch or state | HEAD |
| --- | --- | --- |
| `C:\Users\Mike\Documents\AORebirth` | `master` | `cf1e12b894b1247b34f96f832b217c1cfb828213` |
| `tools-temp/codex-clean-worktree-20260820` | detached, prunable | `db82530b451917f828ce148876da93edf4c59cc2` |
| `tools-temp/worktree-snapshots/account-dao-parallel-base` | detached | `522cbf3a618d859efce62562d7c9e227bdcb4309` |
| `tools-temp/worktree-snapshots/account-dao-parallel-foundation` | `codex/account-dao-parallel-foundation` | `e3acc4c58132809fd67bd2fe8aa58939109fe0dc` |
| `tools-temp/worktree-snapshots/character-dao-parallel-base` | detached | `e3acc4c58132809fd67bd2fe8aa58939109fe0dc` |
| `tools-temp/worktree-snapshots/character-dao-parallel-foundation` | `codex/character-read-online-dao-parallel-foundation` | `a4f6be03b713b2e88421a1b9d51f318110af678f` |
| `tools-temp/worktree-snapshots/dao-triage-base-1` | detached | `cf1e12b894b1247b34f96f832b217c1cfb828213` |
| `tools-temp/worktree-snapshots/dao-triage-base-2` | detached | `cf1e12b894b1247b34f96f832b217c1cfb828213` |
| `tools-temp/worktree-snapshots/dao-triage-dao-1` | detached | `3b58aa7e02636f99d63b1907c5b2bfbc5815f705` |
| `tools-temp/worktree-snapshots/dao-triage-dao-2` | detached | `3b58aa7e02636f99d63b1907c5b2bfbc5815f705` |
| `tools-temp/worktree-snapshots/dao-triage-master-1` | detached | `cf1e12b894b1247b34f96f832b217c1cfb828213` |
| `tools-temp/worktree-snapshots/dao-triage-master-2` | detached | `cf1e12b894b1247b34f96f832b217c1cfb828213` |
| `tools-temp/worktree-snapshots/mission-dao-build-acceptance` | `codex/mission-dao-build-acceptance` | `19f6122a0e19e17a1db017675b386a2506fc81cf` |
| `tools-temp/worktree-snapshots/mission-dao-parallel-base` | detached | `19f6122a0e19e17a1db017675b386a2506fc81cf` |
| `tools-temp/worktree-snapshots/mission-dao-parallel-ready` | `codex/mission-dao-parallel-ready` | `522cbf3a618d859efce62562d7c9e227bdcb4309` |
| `tools-temp/worktree-snapshots/mission-dao-persistence` | `codex/mission-dao-persistence` | `3b58aa7e02636f99d63b1907c5b2bfbc5815f705` |
| `tools-temp/worktree-snapshots/zone-self-scfu` | `codex/fix-zone-self-scfu` | `7f223eaadfc7c5ee424d5aca1bee541d2dcfb8ac` |
| `C:\Users\Mike\Documents\AORebirth-acgentrance-registry-reconstruction` | `codex/acgentrance-registry-reconstruction` | `c09869d5028ad455569eef70c7a4abc86480b253` |
| `C:\Users\Mike\Documents\AORebirth-clean-final-48e4afc4` | detached | `48e4afc402d2ab996d94e69eb5802f0eb7f6f131` |
| `C:\Users\Mike\Documents\AORebirth-integration-20260825` | `codex/integrate-hydration-pf4582-20260825` | `3fabacb35f06af5aac672e6dcaba8b65cd6711c5` |
| `C:\Users\Mike\Documents\AORebirth-linked-f38bfc9e` | detached | `f38bfc9e0e7d41e68cd2d05135fa5c859976317d` |
| `C:\Users\Mike\Documents\AORebirth-linux-sync` | `codex/sync-linux-runtime` | `6da841f98a04d03513d46e8f4c928a12c5f7acc8` |
| `C:\Users\Mike\Documents\AORebirth-malis-live-build` | `codex/malis-live-build` | `0d790abe603b253a621622b322c015fc2b7c8015` |
| `C:\Users\Mike\Documents\AORebirth-malis-mission-evidence` | `codex/malis-mission-evidence` | `1cb8b18c2b3683114e947b0ff42b43cf035d0f23` |
| `C:\Users\Mike\Documents\AORebirth-mission-location-reconciliation` | `codex/mission-location-capture-reconciliation` | `a9da4fc0dee664e43cebdbf5c0a9f2afe51f1e0c` |
| `C:\Users\Mike\Documents\AORebirth-mission-ql-parity` | `codex/mission-harvest-ql-1-250` | `9a95e539362a20abb463cbed78099132e8811a15` |
| `C:\Users\Mike\Documents\AORebirth-modern-mission-capture-planner` | `codex/modern-mission-capture-planner` | `aea19aba8d0069f4b6c34578247ec2ab53a6e584` |
| `C:\Users\Mike\Documents\AORebirth-new-zoneengine-integration` | `codex/integrate-new-zoneengine-20260903` | `cf1e12b894b1247b34f96f832b217c1cfb828213` |
| `C:\Users\Mike\Documents\AORebirth-playfield-hydration-stage-1-acceptance` | `codex/playfield-hydration-stage-1-acceptance` | `0887d8a27790813ecfc2c1f54f63ce7ce8170649` |
| `C:\Users\Mike\Documents\AORebirth-pr22-combat-hotfix` | `codex/pr22-combat-safety-hotfix` | `d57eb52d896fea1a0aa9871085bb9230b9539586` |
| `C:\Users\Mike\Documents\AORebirth-pr22-hotfix-master` | detached | `cf1e12b894b1247b34f96f832b217c1cfb828213` |
| `C:\Users\Mike\Documents\AORebirth-safe-integration-20260825` | `codex/safe-integration-20260825` | `756b080709513495c92b3671e9e92c4d4de81ccd` |
| `C:\Users\Mike\Documents\codex-playfield-hydration-stage-0-1\AORebirth` | `codex/playfield-hydration-stage-0-1` | `98bbbce3ffb9dc4a7f68d4ae838d24ab333938b4` |

Other mission branches found locally or remotely were
`codex/arpa3-mission-evidence`, `codex/dao-missions`,
`codex/mission-delete-hotfix`, and `codex/mission-ql-parity`, in addition to the
mission worktree branches in the table. The new analysis worktree was created
after this inventory.

## Reconstruction validation and repaired stale boundary

The existing reconstruction test passed 16 tests. Its initial deterministic
`generate --check` correctly failed because
`mission-location-capture-source-coverage.json` embedded the absolute path of
the worktree that originally generated it. That made an otherwise identical
checkout appear stale.

The repair records repository-owned source paths relative to the repository and
leaves genuinely external capture paths absolute. The generator then reproduced
and validated:

| Boundary | Result |
| --- | ---: |
| ACGEntrance placements | 2,242 |
| Catalog playfields | 202 |
| External IDs reproduced | 2,235 |
| Local-only placements | 7 |
| Original controlled offers resolved | 270 / 270 |
| Full raw-backed offers resolved | 92,830 |
| Missing-raw unresolved offers | 355 |
| Ambiguous raw-backed matches | 0 |

Reference inputs were byte-identical. The repaired reconstruction test passes
17 tests and its stale check passes. The eligibility generator independently
rehashes all 77 event journals and links all 93,185 offer keys.

## Metadata inventory and analysis populations

| Field | Offers with field | Offers without field |
| --- | ---: | ---: |
| Character surrogate, profession, breed, faction side | 93,185 | 0 |
| Character level | 92,830 | 355 |
| Terminal identity, playfield and coordinates | 93,185 | 0 |
| Difficulty detent and all six secondary sliders | 93,185 | 0 |
| Static expected mission QL | 92,830 | 355 |
| Live decoded mission QL | 0 | 93,185 |
| Strong, unpromoted mission-QL candidate | 67,580 | 25,605 |
| Mission type, credits and XP | 93,185 | 0 |
| Objective type | 0 | 93,185 |
| Captured reward list | 93,185 | 0 |
| At least one reward identity and QL | 93,122 | 63 |
| Exact destination identity, display name, local XYZ and world offsets | 92,830 | 355 |

The populations remain separate:

- `RAW_BACKED_EXACT_DESTINATION`: 92,830 offers;
- `NO_RAW_DESTINATION_UNRESOLVED`: 355 offers;
- `LEVEL2_CONTROLLED_SLIDER_CORPUS`: 270 offers, 54 requests, 27 states.

The 355 unresolved offers remain usable for fields that their capture journal
actually contains; they are not assigned destinations and are excluded from
destination matrices.

## Mission QL and character level

No response-side mission QL is proven decoded in this corpus. Analysis therefore
uses `STATIC_EXPECTED_MISSION_QL` for 92,830 offers; 355 have neither value.
There are 45 represented expected QLs from 1 through 66, with gaps. The
unpromoted candidate reports 67,405 `MATCH`, 10 `MISMATCH`, and 165
`OBSERVED_NOT_COMPARED`; those values remain diagnostics rather than silently
becoming live mission QL.

The full 1..250 destination/QL matrix uses `OBSERVED`, `NOT_YET_OBSERVED`, and
`NO_CAPTURE_COVERAGE` explicitly.

| Character level | Requests | Offers | Expected QLs | Unique destinations | Destination PFs | Slider states |
| ---: | ---: | ---: | --- | ---: | ---: | ---: |
| 2 | 3,359 | 16,795 | 1, 2, 3 | 133 | 3 | 26 |
| 7 | 4,663 | 23,315 | 4, 5, 6, 7, 8, 9, 10, 12 | 88 | 3 | 15 |
| 13 | 2,261 | 11,305 | 9, 10, 11, 13, 14, 15, 16, 19, 22, 23 | 129 | 5 | 2 |
| 25 | 2,761 | 13,805 | 17, 18, 20, 21, 22, 25, 27, 30, 32, 37, 44 | 317 | 9 | 2 |
| 35 | 2,761 | 13,805 | 24, 26, 28, 29, 31, 35, 38, 42, 45, 52, 62 | 315 | 13 | 2 |
| 37 | 2,761 | 13,805 | 25, 27, 29, 31, 33, 37, 40, 44, 48, 55, 66 | 513 | 11 | 2 |

These are `LEVEL_ASSOCIATION_OBSERVED`, not level restrictions. Fourteen
same-QL, same-side, same-terminal/playfield, same-secondary-slider level pairs
exist. Eight are too small to classify. Six have 1,250 offers per level and are
`POSSIBLE_LEVEL_EFFECT` diagnostics: QL 9 and 10 compare levels 7/13; QL 25,
27, 37 and 44 compare levels 25/37. None proves a restriction, none reaches
`STRONG_LEVEL_EFFECT`, and none supports an equality claim.

Easy/Hard remains separate. Sixteen same-level, same-QL, same-terminal/slider
multi-detent groups exist, but every pair has fewer than 200 offers on at least
one side and remains `INSUFFICIENT_CONTROLLED_DATA`. Differences caused by
expected mission QL are not attributed directly to detent semantics.

## Faction, terminals and geography

All 92,830 exactly resolved offers are Omni. Clan and Neutral have no capture
coverage, and there are zero same-condition multi-faction controls. Every
destination and playfield observed in this corpus is therefore
`FACTION_SPECIFIC_IN_CURRENT_CORPUS`; no faction restriction is proven.

Two terminals are represented, with no same-playfield multi-terminal pair and
no same-level/QL/side/slider multi-terminal control:

| Terminal | Terminal PF | Requests | Offers | Unique destinations | Destination PFs | Same-PF offers | Same-PF rate | Same-PF local-distance range |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Basic Individual Mission Terminal `0xC000028F` | 655 | 15,805 | 79,025 | 513 | 11 | 2,894 | 3.662% | 853.806–3,143.261 |
| Individual Mission Terminal `0xC0000320` | 800 | 2,761 | 13,805 | 315 | 13 | 1,160 | 8.403% | 38.960–185.977 |

Sixteen destinations were observed from both terminals; 796 were observed from
one. Terminal influence is confounded by character level and geography in the
current design, so no terminal restriction or formula is promoted. Same-PF
local Euclidean distance is a valid diagnostic. Cross-playfield distance is not
calculated because no universal coordinate system across unrelated playfields
is proven.

## Mission type and sliders

| Mission type | Offers | Requests containing type | Unique destinations | Destination PFs |
| --- | ---: | ---: | ---: | ---: |
| Find Item | 28,946 | 11,206 | 787 | 22 |
| Find Person | 14,673 | 11,577 | 719 | 22 |
| Kill Person | 12,390 | 8,916 | 210 | 15 |
| Repair | 20,572 | 8,930 | 247 | 19 |
| Return Item | 16,249 | 15,465 | 716 | 22 |

Of 812 observed destinations, 756 appear across multiple mission types and 56
appear under one type in the current corpus. There are 174 input-condition
groups with multiple observed types. These are `OBSERVED_TYPE_ASSOCIATION`;
no type restriction is proven.

The controlled level-2 corpus preserves `CENTER`, `FULL_LEFT`, `FULL_RIGHT`,
`SIGNED_VALUE_-50`, and `SIGNED_VALUE_+50` separately for every secondary
slider. Each exact state has two requests and ten offers. All states use the
same two destination playfields; identity samples differ and overlap sparsely.
Good/Bad, Order/Chaos, Open/Hidden, Physical/Mystical, Head On/Stealth, and
Money/XP are each classified `POSSIBLE_DESTINATION_EFFECT`, never definite.
Ten offers per state cannot distinguish random discovery from a slider effect
and cannot estimate destination probabilities.

## Observed frequency, discovery and cohorts

The analyzer forms 174 coherent request-input groups without aggregating levels,
QLs, sides, terminals or sliders. Discovery diagnostics classify 89 groups
`LOW_SAMPLE`, 33 `EXPANDING`, 10 `STABILIZING`, and 42
`SATURATED_FOR_DISCOVERY`. Saturation means only that few or no new identities
arrived late under that exact condition. Per-destination and per-playfield
observation counts, proportions, sample sizes, discovery curves, last-new
positions and Wilson 95% intervals are generated separately.

All 18,566 exact cohorts contain five offers:

| Unique exact destinations in cohort | Cohorts |
| ---: | ---: |
| 5 | 15,888 |
| 4 | 2,481 |
| 3 | 185 |
| 2 | 12 |

Exact destination duplication occurs in 2,678 cohorts. The same 2,678 cohorts
also repeat exact coordinates. Duplicate playfields occur in 17,883 cohorts.
15,078 cohorts contain a repeated display name attached to different exact
destination identities. Independent-draw comparisons are emitted only for
coherent groups with at least 100 cohorts and remain diagnostic; independence
is not inferred.

## Repeated names and client-universe coverage

The 92,830 exact offers contain 812 placement identities but only 85 display
names. Sixty-two observed names map to multiple identities, 81 same-name/
same-playfield groups contain multiple identities, and 17 names span multiple
observed playfields. The largest family is `a building`: 85 observed identities,
three playfields and 26,631 offers. `Borrowed Hole` represents 59 observed
identities in one playfield and 11,787 offers. Name-only analysis would have
destroyed most destination identity information.

Client-universe coverage is:

- 812 / 2,242 placements observed, 36.2177%;
- 1,430 placements not yet observed;
- 22 / 202 playfields observed;
- 180 playfields not yet observed.

Every unobserved record remains `CLIENT_PLACEMENT_NOT_YET_OBSERVED`, not
mission-ineligible. None of the seven local-only placements occurs in the exact
mission corpus: the six PF100 `ACG Entrance` records and PF105 `Alien Mothership`
all have zero observations.

Identity `0xC0000280`, PF640 preserves local client name `Ænima HQ` with raw
bytes `c66e696d61204851`; the supplied external name remains `?nima HQ`. The
destination itself is not observed, and neither representation occurs in
captured title or description text. No wire location-name field is proven and
neither source is corrected.

## Unique-information capture priorities

1. Matched Clan and Neutral captures at the same expected QL, secondary sliders
   and geographically comparable terminal would add the currently absent
   faction controls.
2. Replicated same-QL comparisons across character levels would test the six
   current possible-level-effect diagnostics rather than relying on one sample.
3. A second terminal in PF655 or PF800, plus a cross-playfield terminal under
   matched level/QL/sliders, would separate terminal identity from geography.
4. Larger controlled repeats for all six secondary sliders would distinguish
   random destination discovery from actual slider effects.
5. Additional samples should target the 33 coherent groups still classified
   `EXPANDING`, not already saturated discovery groups.
6. QLs outside the 45 represented expected values, especially every QL above
   66, add coverage unavailable anywhere in the current matrix.

These are unique-information priorities, not a request to start a capture in
this task.

## Generated artifacts and reproducibility

Primary artifacts under
`docs/generated/missions/destination-eligibility-analysis` are:

- `mission-offer-analysis-inventory.jsonl.gz`: one row per offer with explicit
  field availability and population;
- `destination-ql-evidence-matrix.jsonl.gz`: all 2,242 placements by QL 1..250
  with three-state observation classification;
- `destination-condition-evidence-matrix.jsonl.gz`: one row per observed
  destination and complete experimental condition;
- character-level, terminal and mission-type destination/playfield matrices;
- coherent observed destination and playfield frequency tables;
- `mission-destination-eligibility-summary.json`: coverage, controlled
  comparisons, slider, geography, saturation, cohort, repeated-name, local-only
  and encoding results;
- `mission-destination-eligibility-manifest.json`: exact input and generated
  hashes.

Reproduce from this worktree with:

```cmd
cmd /d /c Tools\mission_destination_eligibility_analysis.cmd generate
cmd /d /c Tools\mission_destination_eligibility_analysis.cmd test
cmd /d /c Tools\mission_destination_eligibility_analysis.cmd generate --check
```

The pasted task specification available to this worktree ends mid-sentence at
section 22 after the word `Useful`. This report and the artifacts implement all
complete requirements present in sections 1 through 21 and the stated primary
section-22 matrix boundary; no unavailable instructions after that truncation
are invented.

`LIVE_MISSION_CAPTURE_PERFORMED: NO`

`RUNTIME_MISSION_LOGIC_CHANGED: NO`

`DESTINATION_SELECTION_IMPLEMENTED: NO`

`DESTINATION_PROBABILITIES_INFERRED: NO`
