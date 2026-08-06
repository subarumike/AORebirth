# Current Task

## Active

Rollback every Arete change made after the 2026-07-22 cutoff while preserving
later Subway, Temple, mission, pet, and infrastructure work that is not Arete
specific.

## Rollback scope

- Restore Arete runtime, content, quests, vendors, loot, movement, tests, and
  evidence to commit `fadc678e2ab991ddff032061d2b3fdd8ec8ba857`.
- Remove shared-runtime registrations and packet paths that only served the
  reverted Arete additions.
- Reconcile the generated combat cohort from the restored runtime authority;
  do not hand-edit generated output.
- Retire the post-cutoff Arete 60/60 acceptance script and assertions while
  retaining the pre-cutoff Arete coverage in the complete AOtomation suite.
- Build, validate, restart engines, commit, and push only the rollback.

## Delivery acceptance

- Generated combat cohort write and validation: PASS (`2512f9652d8549eac1c7bc767ab4810fbf7f28b7129c0094c947baeecf06fb6c`).
- Complete AOtomation suite: PASS (989/989).
- Debug build: PASS against the final generated cohort.
- Engine restart and exact-port ownership: PASS (Chat 6996/7012, Login 7500, Zone 7501).
