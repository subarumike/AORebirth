# Current Task

## Active

Harvest one complete live Rubi-Ka mission-terminal offer cohort for every
target QL 1-250. The generated plan assigns every target exactly once to an
exact character-level/difficulty-slot pair and the AOSharp harvester now
resolves target QL to the exact slot, fails closed when a target is unavailable,
and reports completion plus output location. Mike owns AO client, plugin, and
terminal interaction; Codex analyzes only completed session folders. Use
`docs/mission-harvest/mission-ql-1-250-plan.md` as the literal runbook. Do not
substitute a nearby QL, infer response-side mission QL from request metadata, or
change AORebirth mission generation from static planning evidence. Harvester
capture-contract version 2 must preserve the request-time terminal origin,
mission destination, exact known icon type, reward-item descriptors, and every
AOSharp-exposed offer field; a finite roll sample is never proof of pool
exhaustion.

## Prior active checkpoint

Crash-reconnect zombie-session fix is production accepted. Windows-authoritative
source commit `fe6617b3bcd1d3806eddd4dbbb91e9c6680ef499` deployed to Linux
ZoneEngine release `reconnect-fe6617b3` and passed live official-client
acceptance: first fast reconnect under 30 seconds was immediately playable,
old timer deadline had no effect, reconnect after timeout passed, fast reconnect
repeat was 3/3, no ZoneEngine restart occurred, and final `ONLINE_COUNT=0`.
Later Windows `master` commit `5d0a84960df961e504f8761da46521d9968b8cd8`
contains client-patch-only work and does not require a ZoneEngine redeploy.
Evidence: `docs/evidence/CRASH_RECONNECT_LIVE_ACCEPTANCE_20260818.md`.

Linux login/inventory follow-up after `login-hydration-b1c61405` is now source
validated. DailyLogin no longer uses Windows XAMPP claim/reward paths
unconditionally on Linux; claim roots resolve through
`AO_REBIRTH_DAILY_LOGIN_CLAIMS_ROOTS`, then
`AO_REBIRTH_ZONE_STATE_DIR/daily-login/claims`, with legacy XAMPP roots retained
only for Windows runtime compatibility. Live read-only SQL for `Nanotechnica`
proved visible startup inventory persists under `ContainerType=39` and normal
inventory page `ContainerInstance=104`; the earlier
`ContainerInstance=39` query was looking at the wrong column. Validation:
focused DailyLogin contract PASS, Windows debug build PASS, AOtomation
messaging PASS 1018/1018, Linux ZoneEngine publish/offline smoke PASS,
production ZoneEngine release `dailylogin-path-360b3002` active with
startup/database preflight PASS and port `7501` listening.
Evidence: `docs/evidence/LOGIN_INVENTORY_DAILYLOGIN_FOLLOWUP_20260817.md`.

## Prior carried state

Public unified account/forum infrastructure remains frozen and accepted.
Forum community-launch preparation has been applied on production: restrained
AORebirth forum CSS, AORebirth header navigation, guest login/register links
pointing back to AORebirth account routes, final board descriptions, seed
threads, rules/support/bug-report guidance, official/archive read-only
permissions for normal users, conservative PM/avatar/signature/attachment
settings, backup coverage, and registered-user SSO/posting acceptance.
Source-side email verification plumbing is prepared but not production-enabled:
the Account Broker owns hashed one-time verification tokens, SMTP-backed send
configuration, resend, and verify endpoints; the website has accurate resend
and fragment-based verify pages. Evidence:
`docs/project/EMAIL_DELIVERY_PRODUCTION_EVIDENCE_20260815.md`.
Production email provider selection and fail-closed deployment are now recorded:
self-hosted VPS mail is selected, the production token-table migration is
applied after a backup, Account Broker release `email-foundation-20260816-002`
is deployed and healthy without SMTP/account-mail app configuration, and the
VPS Postfix/Dovecot/OpenDKIM stack is configured for `ao-rebirth.com`. Email
now sends through the self-hosted VPS mail stack: Hostinger MX/SPF/DKIM/DMARC
records resolve, Account Broker verification resend to an external mailbox
passed, and MyBB SMTP notification to an external mailbox passed. Production
email is accepted for launch: `SubaruMike` received the verification email,
clicked the link, and production now shows the account email as verified.
Evidence:
`docs/project/EMAIL_PRODUCTION_CONFIGURATION_EVIDENCE_20260816.md`.
Unified account character display is now integrated on production through the
Account Broker. Release `account-characters-20260816-001` is deployed and
healthy on `172.18.0.1:7510`; `/account` renders a read-only My Characters
section from the authenticated unified `AOR_IDENTITY` session, and the broker
queries the live Stage6 `characters` table with
`characters.Username = CanonicalUsername`. `SubaruMike` route acceptance shows
one live character, a controlled zero-character identity renders the empty
state, unauthenticated `/account` redirects, posted username tampering is
ignored, and `/member-index.php` remains blocked at the Apache boundary.

Launch status is currently BLOCKED only on final live moderator acceptance,
authorized Admin CP acceptance, and production-grade email transport/DNS
(`SPF`/`DKIM`/`DMARC` plus authenticated SMTP) if email notifications are
required for launch. Evidence:
`docs/project/MYBB_FORUM_LAUNCH_READINESS_20260815.md`.

## Current checkpoint

