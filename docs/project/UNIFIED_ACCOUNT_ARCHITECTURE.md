# AORebirth Unified Account Architecture

Status: Phase 1 inspection and design only (2026-08-12). No production change,
database mutation, MyBB installation, or service restart was performed.

## Proven current account behavior

The AO client first sends a username. `UserLoginHandler` stores it on the
connection, generates a 32-byte per-connection server challenge, and returns
that challenge. The credentials handler then requires the credential-message
username to match the connection username case-insensitively, loads the `login`
row by username, and permits only rows whose `Flags` value is zero. It decrypts
the AO login key, requires the embedded username and server challenge to match,
and validates the embedded clear password against `login.Password`. Successful
authentication loads characters by the account username string.

The persisted game password format is:

`iterations:base64(30-byte random salt):base64(30-byte PBKDF2-HMAC-SHA1 output)`

The iteration count is randomly selected from 1,111 through 1,366. This format
must remain unchanged for AO client compatibility. The server-side AO login-key
decryption private value is hard-coded in source and must not be copied into the
forum or public web tier.

Critical build boundary: `LoginEncryption.i_Enable` is `false` under `DEBUG`
and `true` under Release. When it is false, `IsValidLogin` returns true before
decrypting or checking the password. The approved local workflow builds Debug.
The production LoginEngine binary/configuration has not been inspected, so
production password enforcement is not yet proven. Deployment work is blocked
until the running production binary is identified and this boundary is tested
without exposing a credential.

## Checked-in account schema

The checked-in InnoDB/latin1 `login` table has an auto-increment integer `Id`,
`CreationDate`, required `Email`, required `FirstName`/`LastName`, unique
`Username` (maximum 32 bytes), required `Password` (maximum 100 bytes),
`AllowedCharacters`, `Flags`, `AccountFlags`, `Expansions`, and `GM`. Email is
not unique. There is no explicit email-verification, reset-token, canonical
normalization, provisioning-state, or linkage column.

Characters do not reference `login.Id`. `characters.Username` is a required
32-byte string, and ownership checks compare that string plus character ID.
Account status is effectively `Flags == 0` for login permission. `GM` is a
separate privilege field; `AccountFlags` and expansions are not identity status.

The live read-only database preflight succeeded against the configured
`cellao_codex_clean` database, proving MySQL connectivity, database identity,
all 34 expected tables, read access, and zero online characters. The safe
preflight does not reveal the server version, column collations, indexes, or
actual `SHOW CREATE TABLE` output, so those production facts remain unverified.
The repository contains table-creation SQL files but no general versioned
migration framework.

## Existing account creation surfaces

LoginEngine exposes an operator console `adduser` command. Its interactive path
requires at least six characters for username/password, validates email, hashes
through the AO password implementation, and inserts a parameterized `login`
row. The non-interactive parameter validator does not enforce the same username
or password rules; the database unique username index is the final duplicate
guard. `setpass` replaces the AO hash using the same algorithm.

The locally imported historical WebCore also contains a PHP registration page.
It accepts alphanumeric profile/name fields, requires an eight-character
password, checks duplicate username and email in application code, creates the
same AO hash, and inserts directly into `login`. It has no CSRF token and is not
an approved public registration system. The current WebEngine allowlist blocks
`register.php`, `process-login.php`, and all other authentication/mutation PHP
routes; WebEngine remains Windows-only, plaintext, development-only software.

## Public infrastructure observation

As observed from the public network on 2026-08-12:

- `ao-rebirth.com` resolves to an IPv4 address and HTTPS returns 200.
- TLS verification succeeds and the public response identifies nginx 1.29.8.
- `/register` and `/login` return 404.
- `forum.ao-rebirth.com` is NXDOMAIN.

These observations do not prove the VPS OS, nginx configuration files, PHP
version, database server/version, firewall rules, backup system, service users,
or private topology. No SSH target or approved production access workflow is
present in this repository or the local SSH configuration.

## Selected identity architecture

Use a new AORebirth Account Broker as the sole public-account authority. Keep
the existing LoginEngine authentication path and `login.Password` format intact.
Do not let nginx/PHP/MyBB connect to the AO game database.

The broker should run on the trusted side of the game-database boundary and own
a separate, least-privilege identity store. The minimum model is:

- `identity_id`: stable opaque ID, never a username;
- unique `game_account_id` mapped to `login.Id`;
- nullable unique `mybb_uid`;
- canonical and normalized username;
- canonical email plus verification state;
- identity/status and provisioning state;
- created/updated timestamps and non-secret audit metadata;
- unique idempotency key/digest for registration attempts.

Do not copy the AO password hash into the identity or forum database. The broker
accepts a password only over HTTPS, holds it only in request memory, invokes the
existing AO hash implementation to create/update `login.Password`, and later
validates website credentials through a narrow internal authentication method.
MyBB receives a random, unavailable local password value solely to satisfy its
local row contract; users never know or use it.

Prefer placing the identity schema on the same private MySQL server as the game
schema, owned by a dedicated broker account, so InnoDB can atomically create the
identity mapping and blocked/pending game row across schemas. This requires an
explicit approved migration and proof of a safe pending value: current code
proves only that `Flags == 0` may log in and any other value is rejected; it does
not prove which nonzero values are already assigned ban semantics. If that
proof fails, use an explicit outbox/saga and create the game account last.

Provisioning is idempotent and stateful: reserve identity, provision a
non-login-capable MyBB row, create/link the game account, then activate all
mappings. Retries look up the idempotency record and converge on the same IDs.
Pending forum rows cannot authenticate because their random local credential is
unknown and the bridge issues no session unless the broker reports `Active`.
No partial failure may delete or overwrite an existing account automatically;
recovery is deterministic and operator-visible.

