# MyBB Forum SSO Production Evidence - 2026-08-15

## Scope

Installed stock MyBB on the production Linux VPS and integrated it as a consumer
of AORebirth Account Broker identity. The AORebirth Account Broker remains the
only account authority. MyBB never receives AO passwords, AO password hashes, or
direct access to the AO game database.

## Official MyBB release

- Version: MyBB 1.8.40.
- Official source checked: `https://mybb.com/download/` and
  `https://mybb.com/versions/1.8.40/`.
- Official full-package SHA-512:
  `40e1c4d72394488737b1e888b5eccc844f491c8514e58396c75ef901ff708949d01ffd290516b505dac61e39c832d7b3e84a2c6c8851aa37bba7adf73fb40f35`.
- Production download verification: PASS.

## Backup

- Backup path:
  `/opt/ao-rebirth/database/backups/mybb-sso-20260815T074821Z`.
- Backup contents:
  - current AORebirth website deployment/config files;
  - Account Broker and stage6 environment/config files;
  - relevant account identity, external mapping, game mapping, provisioning,
    `login`, and `characters` tables.
- Checksum validation:
  - `config-files.tar.gz`: PASS.
  - `account-identity-and-login-tables.sql`: PASS.
- Rollback basis:
  - restore the archived config files from `config-files.tar.gz`;
  - restore affected database tables from `account-identity-and-login-tables.sql`
    if a database rollback is explicitly approved;
  - repoint `/opt/ao-rebirth/accountbroker/current` to the prior release and
    restart `ao-rebirth-accountbroker.service`;
  - stop/remove `ao-rebirth-forum` if forum service rollback is required.

## Production changes

- Retired the compromised controlled acceptance account by rotating its
  production `login.Password` to a new unknown AO-compatible hash.
  - Precondition proof: one `login` row, zero `characters` rows, one
    identity/game mapping row.
  - No deletion was performed.
- Deployed Account Broker release:
  `/opt/ao-rebirth/accountbroker/releases/mybb-sso-20260815-001`.
- Added private Account Broker forum SSO endpoints:
  - `POST /api/forum/sso/issue`;
  - `POST /api/forum/sso/redeem`;
  - `POST /api/forum/mapping/confirm`.
- Added file-backed SSO secret handling through
  `AOREBIRTH_ACCOUNT_BROKER_FORUM_SSO_SECRET_FILE`.
- Installed MyBB filesystem:
  - `/opt/ao-rebirth/forum/releases/mybb-1.8.40-001`;
  - `/opt/ao-rebirth/forum/current`;
  - `/opt/ao-rebirth/forum/shared`.
- Installed forum container:
  - `ao-rebirth-forum`;
  - image `ao-rebirth-forum:mybb-1.8.40`.
- Installed version-controlled MyBB plugin:
  `MyBBIdentityBridge/inc/plugins/aorebirth_identity_bridge.php`.
- Removed the MyBB installer directory after installation.
- Disabled native MyBB registration with `mybb_settings.disableregs=1`.
- Activated the AORebirth Identity Bridge in MyBB plugin cache.
- Created starter forum layout:
  - Announcements / News and Patch Notes;
  - Community / General Discussion, Player Help, Trade;
  - Support / Account and Login Help, Bug Reports.

## Database isolation

- MyBB database: `aorebirth_mybb`.
- MyBB database user: `ao_mybb`.
- MyBB database server: existing website MySQL container `ao-rebirth-mysql`.
- MyBB container networks:
  - `ao_rebirth_database`;
  - `mediacms_default`.
- MyBB is not attached to the stage6/game MySQL network
  `aorebirth_chatengine_stage6_internal`.
- Grant verification: PASS; `ao_mybb` has grants only on `aorebirth_mybb`.
- Denied-access check against the existing `login` table: PASS.

## Validation

- Windows AccountBrokerService build:
  - Debug: PASS.
  - Release: PASS.
- Windows AccountBrokerValidation:
  - Debug: PASS `31/31`.
  - Release: PASS `31/31`.
- Windows UnifiedAccountFlowValidation:
  - Debug: PASS `41/41`.
  - Release: PASS `41/41`.