- Unified password management is production-accepted. Account Broker release
  `password-management-8fd1300f` and website revision `c79468e6` provide
  `/forgot-password`, `/reset-password`, and `/account/password`; the additive
  token migration, real email delivery, website session invalidation, token
  lifecycle, MyBB regression, and final LoginEngine credential path all pass.
  Evidence: `docs/project/PASSWORD_MANAGEMENT_PRODUCTION_EVIDENCE_20260831.md`.
- Password authentication is restored and proven in Debug and Release.
- The proposed identity schema now validates against the local Windows
  development MySQL target.
- The first internal Account Broker foundation is implemented and validated.
- The loopback Account Broker HTTP service now exposes local registration,
  login, current-session, logout, and health endpoints.
- Windows-local unified account flow validation passes in Debug and Release.
- Production Account Broker release `mybb-sso-20260815-001` is deployed and
  healthy on `172.18.0.1:7510`.
- Production Account Broker release `email-foundation-20260816-002` is deployed
  and healthy on `172.18.0.1:7510`; SMTP/account-mail secrets are intentionally
  absent, so mail remains fail-closed.
- Public `/register`, `/login`, `/account`, and `/logout` are enabled on
  `ao-rebirth.com`.
- Public `/account` now includes read-only My Characters display backed by the
  Account Broker and live Stage6 character data. No character schema, character
  rows, LoginEngine behavior, or game authentication was changed.
- Public registration created a controlled production account through the
  broker only; database proof shows one identity row, one linked `login` row,
  one linked game mapping, and normal non-GM account flags.
- Website wrong-password, correct-password, account, logout, duplicate,
  validation, rate-limit, and broker-unavailable failure paths passed.
- Production LoginEngine protocol acceptance passed for the controlled
  website-created account: correct password reached `CHARACTER_LIST`, wrong
  password reached `LOGIN_ERROR`.
- Exposed MySQL root and `aorebirth_stage6` credentials were rotated, old values
  were rejected, and ChatEngine/LoginEngine/ZoneEngine/AccountBroker remained
  healthy after deployment.
- LoginEngine and ZoneEngine Linux database preflights now allow only the six
  governed Account Broker extension tables in addition to the 34 governed game
  tables.
- Legacy PHP account routes remain blocked.
- MyBB 1.8.40 is installed under `/opt/ao-rebirth/forum`, native MyBB
  registration is disabled, the AORebirth Identity Bridge plugin is active, and
  controlled SSO E2E passed with Account Broker external mapping.
- Final cutover-safe production work completed while public DNS remains
  blocked:
  - website Forum SSO handoff now posts the one-time code instead of placing it
    in the callback URL query string;
  - approved 40-row traditional forum board structure is live;
  - MyBB cookie domain was narrowed by clearing `cookiedomain`;
  - controlled acceptance accounts were disabled and game login hashes rotated
    after zero-character/zero-post proof;
  - MyBB credential isolation, sensitive path checks, forum-container failure
    isolation, runtime log scan, and cutover backup passed.
- Final public production forum acceptance passed:
  - Hostinger DNS `forum.ao-rebirth.com A 2.24.96.30` TTL `300`;
  - Let's Encrypt production certificate issued for `forum.ao-rebirth.com`;
  - public HTTP redirects to HTTPS and public forum homepage returns `200`;
  - public SSO creates exactly one MyBB UID and one external mapping;
  - second SSO reuses the same UID/mapping;
  - replay, expired, malformed, and unknown codes are rejected;
  - SSO codes do not appear in request URLs or current URL logs;
  - cookies are Secure/SameSite and session cookies are HttpOnly;
  - final controlled test accounts were disabled and their game passwords
    rotated;
  - final backup exists at
    `/opt/ao-rebirth/database/backups/mybb-public-acceptance-20260815T094721Z`.

## Remaining gates

- No remaining MyBB/forum infrastructure architecture gate is open.
- Email provider selection is complete with self-hosted VPS mail, and the
  production broker/schema/mail-server/app configuration is in place. Broker
  verification email and MyBB notification email were accepted by an external
  mailbox provider. `SubaruMike` verification-link acceptance passed and the
  production account email is verified. Production email is accepted for launch.
- Forum presentation/content launch prep is applied, but community launch is
  blocked until:
  - live moderator sticky/close-open/move/report acceptance passes with a
    controlled moderator account;
  - Admin CP user/group/board/plugin/theme management acceptance passes with
    authorized admin credentials and without exposing secrets;
  - forum email is either intentionally launched without notifications or
    reliable SMTP plus SPF/DKIM/DMARC is configured and proven.
- Repository baseline freeze is in progress under explicit approval. Runtime
  baseline commits are:
  - AORebirth:
    `76258f8fc55a8220d63ef11f9aa039139e2870f6`;
  - website:
    `1ecd84fc44457a0ced44b5f0399ead0eeb654ae3`.
- After the baseline commits are pushed, future account/forum work should stay
  limited to presentation, content, moderation, email/notification
  configuration, and launch preparation unless a proven production defect is
  found.

## Constraints

- Do not redesign the AO login protocol.
- Do not replace the existing password-hash format.
- Do not change character ownership in this stage.
- Do not perform destructive database operations.
- Do not enable legacy website registration/login pages.
- Do not launch the AO client without explicit current authorization.
