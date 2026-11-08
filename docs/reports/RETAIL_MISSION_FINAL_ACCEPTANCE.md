# Retail mission final acceptance

## Source and data under test

Public master was freshly fetched at `cda38284540f45573424fb399a60c9ab7627a6f7`.
The published feature entered final validation at
`2761e89f8807fd36bbf4701b5a8c7ea427a1e670`. Master still points to its original
base. The five expected feature commits were verified in order.

| Commit | Change |
| --- | --- |
| `1b97b9983b63ad14ea94f49c439aff867ba365a3` | Typed offer and key catalogs |
| `df1825503e91dd97ecb70bf0558ac72c72334e57` | Existing retail roller constructs messages from validated definitions |
| `5fb1d66dc33450973b1e4e7ddd5bc02bd300425d` | Architecture rules and proposed-change CI |
| `1e82ab3755325d9e443b811dc65edd0e8bebd318` | Initial validation and evidence gaps |
| `2761e89f8807fd36bbf4701b5a8c7ea427a1e670` | Exact catalog export and publication-byte verification |

The approved private chest content revision is
`2b5431fd382bd5fa00a7b232efb85dc556ad7cc2`. The isolated validation dataset uses
the same protected chest policy bytes plus the previously verified public
mission catalog delta. Raw historical packets remain test fixtures. Exact
selected GameData and compiler receipts are retained outside public Git.

## Architecture

All 17 mutation tests passed. The final enforcement check audited 870 runtime
C# files, with zero introduced findings, unresolved project inputs or private
reference failures. A clean local run of the same public CI command exited 0.
A separate isolated worktree then deliberately introduced a hardcoded objective
ID, a direct recorded-packet decode with a renamed input, and a raw packet
field. The command exited 1 and identified all three rules. The scratch files
were restored byte for byte. The hosted positive CI run passed on the entering
feature revision. No check or exclusion was weakened.

The eight inherited findings remain explicit remediation items. They consist of
two DAO artifact-column comparisons, a playfield identity type literal, the
NPC pet creator equipment-name predicate, two proxy identity masks, a dungeon
ID prefix and a missing-SpecItem diagnostic. Their six source blobs are unchanged
from public master. The guard is a source and catalog enforcement check, with
the authoritative reference pass run separately on private data.

## Original client

**ORIGINAL_CLIENT_ACCEPTANCE=NOT_EXECUTED.** Mike reserves original AO client
testing for himself. No Mike-provided acceptance observations bind this exact
source and GameData. Governed feature validation does not authorize release
startup. The automated and disposable TCP findings below remain separate.

The manual client handoff is to verify the terminal opens, returns five offers
and charges the configured fee only on a successful roll. Check all 11
difficulty positions and the six secondary sliders against matching captured
conditions, then inspect the description, icon, rewards, location and waypoint.
Repeat rolls to observe eligible variation. Accept, duplicate a key, enter and
exit each objective type, finish the objective, verify payout and cleanup, and
confirm expiration and abandonment on separate runs. Reconnect with a partially
looted chest and compare the contents and placement after restart. Client
presentation and input, including walking reachability, remain pending.

## Terminal and performance evidence

The fresh focused suite passed 137 cases. The 13 retained retail responses
reserialized byte for byte. The 78 generated response digest remained
`3d1aa7e35baa65d7669390c439d75311e12d249385c9e9e2eb304ea2fe834604`. This
validates the covered wire and server outcomes, including the five-offer
cohort, credit commit ordering and validated entrance selection. The level
graph covers the 11 difficulty positions. Unknown secondary-slider
distributions and nearest-QL destination eligibility remain evidence gaps.

The established Release benchmark warms 512 rolls, then generates and serializes
4,096 rolls for each of three trials. Each trial produced 10,735,407 wire bytes.
CPU is test-process CPU time. Allocation is the calling thread's allocated
bytes. The current timings vary across trials and were measured separately
from the original baseline.

| Median per 4,096 rolls | Original | Prior repaired | Final |
| --- | ---: | ---: | ---: |
| CPU time | 1,718.75 ms | 125.00 ms | 375.00 ms |
| Allocated bytes | 4,071,802,656 | 221,689,472 | 220,706,432 |