- Linux Account Broker publish: PASS.
- Production Account Broker SSO:
  - unauthorized request rejected;
  - issue succeeded with configured secret;
  - redeem succeeded once;
  - replay failed closed.
- Forum container:
  - `SimpleXML`, `gd`, `mbstring`, `mysqli`, `pdo_mysql`, and `xml` loaded;
  - Identity Bridge plugin PHP lint: PASS.
- Website container:
  - changed PHP files lint clean;
  - `/` returned `200`;
  - `/forum-login` returned `302` for unauthenticated users.
- MyBB SSO E2E:
  - fresh controlled test identity `AORF081515`;
  - MyBB user created as UID `3`;
  - Account Broker external mapping `provider=mybb`, `ExternalAccountId=3`,
    `MappingState=Linked`: PASS.
- Controlled test mapping residue from a pre-fix run was corrected so
  `AORF081409` maps to its actual MyBB UID `2`, not the bootstrap admin UID.
- Final production state:
  - `ao-rebirth-accountbroker.service`: active;
  - `ao-rebirth-loginengine.service`: active;
  - `ao-rebirth-website`: healthy;
  - `ao-rebirth-forum`: running;
  - forum HTTP through nginx-proxy: `301`.

## Remaining blocker

`forum.ao-rebirth.com` does not currently resolve. Public HTTPS/TLS validation
cannot pass until DNS has an A record for `forum.ao-rebirth.com` pointing at
`2.24.96.30` and the ACME companion obtains the certificate.

## Final cutover attempt - 2026-08-15

### DNS

- Authoritative nameservers: `apollo.dns-parking.com`,
  `athena.dns-parking.com`.
- DNS provider: Hostinger.
- Apex `A` record: `ao-rebirth.com -> 2.24.96.30`, TTL `300`.
- `www` record: `www.ao-rebirth.com CNAME ao-rebirth.com`, TTL `300`.
- VPS public egress IP: `2.24.96.30`.
- DNSSEC: no public DS/DNSKEY answer observed; public resolver returned SOA
  authority only.
- `forum.ao-rebirth.com`: still `NXDOMAIN` from public resolver `1.1.1.1`.

Required DNS action remains:

```text
forum.ao-rebirth.com A 2.24.96.30 TTL 300
```

Public TLS, public HTTPS acceptance, public browser SSO, public cookie
inspection, UID reuse over the real hostname, and fresh public mapping remain
blocked until that record exists.

### Source fix from cutover validation

The website forum SSO entry point no longer places the one-time SSO code in the
forum callback URL. `E:\AORebirthWebsite\ao\forum-login.php` now issues the
broker code and returns a no-store, no-referrer, auto-submitted POST form to:

```text
https://forum.ao-rebirth.com/misc.php?action=aor_sso
```

This preserves the existing Account Broker and Identity Bridge design while
preventing normal HTTP request-line logs from recording `code=` query strings.

Validation:

- local PHP lint for `forum-login.php`: PASS;
- deployed mounted PHP to `/opt/ao-rebirth/website/src/forum-login.php`;
- production website-container PHP lint: PASS;
- Identity Bridge reads MyBB request input `code`, compatible with POST;
- malformed POST to `/misc.php?action=aor_sso`: rejected safely with no fallback
  authentication.

### Forum production state not requiring public DNS

- Forum container environment:
  - `VIRTUAL_HOST=forum.ao-rebirth.com`;
  - `VIRTUAL_PORT=80`;
  - `LETSENCRYPT_HOST=forum.ao-rebirth.com`.
- Forum container networks:
  - `ao_rebirth_database`;
  - `mediacms_default`.
- Edge containers running:
  - `nginx-proxy`;
  - `nginx-proxy-acme`;
  - `ao-rebirth-forum`;
  - `ao-rebirth-website`.
- Host-header HTTP check with local override:
  - `http://forum.ao-rebirth.com/` -> `301` to
    `https://forum.ao-rebirth.com/`.
