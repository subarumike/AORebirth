# Current Task

## Active

NewEngine production cutover foundation on Mike-owned
`codex/newengine-production-cutover-001`, based on origin/master
`6e90dda030774726aa2060acb9edb756ea1f635c` and the complete reconciled history
through `4dac603b82dfe64206b155e7e6c0499a9f8ad7f8`.

NewEngine may become default before full gameplay parity. Character, inventory,
transaction, persistence, reconnect and restart integrity remain required.
Unsupported durable actions must reject before mutation. Missing NPC bindings,
dialogue, pets, nanos and quests are tracked feature gaps, not automatic cutover
blockers. No new runtime C# game content is authorized; retain content in
validated data and put reusable mechanics/transactions in C#.

This milestone keeps Legacy present, inventories its shared dependencies and
NewEngine DAO gaps, and tests only offline/disposable environments. Do not modify
the developer branch/worktree, merge to master, deploy, modify production/schema,
launch the client, delete Legacy, or begin the full DAO conversion.

Deliverables and operational blockers are recorded in
`docs/reports/NEWENGINE_CUTOVER_HANDOFF.md`. Historical gameplay-parity reports
remain evidence of missing consumers; their old all-parity release requirement
is superseded by this task's operational-integrity policy.
