# Delmus d2d98446 runtime reconciliation

This is a partial gameplay-closure checkpoint, not a master-ready or production
release. Missing supported adapters remain real regressions; an incomplete
forensic census is not used as a substitute blocker.

## Provenance and per-file decisions

| Field | Value |
| --- | --- |
| Validated base | `e31142e850063726adfda0d33b5b68b0ed6b309f` |
| Delmus input | `d2d9844673a766c02aa768623db9f0ab5b6ea01c` |
| Delmus parent / common ancestor / starting origin master | `6e90dda030774726aa2060acb9edb756ea1f635c` |
| Branch | `codex/zoneengine-final-delmus-reconciliation` |
| Worktree | `C:\Users\Mike\Documents\AORebirth-zoneengine-final-delmus-reconciliation` |
| Introduced file diffs read before implementation | 77 of 77 |
| Minimal rewritten dispositions | 12 |
| Validated-base dispositions | 47 |
| Rejected dispositions | 18 |
| Blind merge/cherry-pick | No |

[Per-file JSON](DELMUS_D2D98446_FILE_RECONCILIATION.json) records every introduced
file, rename source, overlap, runtime/test/data/generated classification, risk,
resolution and implementation status. Counts are **file dispositions**, not
independent features or a claim that every hunk in a rewritten file was accepted.
Retaining a validated file can also reject weaker parallel Delmus behavior.

Delmus is a sibling of e311, not its successor. Its tests and newer date do not
inherit e311's Windows/Linux acceptance. No merge conflict was resolved by taking
an entire newer file over the validated owner/transaction design.

## Reconciled changes

- **Reconnect visibility:** reset a recipient snapshot only for a replacement
  transport; preserve same-session de-duplication and failed-send retry. Closed,
  unbound and stale actor references cannot consume or clear current visibility.
- **Fresh item identities:** `Item.AssignInstanceId` stamps the allocated ID and
  typed world identity together at give, loot and shop allocation sites. This
  fixes a real shop purchase path that previously left typed identity instance
  zero. Bag conversion, untyped non-world items, existing stacks, persistence
  commit ordering and injected ID allocators remain intact. Existing persisted
  or prebound identities cannot be reassigned. No builder/DAO ownership rewrite.
- **Hash categories:** accept Delmus's property-key form when an explicit Hash
  is absent. Preserve existing explicit aliases, including SMGN-to-MSTA, and do
  not silently repair malformed Hash values. Unknown leaves remain unresolved;
  no item/template fallback or category-data migration is introduced.
- **Locality:** living vendor and standalone-shop cells retain their Cold tick;
  existing combat FightingTarget retains Hot ticking. No speculative IsAiBusy
  state or generic brain is imported. Snapshot position reconciliation skips
  actors no longer tracked; the scheduler receives the current tracked set.
- **N06 Buckethead:** exact source-backed summon described below. This is work
  added to close an existing gap, not a feature found in the Delmus revision.

## Rejected or retained-base semantics

The parallel item-backed nano runtime is not imported: New keeps the actual
`nanos.dat` reader, aggregate cost/effect commit, active-set validation, exact
player/session owner, rollback and indeterminate-commit quarantine. Delmus's
unlimited-NCU interpretation for nonpositive capacity, arbitrary cost cap,
unchecked restore/strain timing and alternate whole-active-list persistence do
not replace the validated implementation. Success packets/cost cannot stand in
for unimplemented instant effects.

Generic NPC brains/stat bands, blanket attackability, default proximity/leash
values and profile-free combat are not accepted production mappings. The input
contains an NPC-callable nano path but no proven automatic NPC cast rotation;
this report does not mislabel that as observed periodic NPC casting. No
unproven NPC nano AI is enabled. The temporary player respawn location and
despawn-after-persistence-failure behavior are not carried forward.

The template rename and ignore-file change are rejected; packaging keeps its
accepted paths. Existing vendor price modifiers remain authoritative; no new
ComputerLiteracy cap is imposed. TickStallWatch's process-lifetime observer and
unbounded thread slots are not imported. Its instrumentation is not a dependency
for the accepted scheduling fixes. Grove/brain and parallel persistence wiring
are not added merely to satisfy tests for those rejected implementations.

## Buckethead N06 contract and tests

Authority: compiled Legacy `Core/Controllers/PlayerController.cs` dedicated
Buckethead branch; `Core/SummonedBucketheadTechnodealerRuntime.cs`;
`Core/Playfields/CapturedBucketheadTechnodealerContentProvider.cs`;
`Core/MessageHandlers/VendingMachineFullUpdateMessageHandler.cs` Buckethead filler;
the packaged nano300439 and actual item99566 catalog. The content provider is
linked unchanged as pure data. Its provenance names existing completed captures;
neither runtime nor tests open historical capture directories.