Final CPU trials were 375.00, 312.50 and 437.50 ms. The final medians are 78.2%
lower in CPU and 94.6% lower in allocation than the measured original. The
higher CPU sample relative to the prior repaired run occurred with identical
production code. It establishes timing variability, with the cause unmeasured.

The fresh test-host startup sample measured **468.75 ms CPU**, **473.89 ms
elapsed** and **80,594,096 allocated bytes**. The measured interval prepared
13 response cohorts, 65 offer definitions, 2,242 destinations, 250 qualities,
reward references, mission text/geography and the key policy using the actual
catalog. The shared item and GameData stores were already loaded. This is a
mission-specific one-time measurement, separate from the hot roll loop and
full engine startup.

## Chest policy and baseline controls

The three canonical and isolated files match the exact private Git blobs at
the approved revision.

| Policy | SHA256 |
| --- | --- |
| `Loot/MissionChestRKAllowlist.json` | `1fd1b3d57b46ca9e1e1f08837304f5209d0b82b5fdc4600005a188af042b23f3` |
| `Loot/MissionChestSeasonalExclusions.json` | `d041620afcde32e6723d8a959626c0eef3512d701f1c274e2240225db30ee84d` |
| `Loot/MissionChestSpecialExclusions.json` | `20eba79b7aa504fe50fdd333778f69b6faaee401fcba6e92c3c58bfb62f69085` |

The policy contains 25,118 approved definitions, 13,269 QL bands and all 186
captured pairs across 291 observations. The 23,052 implant definitions remain
eligible. Gift wrapped basket 203261 remains approved at QL 1. All ten
exclusion families resolve. There are 11,152 mandatory denials and no overlap
with approved definitions. The existing denial-only reference 254407 remains
protected even though its item definition is absent.

The 96 selected fresh chest checks and all 50 disposable MySQL chest checks
passed. The real selector generated 19,948 items in 10,000 populations
covering QL 1 through 250, with zero unauthorized selections, excluded items
or preview instance allocations. Mission QL ±5, the configured rarity weights,
one to three items, and expansion and seasonal vetoes remain in force.

The two prior 99-case runs each reported 95 passes, three failures and one
skip. Current master, current feature, and the retained exact-master control
match across the measured chest-specific source closure and relevant private
inputs. These are the prior outcomes and their disposition in this run.

| Prior outcome | Evidence and current handling |
| --- | --- |
| Chest-use reply byte at offset 16 expected 21 and returned 20 | Same native protocol assertion on exact master and feature. The unchanged case was carried forward as an open baseline failure. |
| Style 351, one floor, seed 42, size 9 published three of four instantiated chests | Same assertion intermittently failed on exact master and feature. The case passed in the current selected run. Its prior intermittent failure remains open. |
| Critical original cave test failed before exercising gameplay | The actual saved JSON for the original QL 125 cave, seed 1775745370, was absent from the authorized evidence roots. This exact-layout case was excluded. |
| Companion original cave test was skipped | The same missing file blocks it. It was also excluded. |

Neither the carried-forward failures nor the omitted cases count as fresh
passes. The current suite exercised real style 321, 324, 341 and 351
populations, including multistory layouts and persisted inventories.

## Dungeon reconstruction

The selected suite exercises actual rooms, floor transitions, configured NPC
spawns, exact objective placement, keyed entry and bound dungeon identity.
Version 2 reproduces the golden generator payload and spawn-coordinate bits.
Version 3 reconstructs frozen plan, objective and chest bindings in the
tested cases. The five-floor cave case published chests across multiple
floors. Abandonment saves the ending state before key cleanup and prevents
entry. The expiry check rejects an expired mission at admission.

Version 1 predates this feature. Its historical captured layout path was
replaced by procedural compatibility reconstruction in `8f517d711`.
The current feature preserves that code byte for byte. Original version 1
geometry equality is unverified. The missing original frozen cave JSON
also prevents its exact reproduction. The server-side placement and
reconstruction tests do not establish original-client navigation.

## Five-objective capability matrix

Each row came from the real typed roller at a verified level 60 and difficulty
1 request condition. The fixture projected the selected QL 42 offer into the
existing acceptance path, built its dungeon and resolved its actual GameData
binding. The interaction and reward code is production code. Transaction
storage and client session delivery are in-process fixtures.