- Host-header HTTPS route check with certificate verification disabled because
  public DNS/TLS is not live:
  - `/` -> `200`;
  - `/install/` -> `404`;
  - `/inc/config.php` -> `403`;
  - `/.env` -> `403`;
  - `/backups/` -> `404`;
  - `/secrets/` -> `404`.
- Native MyBB registration remains disabled:
  - `mybb_settings.disableregs=1`.
- Identity Bridge plugin remains active in MyBB plugin cache.

### Cookie scope

MyBB `cookiedomain` was changed from `.ao-rebirth.com` to blank and
`cookiepath` remains `/`. This avoids deliberately issuing forum cookies for
the parent `ao-rebirth.com` domain. Final cookie-flag proof still requires a
real public HTTPS SSO session after DNS/TLS is live.

### Forum structure

The starter forum layout was replaced with the approved traditional structure
after proving total existing forum `threads=0` and `posts=0`.

Top-level categories and child boards now present:

- Official:
  - Announcements;
  - Patch Notes;
  - Server Status;
  - Rules & Policies.
- Community:
  - General Discussion;
  - Introductions;
  - Screenshots & Videos;
  - Organizations / Guilds;
  - Off Topic.
- Game Discussion:
  - New Player Help;
  - Professions;
  - PvE;
  - PvP;
  - Tradeskills;
  - Items & Equipment;
  - Guides & Resources.
- AORebirth Support:
  - Technical Support;
  - Account Support;
  - Bug Reports;
  - Connection / Launcher Issues.
- Development:
  - Development News;
  - Server Development;
  - Client Development;
  - Content & World Restoration;
  - Suggestions & Feature Requests;
  - Testing / Test Server.
- Marketplace:
  - Buying;
  - Selling;
  - Trading;
  - Services.
- Archive:
  - Resolved Bug Reports;
  - Old Patch Discussions;
  - Retired Development Threads.

Intentional permissions:

- categories are not directly postable;
- Official child boards are read-only for normal users at launch;
- Archive child boards are read-only for normal users;
- normal discussion/support/development/marketplace boards remain open.

### Controlled acceptance account cleanup

Controlled accounts checked:

- `AORPub0815063300`;
- `AORF081204`;
- `AORF081311`;
- `AORF081351`;
- `AORF081409`;
- `AORF081515`.

Pre-cleanup proof:

- all had zero game characters;
- MyBB users `AORF081409` UID `2` and `AORF081515` UID `3` had zero posts.

Cleanup action:

- no rows deleted;
- Account Broker identities set to `Disabled`;
- game mappings set to `Disabled`;
- MyBB external mappings for UID `2` and UID `3` set to `Disabled`;
- production game `login.Password` values rotated to new unknown
  AO-compatible PBKDF2 hashes.

Post-cleanup proof:

- all controlled identities: `Disabled`;
- all controlled game mappings: `Disabled`;
- controlled MyBB external mappings: `Disabled`;
- all controlled game character counts: `0`;
- controlled MyBB user post counts: `0`.

### Database isolation

Effective credential checks:

- MyBB credentials -> MyBB DB: PASS;
- MyBB credentials -> `mybb_users`: PASS;
- MyBB credentials -> website/game MySQL `cellao_codex_clean.login`: DENIED;
- MyBB credentials -> stage6 identity DB `account_identities`: DENIED.

The Account Broker production DB user is also denied direct `characters` reads;
cleanup character counts were obtained with an internal root-only stage6
database check and no password/hash output.

### Admin CP and bootstrap secrets

- Admin CP path: standard MyBB `/admin/`.
- HTTPS-routed Admin CP reachability with local Host override:
  - `/admin/` -> `200`;
  - `/admin/index.php` -> `200`.
- MyBB config:
  - `admin_dir=admin`;
  - `hide_admin_links=0`;
  - `secret_pin_set=yes`.
- Bootstrap secret files remain:
  - `/opt/ao-rebirth/forum/shared/secrets/mybb_bootstrap_admin_password`;
  - `/opt/ao-rebirth/forum/shared/secrets/mybb_admin_cp_pin`.
- Both secret files are `root:root 600`.

Disposition: retained as operational bootstrap/Admin CP material until Mike
confirms administrator handoff and password/PIN rotation. Contents were not
printed or copied into evidence.

