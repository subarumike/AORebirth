# Delmus / master branch reconciliation — 2026-09-14

Status: master release history reconciled; remaining Delmus integration is open.

## Result

GitHub master was fast-forwarded from `7be49b22b4c0116af9c48dc88a44f86ba802f7c7` to `9e57985c1bc8cc79465bcb6016e2097aaafbb3b0`, preserving all 84 commits of the existing integration/release history. No force push, new runtime change, database operation or production operation was performed. Delmus's branch remains `39aab0e908f74697aa69bcd0b93bd6c13e6a5777`.

The accepted gameplay source is `31c73fc142be2e12994f2af6b23095c0f541e032`. Its Server, Libraries and GameData trees are identical to the release-history tip. The deployed artifact was built from `4b03fedc0c81779e8897453c23974b4b9ad52100`; this reconciliation does not create a new artifact or supersede the pending official-client/post-client durable-state acceptance in the existing release receipt.

At the first fetch, remote master had one exclusive commit and Delmus had eight. Delmus then advanced from fa5129fa to 39aab0e9 during this task. Against that latest tip, the release-history master has 85 exclusive commits and Delmus has nine. Earlier developer changes were selectively integrated under different commits; these ancestry counts must not be described as nine wholly missing features. The documentation commit containing this report will add one master-side commit.

## Remaining developer work

| Developer input | Recorded disposition |
| --- | --- |
| d2d98446: nano/buffs/AI/stat resolver | Prior cutover reports identify this as the selectively reconciled developer input. Preserve the accepted replacements; do not replay old persistence implementations. |
| 53c858d9: item events/charges, specials, nano upload, portals | Previously deferred developer delta. Later accepted portal/item repairs are not evidence that this whole commit was imported. Review residual behavior against the integrated owners. |
| ced79688 and 5d663b7a: following, combat/LOS, collision and surface data | Separate reconciliation remains necessary, including collision-data format compatibility. Do not replace accepted motor/zoning behavior wholesale. |
| e40c687a, f8505f17 and 8b9d3609: family/stat organization | Family/overlay composition and 32 blocked Subway candidates were selectively integrated. Eight accepted family definitions and exact weapon/placement bridges remain missing in the recorded content receipt. No additional combat actors were accepted. |
| fa5129fa: NPC catalog, equipment and commands | New since the reviewed 8b9d3609 snapshot. Seventeen changed files add catalog loading, family/leaf resolution, NPC wear bonuses/equipment and command support. The actual new catalog payload is absent from Git. |
| 39aab0e9: equipment-only weapons and AAAA fallback | Pushed during this audit. Nine changed paths remove the old catalog and family/overlay files, remove template Weapons and restore missing-hash substitution with AAAA. This conflicts with the integrated blocked-placeholder/content policy. |

All nine full commit IDs, the newest commit's nine file identities, and the complete conflict list are retained in [the JSON receipt](NEWENGINE_DELMUS_MASTER_RECONCILIATION.json).

## Why a whole-branch merge is not ready

A Git merge-tree preview against the release-history tip reports **32 conflicted paths**, spanning character hydration, SQL/DAO adapters, inventory flush/trade, admission/session interfaces, movement/portal landings, locality, NPC spawning and GameData. No merge was applied. Textually clean paths still require semantic review.

The new plural `AORebirth/GameData/NpcTemplates.json` is explicitly ignored and absent from the developer Git tree. Its loader warns and keeps the new catalog empty when the file is absent. The distinct singular `NpcTemplate.json` was still tracked at fa5129fa, but 39aab0e9 deletes it along with the family/overlay JSON. A clean developer checkout therefore contains neither NPC catalog payload; the loader still attempts both paths. This is a packaging/content gap, not an official-client test result. The older `MobTemplates.json` is also deleted on Delmus's side but holds accepted/candidate content on the integrated side, producing a modify/delete conflict. Those content owners must be reconciled explicitly.

The new tracked `MonsterWeapons.json` contains only aliases LEW1 and LEW2. Their presence does not establish complete Subway weapon coverage. The latest fallback requires AAAA to resolve from a loaded catalog; the missing payload also prevents the branch alone from establishing that diagnostic behavior. Existing AAAA BLOCK_SPAWN, accepted placement restrictions, exact weapon association and missing-family failure behavior remain authoritative pending a reviewed replacement. A development-only pink diagnostic must be explicit and inert; it must not silently become an accepted combat actor.

## Safe continuation

1. Obtain the generated plural NPC catalog, its source/generation procedure and intended family/equipment dependencies. Keep generated content reproducibly available through Git or the governed package process.
2. Adapt the new catalog/equipment support in an isolated integration branch using the reconciled master. Retain accepted content and DAO/admission/movement owners; then reconcile the outstanding item/combat/pathing/collision slices rather than choosing an entire side of each conflict.
3. Run the documented Windows source, NPC/equipment, inventory, zoning/admission and connected persistence gates for that runtime candidate. New content needs its exact accepted identity/stats/weapon evidence. Produce the Linux artifact only from the accepted SHA.
4. Keep the already-deployed release's outstanding official-client/durable-state acceptance separate. No new production switch is part of this branch reconciliation.

## Evidence and validation

- Git fetch, pinned ancestry/counts, fast-forward eligibility, runtime-tree equality, merge preview and remote ref verification: PASS.
- New runtime build/gameplay tests: not run; this change is documentation/ref synchronization only. Existing source acceptance is cited, not rerun or claimed as a new result.
- Inspected: Git commit/file trees and diffs; developer .gitignore, GameDataStore, GameDataPaths, SpawnService, HashSpawnSystem, PlayerInventory and MonsterWeapons; the cutover reconciliation/handoff, Delmus NPC integration/import matrix, systemd deployment reconciliation, CURRENT_TASK and PROJECT_STATE; project startup/authority/workflow instructions.
- Changed: this Markdown report, its JSON receipt, CURRENT_TASK and PROJECT_STATE. Existing developer, primary and release worktrees retain their work.

### Pinned line evidence

- `39aab0e9:.gitignore:115` — AORebirth/GameData/NpcTemplates.json
- `39aab0e9:AORebirth/Server/ZoneEngine_New/Core/GameData/GameDataStore.cs:503` — "NpcTemplates.json not found at {0}; catalog empty",
- `39aab0e9:AORebirth/Server/ZoneEngine_New/Core/GameData/GameDataStore.cs:426` — "NpcTemplate.json not found at {0}; catalog empty",
- `9e57985c:docs/reports/NEWENGINE_DELMUS_NPC_INTEGRATION.md:29` — `AAAA_POLICY=BLOCK_SPAWN`. The baseline already contained an AAAA appearance template, but the current placement guard prevented the developer's fallback behavior. The placeholder is now explicitly unresolved, non-attackable, without combat AI or concurrent weapons, and rejected by `SpawnService` before identity allocation. It cannot become an authorized actor just because its hash is present in a template file.
- `9e57985c:docs/reports/NEWENGINE_DELMUS_NPC_INTEGRATION.md:38` — - Family IDs: 0, 3, 63, 138, 148, 149, 150, 151. Eight missing accepted family definitions; no fabricated curves.
- `9e57985c:docs/project/BUILD_ACCEPTANCE_BOUNDARY.md:3` — Status: Release 4b03fedc is installed and operationally healthy. Official-client and post-client durable-state acceptance remain pending; it is not yet promoted as accepted production authority.

Discord-ready: Master now includes our NewEngine/DAO and deployment fixes. Delmus's latest NPC catalog/equipment work is identified, but its generated catalog is missing from Git and the branches have 32 conflicting files. The remaining work is isolated for reconciliation; live services were unchanged.