The admitted graph is exactly SpawnMonster2(BKTH,220,600), wearer target, one
nonperiodic OnUse. Catalog attack/recharge are each100cs, cost1. It creates no
NCU row even though the catalog has NCU49: the supported result is a world
summon, not a buff. Persisted crystal-ID300440 compatibility resolves only to
300439 without granting an upload or rewriting stored rows.

The Legacy dedicated cast path does not evaluate the two action criteria. New
preserves that narrow compatibility only for nano300439's exact unchanged
two-criterion declaration; it does **not** define what136 means for other nanos
or invent a PlayfieldType value for characters lacking that nonpersistent stat.
Changed criteria/graphs fail closed. The first fixture had supplied a zero
PlayfieldType and masked an erroneous added restriction; the final real-catalog
cast fixture deliberately leaves it unset and tests both unset/stale values.

The NPC uses exact source appearance/stats/placement: MonsterData43352,
level220, health101861, scale50, captured flags271061505, speed749, zero captured
texture slots, and the source one-meter rotated forward offset. The companion
VMFU retains the source constants and actual vendor99566 identity/template.
All46 source stock rows must resolve completely before the cast begins;
original template buy/sell modifiers are retained. No vendor99634/hash/placeholder
fallback is used. Normal locality publishes SCFU before VMFU.

The existing nano transaction is the only mana authority. Allocation, binding,
visibility and replacement occur once after a known successful commit. Ordinary
rollback and unknown outcome do not publish a new summon or replace the old one;
unknown outcome retains existing caster quarantine. An invalidated postcommit
owner cancels publication without replaying/refunding a durable cast.

Source-playfield ticking owns the600-second deadline, including while the caster
is no longer ticked there. Disconnect, death, zoning, lost/replaced registry
ownership, summon death and shutdown remove the exact NPC/shop binding and
close trades. Cleanup uses exact references, not reusable identity alone.
Invalid/nonfinite float-wire geometry fails before cost. No independent timer,
NPC spell scheduler, database schema, restart-persistent pet or generic mob is
created.

`BucketheadNanoTests` adds16 real-catalog executions covering timing/no-NCU,
actual pricing and every stock row, SCFU/VMFU order, rollback/unknown commit,
replacement/deadline, six lifecycle exits, one-shot/cancellation fences,
missing-stock/changed-graph failure, invalid geometry/restrictions, persisted
crystal alias and pending disconnect. Existing mission/dialogue/vendor/ownership
tests are retained. `CellHeatSchedulerTests` contains three Delmus cases and two
additional combat/standalone-vendor cases. Reconnect3, identity5 and category2
additional executions bring the candidate from420 to451 New tests.

## Exact remaining supported blockers

These are missing implementations, not claims that authoritative source data is
absent. This checkpoint does not satisfy the request to close all runtime gaps.

| Playfield / feature | Remaining contract | Runtime consequence |
| --- | --- | --- |
| Subway PF127,322 accepted source bindings | `OrdinaryEnemyCatalog`/`OrdinaryEnemyRuntimeService`/`WorldPopulationController` profile and generation consumer: exact placement/variant/appearance, accepted aggro/chase/combat, death/corpse/loot/reward, respawn and cleanup | Zero bindings are certified through a New ordinary-population adapter; generic hash spawns are not credited as equivalent |
| Temple PF1931,167 accepted source bindings | `CapturedTempleOfThreeWindsContentProvider` plus ordinary/named-encounter consumers and exact combat/loadout/loot/respawn associations | Zero bindings are certified through that New accepted population consumer; bosses are not relabeled ordinary mobs |
|27 registered dialogue routes | Exact route/source/entry/side-effect ledger remains in `ZONEENGINE_NEW_DIALOGUE_GAP_CLOSURE.md`; required world bindings and specialized quest adapters are not connected by this patch | Text existence does not produce usable quest/vendor/transport behavior |
| N02 wider morph/equipment vehicles | Catalog graph admission plus owned OnWear/unequip shape/flight contributions | Existing four morph nanos are not proof of the wider equipment/graph domain |
| N05 Mongo100198→100194 | Exact Hit+12 and20m AreaCastNano/TauntNpc bridge to accepted combat NPCs, excluding social/vendors | Supported Mongo effect graph remains rejected; no fabricated287046 HoT |
| N07 pets/shells | Same-transaction shell item+mana; existing pet-family/owner/strain commands and in-process zoning restore | No supported pet world/command authority; a post-cost independent item grant is not an acceptable substitute |
| N08 wider executable nano graphs | Owned percentage/flag contributions and exact nested/area/team requirements and restoration | Unsupported graph remains rejected instead of partially executed |
| N10 player-to-NPC signed Hit/drain | Accepted combat-recipient bridge using existing Character.OnDeath/corpse/reward authority | Player damage/drain against supported NPCs remains disconnected; NPC-source Hit stays quarantined |
| N03 conditional persisted morph | Authoritative pre-morph appearance or an approved safe compatibility decision for affected persisted rows | Nonzero ambiguous MonsterData bases remain preserved/rejected, not reset to zero; no production row census was performed |

