# AORebirth Password Management

Status: source-complete and Windows-local validated; production website integration and deployment remain separate gates.

## Credential authority

AORebirth retains one password. The authoritative credential is `login.Password`,
created with `LoginEncryption.GeneratePasswordHash()` and validated through the
same `PasswordHash.ValidatePassword()` path used by LoginEngine. The identity
database, website, and MyBB do not store a second usable password.

All password mutation is implemented by `AccountBrokerService`. Website callers
must not query or update `login.Password` directly.

## Password policy

Registration, authenticated password changes, and reset consumption use the
central `PasswordPolicy` contract:

- minimum length: 8 characters;
- maximum length: 128 characters;
- no composition rules beyond the length boundary.

The length ceiling bounds hash-work input without imposing legacy composition
restrictions that are not required by the AO credential format.

## Authenticated change

Canonical standalone route: `/account/password`.

The route requires a valid Broker session and CSRF token, verifies the current
password, validates the new password and confirmation, updates `login.Password`,
supersedes outstanding reset tokens, invalidates every Broker session for the
identity, and redirects to login.

Trusted website API:

`POST /api/account/password/change`

Required header: `X-AORebirth-Account-Mail-Secret`.

Required form fields: `identityPublicId`, `currentPassword`, `newPassword`, and
`confirmPassword`. The website must source `identityPublicId` only from its
authenticated `AOR_IDENTITY` session and clear the website session when the
response contains `invalidateSessions=true`.

## Forgotten-password flow

Canonical standalone routes:

- `/forgot-password`;
- `/reset-password?token=<opaque-token>`.

Reset requests always return the same public message:

`If an eligible account exists for that email, a password reset message has been sent.`

Only active, linked identities with a verified email and active game account
receive a token. Unknown, malformed, unverified, disabled, and unmapped targets
receive the same public response and no usable token.

Trusted website APIs:

- `POST /api/password/reset/request` with `email`;
- `POST /api/password/reset/status` with `token`;
- `POST /api/password/reset/consume` with `token`, `newPassword`, and
  `confirmPassword`.

All require `X-AORebirth-Account-Mail-Secret`. Public website forms must also
retain the website's own CSRF protection; the shared secret is a server-to-server
authorization boundary, not a browser CSRF token.

## Reset-token security

`account_password_reset_tokens` stores:

- identity reference;
- SHA-256 digest of the verified normalized email at issuance;
- SHA-256 digest of a 256-bit random bearer token;
- active/superseded/used/expired state;
- creation, expiry, and use timestamps.

Raw tokens are returned only to the mail-sending boundary and are never stored
in MySQL or logged. Issuing a new token supersedes earlier active tokens. Token
consumption locks the game credential and token row, updates `login.Password`,
marks the token used, and supersedes other active tokens in one transaction.
Concurrent reuse therefore succeeds at most once. Authenticated password changes
also supersede all active reset tokens. A digest of the verified email makes an
issued token unusable if email-changing functionality is added later and the
address changes.

## Configuration

- `AOREBIRTH_PASSWORD_RESET_TOKEN_MINUTES`: reset lifetime, default 30;
- `AOREBIRTH_ACCOUNT_BROKER_PASSWORD_RESET_IP_LIMIT`: per-source limit,
  default 10;
- `AOREBIRTH_ACCOUNT_BROKER_PASSWORD_RESET_TARGET_LIMIT`: per-email-digest
  limit, default 3;
- `AOREBIRTH_ACCOUNT_BROKER_PASSWORD_RESET_WINDOW_MINUTES`: limiter window,
  default 15;
- `AOREBIRTH_PUBLIC_BASE_URL`: public origin used in reset links;
- existing `AOREBIRTH_MAIL_SMTP_*` and `AOREBIRTH_MAIL_FROM_*` settings:
  authenticated SMTP transport;
- existing `AOREBIRTH_ACCOUNT_BROKER_ACCOUNT_MAIL_SECRET` or secret-file form:
  trusted website API authorization.

No secrets belong in committed configuration.

## Migration and deployment

Apply
`AORebirth/Libraries/Source/AORebirth.Database/Migrations/20260831_account_password_reset_tokens.sql`
only after a production database backup. Deploy the matching Account Broker,
LoginEngine compatibility host, and ZoneEngine build from the same accepted SHA.
The migration is additive and does not modify existing credentials or identities.

The production PHP website is maintained outside this repository. Its website
task must add the navigation and AORebirth-styled forms, call only the trusted
APIs above, preserve its established CSRF/session controls, and clear its
authenticated session after successful password mutation. The Broker-host pages
and local HTTP harness are the executable reference behavior.
