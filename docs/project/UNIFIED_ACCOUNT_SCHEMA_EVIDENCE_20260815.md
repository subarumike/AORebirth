# AORebirth Unified Account Schema Evidence

Status: repository design and local Windows MySQL validation complete. No
production database schema was changed, no website route was changed, no MyBB
installation was performed, and no Linux deployment was performed.

## Live schema evidence

Read-only audit target: production LoginEngine database
`aorebirth_chatengine_stage6` through the existing MySQL 8.4.10 container
client. Secrets, password hashes, emails, and usernames were not printed.

### `login`

| Field | Live value |
| --- | --- |
| Engine | InnoDB |
| Table collation | `latin1_swedish_ci` |
| Primary key | `Id` |
| `Id` type | `int`, not null, auto increment |
| Username field | `Username varchar(32) NOT NULL`, `latin1_swedish_ci` |
| Password field | `Password varchar(100) NOT NULL`, `latin1_swedish_ci` |
| Email field | `Email varchar(64) NOT NULL`, `latin1_swedish_ci` |
| Status/account fields | `Flags int NOT NULL DEFAULT 0`, `AccountFlags int NOT NULL DEFAULT 0`, `GM int NOT NULL DEFAULT 0` |
| Timestamps | `CreationDate datetime NOT NULL` |
| Unique constraints | unique key `Username` on `Username` |
| Foreign keys | none |

Live aggregate checks:

- `login` rows: 8.
- Distinct usernames: 8.
- Distinct lowercase usernames: 8.
- Usernames matching ASCII alphanumeric 6-32: 5 of 8.
- Usernames with leading/trailing whitespace difference: 0.
- Username lengths observed: 4 through 11.
- `Flags` values observed: only `0`, across 8 rows.

### `characters`

| Field | Live value |
| --- | --- |
| Engine | InnoDB |
| Table collation | `latin1_swedish_ci` |
| Primary key | `Id` |
| `Id` type | `int`, not null, auto increment |
| Owner field | `Username varchar(32) NOT NULL`, `latin1_swedish_ci` |
| Character name field | `Name varchar(32) NOT NULL`, `latin1_swedish_ci` |
| Online field | `Online smallint DEFAULT 0`, nullable |
| Indexes | primary key only |
| Foreign keys | none |

Live aggregate checks:

- `characters` rows: 7.
- Distinct owner usernames: 3.
- Distinct lowercase owner usernames: 3.
- Owner usernames with leading/trailing whitespace difference: 0.
- Owner username lengths observed: 5 through 11.
- Character rows with no exact matching `login.Username`: 0.
- Character rows with only case-mismatched owner/account join: 0.

## Character ownership contract

Character ownership is username-string based today.

- `characters.Username` stores the account owner username.
- `characters` does not reference `login.Id`.
- `CharacterDao.GetAllForUser(username)` resolves account to characters with
  `Username = @username`.
- `CharacterDao.IsCharacterOnAccount(userName, characterId)` checks
  `characters.username = @userName AND id = @characterId`.
- `LoginDataDao.GetByCharacterId(charId)` loads a character, then loads the
  login row by that character's `Username`.
- `LoginEncryption.IsCharacterOnAccount()` delegates to `CharacterDao`.
- Existing character ownership must not be rewritten in this schema phase.

The identity mapping therefore must use stable `login.Id` for future identity
relationships while preserving the existing `characters.Username` owner model
until a separate, explicit character-ownership migration is designed and
approved.

## Flags audit

| Value/Bit | Meaning | Evidence | Safe for new normal account? |
| --- | --- | --- | --- |
| `0` | Login-allowed normal account value | Live database has only `Flags=0`; `CheckLogin` permits only `Flags == 0`; `adduser` creates accounts with `Flags = 0`. | Yes, when the account is intentionally playable. |
| Nonzero values | Login blocked by current source; specific business meanings are not proven | `CheckLogin` compares to constant `LoginAllowedFlag = 0`; no live nonzero rows were observed. | No. Do not use a nonzero pending value without a separate approval. |

`GM` is a separate privilege field. `Program.adduser` treats any GM level above
zero as a GM account, and validation permits 0-511. `AccountFlags` is not used
as the account-login status gate in the inspected LoginEngine path.

## Password contract

The existing game-account password representation is created by:

```text
LoginEncryption.GeneratePasswordHash(clearPassword)
    -> PasswordHash.CreateHash(clearPassword)
```

The stored format remains:

```text
iterations:base64(30-byte random salt):base64(30-byte PBKDF2-HMAC-SHA1 output)
```

