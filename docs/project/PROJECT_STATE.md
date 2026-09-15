# AORebirth Project State

Updated: 2026-09-15

Windows is the authoritative development and acceptance platform. NewEngine is
the default server engine; shared gameplay, DAO persistence, login admission and
zoning behavior remain unchanged by the build separation.

Private production build/deployment tooling is being separated from this public
repository. Windows-required source inventories, compatibility adapters and
contract fixtures now live under SharedBuild. The Windows build passes after
separation; full acceptance and coordinated history cleanup remain in progress.

The complete earlier source history, production receipts and operational details
are preserved privately. No server deployment, database operation or gameplay
change is included in this migration. See BUILD_ACCEPTANCE_BOUNDARY.md.
