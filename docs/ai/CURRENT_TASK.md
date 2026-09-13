# Current Task

Active acceptance: Mike should retry official-client world entry on live source `517f579400b1901db2b4968d5c5393376791803e`. The Legacy sparse-stat/vital reconstruction repair is deployed to LoginEngine and ZoneEngine_New. Exact Windows/Linux acceptance, production dry-run, runtime provenance and stability pass; both engines have zero restarts. All 280 inventory rows and 28 characters remain present. No schema or operator row updates were made for this repair. See `docs/reports/NEWENGINE_LIVE_CHARACTER_LOGIN_REPAIR.md`.

The four approved cutover migrations and verified backup/restore evidence remain recorded in `docs/reports/BB5DD916_PRODUCTION_CUTOVER_RECEIPT.md`. After NewEngine writes, Legacy rollback requires the validated database restore/reconciliation path; changing executables alone is unsafe. The agent does not launch or control the official client.
