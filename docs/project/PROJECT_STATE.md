# AORebirth Project State

Updated: 2026-09-15

The candidate `codex/data-driven-world-20260915` moves NewEngine's compiled
NPC/vendor/quest/dialogue/mission/nano content into existing editable content
sources and removes runtime evidence authorization for spawning. The source audit
records 83 content-bearing files and 74 bridges migrated, with no unresolved
semantic candidates. Generic mechanics, player DAO persistence and Legacy remain.
Public source `e7a306c566c853ae72a7e9e55a39b889b2a6902f` passed Windows exact-source
acceptance (12 mandatory stages, 739 NewEngine and 1,128 AOtomation tests),
connected/disposable persistence acceptance, and native private-platform
build/test/package acceptance. Final receipt updates contain no runtime changes.
Staging/official-client acceptance is required before production. Production is
unchanged by this candidate. See
`docs/reports/NEWENGINE_LEGACY_CONTENT_BRIDGE_REMOVAL.md`.

Windows is the authoritative development and acceptance platform. NewEngine is
the default server engine; shared gameplay, DAO persistence, login admission and
zoning behavior remain unchanged by the build separation.

Production build/deployment tooling is maintained privately. Windows-required
source inventories, compatibility adapters and contract fixtures live under
SharedBuild. Windows and private-platform acceptance pass for the separated
source. Public branch history has been cleaned; hosted-history follow-up and
developer checkout resynchronization remain active.

The complete earlier source history, production receipts and operational details
are preserved privately. This content cleanup includes no production deployment
or production database operation. See BUILD_ACCEPTANCE_BOUNDARY.md.
