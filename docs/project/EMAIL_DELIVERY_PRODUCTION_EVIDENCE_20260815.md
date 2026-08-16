# AORebirth Email Delivery Production Evidence - 2026-08-15

## Scope

Added the source-side email verification foundation without changing the frozen
LoginEngine authentication path, Account Broker password semantics, AO game
login behavior, MyBB SSO design, or MyBB password authority boundary.

This stage does not declare production email ready. Real production delivery is
blocked until a transactional SMTP provider is selected, provider-supplied DNS
records are added at Hostinger, and received-message authentication headers are
proven.

## DNS audit

Read-only DNS checks for `ao-rebirth.com` on 2026-08-15:

- authoritative nameservers: `apollo.dns-parking.com`,
  `athena.dns-parking.com`;
- DNS provider: Hostinger;
- MX: no public MX answer; resolver returned SOA authority only;
- apex TXT/SPF: no public TXT answer; resolver returned SOA authority only;
- DKIM: no provider selector exists because no mail provider has been selected;
- DMARC: `_dmarc.ao-rebirth.com` did not resolve.

No DNS records were added or changed in this stage.

## Provider and sender decision

No provider is selected yet. The prepared source expects one real authenticated
SMTP provider and defaults the public sender identity to AORebirth-owned mail
configuration supplied by production environment variables/files.

Preferred sender identity remains:

```text
AORebirth <noreply@ao-rebirth.com>
```

or, for forum-only mail if intentionally separated:

```text
AORebirth Forum <forum@ao-rebirth.com>
```

The actual sender must match the selected provider's domain-verification and
DKIM setup.

## Broker email verification implementation

The Account Broker now owns email verification state and token mutation.
Website PHP does not write `EmailVerifiedAt` directly.

Repository schema now includes
`account_email_verification_tokens`:

- `TokenHash` is `binary(32)` and stores only a SHA-256 hash of the public
  token;
- token states are `Active`, `Superseded`, `Used`, and `Expired`;
- resend supersedes prior active tokens for the identity;
- verification marks `EmailVerifiedAt` only through Account Broker code;
- token replay returns the used/superseded/expired state without reapplying
  verification;
- malformed or unknown tokens fail closed.

Prepared internal Account Broker endpoints:

- `POST /api/account/identity`;
- `POST /api/email/verification/resend`;
- `POST /api/email/verification/verify`.

These endpoints require `X-AORebirth-Account-Mail-Secret`; the secret is read
from `AOREBIRTH_ACCOUNT_BROKER_ACCOUNT_MAIL_SECRET_FILE` or
`AOREBIRTH_ACCOUNT_BROKER_ACCOUNT_MAIL_SECRET`.

## SMTP configuration contract

The Account Broker mail sender is inert unless authenticated SMTP configuration
is supplied. Supported production inputs:

- `AOREBIRTH_MAIL_SMTP_HOST`;
- `AOREBIRTH_MAIL_SMTP_PORT` default `587`;
- `AOREBIRTH_MAIL_SMTP_TLS` default `StartTls`; use `None` only for a
  deliberately approved non-production test;
- `AOREBIRTH_MAIL_SMTP_USERNAME`;
- `AOREBIRTH_MAIL_SMTP_PASSWORD_FILE` or `AOREBIRTH_MAIL_SMTP_PASSWORD`;
- `AOREBIRTH_MAIL_FROM_ADDRESS`;
- `AOREBIRTH_MAIL_FROM_NAME` default `AORebirth`;
- `AOREBIRTH_PUBLIC_BASE_URL` default `https://ao-rebirth.com`;
- `AOREBIRTH_EMAIL_VERIFICATION_TOKEN_MINUTES` default `120`;
- `AOREBIRTH_ACCOUNT_BROKER_EMAIL_VERIFY_LIMIT` default `3` per 15 minutes.

SMTP passwords and broker mail secrets must be root-owned files outside source,
web root, MyBB files, and Git; expected mode is `600`.

## Website flow

Website changes are prepared under `E:\AORebirthWebsite`:

- registration reports "verification email was sent" only when the broker
  returns a successful SMTP send;
- `/account` refreshes identity state through the broker and shows a resend
  button only while the email is unverified;
- resend failures do not claim success;
- `/verify-email.php` accepts verification tokens through a fragment-based
  email link, `https://ao-rebirth.com/verify-email.php#token=...`, so the token is not
  sent in the normal HTTP request URL; JavaScript posts the token in the
  request body to complete verification.

## MyBB mail boundary

MyBB remains an Account Broker SSO consumer. It must not become the AORebirth
password reset or password authentication authority.

MyBB notification mail is still not production-enabled. Once the provider is
selected, MyBB should be configured to use the same approved SMTP transport or a
separate scoped provider credential. Native MyBB registration/password recovery
must remain redirected/disabled by the AORebirth Identity Bridge boundary.

## Failure handling

If SMTP is not configured:

- registration still creates the AORebirth/game account through the broker;
- no verification email success is reported;
- resend returns `MAIL_NOT_CONFIGURED`;
- no identity is marked verified.

If SMTP send fails after a token is created:

- the broker supersedes that token;
- the website reports failure;
- the account remains unverified and can retry later.

## Validation

Source validation performed:

- `dotnet build LinuxBuild\Projects\AccountBrokerService.Linux.csproj --configuration Release --nologo`: PASS;
- `dotnet build AORebirth\Server\AccountBrokerService\AORebirth.AccountBroker.Service.csproj --configuration Release --nologo`: PASS;
- `dotnet build Tools\AccountBrokerValidation\AccountBrokerValidation.csproj --configuration Release --nologo`: PASS;
- `Tools\AccountBrokerValidation\bin\Release\AccountBrokerValidation.exe`: PASS `41/41`;
- PHP lint:
  - `E:\AORebirthWebsite\ao\includes\config.php`: PASS;
  - `E:\AORebirthWebsite\ao\includes\account-broker.php`: PASS;
  - `E:\AORebirthWebsite\ao\account.php`: PASS;
  - `E:\AORebirthWebsite\ao\account-register.php`: PASS;
  - `E:\AORebirthWebsite\ao\verify-email.php`: PASS.

Local validation database note: the new
`account_email_verification_tokens` table was created only in the configured
local validation database so the broker validation harness could prove the new
code. No production database schema was changed.

## Production blockers

Production email remains blocked on:

1. Select one real transactional SMTP provider.
2. Obtain provider SMTP host/port/TLS mode/username/password without printing
   or committing credentials.
3. Add only provider-supplied Hostinger DNS records for SPF, DKIM, provider
   verification, and return-path/bounce handling if required.
4. Add an intentional DMARC record after deciding whether a monitored reporting
   mailbox exists.
5. Decide whether inbound MX is needed; outbound transactional mail alone does
   not prove an MX requirement.
6. Apply the new Account Broker email-token schema to production only after an
   approved backup/rollback point.
7. Install root-owned production secret files for SMTP and broker account-mail
   authorization.
8. Rebuild/redeploy the Account Broker and website with the new configuration.
9. Configure MyBB SMTP without enabling MyBB as password authority.
10. Send controlled website verification and MyBB notification messages to
    independent mailboxes and prove received-message SPF, DKIM, and DMARC
    headers.

## Final email status

BLOCKED