| Objective | Verified configured content and interaction | Payout and cleanup |
| --- | --- | --- |
| Kill Person | MFPB family. The owner killed the exact bound actor and the objective-death flag was stored. | PASS |
| Find Person | MFPB family. The owner inspected the exact bound actor. | PASS |
| Find Item | URSE. The owner picked up the exact bound world item. | PASS |
| Return Item | COSE. The owner picked up the exact item and used it at the issuing terminal. | PASS |
| Repair Machine | SPPR tool and CRCH target. The owner used the bound tool on the bound machine and the tool was retired. | PASS |

All five runs committed progression, awarded credits, XP and the generated
material reward, rejected duplicate payment on retry, retired the key link
and held key, and released the empty dungeon. The selected reward and relevant
inventory identities came from the generated offer. The Return Item case
additionally verified the committed cash and outbound stat projection at the
issuing terminal.

An initial run passed eight existing cases and failed the five new cases
because the test fixture omitted its inventory mutation callback and exterior
GameData. The next expanded run passed 68 of 69. Return Item applied the
durable reward but could not publish stats on the fixture exterior until it
registered the existing PlayfieldLocality service. The targeted rerun passed
all five. These were test fixture corrections. The runtime source and loot
policy did not change.

The original-client lifecycle, client walking and presentation, and a separate
wire/SQL run for every objective are still unverified. NPC families, item pools
and repair combinations remain provisional beyond the observed references.

## Open retail evidence gaps

The two person objectives still share the only verified configured family.
The item and repair pools still contain the retained single choices. The
ordinary population policy still covers six families with limited upper-QL
evidence. Style 320 still lacks mapped entrances and a door-style binding.
Five inherited terminal reward bands disagree with current item endpoint QLs.
The final validation preserves all these policies and their documented
limitations. Retail distributions, full QL coverage and the original effects
of the secondary sliders outside matched observations remain open.

## Disposable network regression

The fixture used the previously built, hash-verified feature binaries and
the final isolated GameData. It passed login, five-offer rolling and acceptance,
key duplication and trade, two-account entry, chest opening and item transfer,
partial reopen with two items remaining, authenticated reconnect, and
fresh-process reconstruction. The GM preview accepted 20 and 10,000 population
requests and performed zero inventory writes. The non-GM request was rejected.

After restart, the fixture withheld the persisted seasonal test item while
preserving its original row. It preserved the frozen chest population, restored
the player's items and rejected stale requests and duplicate takes. Both players
could interact under the existing opener rules. Owned zone, login and world
processes exited successfully, and no disposable database or network residue
remained.

The network run exercised one generated objective's acceptance and dungeon/chest
lifecycle. The five objective completion runs are the separate in-process matrix
above. Ordinary NPC death across a process restart and original-client door
operation and exit traversal remain unverified.

## Final disposition

The feature validation source with the new tests is
`4a15c83ac4fea3608ca56fb5f7586752a34857d4`. Its product source and GameData
match the entering validated feature byte for byte. The following report commit
contains documentation only. Exact branch readback and the hosted check are
recorded in the private publication receipt.

| Outcome | Result and scope |
| --- | --- |
| `RETAIL_TERMINALS=UNVERIFIED` | Server and wire checks pass. Original-client presentation and interactions remain pending. |
| `MISSION_OBJECTIVES=PARTIAL` | All five complete in-process through payout and cleanup. Original-client and per-objective TCP/SQL lifecycle remain unverified. |
| `DUNGEON_PERSISTENCE=UNVERIFIED` | Tested current and version 2 paths pass. Original version 1 geometry and the missing saved cave cannot be verified. |
| `RK_CHEST_LOOT=PASS` | Exact approved policy files, 186 captured pairs, selection campaign, and disposable multiplayer/restart checks pass. |
| `DOA_ENFORCEMENT=PASS` | Seventeen mutations and the isolated deliberate CI rejection pass. Eight inherited findings remain recorded. |
| `ORIGINAL_CLIENT_ACCEPTANCE=NOT_EXECUTED` | Mike's standing client-control rule applies. |
| `MASTER_INTEGRATION_READY=NO` | Original-client acceptance of the exact candidate is still required. |

The feature is ready for the remaining acceptance work. No new product
regression was demonstrated. Only test coverage and this report were added
during final validation.
