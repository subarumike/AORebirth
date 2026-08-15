# AORebirth Unified Account Flow Evidence

Status: first usable Windows-local unified account flow implemented and
validated on 2026-08-15. No production database schema was changed, no
production website route was enabled, no MyBB installation was performed, and
no Linux deployment was performed.

## Implemented flow

```text
local website page/API
  -> loopback Account Broker HTTP service
  -> AORebirth Account Broker library
  -> AORebirth identity tables
  -> AO game login account
  -> server-side website session
```

The controlled Windows flow is hosted by:

- `AORebirth/Server/AccountBrokerService/AORebirth.AccountBroker.Service.csproj`

Default hosting is loopback only:

```text
http://127.0.0.1:7510/
```

The service rejects non-loopback prefixes for this stage.

## Legacy website inspection

Inspected local WebCore runtime pages:

- `AORebirth/Built/Debug/htdocs/register.php`
- `AORebirth/Built/Debug/htdocs/process-login.php`
- `AORebirth/Built/Debug/htdocs/member-index.php`
- `AORebirth/Built/Debug/htdocs/member-profile.php`
- `AORebirth/Built/Debug/htdocs/includes/config.php`
- `AORebirth/Built/Debug/htdocs/includes/header.php`

Findings:

- legacy registration writes directly to `login`;
- legacy login reads `login.Password` directly in PHP;
- legacy registration has no CSRF protection;
- legacy member profile exposes raw account flags, expansion values, GM level,
  and internal account ID;
- legacy shared config requires direct database credentials in PHP environment;
- current WebEngine route policy rejects `register.php`, `process-login.php`,
  `member-index.php`, `member-profile.php`, `admin/`, and `includes/`.

Decision: do not reactivate the legacy PHP account pages. The new local proof is
implemented in tracked broker service code, and the existing WebEngine public
route block remains unchanged.

## Service/API boundary

Endpoints:

| Endpoint | Method | Purpose |
| --- | --- | --- |
| `/health` | GET | readiness check |
| `/api/csrf` | GET | issue server-tracked CSRF token |
| `/api/register` | POST | create unified identity and AO game account |
| `/api/login` | POST | authenticate via broker and create session |
| `/api/session` | GET | return current safe identity/session summary |
| `/api/logout` | POST | invalidate current session |
| `/register` | GET/POST | local registration page/form |
| `/login` | GET/POST | local login page/form |
| `/member` | GET | minimal authenticated member page |
| `/logout` | POST | form logout |

The service exposes no arbitrary SQL endpoint and no administrative account
mutation endpoint.

## Registration contract

Accepted fields:

- `username`
- `password`
- `email`
- `idempotencyKey`
- `csrf`

Validation:

- username: ASCII alphanumeric 6-32;
- password: 8-128 characters for this website flow;
- email: .NET `MailAddress` parse with exact address comparison;
- idempotency key: required, maximum 128 characters;
- CSRF token: cookie plus submitted form/API token must match a server-tracked
  unexpired token.

Registration calls `AccountBrokerService.CreateGameAccount()`, which:

- reserves the AORebirth identity;
- creates the AO game `login` row as the final sensitive step;
- generates `login.Password` with `LoginEncryption.GeneratePasswordHash()`;
- creates the identity/game mapping;
- marks the identity and provisioning job active;
- converges repeated idempotency-key retries on the same identity/game account.

Concurrent duplicate registrations are translated to safe `409` conflicts
instead of leaking SQL errors.

## Website login/session model

Website login calls `AccountBrokerService.AuthenticateWebsiteIdentity()`.

The website service does not read or validate `login.Password` itself. Password
verification stays inside the trusted broker using the existing AORebirth
password hash implementation. A correct game password succeeds only when the AO
game account has an active AORebirth identity mapping. Existing unmapped AO
accounts fail safely with `IDENTITY_MAPPING_REQUIRED`; no identity is silently
created on arbitrary password attempts.

Sessions:

- generated with 32 random bytes from `RandomNumberGenerator`;
- stored server-side in the broker process;
- expire after `AOREBIRTH_ACCOUNT_BROKER_SESSION_MINUTES` or 480 minutes by
  default;
