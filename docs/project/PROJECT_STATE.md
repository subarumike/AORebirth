# AORebirth Project State

Updated: 2026-10-10

OBSERVED (2026-10-10, original-client mission acceptance): Mike reports that
30f1b3c33eb1334f6dc6e514cb3c7ac8a5f84150 works in the original AO client for
level/slider QL, five offers and credit deductions, geographic selection,
acceptance/persistence, dungeon entry, deletion/key removal and exact destinations.
The annotated tag mission-destinations-working-30f1b3c33 preserves that source.

PROVEN (2026-10-10, mission integration with master ce4a3bf973): The merged tree
preserves the verified mission implementation and all master perk/buff/aura/taunt
runtime/test changes exactly. Only CURRENT_TASK.md required content conflict
resolution; both branches' project history is retained. Two runtime destination
files remain, with 2,242 placements, 812 supported identities and 45 QL pools.
Approved build, offline catalog comparison, 81 mission cases, 74 real DAO identity
checks and 275 full DAO persistence checks passed. Ordinary full-test compilation
has the same six CS0246 diagnostics on exact fetched master and the merged tree,
proven by normalized diagnostic comparison; no new errors. This is an explicit
preexisting limitation, not a full-project test pass. Mission objectives and PF324
template selection remain unchanged and unfinished. No new feature or Linux work.
See docs/evidence/MISSION_DESTINATION_MASTER_MERGE.md.

PROVEN (2026-10-10, two-file mission destination migration on integration branch):
MissionDestinationCatalog now loads only MissionDestinations.json and
MissionEntrancePlacements.json. All 2,242 placements, 812 observed identities
and WorldPos records, 47 origin/QL pools and 45 QL-wide pools are preserved.
Selection prefers terminal-playfield/QL observations, then falls back within
the same QL; uncovered QLs remain unsupported. This geographic policy is not
proof of retail exclusions or probabilities. Capture-time QL labels deliberately
preserve all existing pools; the known current-lookup QL22 discrepancy is not
applied. Other mission generation, QL, content, DAO/schema and dungeon mechanics
are unchanged. Runtime data is 2,059,393 bytes, 55.24% smaller.
Offline comparison, approved build, 81 focused mission regressions, 74 real DAO
identity checks and 275 full DAO checks passed before retiring the three old
files. Offline comparison and all 81 focused tests passed again after retirement.
Original-client validation of this migration remains UNVERIFIED; prior five-offer
client acceptance belongs to 73fc97a75. No master merge or Linux deployment.
See `docs/evidence/MISSION_DESTINATION_TWO_FILE_MIGRATION.md`.

PROVEN (2026-10-09, original mission destination-distribution reconstruction):
All 77 original journals reconcile to 93,185 retained offers: 92,830 raw-backed
exact destinations, 355 missing-raw records, 812 entrance identities and 22
destination playfields. Complete level/slider/zone, expected-QL/zone and
zone/level tables plus source-linked CSV/JSON are under
docs/generated/missions/destination-zone-distribution/. All eleven difficulty
positions were observed at each of six captured levels. Level35 used Borealis;
the other levels used Andromeda, so geography remains a confounder. Ten recorded
QL labels differ from the current established lookup; both labels are retained.
Original level2 metadata is recovered for 355 unresolved rows whose historical
summary had lost the session fallback. Their destinations remain unresolved.
These are observed distributions, not eligibility rules or probabilities.
Runtime repair 73fc97a75, GameData, DAO/schema and running services are unchanged.
Independent output arithmetic/hash review and original-source reproducibility
check passed for all 23 generated files; every resolved offer is accounted for
exactly once in each relevant view. No build or gameplay suites were run.

PROVEN (2026-10-09, mission roll repair on integration branch): the original
joint capture-condition gate rejected uncaptured character metadata and even
exact captured centered requests when the existing type mix required an absent
type bucket. Mike approved provisional expected-QL-only destination reuse.
The shared catalog now unions exact observed entrances at that QL, retains
all original condition metadata, allows repeats and rejects uncovered QLs.
Cross-condition retail eligibility remains UNPROVEN. Identity, WorldPos,
acceptance, keys, dungeon, DAO and schema contracts remain unchanged.
Approved build PASS; 61 focused destination/fee/QL regressions, 74 real DAO
identity checks and 275 full disposable DAO checks passed. The full DAO run
required the existing identity route's compiler override. OBSERVED: Mike confirmed
five offers after rebuilding local runtime repair 73fc97a75. The logged QL25
Borealis request selected from 124 candidates; read-only verification confirms
five exact persisted destinations and a 25-credit charge. Manual mission
acceptance/entry/completion remains unverified. No master merge or Linux
deployment is claimed.
See `docs/evidence/MISSION_ROLL_ELIGIBILITY_REPAIR.md`.

HISTORICAL (2026-10-09, original mission destination integration; selection policy
and client-roll status superseded by the repair above): one shared
MissionDestinationCatalog now supplies generated offers and quest dungeons.
It retains 2,242 physical entrances, of which 812 are observed in 92,830 exact
retail offers. Destination selection uses 547 captured joint condition/type
sets, uniformly with replacement; uncaptured conditions fail closed. Offers
retain complete entrance identity and captured WorldPos, and acceptance
validates those exact values. The old 140-location population and separate
entrance catalog/data are retired. The approved DAO exception accepts nonzero
entrance identity bit patterns without relaxing other identity contracts or
changing the schema. This is feature-branch work, not a master merge or live
deployment; live AO client acceptance is UNVERIFIED. See
`docs/evidence/MISSION_DESTINATION_INTEGRATION.md` for validation and limits.

