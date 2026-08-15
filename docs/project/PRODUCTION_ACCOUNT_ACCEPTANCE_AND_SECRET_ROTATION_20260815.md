# Production Account Acceptance and Secret Rotation — 2026-08-15

## Scope

This evidence closes the remaining pre-MyBB production account-system gates:

- prove a public website-created account through the real LoginEngine wire
  protocol path;
- rotate the DB credentials exposed during production diagnostics;
- keep MyBB/forum provisioning out of scope.

No MyBB installation, forum SSO, forum provisioning, or production schema change
was performed in this phase.

## Source fixes promoted

Credential rotation exposed a latent Linux preflight bug: the production
database correctly contained the four Account Broker identity tables beside the
34 governed game tables, but LoginEngine and ZoneEngine still required the
visible table count to equal exactly 34.

The Linux preflight policy now requires all 34 governed game tables, allows only
the four governed account extension tables, and still rejects any other
unexpected table.

Affected source:

- `LinuxBuild/Compatibility/LoginEngine/LinuxProgram.cs`
- `AORebirth/Server/ZoneEngine/Program.cs`

## Production deployment

The validated Linux LoginEngine and ZoneEngine artifacts were deployed to:

- `/opt/ao-rebirth/loginengine/releases/account-gates-20260815-001`
- `/opt/ao-rebirth/zoneengine/releases/account-gates-20260815-001`

Current symlinks:

- `/opt/ao-rebirth/loginengine/current`
- `/opt/ao-rebirth/zoneengine/current`

Post-deploy production health:

- `ao-rebirth-chatengine.service`: active
- `ao-rebirth-accountbroker.service`: active
- `ao-rebirth-loginengine.service`: active
- `ao-rebirth-zoneengine.service`: active
- LoginEngine listener: `0.0.0.0:7500`
- ZoneEngine listener: `0.0.0.0:7501`
- ZoneEngine database gate: `requiredTables=34`, `visibleTables=38`,
  `onlineCharacters=0`

## Real LoginEngine protocol acceptance

`Tools/ProductionLoginAcceptance` was added as a credential-free source tool for
production acceptance. It performs the real LoginEngine TCP system-message flow:

1. send `UserLogin`;
2. receive `ServerSalt`;
3. build the encrypted AO credential key with the existing AO login encryption
   contract;
4. send `UserCredentials`;
5. verify the resulting LoginEngine message.

Production result against `2.24.96.30:7500`:

- real protocol exercised: yes
- correct password: PASS
- correct-password terminal stage: `CHARACTER_LIST`
- incorrect password: PASS
- incorrect-password terminal stage: `LOGIN_ERROR`

This proves the website-created production account reaches the same
LoginEngine-controlled account/character-selection path as the AO client login
protocol. The official GUI client was not launched by this agent.

## Credential rotation

Rotated production DB credentials:

- MySQL root password used by the stage6 container
- `aorebirth_stage6` password used by ChatEngine, LoginEngine, and ZoneEngine

Updated secret-bearing production files:

- `/etc/ao-rebirth/chatengine/stage6/mysql.env`
- `/etc/ao-rebirth/chatengine/chatengine.env`
- `/etc/ao-rebirth/chatengine/stage6/chatengine.env`
- `/etc/ao-rebirth/loginengine/loginengine.env`
- `/etc/ao-rebirth/zoneengine/zoneengine.env`

The Account Broker remains on its dedicated least-privilege DB user and did not
require the stage6 runtime credential.

Root-only backup created before rotation:

- `/opt/ao-rebirth/database/backups/credential-rotation-20260815T070101Z`
- `/opt/ao-rebirth/database/backups/credential-rotation-20260815T070101Z/SHA256SUMS`

Old credential rejection proof from the root-only backup values:

- old root credential rejected: PASS
- old `aorebirth_stage6` credential rejected: PASS

## DB grants after rotation

Account Broker DB user:

- global grants: `USAGE` only
- table grants: `SELECT`, `INSERT`, `UPDATE` only on:
  - `account_external_mappings`
  - `account_game_mappings`
  - `account_identities`
  - `account_provisioning_jobs`
  - `login`

Stage6 runtime DB user:

- global grants: `USAGE` only
- database grants: `SELECT`, `INSERT`, `UPDATE`, `DELETE` on
  `aorebirth_chatengine_stage6.*`

## Website and mapping checks after rotation

Production route and broker health:

- broker health `http://172.18.0.1:7510/health`: `200`
- `https://ao-rebirth.com/register`: `200`
- `https://ao-rebirth.com/login`: `200`
- unauthenticated `https://ao-rebirth.com/account`: `302`
- legacy `https://ao-rebirth.com/register.php`: `403`
- legacy `https://ao-rebirth.com/process-login.php`: `403`

Database proof for the controlled public account:

- `login` rows for the account: `1`
- linked `account_game_mappings` rows: `1`
- visible production base tables: `38`

## Validation performed

Windows/local gates:

- AccountBrokerValidation Debug: PASS 28/28
- AccountBrokerValidation Release: PASS 28/28
- UnifiedAccountFlowValidation Debug: PASS 34/34
- UnifiedAccountFlowValidation Release: PASS 34/34
- LoginAuthenticationValidation Debug: PASS 14/14
- LoginAuthenticationValidation Release: PASS 14/14
- `Tools/run_database_preflight_tests.cmd`: PASS
- `tools/run_aotomation_messaging_tests.cmd`: PASS 1013/1013
- LoginEngine Linux publish: PASS
- ZoneEngine Linux publish: PASS, offline smoke PASS
- ProductionLoginAcceptance Release build/publish: PASS
- AORebirth `git diff --check`: PASS

Production gates:

- DB credential rotation: PASS
- old DB credentials rejected: PASS
- LoginEngine active/listening: PASS
- ZoneEngine active/listening: PASS
- ChatEngine active: PASS
- AccountBroker active/health: PASS
- public account route checks: PASS
- real LoginEngine protocol correct-password acceptance: PASS
- real LoginEngine protocol wrong-password rejection: PASS
- account mapping retained after rotation: PASS

## Remaining risks

The controlled production acceptance credential was accidentally printed in the
local task transcript while checking the temporary VPS file format. Treat that
test account as compromised. The plaintext VPS credential file was removed after
protocol acceptance; disabling or rotating the controlled test account itself
requires explicit approval because that is a destructive account operation.

MyBB/forum provisioning remains intentionally unimplemented.
