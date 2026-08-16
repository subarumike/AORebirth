# AORebirth Unified Account Architecture

Status: Database/schema evidence phase validated locally, LoginEngine password
authentication restored, Account Broker foundation added, a Windows-local
unified account registration/login flow implemented through the loopback Account
Broker service, and the first public website account flow promoted through
`https://ao-rebirth.com/register`, `/login`, `/account`, and `/logout`
(2026-08-15). Production LoginEngine protocol acceptance for a website-created
account and DB credential rotation are complete. Stock MyBB 1.8.40 is installed
on production Linux as an Account Broker identity consumer; public forum
production acceptance for `https://forum.ao-rebirth.com` passed on 2026-08-15.

## Proven current account behavior

The AO client first sends a username. `UserLoginHandler` stores it on the
connection, generates a 32-byte per-connection server challenge, and returns
that challenge. The credentials handler then requires the credential-message
username to match the challenged connection username case-insensitively, loads
the `login` row by username, and permits only rows whose `Flags` value is zero.
It decrypts the AO login key, requires the embedded username and server
challenge to match, and validates the embedded clear password against
`login.Password`. Successful authentication loads characters by the canonical
account username string.

The persisted game password format is:

`iterations:base64(30-byte random salt):base64(30-byte PBKDF2-HMAC-SHA1 output)`

The iteration count is randomly selected from 1,111 through 1,366. This format
must remain unchanged for AO client compatibility. The server-side AO login-key
decryption private value is hard-coded in source and must not be copied into the
forum or public web tier.

Critical build boundary resolved: `LoginEncryption.i_Enable` is unconditional
`true` in current source. Debug and Release validation both prove that correct
passwords pass and incorrect, blank, malformed, salt-mismatched, and
username-mismatched credentials fail closed.

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

## Database/schema evidence update

The 2026-08-15 read-only production audit inspected the LoginEngine database
`aorebirth_chatengine_stage6` through the existing MySQL 8.4.10 container
client and captured metadata only.

Live `login` facts:

- engine/collation: InnoDB, `latin1_swedish_ci`;
- primary key: `Id int NOT NULL AUTO_INCREMENT`;
- username: `Username varchar(32) NOT NULL`, `latin1_swedish_ci`, unique key;
- password: `Password varchar(100) NOT NULL`, `latin1_swedish_ci`;
- email: `Email varchar(64) NOT NULL`, `latin1_swedish_ci`;
- status/privilege fields: `Flags int NOT NULL DEFAULT 0`,
  `AccountFlags int NOT NULL DEFAULT 0`, `GM int NOT NULL DEFAULT 0`;
- timestamp: `CreationDate datetime NOT NULL`;
- foreign keys: none.

Live `characters` facts:

- engine/collation: InnoDB, `latin1_swedish_ci`;
- primary key: `Id int NOT NULL AUTO_INCREMENT`;
- owner field: `Username varchar(32) NOT NULL`, `latin1_swedish_ci`;
- indexes: primary key only;
- foreign keys: none.

Live aggregate findings:

- 8 `login` rows, 8 distinct usernames, 8 distinct lowercase usernames;
- only 5 of 8 usernames match the future ASCII alphanumeric 6-32 broker rule;
- 7 `characters` rows with 3 distinct owner usernames;
- 0 exact owner orphans and 0 case-only owner/account mismatches;
- `login.Flags` observed value: only `0`.

Source reconciliation:

- `characters.Username` remains the account-owner field;
- `CharacterDao.GetAllForUser()` and `CharacterDao.IsCharacterOnAccount()` use
  username-string equality;
- `LoginDataDao.GetByCharacterId()` resolves character to account by loading the
  character row and then querying `login` by `character.Username`;
- `CheckLogin` permits only `Flags == 0`;
- `CheckLogin.IsLoginCorrect()` loads `login.Password` and calls
  `LoginEncryption.IsValidLogin()` after challenge, username, and `Flags`
  checks.

