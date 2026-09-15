# AORebirth Project State

Updated: 2026-09-15

The candidate `codex/data-driven-world-20260915` moves NewEngine's compiled
NPC/vendor/quest/dialogue/mission/nano content into existing editable content
sources and removes runtime evidence authorization for spawning. The source audit
records 83 content-bearing files and 74 bridges migrated, with no unresolved
semantic candidates. Generic mechanics, player DAO persistence and Legacy remain.
Automated release acceptance is in progress; staging/official-client acceptance
is required before production. Production is unchanged by this candidate. See
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
are preserved privately. No server deployment, database operation or gameplay
change is included in this migration. See BUILD_ACCEPTANCE_BOUNDARY.md.