Properties:

- salt size: 30 bytes from `RNGCryptoServiceProvider`;
- hash size: 30 bytes;
- PBKDF2 implementation: `Rfc2898DeriveBytes`;
- minimum iterations: 1111;
- added random iteration byte: 0-255, producing 1111-1366 iterations;
- password string normalization: no explicit trim/case normalization in the hash
  function;
- expected storage: `login.Password varchar(100)`.

Resolved follow-up finding: password authentication was restored on 2026-08-15.
`CheckLogin.IsLoginCorrect()` now loads `login.Password` through `LoginPasswd`
and calls `LoginEncryption.IsValidLogin()`, which decrypts the supplied login
key, validates the embedded password with `PasswordHash.ValidatePassword()`, and
requires the embedded server salt to match the active challenge. Future
registration must generate this same `login.Password` format.

## Username and collation semantics

Live MySQL collation for both `login.Username` and `characters.Username` is
`latin1_swedish_ci`, which is case-insensitive for normal equality and unique
key comparison.

Current source semantics:

- Login challenge username comparison is ordinal case-insensitive.
- `LoginDataDao.GetByUsername()` queries `login.Username = @username`.
- `characters.Username` ownership queries use direct equality.
- `Program.adduser` validates duplicate username through `LoginDataDao.Exists()`
  and the database unique key, but it does not enforce alphanumeric or 6-32
  username rules in the non-interactive parameter validator.
- Interactive `adduser` enforces a minimum username length of 6 before it calls
  parameter validation.

Approved Account Broker rule for new public registrations:

- accept only ASCII alphanumeric usernames;
- require length 6-32;
- preserve the submitted canonical case for display;
- store `NormalizedUsername = lowercase ASCII canonical username`;
- enforce a binary unique index on `NormalizedUsername`;
- reject whitespace, Unicode, lookalikes, and case variants before provisioning.

This prevents Account Broker identities from diverging from the game database's
case-insensitive username behavior.

Approved legacy-link exception:

- existing game accounts may be represented with ASCII alphanumeric usernames of
  length 1-32;
- legacy linking must preserve `login.Username`, `login.Password`, `login.Id`,
  and all `characters.Username` ownership values;
- a short legacy username is valid for linking but not valid for new public
  registration.

## Existing account strategy

Existing game accounts can be linked later without changing passwords,
usernames, `login.Id`, or character ownership.

Deterministic backfill/linking key:

1. use exact existing `login.Id` as `GameAccountId`;
2. derive `NormalizedUsername` from lowercase ASCII `login.Username`;
3. reject or manually review existing accounts whose username does not satisfy
   the ASCII alphanumeric 1-32 legacy-link policy;
4. do not alter `characters.Username`;
5. do not rewrite `login.Password`;
6. create one `account_identities` row and one `account_game_mappings` row per
   approved existing `login.Id`.

Conflict rules:

- if two existing `login.Username` values normalize to the same broker username,
  do not auto-link either account;
- if an existing username is not ASCII alphanumeric or exceeds 32 characters,
  require manual review;
- duplicate or shared email values must not block identity creation because
  existing `login.Email` is not unique.

## Approved identity schema

Repository definition:
`AORebirth/Libraries/Source/AORebirth.Database/SqlTables/aorebirth_identity.sql`.

Selected placement: a dedicated AORebirth identity database on the same private
MySQL server as the game database. The identity tables are not placed inside the
MyBB database and MyBB is not the identity authority.

Tables:

### `account_identities`

- `IdentityId bigint unsigned` primary key.
- `IdentityPublicId char(36) ascii_bin`, unique opaque public identifier.
- `CanonicalUsername varchar(32) ascii_bin`.
- `NormalizedUsername varchar(32) ascii_bin`, unique.
- `CanonicalEmail varchar(254) nullable`.
- `NormalizedEmail varchar(254) nullable, unique`.
- `EmailVerifiedAt datetime(6) nullable`.
- `IdentityStatus enum('Reserved','Active','Suspended','Disabled')`.
- `CreatedAt`, `UpdatedAt`.

### `account_game_mappings`

- `IdentityId bigint unsigned` primary key and foreign key to
  `account_identities.IdentityId`.
- `GameAccountId int`, unique stable mapping to `login.Id`.
- `MappingState enum('Pending','Linked','Disabled')`.
- `CreatedAt`, `LinkedAt`.

No physical foreign key is declared to `login.Id` because the selected placement
is a dedicated identity database whose migration must remain reviewable without
hard-coding the production game database name. The Account Broker must verify
`login.Id` existence and create/link rows transactionally on the same private
MySQL server.

