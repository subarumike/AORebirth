# Current Task

Official-client acceptance and release preparation of exact candidate
`9817b708aef60027649b54cb06ea9ce2fd2c6a64` on `codex/release-9817b708`.
Fresh Windows acceptance, 580 NewEngine tests, 1129 AOtomation tests, 12 mandatory
stages, connected restart/persistence, schema validation, 551 character / 303
account / 261 mission / 275 isolated mission DAO checks, Linux acceptance and
64 deployment failure tests PASS.

The exact Linux artifacts are running on isolated local test container
`aorebirth-linux-staging-9817b708`; character 9950 and existing data are preserved.
Waiting for Mike's official EP1 client world-entry result, then two zone changes,
inventory move/equip/unequip, logout/relogin and a separate test-server restart.
Use the desktop `AORebirth 9817 Test - E Client` shortcut. Credentials remain in
the existing private local staging credential file, never in tracked reports.

Do not promote master before all official-client gates pass. Afterwards fetch
master again, use a separate clean integration worktree and validate the exact
result before preparing the final release. Direct master push is not authorized.
Production operations, schema changes and Legacy removal are not authorized.
The prior height discrepancy is closed; do not reopen it without a new failure.
See `docs/reports/NEWENGINE_9817B708_OFFICIAL_CLIENT_ACCEPTANCE.md` and
`docs/reports/NEWENGINE_RELEASE_PREPARATION.md`.