- invalidated on logout;
- stored in `aor_session` cookie with `HttpOnly` and `SameSite=Lax`;
- add `Secure` automatically when the request uses HTTPS;
- contain no password, hash, salt, or privilege data.

The member page shows only:

- username;
- email verification status;
- identity status;
- game account linkage status.

It does not expose password hashes, salts, raw flags, GM level, expansion bits,
or internal database IDs.

## CSRF and rate limiting

CSRF:

- `/api/csrf`, `/register`, `/login`, and `/member` issue high-entropy tokens;
- state-changing operations require a matching cookie and submitted token;
- covered operations: registration, login, logout.

Rate limiting:

- registration attempts: fixed-window in-memory limiter keyed by remote address;
- login attempts: fixed-window in-memory limiter keyed by remote address plus
  normalized submitted username;
- defaults: 5 attempts per registration window and 5 attempts per login window;
- test overrides use environment variables to prove limiter behavior.

For later proxy deployment, nginx/Apache must preserve a trustworthy client IP
signal or the broker must be configured to trust a specific proxy header only
from the local reverse proxy.

## Configuration and secrets

Configuration is supplied by environment/arguments:

- `AO_REBIRTH_MYSQL_CONNECTION`: broker database connection string;
- `AOREBIRTH_ACCOUNT_BROKER_URL`: optional loopback URL prefix;
- `AOREBIRTH_ACCOUNT_BROKER_SESSION_MINUTES`: session lifetime;
- `AOREBIRTH_ACCOUNT_BROKER_REGISTER_LIMIT`: registration limiter;
- `AOREBIRTH_ACCOUNT_BROKER_LOGIN_LIMIT`: login limiter.

No DB password, session token, API key, or broker secret is hardcoded.

## Windows E2E validation

Validation tool:

- `Tools/UnifiedAccountFlowValidation/UnifiedAccountFlowValidation.csproj`

The validation recreates the proposed local `account_*` identity tables,
starts the loopback broker service, uses HTTP forms/API with cookies and CSRF,
and verifies database state.

Debug:

```text
PASS UnifiedAccountFlowValidation 34/34
```

Release:

```text
PASS UnifiedAccountFlowValidation 34/34
```

Covered cases:

- service health/readiness;
- registration page/form;
- CSRF issue and enforcement;
- successful registration;
- one identity row;
- one AO `login` row;
- one identity/game mapping;
- `Flags=0`;
- compatible AO password-hash format;
- correct password validates;
- wrong password fails;
- idempotent retry creates no duplicate login row;
- duplicate username rejected;
- case-equivalent username rejected;
- duplicate email rejected;
- invalid username/email/password rejected;
- wrong website login rejected;
- correct website login accepted;
- session cookie has `HttpOnly` and `SameSite=Lax`;
- current session endpoint returns safe identity summary;
- member page requires authentication and shows only safe fields;
- logout invalidates session;
- login rate limit returns `429`;
- concurrent duplicate registration leaves one login row;
- unmapped legacy AO account login fails safely;
- responses do not expose password hashes or DB secrets.

## Regression validation

- AccountBrokerValidation Debug: PASS `28/28`.
- AccountBrokerValidation Release: PASS `28/28`.
- LoginAuthenticationValidation Debug: PASS `14/14`.
- LoginAuthenticationValidation Release: PASS `14/14`.
- Account identity schema validation: PASS.
- Database preflight wrapper: PASS.
- AOtomation messaging wrapper: PASS `1013/1013`.

## Production state

- production DB changed: NO.
- production website routes enabled: NO.
- MyBB installed: NO.
- Linux touched: NO.

## Linux deployment readiness

Later Linux deployment requires:

- explicit production identity schema migration approval;
- backup and rollback approval;
- restricted broker database user;
- systemd unit for the broker service;
- environment/secrets file owned by the service account;
- nginx/Apache reverse-proxy route to loopback broker;
- health check against `/health`;
- route enablement only after Windows proof is accepted;
- rollback by disabling the proxy route and stopping the broker service.

No Linux deployment was executed in this stage.
