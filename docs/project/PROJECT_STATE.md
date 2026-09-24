# AORebirth Project State

Updated: 2026-09-22

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
