# Current Task

Active repair: Mike confirms world entry and reports intermittent running in place after entering Fair Trade with the movement key held through loading. A regression test reproduces retained movement input when the key release arrives during Loading. The source repair resets movement at cross-playfield arrival; transfer tests pass. Exact Windows acceptance and live visual verification remain pending. See `docs/reports/NEWENGINE_TRANSFER_MOVEMENT_RESET.md`. Production still runs the accepted login repair at `517f579400b1901db2b4968d5c5393376791803e`; no production mutation is part of the movement diagnosis/source repair.

The four approved cutover migrations and verified backup/restore evidence remain recorded in `docs/reports/BB5DD916_PRODUCTION_CUTOVER_RECEIPT.md`. After NewEngine writes, Legacy rollback requires the validated database restore/reconciliation path; changing executables alone is unsafe. The agent does not launch or control the official client.
