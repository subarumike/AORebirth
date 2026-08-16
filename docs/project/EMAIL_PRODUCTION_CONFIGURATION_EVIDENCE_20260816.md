# AORebirth Email Production Configuration Evidence - 2026-08-16

## Scope

This stage moves the already implemented AORebirth email-verification
foundation from source-only into a production-ready code/schema position while
keeping outbound mail disabled until real provider credentials, provider DNS,
and received-message authentication proof exist.

No LoginEngine authentication, Account Broker password semantics, AO game login
behavior, MyBB SSO design, MyBB password authority, or password hashing was
changed.

## Provider decision

Selected production provider: Postmark.

Selected sender identity:

```text
AORebirth <noreply@ao-rebirth.com>
```

Selected SMTP configuration contract:

```text
AOREBIRTH_MAIL_SMTP_HOST=smtp.postmarkapp.com
AOREBIRTH_MAIL_SMTP_PORT=587
AOREBIRTH_MAIL_SMTP_TLS=StartTls
AOREBIRTH_MAIL_FROM_ADDRESS=noreply@ao-rebirth.com
AOREBIRTH_MAIL_FROM_NAME=AORebirth
AOREBIRTH_PUBLIC_BASE_URL=https://ao-rebirth.com
```

The SMTP username/password must be installed as root-owned production secret
files only after the Postmark sender domain is verified. They were not created,
printed, committed, or logged in this stage.

Rationale:

- Postmark is transactional-email focused and has direct SMTP support for
  account verification and forum notifications.
- Postmark documents `smtp.postmarkapp.com` and STARTTLS-capable port `587`.
- Postmark documents sending-domain authentication with DKIM and a custom
  Return-Path for SPF alignment.
- SES is cheaper but adds AWS account, region, sandbox, and custom MAIL FROM
  operational complexity that is unnecessary for the first low-volume launch.
- Resend is viable and inexpensive, but Postmark is the more conservative
  transactional-deliverability choice for account verification.

Provider source references:

- https://postmarkapp.com/pricing
- https://postmarkapp.com/developer/user-guide/send-email-with-smtp
- https://postmarkapp.com/support/article/how-do-i-verify-a-domain
- https://postmarkapp.com/support/article/910-how-do-i-add-a-custom-return-path
- https://postmarkapp.com/support/article/how-do-i-set-up-spf-for-postmark

## DNS plan and current DNS state

Read-only DNS checks for `ao-rebirth.com` on 2026-08-16:

- authoritative nameservers: `apollo.dns-parking.com`,
  `athena.dns-parking.com`;
- DNS provider: Hostinger;
- MX: no public MX answer; resolver returned SOA authority only;
- apex TXT/SPF: no public TXT answer; resolver returned SOA authority only;
- DMARC: `_dmarc.ao-rebirth.com` did not resolve;
- DKIM: no Postmark selector/value exists in DNS because the Postmark sending
  domain has not yet been created and verified.

Required Hostinger DNS records:

1. Add the exact Postmark-supplied DKIM TXT hostname/value from Postmark DNS
   Settings. Do not invent this selector or value.
2. Enable and add Postmark custom Return-Path. The expected default is:

   ```text
   pm_bounces.ao-rebirth.com CNAME pm.mtasv.net
   ```

   If the Postmark dashboard supplies a different hostname/value, the dashboard
   value is authoritative.
3. Add DMARC only after deciding whether a monitored reporting mailbox exists.
   Without a reporting mailbox, the safe starter record is:

   ```text
   _dmarc.ao-rebirth.com TXT "v=DMARC1; p=none; adkim=r; aspf=r"
   ```

No MX record is required for outbound transactional verification mail unless
AORebirth intentionally creates a monitored inbound mailbox for replies,
bounces outside Postmark Return-Path handling, or abuse/support handling.

No DNS records were added or changed in this stage.

## Production database

Production backup before schema change:

```text
/opt/ao-rebirth/database/backups/email-production-20260816T002205Z/account-identity-pre-email.sql.gz
/opt/ao-rebirth/database/backups/email-production-20260816T002205Z/SHA256SUMS
```

`SHA256SUMS` verification passed after the backup was written.

Repository migration:

