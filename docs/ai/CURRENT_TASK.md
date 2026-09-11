# Current task

DAO-owned character, stats and inventory persistence, in progress.

- Branch: `codex/character-inventory-persistence-dao-001`.
- Worktree: `tools-temp/character-inventory-dao001`.
- Starting source: `af7e2357544425b630014d29e1c3c4e3dfcce5d8`; accepted c47f463 is an ancestor, intervening changes are receipt documents only.
- Baseline exact-source Windows acceptance and all 12 mandatory stages: PASS.
- Shared DAO now owns NewEngine full character/stat/item storage and existing compound nano/item/credit transactions. Gameplay, hydration validation, session ownership and mission authority stay in the runtime.
- In progress: real MySQL fault tests, actual loot/equipment operations, connected zoning/reconnect/restart and exact-source Windows/Linux publication acceptance.
- No schema change, unsafe SetGM use, client launch, production operation, merge, deployment or push authorized. Local commits only.
- Report: `docs/reports/CHARACTER_INVENTORY_PERSISTENCE_DAO.md`.