### Failure isolation

Forum container stop test:

- stopped only `ao-rebirth-forum`: PASS;
- `https://ao-rebirth.com/`: `200`;
- `https://ao-rebirth.com/register`: `200`;
- `ao-rebirth-accountbroker`: active;
- `ao-rebirth-loginengine`: active;
- `ao-rebirth-zoneengine`: active;
- restored `ao-rebirth-forum`: running.

Forum DB outage was not simulated by stopping `ao-rebirth-mysql` because MyBB
currently shares the AORebirth website MySQL container. Credential/schema
isolation is proven; a container-level forum-DB-only outage test requires a
separate MyBB DB service or another non-destructive outage mechanism.

### Logs and secret scan

After the POST handoff fix, the forum container was recreated so old current
container access logs from GET-based SSO validation were discarded.

Current runtime scan:

- current `ao-rebirth-forum` logs with `aor_sso` URL `code=` query: `0`;
- current `nginx-proxy` logs with forum `aor_sso` URL `code=` query: `0`;
- current forum/proxy/AccountBroker logs for obvious DB password, bootstrap,
  PIN, SSO-secret, or password-hash keywords: `0`.

### Backup and rollback

Additional cutover backup created:

```text
/opt/ao-rebirth/database/backups/mybb-cutover-20260815T091336Z
```

Contents:

- `aorebirth_mybb.sql`;
- `forum-config.tar.gz`;
- `identity-bridge-plugin.tar.gz`;
- `SHA256SUMS`.

Rollback remains isolated from game-server rollback:

1. disable website Forum navigation or point it back to a maintenance/coming
   soon page;
2. stop `ao-rebirth-forum`;
3. restore MyBB SQL/files/config/plugin from the cutover backup if required;
4. leave AORebirth website registration/login and AO LoginEngine operational.

### Service health after cutover-safe work

- `ao-rebirth-chatengine`: active;
- `ao-rebirth-loginengine`: active;
- `ao-rebirth-zoneengine`: active;
- `ao-rebirth-accountbroker`: active;
- `ao-rebirth-website`: running;
- `ao-rebirth-forum`: running;
- `ao-rebirth-mysql`: running;
- `nginx-proxy`: running;
- `nginx-proxy-acme`: running;
- `aorebirth-chatengine-mysql-stage6`: running;
- `https://ao-rebirth.com/`: `200`;
- `https://ao-rebirth.com/register`: `200`;
- forum HTTPS route with local Host override: `200`.

## Final public production acceptance - 2026-08-15

### DNS

- Hostinger DNS record is live:
  - `forum.ao-rebirth.com A 2.24.96.30`;
  - TTL `300`.
- Authoritative nameserver checks:
  - `apollo.dns-parking.com`: `2.24.96.30`, TTL `300`;
  - `athena.dns-parking.com`: `2.24.96.30`, TTL `300`.
- Independent public resolver check:
  - `1.1.1.1`: `2.24.96.30`, TTL `300`.
- Conflicting AAAA: none; public resolver returned SOA authority only.

### TLS and public forum

- ACME automation:
  - existing `nginx-proxy-acme` companion was restarted after DNS propagated;
  - issuance used Let's Encrypt production endpoint
    `https://acme-v02.api.letsencrypt.org/directory`;
  - `forum.ao-rebirth.com` certificate issued and installed.
- Certificate:
  - issuer: `Let's Encrypt`, CN `YR1`;
  - subject: `CN=forum.ao-rebirth.com`;
  - SAN: `DNS:forum.ao-rebirth.com`;
  - validity: `2026-08-15 08:33:09 GMT` through
    `2026-11-13 08:33:08 GMT`;
  - chain validation by public `curl`: PASS.
- Public HTTP:
  - `http://forum.ao-rebirth.com/` -> `301`
    `https://forum.ao-rebirth.com/`.
- Public HTTPS:
  - `https://forum.ao-rebirth.com/` -> `200`.
