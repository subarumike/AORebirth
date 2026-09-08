# ZoneEngine_New approved mission/gameplay checkpoint

Branch: `codex/zoneengine-new-gameplay-reconciliation`.
Starting validated infrastructure: `6dab287bd52590d7b9c0e7054bef95160a1594ba`.
This checkpoint is not a declaration of full Legacy gameplay parity or permission
to deploy, migrate production, launch the client, or switch master.

## Approved changes

Mike explicitly approved the additive generated-mission schema/DAO changes with
"yes make the changes". Seven normalized generated-mission tables now represent
offer batches, frozen offers, accepted bindings, observations, artifacts,
allocation sequences and exact world objects. The existing mission DAO owns the
transactions. Startup remains read-only and migration remains operator-owned.
Existing authored mission tables are required dependencies, not a second schema
or an opaque replacement store.

| Area | Implemented checkpoint |
| --- | --- |
| Generated missions | Existing roll/reward rules, SQL identity allocation and fee freeze; exact acceptance/key/component/object persistence; five accepted ACG bundles, mission-only NPC combat, journal/reconnect, frozen exits and ordered cleanup. |
| Mission rewards | Atomic item/cash/XP/SK/IP/refill/ledger completion; token progress frozen at objective verification, including full-inventory retry; level220 XP no-op does not block other rewards. |
| Mission corpses | Exact NPC instance under corpse type; durable owner-only cash and no invented item pool; accepted 600ms spawn, 10s dead-NPC removal and absolute corpse expiry; delayed use is fenced to its initiating actor/session/world. |
| Corpse presentation | Shared unchanged accepted L7 packet and mission mesh map; exact raw visibility path because the existing typed CFU decoder cannot represent this payload. Ordinary typed spawns stay unchanged. |
| Teams | Accepted packet/chat/raid routes, owner-tick projections and exact-session lifecycle; same-actor zoning preserves membership and actual disconnection leaves it. |
| Nanos | Packaged actual catalog, existing active-nano table, atomic multi-owner state/cost/modifier writes, attach/restore/remove/expiry, accepted map/aura and selected vehicle morphs, owned flight restrictions. |
| Inventory | Atomic moves/equip/split/delete, exact persisted stack hydration, supported crystal/consumable/package use, nonempty bag retirement rejection and existing trade/shop guarantees. |
| Authored/DOJA transactions | Validated content registration, Stan/package objective transitions, full-bar DOJA reward, account/character cooldown and exact post-commit packet plans. Trusted world dialogue/trade activation is still missing. |
| XP contribution review | Future level/title/equipment criteria and active nano contributions are computed without live mutation; durable base refills equal the eventual rebased actor and do not persist bonus ownership twice. |
| Validation compatibility | Legacy source-contract tests include the extracted shared helpers. Coverage hashes all seven combat fragments. Canonical generation supports this checkout's exact live leased staging tree, rejects foreign/reparse anchors, and gives the legacy item projector short paths to identical verified inputs. |

## Validation record

- Final focused New tests and startup: PASS (308/308, none failed or skipped).
  Evidence: `build-verify/gameplay-focused-final.log`.
- DAO architecture guard/self-test: PASS; seven production SQL sites, seven
  reviewed baseline exceptions, no new violation and no direct mission runtime SQL.
- Fresh approved disposable MySQL run: PASS. Includes negative/current/incompatible
  schema, exact migration and rerun, unchanged database fingerprints, snapshot and
  trade atomicity, inventory/nano/authored transactions, exact world persistence,
  corpse/token late failure and deduplication, objective-time token freeze, completed
  NPC guards, max-level XP no-op and runtime start/stop/restart. Exact owned Docker
  container/network residue NONE; cleanup PASS; production contact NO.
- A subsequent narrow authored-state guard prevents completed daily missions from
  receiving a false new acceptance journal. It changes no schema/DAO transaction;
  its regression passes in the final 308-test focused run after that disposable run.
- Full Windows Debug build and source-level mandatory integration: PASS (all 12
  stages, including the complete 1,129-test AOtomation suite and New acceptance).
  Evidence: `build-verify/gameplay-mandatory-gate.log`.
  The final staging review subsequently removes only surplus EOF blank lines;
  accepted-input regeneration refreshes those physical source hashes. Final
  committed bytes are covered by the post-commit exact-SHA gate, not inferred
  from this earlier source-level result.
- Accepted-input canonical regeneration: PASS; `historicalRawDependency=NO`.
  The 1,565 initial actors, 559 certified, 1,006 unresolved and 1,551 bindings
  remain unchanged. Catalog, fixtures, inventory and formula artifacts remain
  byte-identical; only physical source provenance changed in active coverage.
  Final generation identity: `b580c816f081af3f8a497509d3e875160eac19740f71c42dda12d0000406b137`.
  The EOF-only refresh changes only `contentInputs` in coverage; every other
  coverage field remains identical to the preceding validated cohort.
- Exact-SHA Windows then Linux acceptance is a post-commit attestation; its final
  results belong to the acceptance artifacts for the actual committed SHA.
  Earlier results do not imply coverage of later edits.
- No live gameplay validation was performed. No client or capture was launched.
  Production databases, migrations, services and deployment were not touched.

## Remaining supported-runtime work

These are concrete remaining connections, not a requirement to implement all AO
features or resolve every unproven capture before making progress:

- Trusted New NPC identities/spawns and actual dialogue/trade invocation for the
  authored Stan/Scarlett paths; direct transaction methods alone are not gameplay.
- General accepted NPC profile/variant activation, special/parallel combat,
  accepted encounter/loot/respawn consumers. The exact mission-only adapter and
  resolver do not establish this wider bridge, and blocked placements stay blocked.
- Remaining accepted nano specialties such as Sparrow's nested removal child,
  pets/shells, team warp, Mongo and Buckethead. Unsupported effect graphs still
  reject before cost; no fabricated effects are substituted.
- Exact generated-artifact cleanup inside unverified bag/bank ownership trees;
  pending cleanup is preserved instead of deleting unproven rows.
- Review/import of occupied Legacy sidecar mission state before any real engine
  switch, plus authored daily-repeat lifecycle. Production migration is separate.

## Detailed files, authority and risks

- `docs/evidence/ZONEENGINE_NEW_MISSIONS_RECONCILIATION.md`
- `docs/evidence/ZONEENGINE_NEW_TEAMS_RECONCILIATION.md`
- `docs/evidence/ZONEENGINE_NEW_NANOS_RECONCILIATION.md`
- `docs/evidence/ZONEENGINE_NEW_INVENTORY_RECONCILIATION.md`
- `docs/evidence/ZONEENGINE_NEW_NPC_COMBAT_RECONCILIATION.md`

Those records enumerate inspected authority, implementation files, test fixtures
and explicitly retained limitations. `docs/ai/CURRENT_TASK.md` contains only this
active reconciliation; previous completed-task prose remains in evidence/Git history.
