# Current Task

Active: purge retained Legacy implementations on `codex/purge-retained-legacy-001`,
starting from master `4159a00c0e7c59f3139fa7ce3054d722f001e6a6`.

Mike explicitly superseded the previous functionality-preservation interpretation.
Delete the 57 relocated implementations and audit additional derived code. Optional
gameplay may become unavailable; unsupported requests must reject before mutation.
Login, inventory integrity, zoning, admission and DAO durability remain required.
No production operation or developer-branch modification is authorized.

Implementation and acceptance are IN PROGRESS. The branch is not a deployable release.
See `docs/reports/NEWENGINE_RETAINED_LEGACY_IMPLEMENTATION_AUDIT.json`.

Prior retirement accepted public runtime source: `bf7ce16b`. The approved private build tooling is
the default at `2a9287d4`. Windows, native packaging and disposable persistence
acceptance passed. Historical recovery remains; production is unchanged.

See `docs/reports/LEGACY_ENGINE_RETIREMENT.md` for exact source/package identities.