The repository-backed Account Broker identity schema is now defined in
`AORebirth/Libraries/Source/AORebirth.Database/SqlTables/aorebirth_identity.sql`
with validation coverage in
`Tools/AccountIdentitySchema/validate_account_identity_schema.sql`. It was
executed successfully against the local Windows development MySQL database
`cellao_codex_clean` after confirming production was out of scope and Docker was
not available for a throwaway server. The detailed evidence report is
`docs/project/UNIFIED_ACCOUNT_SCHEMA_EVIDENCE_20260815.md`.

## Existing account creation surfaces

LoginEngine exposes an operator console `adduser` command. Its interactive path
requires at least six characters for username/password, validates email, hashes
through the AO password implementation, and inserts a parameterized `login`
row. The non-interactive parameter validator does not enforce the same username
or password rules; the database unique username index is the final duplicate
guard. `setpass` replaces the AO hash using the same algorithm.

The locally imported historical WebCore also contains PHP account pages.
`register.php` inserts directly into `login`; `process-login.php` reads
`login.Password` directly in PHP; the legacy flow has no CSRF protection; and
`member-profile.php` exposes raw account flags, expansion bits, GM level, and
internal account ID. These pages are not an approved public registration/login
system. The current WebEngine allowlist blocks `register.php`,
`process-login.php`, `member-index.php`, `member-profile.php`, and all other
authentication/mutation PHP routes; WebEngine remains Windows-only, plaintext,
development-only software.

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

Use the new AORebirth Account Broker as the sole account-provisioning authority.
Keep the existing LoginEngine authentication path and `login.Password` format
intact. Do not let nginx/PHP/MyBB connect to the AO game database.

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

The 2026-08-15 audit did not prove a safe nonzero pending `login.Flags` value.
Future registration must not create a playable `Flags=0` game account until the
broker has validated input, generated `login.Password` through the existing
AORebirth password implementation, and is ready to complete activation. The
approved schema therefore supports identity-first provisioning, durable
idempotent recovery, and stable `login.Id` mapping, with game-account
creation/linking performed last unless a separate pending-flag policy is
explicitly approved.

Provisioning is idempotent and stateful. The current internal broker foundation
reserves identity, creates or links the game account as the final sensitive
step, stores `login.Password` through `LoginEncryption.GeneratePasswordHash()`,
and activates the identity/game mapping. Retries look up the idempotency record
and converge on the same IDs. MyBB UID linkage is represented by
`account_external_mappings` with `Provider='mybb'`; the bridge confirms the
external UID through the broker after MyBB provisioning. No partial failure may
delete or overwrite an existing account
automatically; recovery is deterministic and operator-visible.

The first usable Windows flow is hosted by
`AORebirth/Server/AccountBrokerService/AORebirth.AccountBroker.Service.csproj`.
It exposes:

- `GET /health`;
- `GET /api/csrf`;
- `POST /api/register`;
- `POST /api/login`;
- `GET /api/session`;
- `POST /api/logout`;
- local HTML pages `/register`, `/login`, `/member`, and `/logout`.

The service uses the broker library as the only database-facing account
authority. The website pages do not query `login.Password`, do not hold game
database credentials separately, and do not expose administrative mutation.

The first public website promotion is documented in
`docs/project/PUBLIC_UNIFIED_ACCOUNT_FLOW_EVIDENCE_20260815.md`. Production now
runs the Linux Account Broker on the trusted Docker bridge address
`172.18.0.1:7510`, with public PHP routes calling broker `/api/register`,
`/api/login`, `/api/session`, and `/api/logout`. Legacy PHP account endpoints
such as `/register.php` and `/process-login.php` remain blocked.

