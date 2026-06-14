# Current Task

Generated: 2026-06-14

## Current Objective

Enforce DAO transaction safety after the MySqlConnector migration.

Scope:

- Do not change packet behavior, SQL text, database schema, checked-in SQL, config connection strings, capture data, or tools.
- Only fix transaction ownership, propagation, and commit/rollback handling for DAO/database calls.
- Preserve existing runtime behavior outside transaction consistency.

## Current Findings

- Dapper usage is concentrated in `AORebirth.Database`.
- Runtime `BeginTransaction` usage is limited to DAO methods.
- `ItemDao.Save(List<DBItem>)` opens or receives a transaction but delegates delete/add operations with the original nullable transaction instead of the active transaction.
- `CharacterDao.Delete` opens or receives a transaction but performs organization/stat/item cleanup through DAO calls that do not receive that transaction.
- Generic DAO write helpers pass the active transaction to Dapper after the login-select fix, but still commit locally owned transactions from `finally`, which can commit partial unit-of-work changes after exceptions.

## Current Implementation State

- `AORebirth.Database.Dao.Dao<T,TU>` now treats locally created transactions as owned unit-of-work transactions: commit after success, rollback on exception, dispose only when locally owned.
- `StatDao.BulkReplace` now uses the active transaction for the delete and all replacement inserts, with rollback on local transaction failure.
- `ItemDao.Save(List<DBItem>)` now passes the active connection/transaction to the container delete and every item insert.
- `CharacterDao.Delete` now keeps organization cleanup, stat cleanup, inventory cleanup, character delete, and stat delete under the active transaction.
- `CharacterDao.SetPlayfield` now propagates its optional connection/transaction into the underlying save call.

## Validation

- Transaction/Dapper scans completed for `BeginTransaction`, `IDbTransaction`, `Query`, `Execute`, `QueryAsync`, and `ExecuteAsync`.
- Debug build succeeded with `0` errors.
- ChatEngine, LoginEngine, and ZoneEngine were restarted from `AORebirth\Built\Debug`.
- Runtime listeners came up on `6996`, `7012`, `7500`, and `7501`.
- Live client login succeeded through LoginEngine and ZoneEngine for character `18` / `Mikedoc`.
- Fresh Login/Zone/Chat log scan after live login found no MySqlConnector, Dapper, transaction, or DB exception lines in the current run window.
- Live vendor/shop interaction succeeded: shop windows opened and Mike confirmed buying works. Zone logs showed `GenericCmd action=Use` against `VendingMachine` targets plus trade item actions, with no current-window DB/transaction errors.
- Timed logout succeeded: Zone log showed `CharacterAction action=Logout(120)` at `18:45:01` and disconnect at `18:45:31`, with no current-window DB/transaction errors.

## Next Step

Transaction sweep complete. Continue normal runtime work.
