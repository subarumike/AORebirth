# AI Start Here

This repository is Mike's local AO Rebirth server workspace for Anarchy Online server repair work.

Repo-wide agent rules live in [AGENTS.md](AGENTS.md) and are normally loaded before this file.
This is the repository's single startup index; `docs/ai/AI_START.md` is only a
compatibility pointer.

Expected startup context/read order:

1. [AGENTS.md](AGENTS.md)
2. [AI_START_HERE.md](AI_START_HERE.md)
3. [docs/project/DEVELOPMENT_AUTHORITY.md](docs/project/DEVELOPMENT_AUTHORITY.md)
4. [docs/project/PROJECT_STATE.md](docs/project/PROJECT_STATE.md)
5. [docs/ai/CURRENT_TASK.md](docs/ai/CURRENT_TASK.md)
6. [docs/project/KNOWN_DECISIONS.md](docs/project/KNOWN_DECISIONS.md)
7. [docs/project/SUBSYSTEMS.md](docs/project/SUBSYSTEMS.md) — current engine ownership + pull workflow
8. [docs/project/ARCHITECTURE.md](docs/project/ARCHITECTURE.md)
9. [docs/ai/WORKFLOW.md](docs/ai/WORKFLOW.md)

Before editing source code, project files, or build/deployment implementation,
also read [docs/ai/CODE_STANDARDS.md](docs/ai/CODE_STANDARDS.md).

Conditional/reference instructions:

- Read [docs/ai/TESTING.md](docs/ai/TESTING.md) when Mike explicitly requests
  fixture, unit, or regression tests, or when the task modifies the testing
  workflow itself. Reading this file does not authorize running those suites.
- Read [docs/ai/REGRESSION_GUARDS.md](docs/ai/REGRESSION_GUARDS.md) when the
  requested change affects a listed protected gameplay system. Its constraints
  do not independently authorize automated test execution.
- Read task-specific evidence documents only when the requested task enters
  their scope.

Before exploratory commands for recurring build, engine, capture, or validation work, read the documented workflow and use the approved wrapper/command first. If the documented command is missing, ambiguous, or stale, stop and report the documentation gap instead of rediscovering the workflow.

Keep this file short. Put active task details in `docs/ai/CURRENT_TASK.md`, stable project status in `docs/project/PROJECT_STATE.md`, and historical/reference material under `docs/reference/` or `docs/archive/`.