The unchanged27 dialogue gaps are: Rex, Marcus, Flint, Alex, Bill; Vernon, Mason,
Lorelei, Lolly, Shipping Manifest Terminal, Vaughn; Karrec, Annoying Dude, Maddy;
Veronica, Prophet, Hypnagogic, Dreaming Silvertail; Son Len, El-Mada; Rosenblatt,
Rodriguez, Joshua, Donna, Fala, Lux-Wei; Zyvania. Their existing per-route ledger
remains authoritative for node, mutation and dependency details. No placeholder
dialogue or guessed NPC appearance was activated to inflate the count.

The broader inventory's overlapping static, combat and raw-placement views are
not summed into a fabricated total. Existing22 static NPC adapters and the
Buckethead dynamic owner factory are distinct measures. Required ordinary
disconnected bindings are322+167=489 in the named supported domains, not an
exhaustive global actor total. Master remains blocked by those known missing
consumers and the concrete dialogue/nano regressions, **not** by UNKNOWN !=0.

## Acceptance and release boundary

Pre-commit Windows validation on this source:451/451 New Engine tests,
1,129/1,129 AOtomation tests and12/12 mandatory stages PASS. Legacy/New/Release
builds, DAO/generated-combat/source/package guards and pinned playfield validation
PASS. Actual disposable migration/copy-failure rollback, inventory/nano/mission
atomicity, runtime start/stop/restart and unchanged runtime database fingerprint
PASS; disposable container/network residue NONE, production contact NO. The first
exact-SHA wrapper attempt failed its clean-tree prerequisite before
validation; the committed SHA must still pass the clean-source wrapper and Linux.

Windows builds/tests/mandatory gates and disposable schema/runtime checks must
pass before the candidate source commit. Linux then validates that pushed exact
SHA; a source fix requires returning to Windows. Exact execution counts, SHA,
placement-manifest hash, exit receipts and branch/master status are recorded in
the ignored worktree artifact `build-verify/delmus-final-results.md`, which can
name the containing commit without creating a self-referential source commit.
Do not interpret this source report or file-disposition inventory as a Linux
PASS before that execution receipt exists.

Mandatory conclusions independent of infrastructure test success:

```text
DELMUS_FILES_ANALYZED=77
CHANGES_BLINDLY_MERGED=NO
SUBWAY_ACCEPTED_RUNTIME_RECONCILED=NO
TOTW_ACCEPTED_RUNTIME_RECONCILED=NO
REQUIRED_ACCEPTED_NPCS_CONNECTED=NO
BROADER_FORENSIC_CENSUS_REQUIRED_FOR_MASTER=NO
DIALOGUE_ROUTES_STARTING_GAP=27
DIALOGUE_ROUTES_REPAIRED=0
DIALOGUE_ROUTES_REMAINING=27
DEFINITE_NANO_GAPS_STARTING=6
DEFINITE_NANO_GAPS_REPAIRED=1
DEFINITE_NANO_GAPS_REMAINING=5
PERSISTED_MORPH_HANDLING_RECONCILED=NO
UNPROVEN_NPC_AI_ENABLED=NO
UNPROVEN_NPC_NANO_AI_ENABLED=NO
UNPROVEN_RUNTIME_FALLBACKS_ADDED=NO
RUNTIME_DEPENDS_ON_HISTORICAL_CAPTURES=NO
ZONEENGINE_NEW_MASTER_READY=NO
MASTER_SWITCH_PERFORMED=NO
PRODUCTION_DATABASE_MODIFIED=NO
PRODUCTION_MIGRATIONS_APPLIED=NO
PRODUCTION_SERVICES_CHANGED=NO
PRODUCTION_DEPLOYMENT_PERFORMED=NO
LIVE_CLIENT_TEST_PERFORMED=NO
PUBLIC_NETWORK_EXPOSURE_CHANGED=NO
```
