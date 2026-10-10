# Current Task

Consolidate mission destinations on `codex/mission-destination-integration`
into exactly two runtime files: `MissionDestinations.json` and
`MissionEntrancePlacements.json`. Keep `MissionDestinationCatalog` as the
single data owner. Prefer terminal-playfield/expected-QL pools; use the same
QL-wide union when the origin/QL pair is unobserved. Preserve duplicate picks
and all other mission mechanics, GameData, DAO and schema contracts.

Migration baseline: `0721fa8844407bf6e1462341bf46d122bda44437`.
Use the completed source-linked 93,185-offer analysis offline. Preserve all
2,242 physical placements, 812 observed identities and 45 existing QL pools.
The migration deliberately retains original capture-time QL labels: switching
to the research report's current lookup projection would remove three existing
QL22 destinations. The runtime mission-QL calculation remains unchanged.

Before retiring the three obsolete JSON files, compare every identity,
coordinate bit pattern, rotation, WorldPos offset and QL pool against the old
four-file system, then run the explicitly requested mission and DAO regressions.
Verify QL29 origin separation, Shade's Borealis QL25 fallback, unsupported-roll
fee protection, five-offer generation, acceptance, dungeon entry, abandonment
and key removal. Runtime must never load captures or research outputs.

Implementation and automated validation are complete. Offline migration/check,
approved build, 81 focused mission regressions, 74 real DAO identity checks and
275 full disposable DAO checks passed before retirement. The three old JSON
files were then removed; the offline check and all 81 focused tests passed
again. Exactly two runtime destination files remain (2,059,393 bytes total).

See `docs/evidence/MISSION_DESTINATION_TWO_FILE_MIGRATION.md` and
`docs/generated/missions/destination-runtime-migration/migration-receipt.json`.
Live AO client acceptance of this migration is not yet claimed. No Linux or
master deployment is requested.
