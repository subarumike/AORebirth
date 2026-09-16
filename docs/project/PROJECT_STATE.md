# AORebirth Project State

Updated: 2026-09-15

The isolated retained-implementation purge passed acceptance on
`codex/purge-retained-legacy-001`, tested public source `d265e618` from master `4159a00c`.
All 57 relocated files are deleted. Optional missions/dialogue/combat/item-use/new-cast
paths are unavailable instead of preserving old implementations. Windows/all 12
mandatory stages, 520 NewEngine and 339 AOtomation tests, frozen disposable schema/
connected acceptance and private native Linux build/520 tests/package acceptance passed.
Private assembled source is `c30ec82d`, with operations `2a9287d4`. Master, Delmus's
branch and production were not modified. This is an accepted candidate, not a deployed
release. See `docs/reports/NEWENGINE_RETAINED_LEGACY_PURGE.md` and its acceptance receipt.
The earlier retirement acceptance below describes the baseline, not this purge.

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

Windows is the authoritative development and acceptance platform. NewEngine is
the default server engine; shared gameplay, DAO persistence, login admission and
zoning behavior remain unchanged by the build separation.

Production build/deployment tooling is maintained privately. Windows-required
source inventories, compatibility adapters and contract fixtures live under
SharedBuild. Windows and private-platform acceptance pass for the separated
source. Public branch history has been cleaned; hosted-history follow-up and
developer checkout resynchronization remain active.

The complete earlier source history, production receipts and operational details
are preserved privately. This retirement includes no production deployment or
production database operation. See BUILD_ACCEPTANCE_BOUNDARY.md.