- Representative HTTPS assets:
  - `css.php?...` -> `200`;
  - `/jscripts/general.js?...` -> `200`;
  - `/jscripts/jquery.js?...` -> `200`;
  - `/jscripts/jquery.plugins.min.js?...` -> `200`;
  - `/images/logo.png` -> `200`;
  - `/images/collapse.png` -> `200`.
- Mixed-content check:
  - no active `http://` asset references;
  - only XHTML namespace/DTD references used `http://`.

### Public sensitive-path and registration checks

- `/install/` -> `404`.
- `/inc/config.php` -> `403`.
- `/.env` -> `403`.
- `/backups/` -> `404`.
- `/secrets/` -> `404`.
- `/member.php?action=register` -> `200` with expected AORebirth account
  boundary text; native MyBB registration remains disabled.
- `/member.php?action=login` -> `200` with expected AORebirth login boundary
  text; MyBB does not request or validate the AO password itself.

### Public SSO acceptance

Public browser-equivalent, cookie-aware SSO was validated through the real
public HTTPS hostnames. The harness used public website registration/login
forms, website session cookies, `/forum-login`, and the MyBB POST callback.

Controlled accounts used during final acceptance:

- `AORF093834`;
- `AORF093855`;
- `AORF093922`;
- `AORF094033`;
- `AORF094459`.

Fresh provisioning and replay/malformed/unknown checks:

- public `/register`: PASS;
- public `/login`: PASS;
- `/forum-login` page: PASS;
- SSO code in callback URL: NO;
- public POST redemption to
  `https://forum.ao-rebirth.com/misc.php?action=aor_sso`: PASS;
- forum homepage after SSO showed expected controlled username: PASS;
- replay of same code: REJECTED;
- unknown code: REJECTED;
- malformed code: REJECTED.

Mapping reuse:

- controlled identity `AORF093922`;
- first public SSO created MyBB UID `6`;
- second public SSO for the same authenticated identity succeeded;
- DB proof after the second SSO:
  - exactly one MyBB user for `AORF093922`;
  - exactly one `provider=mybb` external mapping;
  - one distinct external account ID;
  - no duplicate UID;
  - no duplicate external mapping.

Expiration:

- controlled identity `AORF094033`;
- SSO code issued through public `/forum-login`;
- code was held for `130` seconds while production TTL remained `120` seconds;
- expired-code POST to public MyBB SSO callback: REJECTED.

### Cookie security

Website cookie observed:

- `aorebirth_session`;
- `Secure`: YES;
- `HttpOnly`: YES;
- `SameSite=Lax`: YES;
- `Path=/`;
- parent-domain cookie: NO.

Forum cookies observed:

- `mybb[lastvisit]`:
  - `Secure`: YES;
  - `SameSite=lax`: YES;
  - `Path=/`;
  - parent-domain cookie: NO.
- `mybb[lastactive]`:
  - `Secure`: YES;
  - `SameSite=lax`: YES;
  - `Path=/`;
  - parent-domain cookie: NO.
- `sid`:
  - `Secure`: YES;
  - `HttpOnly`: YES;
  - `SameSite=lax`: YES;
  - `Path=/`;
  - parent-domain cookie: NO.
- `mybbuser`:
  - `Secure`: YES;
  - `HttpOnly`: YES;
  - `SameSite=lax`: YES;
  - `Path=/`;
  - parent-domain cookie: NO.

MyBB cookie hardening performed during public validation:

- `mybb_settings.cookiesecureflag` changed from `0` to `1`;
- `mybb_settings.cookiesamesiteflag` remained `1`;
- MyBB 1.8.40 only emits SameSite when a cookie call supplies a value, so the
  deployed MyBB cookie helper now defaults to `SameSite=lax` when the setting is
  enabled;
- local authoritative deployment artifacts added under
  `MyBBIdentityBridge/deploy/`;
- an attempted Apache `Header edit* Set-Cookie` approach was rejected after it
  caused Apache worker segfaults on this image; the rule was removed and the
  forum image rebuilt before acceptance continued.

### Password authority

For final public test users:

- AO plaintext password in MyBB: NO;
- AO password hash copied to MyBB: NO;
- MyBB password field length: `32`;
- MyBB password field matched AO hash format: NO;
- direct equality between AO `login.Password` and MyBB password field: NO;
- MyBB independently authenticates AO password: NO;
- Account Broker remains authentication authority: YES.