```text
AORebirth/Libraries/Source/AORebirth.Database/Migrations/20260816_account_email_verification_tokens.sql
```

Production migration copy:

```text
/opt/ao-rebirth/database/migrations/20260816_account_email_verification_tokens.sql
```

Production migration result:

- `account_email_verification_tokens` exists;
- primary key exists;
- `UX_account_email_verification_tokens_hash` exists;
- `IX_account_email_verification_tokens_identity_state` exists;
- foreign key targets `account_identities.IdentityId`;
- no password, login, character, MyBB, or mapping table was modified.

## Production Account Broker deployment

Account Broker release deployed:

```text
/opt/ao-rebirth/accountbroker/releases/email-foundation-20260816-002
```

Previous release:

```text
/opt/ao-rebirth/accountbroker/releases/mybb-sso-20260815-001
```

Deployment notes:

- The first framework-dependent release attempt failed because the VPS does not
  have the matching .NET runtime registered for that apphost.
- The rollback guard restored the previous release and restarted the broker.
- The failed release artifact was removed.
- The self-contained release `email-foundation-20260816-002` deployed
  successfully.

Post-deployment checks:

- `/opt/ao-rebirth/accountbroker/current` points to
  `email-foundation-20260816-002`;
- `ao-rebirth-accountbroker.service` is active;
- broker health endpoint on `http://172.18.0.1:7510/health` passed;
- no `AOREBIRTH_MAIL_*`, `AOREBIRTH_PUBLIC_BASE_URL`, or
  `AOREBIRTH_ACCOUNT_BROKER_ACCOUNT_MAIL_SECRET*` variables are configured in
  `/etc/ao-rebirth/accountbroker/accountbroker.env`.

The deployed broker therefore contains the email-verification code path but
cannot send mail and cannot accept website mail-control calls yet. This is the
intended fail-closed state until provider setup is complete.

## MyBB notification boundary

MyBB notification mail is not production-enabled in this stage.

Required MyBB work after Postmark DNS and SMTP credentials are real:

1. Configure MyBB to use authenticated SMTP through the approved Postmark
   transport or a separate scoped Postmark server token.
2. Preserve the AORebirth Identity Bridge boundary: MyBB must not provide
   AORebirth password reset, password authentication, or native registration
   authority.
3. Send a controlled MyBB notification to an independent mailbox.
4. Prove received-message SPF, DKIM, and DMARC alignment from message headers.

## Acceptance not performed

The following production acceptance steps were intentionally not performed:

- real website registration email send;
- real resend verification email send;
- received verification email link click;
- received-message SPF/DKIM/DMARC header proof;
- MyBB notification send;
- MyBB received-message SPF/DKIM/DMARC header proof.

Reason: the Postmark account/domain is not configured, provider DKIM values do
not exist yet, Hostinger DNS has not been updated with provider records, and no
SMTP credential has been supplied.

## Rollback

Broker rollback if required:

1. Relink `/opt/ao-rebirth/accountbroker/current` to
   `/opt/ao-rebirth/accountbroker/releases/mybb-sso-20260815-001`.
2. Restart `ao-rebirth-accountbroker.service`.
3. Verify `systemctl is-active ao-rebirth-accountbroker.service`.
4. Verify `http://172.18.0.1:7510/health`.

Database rollback if required before any tokens are created:

1. Stop Account Broker or disable the email-verification code path.
2. Restore from
   `/opt/ao-rebirth/database/backups/email-production-20260816T002205Z/account-identity-pre-email.sql.gz`.
3. Verify identity/account/game-login acceptance.

After real verification tokens exist, schema rollback requires explicit review
because token rows may represent live account state.

## Final email status

BLOCKED

Blocking items:

1. Create/approve the Postmark account and verified sender domain for
   `ao-rebirth.com`.
2. Add exact Postmark DKIM TXT and Return-Path CNAME records at Hostinger.
3. Add an intentional DMARC record.
4. Install root-owned SMTP and broker account-mail secret files without
   printing or committing credentials.
5. Configure MyBB SMTP after the provider is verified.
6. Send controlled website and MyBB emails to independent mailboxes.
7. Prove received-message SPF, DKIM, and DMARC headers.
