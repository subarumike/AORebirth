# NewEngine persistence and DAO gap inventory

The deterministic [JSON inventory](NEWENGINE_DAO_GAP_INVENTORY.json) scans 216
NewEngine C# source files and records 99 method rows across 34 files:
28 direct-SQL rows in 11 files, 51 repository-consumer rows, and 20 file/
serialization rows. SQL/repository consumers occupy 29 distinct files.
These counts are method/file footprints, not 99 independent integrity bugs.

Each row records source hash, class, method/signature/line, table candidates,
operation, transaction behavior, equivalent interface types where resolved,
migration destination and whether the path needs cutover integrity review.
CUTOVER_CRITICAL=YES marks a state/SQL review surface; it does not assert a
confirmed missing transaction. Table candidates can be file-scoped constants;
dynamic/helper SQL requires review. Static candidates and interface receivers
are not a proof of atomicity. File content loaders must not be forced into SQL.

## Transaction owners to preserve

| Aggregate | Existing owner and inspected boundary | Destination |
| --- | --- | --- |
| Character/base stats | MySqlCharacterRepository.SaveSnapshot owns transaction, commits snapshot+stats and propagates failure | Shared character DAO implementation behind existing contract |
| Inventory, stack, consumed item, uploaded nano, resulting stats | MySqlInventoryMutationPersistence.Persist uses one REPEATABLE READ transaction and publishes only after aggregate success | Shared inventory mutation DAO; keep the entire aggregate |
| Trades/vendor exchange/credits | MySqlTradePersistence.Persist locks distinct participant characters in stable order, writes inventory/nanos/cash, commits once; ambiguous commit becomes unknown-outcome exception | Shared trade DAO coordinator, no participant commits |
| Login coalescing | MySqlCharacterCoalesceCommit.Persist shares one transaction for inventory and uploaded nanos | Existing character hydration/coalesce interface, DAO implementation |
| Authored/generated mission state and rewards | Reuse existing IMissionDao/IGeneratedMissionDao and IMissionInventoryMutationTransaction; no independent reward/inventory commits | Existing mission DAO/data-access layer |
| Supported active nano/morph | Retain aggregate active nano/stat persistence and saved baseline semantics | Character nano DAO; keep restoration metadata atomic |
| Readiness/configuration | Program/RuntimeStartup and schema checker have legitimate composition/read roles | Provider construction and explicit schema administrative boundary |

The source boundaries and existing deterministic failure tests are evidence of
intended atomicity. Fresh disposable MySQL failure/rollback execution is still
required. Do not convert a table-by-table DAO move into several commits for one
gameplay operation.

## Confirmed integrity repair versus migration debt

The generic ItemTemplate executor had no aggregate transaction for arbitrary
durable effects. This task closes that action boundary before mutation; it does
not invent a new transaction provider or migrate DAO implementations.
Cutover-critical DAO gaps moved/fixed: 0. Runtime integrity boundaries fixed: 1.
All 99 inventoried method rows remain in place; 79 are SQL/repository rows.
There is no evidence-based count of 99 independent post-cutover DAO projects.

## Bounded migration order

1. Centralize character/stat and inventory read/write implementations in the
   existing data-access project behind current interfaces; preserve instance
   identity allocator and save/hydration semantics.
2. Move inventory mutation and trade coordinators together with their transaction
   participants. Test pre-commit rollback, ambiguous outcome quarantine,
   concurrent ownership, credits and consumed quantities before publication.
3. Reuse the mission DAO foundation for authored/generated state and rewards.
   Keep reward ledger, inventory/credits and mission progress in one owner.
4. Consolidate uploaded/active nano and morph persistence while preserving saved
   baseline/restoration behavior.
5. Move provider construction out of gameplay and remove redundant SQL access
   only after callers use the accepted interface. Account/session operations
   outside NewEngine are not silently treated as covered by this NewEngine census.

Do not perform a full DAO conversion for this milestone. The inventory is a
migration map; the disposable/connected integrity gate remains independent of
where an implementation file lives.