### DB isolation

Effective MyBB credential checks:

- MyBB credentials -> MyBB DB: PASS;
- MyBB credentials -> `mybb_users`: PASS;
- MyBB credentials -> website/game DB `cellao_codex_clean.login`: DENIED;
- MyBB credentials -> stage6 identity DB `account_identities`: DENIED.

### Navigation and forum structure

- Unauthenticated `https://ao-rebirth.com/` Forum navigation points to the live
  public forum URL: PASS.
- Unauthenticated homepage no longer exposes the old Coming Soon forum link:
  PASS.
- Authenticated `https://ao-rebirth.com/` Forum navigation points to
  `/forum-login`: PASS.
- Authenticated Forum navigation -> public SSO -> logged-in forum session:
  PASS.
- All approved top-level categories and boards are visible publicly after HTML
  entity normalization: PASS.

### Failure isolation

Post-public-cutover forum-container stop test:

- stopped only `ao-rebirth-forum`: PASS;
- `https://ao-rebirth.com/`: `200`;
- `https://ao-rebirth.com/register`: `200`;
- `ao-rebirth-accountbroker`: active;
- `ao-rebirth-loginengine`: active;
- `ao-rebirth-zoneengine`: active;
- restored `ao-rebirth-forum`: running;
- public `https://forum.ao-rebirth.com/` after restore: `200`.

The immediate HTTPS check during restore raced nginx-proxy SNI refresh and
returned a transient TLS SNI error; the follow-up public check returned `200`
with the valid forum certificate.

### Logs and secret scan

Current production log scan after public SSO tests:

- current forum container logs with `aor_sso` URL `code=` query: `0`;
- current nginx-proxy logs with forum `aor_sso` URL `code=` query: `0`;
- current website logs with `code=` query marker: `0`;
- current forum/proxy/website/broker logs for obvious DB password, bootstrap,
  Admin CP PIN, SSO secret, password-hash, Set-Cookie/session, or CSRF-token
  markers: `0`.

### Final controlled-account cleanup

Cleanup preconditions:

- final-pass controlled accounts had zero game characters;
- final-pass controlled MyBB users had zero posts.

Cleanup action:

- no rows deleted;
- identities set to `Disabled`;
- game mappings set to `Disabled`;
- MyBB external mappings set to `Disabled` where present;
- production game `login.Password` values rotated to new unknown AO-compatible
  PBKDF2 hashes.

Post-cleanup state:

- `AORF093834`: identity `Disabled`, game mapping `Disabled`, MyBB mapping
  `Disabled`;
- `AORF093855`: identity `Disabled`, game mapping `Disabled`, MyBB mapping
  `Disabled`;
- `AORF093922`: identity `Disabled`, game mapping `Disabled`, MyBB mapping
  `Disabled`;
- `AORF094033`: identity `Disabled`, game mapping `Disabled`, no MyBB mapping
  because its only code was used for expiration rejection;
- `AORF094459`: identity `Disabled`, game mapping `Disabled`, MyBB mapping
  `Disabled`.

### Final backup

Final post-acceptance backup:

```text
/opt/ao-rebirth/database/backups/mybb-public-acceptance-20260815T094721Z
```

Contents:

- `aorebirth_mybb.sql`;
- `account-identity-login.sql`;
- `forum-config-and-plugin.tar.gz`;
- `SHA256SUMS`.

### Final service health

- `ao-rebirth-chatengine`: active;
- `ao-rebirth-loginengine`: active;
- `ao-rebirth-zoneengine`: active;
- `ao-rebirth-accountbroker`: active;
- `ao-rebirth-website`: running;
- `ao-rebirth-forum`: running;
- `ao-rebirth-mysql`: running;
- `nginx-proxy`: running;
- `nginx-proxy-acme`: running;
- `aorebirth-chatengine-mysql-stage6`: running;
- `https://ao-rebirth.com/`: `200`;
- `https://ao-rebirth.com/register`: `200`;
- `https://forum.ao-rebirth.com/`: `200`.
