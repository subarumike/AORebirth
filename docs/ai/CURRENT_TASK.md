# Current Task

Pending acceptance: Mike's official-client retry of Fair Trade and backyard entry
and exit facing on live source `cb12160c37507f8318b7e9e39f424f9bf13faa23`.
Door arrivals face along the destination door's clearance; explicit LineTeleport
arrivals face away from the destination line. Ordinary wall-border crossings
preserve the character's heading. Observed backyard routes PF800 -> PF3081 -> PF954
are covered. Exact Windows/Linux acceptance passes (555 tests each), and live
runtime provenance and stability pass with zero restarts. All 280 inventory rows
and 28 characters remain present. See `docs/reports/NEWENGINE_ZONE_ARRIVAL_HEADING.md`.

The prior movement-input reset is included in this release. Source is pushed on
`codex/reset-transfer-movement-input` and included in local master; remote master
was not moved. The deployment README records that online characters do not block
explicitly authorized maintenance; graceful shutdown and saved-state checks remain required.

The four approved cutover migrations and verified backup/restore evidence remain recorded in `docs/reports/BB5DD916_PRODUCTION_CUTOVER_RECEIPT.md`. After NewEngine writes, Legacy rollback requires the validated database restore/reconciliation path; changing executables alone is unsafe. The agent does not launch or control the official client.