The production account acceptance and secret-rotation gates are documented in
`docs/project/PRODUCTION_ACCOUNT_ACCEPTANCE_AND_SECRET_ROTATION_20260815.md`.
The controlled website-created production account was proven through the real
LoginEngine protocol: correct credentials reached `CHARACTER_LIST`, and wrong
credentials reached `LOGIN_ERROR`. The exposed MySQL root and `aorebirth_stage6`
credentials were rotated; old values were rejected; LoginEngine and ZoneEngine
were republished with a preflight policy that allows only the four governed
Account Broker extension tables beside the 34 governed game tables. Forum SSO
production evidence is recorded in
`docs/project/MYBB_FORUM_SSO_PRODUCTION_EVIDENCE_20260815.md`.

The 2026-08-15 forum cutover-safe update changed the website handoff from a
callback URL query code to an auto-submitted POST form. The Account Broker still
issues the same short-lived one-time code and the MyBB Identity Bridge still
redeems it through the same server-to-server broker endpoint; the transport
change prevents normal web access logs from recording `code=` request-line
queries.

The final public forum acceptance also hardened MyBB cookies for public HTTPS:
`cookiesecureflag=1`, blank `cookiedomain`, and default `SameSite=lax` emission
from the MyBB 1.8.40 cookie helper when the MyBB SameSite setting is enabled.
This preserves separate website/forum session cookies while preventing
parent-domain forum cookies.

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
the browser carries only a one-time opaque code, and the approved website
handoff submits that code by POST to avoid `code=` query strings in ordinary
request logs. MyBB's own security notice warns that 1.8.x has known unresolved
security weaknesses, reinforcing strict database isolation and the rule that a
forum compromise must not reach game credentials or game-database write access.

## Canonical username policy

The current surfaces are inconsistent. LoginEngine's interactive command uses
a six-character minimum, WebCore allows alphanumeric usernames but has no
explicit username length minimum, the database caps usernames at 32 bytes, and
login comparisons use case folding while the production collation is unknown.

Adopt split username policy:

- new public registrations: ASCII alphanumeric, length 6-32;
- legacy existing-account links: ASCII alphanumeric, length 1-32, so the
  observed short live usernames can remain representable without rewriting game
  accounts or character ownership.

Both policies use invariant ASCII lowercase normalization and the schema keeps a
unique normalized-username index. Reject Unicode, whitespace, lookalikes, case
variants, and a reviewed reserved/system-name list before new provisioning.
Preserve the chosen canonical case for display. AO character names remain a
separate policy and identifier.

Website-session policy:

- session tokens are generated from 32 random bytes;
- session state is stored server-side in the broker process;
- cookies are `HttpOnly`, `SameSite=Lax`, and `Secure` when HTTPS is used;
- logout invalidates the server-side token;
- no password, salt, hash, privilege flag, or raw database ID is stored in the
  cookie.

The current session and rate-limit stores are intentionally lightweight and
single-process for the Windows proof. Production Linux deployment must either
run one broker instance behind the loopback reverse proxy or replace these
stores with an approved shared backing store before horizontal scaling.

Email verification policy:

- Account Broker owns verification state and the only write path to
  `account_identities.EmailVerifiedAt`;
- website PHP may request resend/verify through internal broker endpoints but
  must not write identity verification state directly;
- public verification tokens are cryptographically random and stored only as
  SHA-256 hashes in `account_email_verification_tokens`;
- resend supersedes prior active tokens for the identity;
- valid verification is single-use, expired tokens fail closed, and malformed
  or unknown tokens do not disclose unrelated account existence;
- verification links use the website fragment form
  `https://ao-rebirth.com/verify-email.php#token=...` so the token is not sent in
  normal HTTP request URLs;
- SMTP credentials and broker mail authorization secrets live only in
  production secret files/environment, never source, web root, MyBB files, or
  Git;
- production transactional email provider is the self-hosted VPS
  Postfix/Dovecot/OpenDKIM stack for the first public verification/notification
  flow; broker release `email-foundation-20260816-002` is deployed fail-closed
  until Hostinger DNS and SMTP app configuration are complete;
- MyBB may use the approved SMTP transport for notifications, but it must not
  become an AORebirth password reset or password authentication authority.

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

