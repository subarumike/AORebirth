# AORebirth Account Broker Foundation Evidence

Status: internal repository foundation complete and locally validated on
2026-08-15. No production database schema was changed, no public registration
route was enabled, no MyBB installation was performed, no website configuration
was changed, and no Linux deployment was performed.

## Scope

This stage adds the first internal Account Broker foundation for unified
accounts. It is a trusted-side .NET Framework library, not a public web route.
Website, forum, and MyBB integration remain future stages.

Follow-up Windows-local service/API flow evidence is recorded in
`docs/project/UNIFIED_ACCOUNT_FLOW_EVIDENCE_20260815.md`.

Implemented repository surfaces:

- `AORebirth/Libraries/Source/AORebirth.AccountBroker/`
- `Tools/AccountBrokerValidation/`
- `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/aorebirth_identity.sql`
- `Tools/AccountIdentitySchema/validate_account_identity_schema.sql`

## Broker contract

The Account Broker uses an injected `Func<IDbConnection>` data-access boundary.
It does not give PHP, MyBB, nginx, or public website code direct access to the
AO game database.

Implemented operations:

- create a new identity-first game account with an idempotency key;
- generate `login.Password` with `LoginEncryption.GeneratePasswordHash()`;
- create normal playable accounts with `Flags=0`, `AccountFlags=0`, `GM=0`,
  `AllowedCharacters=6`, and `Expansions=127`;
- retry the same idempotency key and converge on the same identity/game-account
  IDs;
- link an existing game account by stable `login.Id`;
- preserve existing `login.Username`, `login.Password`, and character ownership
  during legacy linking;
- reserve future external mappings such as `Provider='mybb'` and
  `ExternalAccountId='<uid>'`;
- reject duplicate usernames, duplicate game mappings, conflicting identity
  mappings, and conflicting external mappings.

The transaction updates provisioning rows by `IdempotencyKeyHash`, so retry
convergence is scoped to the exact request being resumed.

## Username and email policy

New public registrations:

- ASCII alphanumeric only;
- length 6-32;
- normalized by invariant ASCII lowercase;
- duplicate case variants rejected.

Legacy existing-account links:

- ASCII alphanumeric only;
- length 1-32;
- normalized by invariant ASCII lowercase;
- designed to represent observed short live usernames without rewriting
  accounts or characters.

Email is stored as canonical plus normalized lowercase fields in the identity
schema. Existing `login.Email` is not unique, so legacy linking must not rely on
email uniqueness.

## Local validation

Schema validation target:

- local Windows development MySQL database: `cellao_codex_clean`;
- MySQL client/server proof: local client successfully executed the validation
  script;
- Docker Desktop was unavailable and the configured local user lacked
  `CREATE DATABASE`, so validation reused the controlled development database
  and recreated only the proposed `account_*` tables.

Schema validation result:

```text
AORebirth account identity schema validation PASS | IdentityRows 3 | GameMappingRows 1 | ExternalMappingRows 1 | ProvisioningState GameAccountLinked
```

Account Broker validation result:

```text
PASS AccountBrokerValidation 28/28
```

Covered broker cases:

- normal account creation;
- password hash accepted by current AO login-key validation;
- wrong password rejected;
- idempotent retry returns the same IDs and no duplicate `login` row;
- case-equivalent duplicate username rejected;
- invalid username rejected;
- short legacy account linked without changing password, username, or
  character ownership;
- game-account mapping conflict rejected;
- MyBB-style external mapping accepted and made idempotent;
- conflicting external mapping rejected;
- simulated interrupted provisioning after identity/job/game/mapping converges
  without duplicate game accounts.

Regression validation:

- `LoginAuthenticationValidation` Debug: PASS 14/14.
- `LoginAuthenticationValidation` Release: PASS 14/14.
- `Tools\run_database_preflight_tests.cmd`: PASS.
- `Tools\run_aotomation_messaging_tests.cmd`: PASS 1013/1013.

## Production state

Unchanged:

- production database schema;
- production database rows;
- website account routes;
- MyBB/forum installation;
- Linux services;
- public listener policy.

Remaining gates before public registration:

- approve production identity migration and rollback;
- approve backup/restore plan;
- create a restricted broker database credential;
- package and host the broker service on the trusted side;
- add HTTPS public registration/login only through the broker;
- install/configure MyBB and an AORebirth Identity Bridge only after broker
  deployment is approved;
- run production read-only post-deploy verification.
