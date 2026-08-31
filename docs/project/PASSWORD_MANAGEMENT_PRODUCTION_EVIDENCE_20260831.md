# Password Management Production Evidence - 2026-08-31

## Starting state and authority

- Password-management implementation baseline:
  `f04d5fa0c10687d275405125ca15ceff935f4ae4`.
- Exact deployed Account Broker source:
  `8fd1300fb399c57ae683f62c07c95920778b22f1` on `master`.
- Website starting revision:
  `d7a92cd224129b1f1030196df05df632cb412be4`.
- Exact deployed website revision:
  `c79468e6395aefbe91709237be754490b1029b70` on
  `codex/password-management-website`.
- `login.Password` remains the only website/game credential. The production PHP
  website calls the trusted Account Broker APIs and never reads, hashes, or
  updates that column directly.

The production route is:

```text
browser -> PHP website -> authenticated Account Broker API -> login.Password
```

## Website integration

The website changes are separately versioned in `E:\AORebirthWebsite` and were
prepared in the isolated `E:\AORebirthWebsite-password-management` worktree.
The original dirty website checkout was not modified.

- `ao/account-recovery-request.php`: public `/forgot-password`, PHP CSRF, shared
  Broker helper, and one generic eligible/nonexistent/unverified response.
- `ao/account-recovery.php`: public `/reset-password`, fragment-token capture,
  clean redirect, short-lived PHP session state, status/consume calls, CSRF,
  and safe unusable-token response.
- `ao/account-credentials.php`: authenticated `/account/password`, current/new
  password submission only to Account Broker, and local logout after success.
- `ao/account-login.php`: discoverable forgot-password link and preservation of
  the Broker session cookie jar after successful login.
- `ao/account-logout.php`: revokes the Broker session before clearing PHP state.
- `ao/account.php`: password-management link and fail-closed Broker authority.
- `ao/forum-login.php`: fail-closed Broker authority before issuing MyBB SSO.
- `ao/includes/account-broker.php`: shared password APIs plus Broker-session
  binding, validation, and logout.
- `ao/includes/header.php`: navigation uses current Broker authority instead of
  trusting stale PHP identity state.
- `deploy/website/apache-site.conf`: clean public routes, reset response headers,
  and access logging that omits query strings.
- `tests/account-credential-source-validation.php`: 53 source/security checks.

## Reset token URL and log handling

Canonical password-reset email uses:

```text
https://ao-rebirth.com/reset-password#token=<opaque-token>
```

The fragment is not sent in the HTTP request. Page JavaScript immediately
removes it from browser history and POSTs it to the same-origin PHP route. PHP
holds it only in its existing server-side session for at most 1,800 seconds and
redirects to clean `/reset-password`. The page sends `Referrer-Policy:
no-referrer` and `Cache-Control: no-store`; it loads no third-party reset-page
resources. Apache request logging uses `%m %U %H`, without the query string.

The compatibility `/reset-password?token=...` entry remains accepted and PHP
immediately redirects it to the clean route, but the canonical system never
generates that form. The edge proxy's request-line log can observe a manually
supplied compatibility query before PHP cleans it; this is the only remaining
token-URL caveat.

A real production log scan compared all five controlled bearer tokens (three
password-reset and two email-verification tokens) against Account Broker,
website, and edge-proxy logs. Token matches were zero. The final controlled
password also had zero log matches.

## Production database migration

Target database: `aorebirth_chatengine_stage6` through the production Stage6
MySQL instance. No connection string or credential is recorded here.

Pre-migration evidence:

- required game tables: `34`;
- online characters: `0`;
- base tables: `39`;
- `account_password_reset_tokens`: absent;
- login rows: `34`;
- identity rows: `27`;
- credential digest:
  `c56b5cdf00244bfb069e6950e96a886dae4fdcc83de1bd7a36784605f236922c`.

Backup:

- `/opt/ao-rebirth/database/backups/password-management-20260831T105649Z/aorebirth_chatengine_stage6-pre-password-management.sql.gz`;
- gzip integrity: PASS;
- backup SHA-256 is recorded in that directory's `SHA256SUMS`.

Only
`AORebirth/Libraries/Source/AORebirth.Database/Migrations/20260831_account_password_reset_tokens.sql`
was applied. The deployed migration SHA-256 is
`5db0492da17f4eff25743dc7b42abd283b706d573afaac5dc011caf1fb0b0421`.

Immediate post-migration evidence:

- base tables: `40`;
- reset-token table: present;
- reset-token rows: `0`;
- login rows: `34`;
- identity rows: `27`;
- credential digest unchanged exactly;
- 8 tracked columns, primary key, unique `TokenHash` index, both tracked
  secondary indexes, restrictive identity foreign key, and both checks: PASS;
- exact Account Broker `SELECT`/`INSERT`/`UPDATE` grants on the new table: PASS.

After controlled acceptance, expected state is login rows `35`, identity rows
`28`, one non-GM linked/verified controlled identity, and three reset-token
history rows (`Superseded=1`, `Used=2`, active=0). This delta belongs only to
the controlled acceptance account.

## Production deployment

Account Broker:

