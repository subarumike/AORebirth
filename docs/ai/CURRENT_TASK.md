# Current Task

Merge the manually verified mission destination implementation into public master.

Verified source: codex/mission-destination-integration at
30f1b3c33eb1334f6dc6e514cb3c7ac8a5f84150. Mike reports original AO client
verification of rolling/QL, five offers/fees, terminal geography, acceptance,
persistence, dungeon entry, mission deletion and key removal.
Preservation tag: mission-destinations-working-30f1b3c33.
Fetched target: ce4a3bf973b92cc7b42c3a867e636f04b89eaca7.
Merge base: bddea56ca2d47c5eb64ed53f6e9efd279ff4f694.

Preserve both parents: master has the completed PR31/PR32 perk/buff/aura/taunt
integration recorded in docs/evidence/PR31_PR32_INTEGRATION.md. The mission
branch has the exact two-file destination catalog, 2,242 placements, 812 supported
identities and 45 QL pools documented in
docs/evidence/MISSION_DESTINATION_TWO_FILE_MIGRATION.md.
The only content conflict was this active-task document. Project-state history
from both branches is retained. Runtime, content and test files merged cleanly.

No new gameplay changes, refactoring, mission-objective changes, PF324 template
selection changes or retired destination/location systems are permitted.
Merged validation passed: approved build, offline exact catalog check, 81 mission
regressions, 74 real DAO identity checks and 275 full DAO persistence checks.
The ordinary full test project reproduces the same six CS0246 compilation
diagnostics on untouched fetched master and the merged tree; no new diagnostics.
No source/test workaround was introduced. See
docs/evidence/MISSION_DESTINATION_MASTER_MERGE.md for the comparison and scope.
The validated merge and exact preservation tag are ready for publication to master.
No Linux work or deployment is requested.