### `account_external_mappings`

- `ExternalMappingId bigint unsigned` primary key.
- `IdentityId bigint unsigned` foreign key to `account_identities.IdentityId`.
- `Provider varchar(32) ascii_bin`.
- `ExternalAccountId varchar(64) ascii_bin`.
- `MappingState enum('Pending','Linked','Disabled')`.
- `CreatedAt`, `LinkedAt`.
- unique provider/account constraint.
- unique identity/provider constraint.

For MyBB, the intended values are:

```text
Provider = mybb
ExternalAccountId = uid
```

No MyBB password is stored here.

### `account_provisioning_jobs`

- `ProvisioningJobId bigint unsigned` primary key.
- `IdempotencyKeyHash binary(32)`, unique.
- `IdentityId bigint unsigned` nullable foreign key.
- requested normalized username/email.
- optional requested game account ID.
- optional requested external provider/account ID.
- `ProvisioningState` enum.
- `ProvisioningStep tinyint unsigned`.
- attempt and non-secret failure fields.
- `CreatedAt`, `UpdatedAt`.

## Provisioning model

Valid lifecycle:

```text
IdentityReserved (10)
  -> GameAccountPending (20)
  -> GameAccountLinked (30)
  -> Active (60)
```

Future MyBB lifecycle:

```text
IdentityReserved (10)
  -> GameAccountPending (20)
  -> GameAccountLinked (30)
  -> ExternalAccountPending (40)
  -> ExternalAccountLinked (50)
  -> Active (60)
```

Manual failure lifecycle:

```text
any non-active state -> ManualReview (90)
```

Recovery invariants:

- retries use the same `IdempotencyKeyHash`;
- state advancement is monotonic by `ProvisioningStep`;
- duplicate identities are blocked by `NormalizedUsername`;
- duplicate game mappings are blocked by unique `GameAccountId`;
- duplicate MyBB mappings are blocked by unique `(Provider, ExternalAccountId)`;
- no game account row should be created as playable until registration is ready
  to complete and a generated password hash has been stored through the existing
  AORebirth password implementation;
- nonzero `login.Flags` pending values are not approved by this evidence phase.

## Security boundary

Future credential separation:

- Account Broker: trusted service account with the narrow rights required to
  create identities, create/link game accounts, create/link external mappings,
  and invoke approved internal authentication operations.
- LoginEngine: existing game-runtime database account; no dependency on MyBB.
- MyBB/PHP: no unrestricted game database credentials, no AO password hashes, no
  direct writes to `login`, and no authority to create playable game accounts.
- Migration tooling: operator-controlled administrative credentials used only
  for reviewed migrations and backups.

The public website must call the Account Broker. The legacy PHP account pages
must not become the Account Broker.

## Website boundary verification

Live HTTPS status on 2026-08-15:

- `/register.php`: 403.
- `/process-login.php`: 403.
- `/member-index.php`: 403.
- `/member-profile.php`: 403.
- `/admin/`: 403.

No website route was enabled.

## Linux readiness

Eventual Linux production work, after Windows implementation and validation, will
need:

- an approved backup and restore plan;
- explicit identity-database creation/migration approval;
- a restricted Account Broker database credential;
- broker service packaging/configuration/secrets;
- website routing to the broker;
- future MyBB installation and bridge configuration;
- post-deployment read-only verification that game auth and character ownership
  still resolve unchanged.

No Linux change is part of this stage.

## Validation status

Validation artifact:
`Tools/AccountIdentitySchema/validate_account_identity_schema.sql`.

The validation script covers:

- case-variant username rejection through normalized username uniqueness;
- legacy short username representation;
- one identity to one game account;
- one game account to one identity;
- duplicate game mapping rejection;
- unique provider/external ID mapping for future MyBB;
- linking an existing game account by stable `login.Id` without touching
  password, username, or characters;
- valid forward provisioning advancement;
- invalid provisioning state/step rejection.

Execution result on the local Windows development MySQL target:

```text
AORebirth account identity schema validation PASS | IdentityRows 3 | GameMappingRows 1 | ExternalMappingRows 1 | ProvisioningState GameAccountLinked
```

Execution notes:

- Docker Desktop was unavailable, so a fresh throwaway MySQL container could not
  be started.
- The configured local development database user could not create a new schema.
- The validation therefore used the existing local development database
  `cellao_codex_clean` as the disposable target and recreated only the
  `account_*` validation tables owned by this proposal.
- No production database/schema was changed.
