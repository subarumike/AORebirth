# AORebirth Email Production Configuration Evidence - 2026-08-16

## Scope

This stage moves the already implemented AORebirth email-verification
foundation from source-only into a production-ready code/schema position and
switches the production mail plan from a third-party transactional provider to
the controlled VPS mail stack.

No LoginEngine authentication, Account Broker password semantics, AO game login
behavior, MyBB SSO design, MyBB password authority, or password hashing was
changed.

## Provider decision

Selected production provider: self-hosted AORebirth mail on the production VPS.

The VPS already runs Postfix, Dovecot, and OpenDKIM for other Mike-controlled
domains. AORebirth now extends that stack instead of relying on Postmark or
another third-party transactional host.

Selected sender identity:

```text
AORebirth <noreply@ao-rebirth.com>
```

Selected SMTP configuration contract:

```text
AOREBIRTH_MAIL_SMTP_HOST=mail.twidbits.com
AOREBIRTH_MAIL_SMTP_PORT=587
AOREBIRTH_MAIL_SMTP_TLS=StartTls
AOREBIRTH_MAIL_SMTP_USERNAME=noreply@ao-rebirth.com
AOREBIRTH_MAIL_FROM_ADDRESS=noreply@ao-rebirth.com
AOREBIRTH_MAIL_FROM_NAME=AORebirth
AOREBIRTH_PUBLIC_BASE_URL=https://ao-rebirth.com
```

`mail.twidbits.com` is used as the authenticated SMTP host because the VPS
currently has working reverse DNS and mail TLS identity there. This does not
change the AORebirth sender domain: messages are sent as `ao-rebirth.com` and
signed with the AORebirth DKIM key.

Rationale:

- Mike explicitly chose controlled self-hosted mail over third-party
  transactional hosting.
- The existing VPS mail stack already supports authenticated SMTP on port
  `587`, Dovecot SASL, virtual mailboxes, and OpenDKIM signing.
- Self-hosting keeps the full mail pipeline under AORebirth/Mike control.
- The tradeoff is that AORebirth now owns deliverability, IP reputation,
  blacklist monitoring, bounce handling, and Gmail/Outlook acceptance
  diagnostics.

## DNS plan and current DNS state

Read-only DNS checks for `ao-rebirth.com` on 2026-08-16:

- authoritative nameservers: `apollo.dns-parking.com`,
  `athena.dns-parking.com`;
- DNS provider: Hostinger;
- MX: no public MX answer; resolver returned SOA authority only;
- apex TXT/SPF: no public TXT answer; resolver returned SOA authority only;
- DMARC: `_dmarc.ao-rebirth.com` did not resolve.

Required Hostinger DNS records:

```text
A     mail                 2.24.96.30
MX    @                    10 mail.ao-rebirth.com
TXT   @                    v=spf1 ip4:2.24.96.30 -all
TXT   _dmarc               v=DMARC1; p=none; adkim=r; aspf=r
TXT   aor20260816._domainkey v=DKIM1;h=sha256;k=rsa;p=MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA57JMyzCdv8BqIGys70R8nYoYLqn1Kr3zvoVZ7dKjwEp60hOLKV8S0hW/4EUeVvJ2O1Rkf4mZgCoyOOm+s42LIrFqZaYE2xTdDebjDLU0T/+bN3HThJrm0nov2UztQR68g+98cj9tZtGMIrI8EPk+VzfsJ+t1kopEl9PO4nQbOZ/WJXgvo+7AXFrX3Xjwh99Vb1hWqhwK5RCIS0q6gGr2n9TBTxWadLkJIszawu1pTOVpUKMrAvjpUw8DwAMetXsn6T4TgVYfXZ2Hd6k8xxWGr9Ufu3TPvQfpPXUoF7fSWj7VVzMtzwXqpH31I0KxaT9QbGJ3gBUNB/fPwQLz9ok75wIDAQAB
```

TTL `300` is acceptable for all records during cutover.

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
intended fail-closed state until public DNS is live and received-message
authentication is proven.

## Production VPS mail stack

AORebirth mail-domain configuration was added to the existing VPS mail stack.

Backup before mail configuration change:

```text
/root/aorebirth-mail-backups/20260816T004712Z
```

Configured mail identities:

```text
noreply@ao-rebirth.com
forum@ao-rebirth.com
postmaster@ao-rebirth.com -> forum@ao-rebirth.com
abuse@ao-rebirth.com -> forum@ao-rebirth.com
```

Secret handling:

- SMTP passwords were generated on the VPS only;
- plaintext SMTP passwords are stored only in root-owned files under
  `/etc/ao-rebirth/mail`;
- Dovecot stores SHA-512 password hashes in `/etc/dovecot/users`;
- no SMTP password was printed, committed, or copied into the repository.

Postfix/Dovecot/OpenDKIM changes:

- `ao-rebirth.com` added to `virtual_mailbox_domains`;
- `noreply@ao-rebirth.com` and `forum@ao-rebirth.com` added to virtual mailbox
  maps;
- sender-login maps allow each AORebirth SMTP identity to send only as itself;
- OpenDKIM selector `aor20260816` generated for `ao-rebirth.com`;
- OpenDKIM signs `*@ao-rebirth.com`;
- Postfix, Dovecot, and OpenDKIM remained active after reload/restart.

Validation:

- `postfix check`: PASS;
- Postfix map lookup for `noreply@ao-rebirth.com`: PASS;
- Postfix map lookup for `forum@ao-rebirth.com`: PASS;
- Dovecot SMTP auth for `noreply@ao-rebirth.com`: PASS;
- Dovecot SMTP auth for `forum@ao-rebirth.com`: PASS;
- local-only send from `noreply@ao-rebirth.com` to
  `forum@ao-rebirth.com`: PASS;
- local-only DKIM header with `d=ao-rebirth.com`: PASS;
- local test message removed after validation.

## MyBB notification boundary

MyBB notification mail is not production-enabled in this stage.

Required MyBB work after Hostinger DNS is live:

1. Configure MyBB to use authenticated SMTP through the self-hosted VPS
   transport with `forum@ao-rebirth.com`.
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

Reason: Hostinger DNS has not been updated with the AORebirth MX/SPF/DKIM/DMARC
records, and received-message authentication cannot be proven before public DNS
is live.

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

1. Add the AORebirth MX/SPF/DKIM/DMARC records at Hostinger.
2. Wait for public DNS propagation and verify `mail.ao-rebirth.com`,
   `ao-rebirth.com` MX, SPF, DKIM, and DMARC.
3. Configure Account Broker SMTP and account-mail secret files from the
   already-generated VPS secrets.
4. Configure MyBB SMTP with `forum@ao-rebirth.com`.
5. Send controlled website and MyBB emails to independent mailboxes.
6. Prove received-message SPF, DKIM, and DMARC headers.
