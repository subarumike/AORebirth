# Verified mission destination merge

## Identity and scope

| Identity | SHA or name |
| --- | --- |
| Manually verified source | `30f1b3c33eb1334f6dc6e514cb3c7ac8a5f84150` |
| Source branch | `codex/mission-destination-integration` |
| Fetched master before merge | `ce4a3bf973b92cc7b42c3a867e636f04b89eaca7` |
| Common ancestor | `bddea56ca2d47c5eb64ed53f6e9efd279ff4f694` |
| Annotated preservation tag | `mission-destinations-working-30f1b3c33` |
| Target | Public `master` |

OBSERVED: Mike explicitly reported successful original AO client verification
of the source commit: level/slider QL, five offers and correct charges, terminal
geography, acceptance/persistence, dungeon entry, deletion/key removal, and exact
entrance identity/coordinates. This supersedes the earlier unverified-client
status in the source migration report. No agent-operated client test is claimed.

This merge includes no new gameplay implementation. Mission objectives and
PF324 dungeon-template selection remain separate unfinished work, unchanged.
The merge commit containing this report has fetched master as its first parent
and the verified source as its second parent. The tag preserves the exact
verified source independently of later master development.

## Conflict resolution and preservation

The only content conflict was `docs/ai/CURRENT_TASK.md`: master described the
completed PR31/PR32 integration, while the feature described the completed
destination migration. It now describes this merge and links both evidence
reports. `docs/project/PROJECT_STATE.md` merged automatically; both histories,
including the complete existing PR31/PR32 status paragraph, are retained.

No runtime, content, build-project or test source conflict required resolution.
Independent Git-blob comparisons prove:

- All 19 feature runtime/content paths relative to the common ancestor preserve
  16 exact source blobs and three deletions.
- All 12 disjoint master paths preserve their exact master blobs: seven runtime
  files, three test files, the focused PR31/PR32 targets file and its report.
- The full GameData tree, mission mechanics, quest/dungeon source, mission DAO,
  mission handler and startup registration match the verified source.
- Master retains its player/perk requirements, Skill modifiers, buff/aura and
  positive-taunt changes. No unrelated developer work was discarded.

Exactly two destination runtime files remain:

| File | Bytes | Preserved content |
| --- | ---: | --- |
| `MissionDestinations.json` | 657,035 | 812 identities in 47 terminal/QL pools; 45 QL-wide unions. |
| `MissionEntrancePlacements.json` | 1,402,358 | 2,242 placements, exact identities/XYZ bits/rotations, 812 captured WorldPos records. |

The source's offline migration `generate --check` passes on the merged tree.
Andromeda QL29 remains 215 entrances; Borealis QL29 remains 196. Borealis QL25
still has the 124-entry same-QL fallback. The known capture-label QL22 difference
is deliberately preserved exactly as in the verified implementation.

The three retired destination metadata JSONs remain absent. Old
`MissionEntrances.json`, `MissionRollLocations.cs` and `MissionEntranceCatalog.cs`
also remain absent, with no reintroduced runtime references.

## Merged validation

The approved Windows build passed using `cmd /d /c tools\build_aorebirth_debug.cmd`.
The existing `MissionDestinationFocusedTests=true` route passed 81/81 cases
with no skips: 41 destination/handler, 27 dungeon/lifecycle and 13 QL graph cases.
It covers supported and unsupported generation/fees, exact frozen destinations,
acceptance, persistence projections, dungeon entry, deletion and key retirement.

Real disposable-MySQL identity validation passed 74 checks, including rejection
of zero, acceptance of positive and high-bit entrance values, exact unsigned
readback, and unchanged positive-only contracts for other generated identities.
Full disposable-MySQL persistence validation passed all 275 checks, including
publication/fees, rollback, concurrency and key state. No live database or schema
was changed by these disposable checks.

The ordinary full ZoneEngine_New test command was also run without the focused
selection on both untouched fetched master and the merged tree. Both exited 1
before running tests, with these same six CS0246 errors, including identical
file positions and diagnostic messages:

| Test source | Position | Missing type |
| --- | --- | --- |
| `GeneratedMissionCorpseInteractionTests.cs` | 111,18 | `GeneratedMissionCorpseDynel` |
| `GeneratedMissionMaterializationTests.cs` | 197,73 | `MissionAcgMaterializedInstance` |
| `GeneratedMissionMaterializationTests.cs` | 197,12 | `GeneratedMissionWorld` |
| `MissionContentEditabilityTests.cs` | 154,30 | `MissionNpcContent` |
| `GeneratedMissionMaterializationTests.cs` | 210,109 | `MissionAcgLayoutBundle` |
| `GeneratedMissionMaterializationTests.cs` | 210,38 | `MissionAcgMaterializedInstance` |

PROVEN_PREEXISTING_FULL_TEST_COMPILE_FAILURE: exact normalized diagnostic-set
comparison matches fetched master, with zero new diagnostics. No test, fixture,
runtime behavior or normal project selection was changed to hide these failures.
This is not reported as a successful full-project test run.

Result: PASS_WITH_PROVEN_PREEXISTING_FULL_TEST_COMPILE_FAILURE. All requested
executable mission and DAO selections passed; no new regression was observed.

Builds retain NuGet dependency advisories and source warnings. The full DAO
harness uses the already-established isolated-source/`LangVersion=latest`
invocation documented in the two-file migration report; this changes compilation
settings only, not the executed persistence checks or production source.

Ignored receipts are `build-verify/mission-master-merge-build.log`,
`mission-master-merge-focused-tests.log`, `mission-master-merge-focused.trx`,
`mission-master-merge-full-tests.log`, `mission-master-merge-baseline-comparison.json`,
`mission-master-merge-dao-identity.log` and `mission-master-merge-dao-full.log`.
The exact-master full-test baseline log remains in the existing master worktree.

## Boundaries

No refactoring, new feature, raw-capture loading, probability change, faction
destination restriction, DAO/schema redesign, objective change or PF324 template
selection change was made during reconciliation. The verified source's earlier
scoped entrance-identity persistence fix is carried forward unchanged.
No Linux work or production deployment is included. New manual testing of the
combined master tree is not claimed; the accepted mission source and existing
master implementation are both preserved byte-for-byte.
