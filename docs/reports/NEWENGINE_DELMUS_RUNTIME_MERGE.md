# Delmus NewEngine runtime merge

Date: 2026-09-14. Mike authorized merging the new changes and ignoring the missing JSON for now.

## Source and scope

This is a real merge of Delmus's `New-ZoneEngine` tip `39aab0e908f74697aa69bcd0b93bd6c13e6a5777` into master baseline `864db2b7823cf1d9bd7094bc02aced057eb510cc`, prepared on `codex/merge-delmus-newengine-20260914` in an isolated AORebirth worktree. It reconciles nine exclusive developer commits and 32 conflicted paths. Delmus's branch and the primary working checkout are preserved.

The incoming `.gitignore` entry `AORebirth/GameData/NpcTemplates.json` is retained. The file is absent and optional at startup. Existing accepted `MobTemplates.json` and family/overlay catalogs remain available; ignoring the new catalog does not manufacture missing NPC content or approve placeholder combat.

## Reconciled behavior

- Integrated NPC template/equipment/monster-weapon support, family-band materialization, NPC AI/follow behavior, line-of-sight caching, surface collision and grounded movement changes, item-event extraction, charge tracking, death/respawn support, stat rebase scheduling and tick diagnostics.
- Retained accepted DAO-backed character hydration, aggregate inventory/trade writes, player nano persistence, authenticated login/zone admission, transfer ownership, movement-input reset and arrival heading. Incoming actor-local nano runtime is restricted to NPCs; player nano effects continue through the accepted service.
- Charge updates now cross the existing shared character DAO transaction. Omitted counts preserve the stored count; retired rows remain present with a valid stored count. No schema or migration changes are included.
- Already-known nano crystals are rejected before consumption. Successful upload and item retirement remain one transaction; failure tests still exercise a new, unknown nano.
- Existing accepted NPC hashes take precedence over the optional new catalog. Materialized family bands are applied once. Missing family references and blocked Subway/AAAA templates retain their fail-closed behavior. No automatic AAAA actor substitution is enabled.
- Restored accepted locality visibility bookkeeping after a clean Git merge incorrectly cleared visibility on every activation. Regression tests retain duplicate-spawn, reconnect and transfer coverage.

## Validation and evidence

Accepted runtime source: `b09e80dc09f4aa5081d5f64b141587acab2332ee`, containing true merge `f62ce6754a5b29f980d1c1e28a6e8297f190fb7a` and the compatibility-contract update. The companion JSON receipt records changed files, inspected files, source identities and evidence hashes. The subsequent receipt commit changes documentation only.

- Exact-source Windows acceptance, all 12 mandatory stages and 703 NewEngine tests: PASS.
- Windows/Linux assembly compatibility, accepted placement corpus and DAO architecture guards: PASS.
- Character DAO validation, 551 checks and real disposable MySQL integration: PASS.
- Disposable schema, transaction, remaining-charge persistence, location-only count preservation and two process restarts: PASS. This ran on merge source f62ce675; the subsequent contract update changes no runtime code.
- Connected LoginEngine authentication, character selection, NewEngine entry, admission negatives, concurrent claims, inventory/equipment, logout/reconnect, distinct-process restart, nano/morph/mission reload and final durable state: PASS on b09e80dc.
- Disposable cleanup: PASS. No production contact. Native Linux build and official-client gameplay acceptance were not run for this merge.

The first exact-source run built successfully and identified the added nullable charge count as a public-contract baseline change. The approved Stage 2 generator updated only that property's metadata; Windows and Linux compatibility checks both pass against the regenerated contract.

The isolated worktree uses 4,710 existing accepted playfield input files from the earlier AORebirth acceptance worktree. Each was SHA-256 verified during seeding; the local receipt is `build-verify/playfield-inputs.json`. These ignored geometry inputs and the absent NPC catalog are not added to Git.

## Remaining limits

No production deployment, production database operation, schema migration, game-client launch or official-client acceptance is part of this merge. The installed release remains separate. New NPC content dependent on the missing plural catalog is unavailable until that data and its provenance are supplied. Runtime changes require official-client acceptance before release promotion.
