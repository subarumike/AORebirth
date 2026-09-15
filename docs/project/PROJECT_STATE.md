# AORebirth Project State

Updated: 2026-09-15

Master `c5af4ac18a1378dc41c37d31b9ac62ac46c5f8a0` contains the accepted NewEngine
content cleanup: editable NPC/vendor/quest/dialogue/mission/nano content and no
runtime evidence authorization for spawning. Its source audit and platform
acceptance remain recorded in
`docs/reports/NEWENGINE_LEGACY_CONTENT_BRIDGE_REMOVAL.md`.

The authorized retirement on `codex/retire-legacy-engine-20260915` removes the
Legacy engine implementation, project and fresh build/launch routes. Retained
shared mechanics, entities, editable content and offline fixtures have explicit
current owners. The dependency inventory has zero Legacy edges. Full Windows
build and 739 NewEngine tests pass; exact-source and private-platform acceptance
are being completed. Historical release/database-restore recovery is retained.
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