1. VPS OS/version: unresolved; not required for this schema-only stage.
2. nginx: public header reports 1.29.8; configuration unresolved.
3. PHP version: unresolved.
4. database engine/version: production LoginEngine database is MySQL 8.4.10.
5. AO authentication architecture: proven above.
6. AO account schema: live `login` and `characters` metadata documented in
   `docs/project/UNIFIED_ACCOUNT_SCHEMA_EVIDENCE_20260815.md`.
7. password algorithm: proven above.
8. unified identity architecture: Account Broker plus separate identity mapping.
9. MyBB mechanism: stock core plus AORebirth Identity Bridge and one-time-code SSO.
10. database/schema changes: production now includes
`account_email_verification_tokens` from migration
`20260816_account_email_verification_tokens.sql`; no password, login,
character, MyBB, or mapping table was modified.
11. files changed: this report, evidence reports, schema proposal, validation
    SQL, Account Broker library, Account Broker validation harness,
    active-task pointer, and project-state summary.
12. services/configuration changed: Account Broker release
`email-foundation-20260816-002` is deployed and healthy; SMTP/account-mail
secret configuration remains absent, so outbound mail is disabled.
13. MyBB installed: yes, stock MyBB 1.8.40 under `/opt/ao-rebirth/forum`.
14. checksum: MyBB 1.8.40 package verification passed during installation.
15. domain/TLS: apex HTTPS PASS; forum HTTPS PASS with Let's Encrypt
production certificate for `forum.ao-rebirth.com`.
16-21. registration/login/failure/security/regression tests: registration is
implemented on the public website through the production Account Broker.
Identity schema validation passes against the local Windows development MySQL
target. Unified account flow validation passes 41/41 in Debug and Release.
Account Broker validation passes 31/31 in Debug and Release. LoginEngine
password-authentication validation passes 14/14 in Debug and Release, database
preflight passes, and AOtomation messaging passes 1013/1013. MyBB internal SSO
E2E passed, and public forum SSO through `https://forum.ao-rebirth.com` passed.
22. backup locations:
`/opt/ao-rebirth/database/backups/mybb-sso-20260815T074821Z` and
`/opt/ao-rebirth/database/backups/mybb-cutover-20260815T091336Z`.
23. rollback: disable Forum navigation/SSO entry, stop `ao-rebirth-forum`, and
restore MyBB DB/files/plugin from the cutover backup if required. Game-server
rollback is not required for forum rollback.
24. unresolved issues: no remaining forum/account infrastructure acceptance
gate is open. Production email remains blocked on Hostinger MX/SPF/DKIM/DMARC
DNS, Account Broker/MyBB SMTP configuration, and received-message
SPF/DKIM/DMARC proof. Later forum work should focus on presentation, content,
moderation policy, email notification completion, and community launch.

## Source evidence

- `AORebirth/Server/LoginEngine/MessageHandlers/UserLoginHandler.cs:69-94`
- `AORebirth/Server/LoginEngine/MessageHandlers/UserCredentialsHandler.cs:74-110`
- `AORebirth/Server/LoginEngine/Packets/CheckLogin.cs:96-127`
- `AORebirth/Libraries/Source/AORebirth.Core/Encryption/LoginEncryption.cs:62-65,183-219`
- `AORebirth/Libraries/Source/AORebirth.Core/Encryption/PasswordHash.cs:45-86,99-158`
- `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/login.sql:1-17`
- `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/characters.sql:1-26`
- `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/aorebirth_identity.sql`
- `AORebirth/Libraries/Source/AORebirth.AccountBroker/AccountBrokerService.cs`
- `AORebirth/Libraries/Source/AORebirth.AccountBroker/UsernamePolicy.cs`
- `AORebirth/Server/AccountBrokerService/Program.cs`
- `Tools/UnifiedAccountFlowValidation/Program.cs`
- `Tools/AccountBrokerValidation/Program.cs`
- `Tools/AccountIdentitySchema/validate_account_identity_schema.sql`
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