## MyBB integration

Use a version-controlled `AORebirth Identity Bridge` plugin and stock MyBB core.
Official MyBB 1.8 plugin documentation recommends plugins instead of core edits,
and the 1.8.40 hook catalog includes registration, login, logout, password,
email, username, session, and login-datahandler hooks. The bridge should:

- redirect normal registration, login, lost-password, password-change,
  email-change, and username-change entry points to AORebirth;
- accept a short-lived, single-use opaque authorization code on a plugin-owned
  callback (for example through `misc_start`), then exchange it server-to-server
  with the broker over authenticated HTTPS;
- provision/link the MyBB UID through MyBB APIs, never direct core edits;
- create a MyBB session only for an `Active` identity and rotate session/login
  material through the pinned MyBB 1.8.40 APIs;
- synchronize only explicitly approved username, email, and suspension fields;
- provide diagnostics and repair commands that expose IDs/status, never secrets.

Do not use MyBB password-verification hooks to send a user's AO password through
forum PHP. Do not place a bearer assertion containing identity claims in a URL;
the browser carries only a one-time opaque code. MyBB's own security notice
warns that 1.8.x has known unresolved security weaknesses, reinforcing strict
database isolation and the rule that a forum compromise must not reach game
credentials or game-database write access.

## Canonical username policy

The current surfaces are inconsistent. LoginEngine's interactive command uses
a six-character minimum, WebCore allows alphanumeric usernames but has no
explicit username length minimum, the database caps usernames at 32 bytes, and
login comparisons use case folding while the production collation is unknown.

Adopt ASCII alphanumeric usernames, length 6-32, with invariant ASCII lowercase
normalization and a unique normalized-username index. Reject Unicode, whitespace,
lookalikes, case variants, and a reviewed reserved/system-name list before any
provisioning. Preserve the chosen canonical case for display. AO character names
remain a separate policy and identifier.

## Security and deployment gates

Before implementation or installation:

1. Obtain approved read-only VPS access and record OS, packages, nginx/PHP/DB
   versions, complete vhost/TLS/DNS/firewall topology, service users, and backups.
2. Identify the running LoginEngine binary and prove password enforcement; do
   not deploy or expose a Debug authentication bypass.
3. Capture production `SHOW CREATE TABLE login`/`characters`, collations,
   indexes, duplicate/case behavior, and existing `Flags` values through a
   credential-safe read-only audit.
4. Approve the identity schema and migration/rollback before any DDL.
5. Build the broker and bridge with CSRF, strict cookies, rate limits, audit
   redaction, secret isolation, idempotency, and failure-recovery tests.
6. Back up every affected database and configuration, then rehearse restore.
7. Install MyBB only after the above gates pass. The official download page
   currently lists MyBB 1.8.40 (2026-05-28); re-check and verify its official
   checksum at execution time.

## Required-report status

1. VPS OS/version: unresolved; VPS access required.
2. nginx: public header reports 1.29.8; configuration unresolved.
3. PHP version: unresolved.
4. database engine/version: MySQL path proven; server/version unresolved.
5. AO authentication architecture: proven above.
6. AO account schema: checked-in schema documented; live DDL unresolved.
7. password algorithm: proven above.
8. unified identity architecture: Account Broker plus separate identity mapping.
9. MyBB mechanism: stock core plus AORebirth Identity Bridge and one-time-code SSO.
10. database/schema changes: none.
11. files changed: this report and active-task pointer only.
12. services/configuration changed: none.
13. MyBB installed: no.
14. checksum: not applicable; no package downloaded.
15. domain/TLS: apex HTTPS PASS; forum NXDOMAIN.
16-21. registration/login/failure/security/regression tests: not run; nothing deployed.
22. backup locations: unresolved.
23. rollback: no production mutation to roll back.
24. unresolved issues: the production-access, live-DDL, build-mode, topology,
    backup, and migration approvals listed above.

## Source evidence

- `AORebirth/Server/LoginEngine/MessageHandlers/UserLoginHandler.cs:69-94`
- `AORebirth/Server/LoginEngine/MessageHandlers/UserCredentialsHandler.cs:74-110`
- `AORebirth/Server/LoginEngine/Packets/CheckLogin.cs:96-127`
- `AORebirth/Libraries/Source/AORebirth.Core/Encryption/LoginEncryption.cs:62-65,183-219`
- `AORebirth/Libraries/Source/AORebirth.Core/Encryption/PasswordHash.cs:45-86,99-158`
- `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/login.sql:1-17`
- `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/characters.sql:1-26`
- `AORebirth/Libraries/Source/AORebirth.Database/Dao/LoginDataDao.cs:82-84,162-190`
- `AORebirth/Libraries/Source/AORebirth.Database/Dao/CharacterDao.cs:183-185,236-246`
- `AORebirth/Server/LoginEngine/Program.cs:110-317,539-554,797-828`
- `AORebirth/Server/WebEngine/WebRequestPathPolicy.cs:46-48,102-117`
- local validated WebCore runtime: `AORebirth/Built/Debug/htdocs/register.php`,
  `engine.php`, and `process-login.php` (ignored, manifest-bound runtime assets)
- MyBB plugin guidance: https://docs.mybb.com/1.8/development/plugins/basics/
- MyBB 1.8.40 hooks: https://docs.mybb.com/1.8/development/plugins/hooks/
- MyBB current download: https://mybb.com/download/
- MyBB security notice: https://github.com/mybb/mybb/security