- release: `/opt/ao-rebirth/accountbroker/releases/password-management-8fd1300f`;
- current symlink: exact release above;
- binary SHA-256:
  `5fe252213e8dbe0fc1f001b405eb8982e63c996009f6b355f9496342e7951c2e`;
- artifact archive SHA-256:
  `2220fe4c7a371329d825733b05b03211303b10025d9d2febca68d15ba2fa3100`;
- `ao-rebirth-accountbroker.service`: active;
- `http://172.18.0.1:7510/health`: HTTP 200.

Password-reset TTL, source/target rate limits, SMTP host/port/TLS, public base
URL, shared-secret file, SMTP-password file, and bind configuration were all
present. Secret values were not printed. Secret files remain root-owned with
the service group and mode `0640`.

Website:

- deployed source revision:
  `c79468e6395aefbe91709237be754490b1029b70`;
- backup:
  `/opt/ao-rebirth/website/backups/password-management-20260831T105649Z/website-pre-password-management.tar.gz`;
- backup gzip integrity: PASS;
- backup SHA-256:
  `f10dd7f5d5b88bf5edab7126d98211f1a0d7c5eadafbb154ad5840bd58ec00da`;
- deployed password files byte-match the accepted source;
- `ao-rebirth-website`: running and healthy;
- production-image PHP lint: PASS for every changed PHP file;
- production-image Apache configuration: syntax OK.

## Email and end-to-end acceptance

Controlled account: `PwdA88176237`, non-GM, verified local production alias
`abuse@ao-rebirth.com`. Its credential file is root-only (`0600`) at
`/opt/ao-rebirth/accountbroker/acceptance/password-management-20260831.credentials`.
No password or bearer token is recorded in repository evidence.

The real configured Account Broker SMTP path delivered both verification and
password-reset messages to the production Maildir. The reset messages contained
the canonical public HTTPS fragment URL, UTC expiration, and ignore-if-not-
requested wording. They contained no localhost, private service address,
internal Broker hostname/port, or plaintext password.

Production acceptance:

- `/forgot-password`: PASS;
- verified/nonexistent/unverified response indistinguishability: PASS;
- no reset email for the unverified identity: PASS;
- `/reset-password` fragment capture and clean URL: PASS;
- `/account/password`: PASS;
- incorrect current password rejection: PASS;
- authenticated password change: PASS;
- two-session invalidation after change: PASS;
- old password rejection and changed password acceptance: PASS;
- real reset email delivery: PASS;
- password reset and post-reset session invalidation: PASS;
- used-token replay rejection: PASS;
- older-token superseding and rejection: PASS;
- newer token one-time success: PASS;
- final website login: PASS;
- registration page, login, account, logout, verification, and resend: PASS;
- MyBB SSO issue/redeem and authenticated forum state: PASS;
- browser JavaScript fragment cleanup and safe invalid-token response: PASS;
- authoritative LoginEngine protocol, correct credential to `CHARACTER_LIST`:
  PASS;
- authoritative LoginEngine protocol, wrong credential to `LOGIN_ERROR`: PASS.

## Automated validation

- AccountBrokerValidation Debug and Release: PASS `66/66` each.
- UnifiedAccountFlowValidation Debug and Release: PASS `83/83` each.
- LoginAuthenticationValidation Debug and Release: PASS `14/14` each.
- Account identity/password-reset schema validation: PASS, including one
  single-use reset-token scenario.
- `cmd /d /c tools\build_aorebirth_debug.cmd`: PASS.
- `cmd /d /c LinuxBuild\publish-accountbroker.cmd linux-x64 true`: PASS for
  exact source `8fd1300fb399c57ae683f62c07c95920778b22f1`.
- WebsitePasswordManagementValidation: PASS `53/53`.
- Production PHP lint and container health: PASS.
- Production database schema, controlled row delta, and token lifecycle: PASS.
- Production secret log scan: PASS.
- ProductionLoginAcceptance: PASS `2/2`.

## Security conclusions

- One password authority remains: `login.Password`.
- PHP stores no password hash and performs no credential database mutation.
- Password mutations occur only through authenticated Account Broker APIs.
- Reset tokens are 256-bit random values; only SHA-256 digests persist.
- Real passwords and bearer tokens had zero matches in inspected runtime logs.
- All three new forms retain existing PHP CSRF controls and secure session
  cookies.
- Forgot-password responses do not disclose existence, verification, or rate
  limiting.
- Successful change/reset invalidates Broker sessions and outstanding reset
  tokens; the PHP layer fails closed when Broker authority is gone.

## Rollback and remaining risk

Rollback artifacts are the database and website backups above, the previous
Account Broker release
`/opt/ao-rebirth/accountbroker/releases/account-characters-20260816-001`, and
the exact deployment manifests under the password-management release folders.
A database rollback is destructive and remains separately approval-gated.

The controlled acceptance account and its one MyBB mapping remain active so the
final cross-system credential proof is reproducible; retirement is a separate
explicit operation. The compatibility query-token caveat described above is the
only known token-URL exposure surface; canonical production email and browser
flows use fragments and were proven absent from logs.