PROVEN (2026-10-09, merged through PR33): PR31's perk targeting,
related selector grouping, Skill modifiers and periodic child casts combine
with PR32's positive taunts without its persistent-selector changes.
Windows build PASS; focused tests PASS67. The related regression selection
has 205 passes and the same seven failures proven on starting master; the full
test project retains proven baseline missing-mission-type compile failures.
Live AO client acceptance remains UNVERIFIED. Public master merge:
`a57b22776d75f6efffab55031728e0c0c05ed7e8`; PR31 and PR32 are closed as
incorporated/superseded. No new build, engine operation or deployment was run
for the merge. See `docs/evidence/PR31_PR32_INTEGRATION.md`.

PROVEN (2026-10-09, XP award updates): AwardXp now flushes dirty stats after
each non-level-up XP award. Source review confirms that the previous dirty
stat collection retained only the latest value between flushes. XP amounts,
modifiers, caps and level calculations are unchanged, and level-up updates
retain NewLevel-before-stat-flush ordering. Approved Windows build PASS.
No automated suites or AO client automation were performed. OBSERVED:
Mike verified the XP fix in the local AO client on October9 and approved merging.

OBSERVED (2026-10-06): Mike reports the JSON starter-kit change works after
local client testing of source ac4720b9bb202ed48c44995c6ec16870600f9456.
PROVEN: the approved Windows build and local restart completed, with database
readiness and core-engine process/listener ownership verified. The catalog
preserves all 121 starter item rows across 14 professions; validation runs
before character insertion and respects the configured GameData root.
No automated suites or schema changes were performed. The user did not enumerate
profession-by-profession coverage. Linux deployment is not part of this result.

The normal Windows build now uses the existing .NET 10 ChatEngine/LoginEngine
projects and ZoneEngine_New through NewZoneEngineBuild/build.cmd. The read-only
.NET Framework DatabasePreflight has an isolated output directory so its
referenced assemblies do not overwrite core-engine dependencies.

Movement and zone-trigger diagnostics are gated by the existing Network debug
category. They record incoming coordinates and sampled crossings without
changing movement or zoning behavior. The intermittent exit-zoning cause and
live client acceptance remain unresolved.

Linux source governance now requires literal equality between public GitHub
master, linux-private/master, Linux build HEAD and live Login/Zone source SHAs.
Linux portability and build/deployment tools live in public source under
`LinuxBuild`; private patch assembly is retired. Windows and Linux exact-SHA
acceptance remain mandatory before production promotion. Source integration
alone does not claim deployment or client acceptance. See
`LinuxBuild/README.md` and `docs/project/DEVELOPMENT_AUTHORITY.md`.

The accepted NewEngine content cleanup reached master at
`c5af4ac18a1378dc41c37d31b9ac62ac46c5f8a0`: editable
NPC/vendor/quest/dialogue/mission/nano content and no
runtime evidence authorization for spawning. Its source audit and platform
acceptance remain recorded in
`docs/reports/NEWENGINE_LEGACY_CONTENT_BRIDGE_REMOVAL.md`.

The Legacy engine implementation, project and fresh public build/launch routes
are retired. Retained
shared mechanics, entities, editable content and offline fixtures have explicit
current owners. The dependency inventory has zero Legacy edges. Public source
`bf7ce16bbbdc6fd7d5baad5cfe31ebfd781ddfdb` passed all 12 mandatory Windows stages,
including 743 NewEngine and 923 AOtomation tests. Native private-platform package
acceptance and frozen-binary disposable schema/connected acceptance also passed.
The tested private build tooling is now the shared default at `2a9287d4`, following
Mike's explicit approval; fresh builds and publication use NewEngine only.
Historical release/database-restore recovery is retained.
See `docs/reports/LEGACY_ENGINE_RETIREMENT.md`. Production is unchanged; staging
and official-client acceptance remain required before deployment.

ZoneEngine_New collision and movement run on the vendored Lost-Eden Vehicle port
(`AORebirth.World.Vehicle`); BepuPhysics is removed and the Recast navmesh remains for
NPC route planning. Live engine validation of the port is still pending.

Windows is the authoritative development and acceptance platform. NewEngine is
the default server engine; shared gameplay, DAO persistence, login admission and
zoning behavior remain unchanged by the build separation.

Production build/deployment tooling is maintained in public master. Windows-required
source inventories, compatibility adapters and contract fixtures live under
SharedBuild. Earlier Windows and private-platform acceptance passed for the
separated source. Public branch history has been cleaned; hosted-history follow-up and
developer checkout resynchronization remain active.

The complete earlier source history, production receipts and operational details
are preserved privately. This retirement includes no production deployment or
production database operation. See BUILD_ACCEPTANCE_BOUNDARY.md.

NPC dialogue: Knubot scripts (`GameData/Knubot/**/*.json`, `ZoneEngine_New/Core/Knubot`) drive all NPC
conversations; an NPC talks only when a script names it. The Arete, garden vendor, Nascence, Subway and
Zyvania NPCs have no scripts yet and are silent.
