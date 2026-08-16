# MyBB Forum Launch Readiness - 2026-08-15

## Scope

Prepared the accepted AORebirth MyBB 1.8.40 forum for public community launch
without changing the frozen unified account, Account Broker, LoginEngine,
ZoneEngine, game authentication, SSO token, password-hash, character ownership,
or database-isolation architecture.

The launch-prep deployment script is version-controlled at
`MyBBIdentityBridge/deploy/prepare-mybb-public-launch.sh`; the restrained forum
theme layer is version-controlled at
`MyBBIdentityBridge/deploy/aorebirth-forum-launch.css`.

## Production backup

Before production changes, MyBB DB/files were backed up under
`/opt/ao-rebirth/database/backups`.

Valid launch-prep backups include:

- `/opt/ao-rebirth/database/backups/mybb-launch-prep-20260815T115526Z`
- `/opt/ao-rebirth/database/backups/mybb-launch-public-20260815T120211Z`

Each valid backup contains a MyBB SQL dump created with `--no-tablespaces`, a
tarball of shared forum runtime state, and `SHA256SUMS`.

During launch audit, one narrow config inspection printed the MyBB DB password
to the agent transcript. The credential was immediately rotated in MySQL, the
active mounted MyBB config was updated, the persisted MyBB secret file was
synchronized by the launch script, and `ao-rebirth-forum` returned HTTPS `200`
after restart. No old or new credential value is recorded here.

## Theme / presentation

- Theme name: AORebirth public forum launch theme layer.
- MyBB base: stock MyBB Default theme remains the base; this stage adds a
  version-controlled CSS layer and template-level header links.
- CSS deployed to production:
  `/opt/ao-rebirth/forum/current/aorebirth-forum-launch.css`.
- MyBB template changes made through the database, not core file edits:
  - `headerinclude`: links the AORebirth CSS layer.
  - `header`: adds AORebirth Home, Forum, Register, Login/My Account, and
    Downloads navigation.
  - `header_welcomeblock_guest`: removes the stock quick-login modal and points
    guests to AORebirth website login/registration.
- Logo/header: no approved version-controlled forum logo asset was identified
  in AORebirth-owned project files for this stage. The pre-existing MyBB
  `images/logo.png` remains. The untracked local `aor-logo.png` was not used.
- MyBB powered-by branding remains intact.
- Mobile result: the CSS layer adds compact mobile rules for wrapper width,
  logo scaling, nav wrapping, and hiding thread/post/last-post columns on narrow
  forum-index views.

## Forum descriptions and seed content

All live category/forum descriptions were updated for the approved 40-row
traditional forum structure:

- Official: Announcements, Patch Notes, Server Status, Rules & Policies.
- Community: General Discussion, Introductions, Screenshots & Videos,
  Organizations / Guilds, Off Topic.
- Game Discussion: New Player Help, Professions, PvE, PvP, Tradeskills, Items
  & Equipment, Guides & Resources.
- AORebirth Support: Technical Support, Account Support, Bug Reports,
  Connection / Launcher Issues.
- Development: Development News, Server Development, Client Development,
  Content & World Restoration, Suggestions & Feature Requests, Testing / Test
  Server.
- Marketplace: Buying, Selling, Trading, Services.
- Archive: Resolved Bug Reports, Old Patch Discussions, Retired Development
  Threads.

Seed staff content created by `AORebirthAdmin`:

- Announcements: `Welcome to AORebirth`
- Rules & Policies: `Forum Rules`
- Rules & Policies: `How AORebirth Accounts Work`
- Server Status: `Current Server Status`
- Patch Notes: `Patch Notes / Update Index`
- Bug Reports: `How to Report a Bug`
- Technical Support: `Technical Support - Read Before Posting`
- Account Support: `Account Support - Protect Your Account Information`
- Development News: `Current Development Status`
- Suggestions & Feature Requests: `Suggestions / Feature Request Guidelines`
- Introductions: `Introduce Yourself`

The bug-report template includes:

```text
Title:
Character:
Playfield:
Date/Time:
What happened:
What should have happened:
Steps to reproduce:
Client version:
Screenshots/logs:
```

Support and account guidance explicitly tells users not to post passwords,
verification links, session cookies, private account data, or other credentials.

## Groups / permissions

Existing MyBB groups remain:

- Guests
- Registered
- Super Moderators
- Administrators
- Awaiting Activation
- Moderators
- Banned

No game GM authority is granted or inferred from forum role.

Official forums and archive forums are closed/read-only for normal users:

- Announcements
- Patch Notes
- Server Status
- Rules & Policies
- Resolved Bug Reports
- Old Patch Discussions
- Retired Development Threads

Explicit forum permission rows give Guests, Registered, Awaiting Activation,
and Banned groups read access only for those forums. Staff/moderators retain
staff-side moderation paths through MyBB group permissions.

Selected official reply policy:

- Announcements: staff-created, public read, normal-user replies disabled.
- Patch Notes: staff-created, public read, normal-user replies disabled.
- Server Status: staff-created, public read, normal-user replies disabled.
- Rules & Policies: staff-created/edited, public read, normal users cannot
  create topics.

## User features

- Private messages: enabled.
- PM flood interval: 60 seconds.
- Post flood interval: 60 seconds.
- Avatars: enabled, local upload preferred, remote avatars disabled, 100x100
  max display/dimension policy, 100 KB configured avatar size.
- Signatures: 255 characters, BBCode enabled, raw HTML disabled, max one image.
- Attachments: enabled, max five per post, thumbnails enabled at 160x160.
- Executable/script attachment extensions are disabled, including `exe`, `bat`,
  `cmd`, `com`, `scr`, `ps1`, `vbs`, `js`, `jar`, `msi`, `dll`, `php`,
  `phtml`, `sh`, `py`, and `pl`.
- BBCode: enabled for normal forum behavior.
- Raw HTML: disabled for posts, announcements, signatures, and PMs.

## Anti-spam

- Native MyBB registration remains disabled.
- Public registration continues through AORebirth account routes and Account
  Broker.
- MyBB post flood check is enabled at 60 seconds.
- PM flood interval is 60 seconds.
- StopForumSpam email setting remains present from the MyBB install, but
  native MyBB registration is not the public registration path.
- No CAPTCHA or heavyweight anti-spam plugin was installed.
- No separate brand-new-user URL limit was configured in this stage; initial
  risk is reduced by external AORebirth registration plus flood controls.

## Email / notifications

- MyBB sender/contact/return address was set to `forum@ao-rebirth.com`.
- MyBB mail handler remains `mail`, but `enableemail` is set to `0` where the
  setting exists.
- Public DNS check found `forum.ao-rebirth.com A 2.24.96.30`.
- Public DNS TXT/SPF record for `ao-rebirth.com` was not present.
- `_dmarc.ao-rebirth.com` did not resolve.
- DKIM was not proven.
- Authenticated SMTP was not configured.

Email notifications are intentionally blocked/limited for launch until
SPF/DKIM/DMARC and a reliable authenticated transport are configured. MyBB must
not become the AORebirth password-reset authority.

Source-side AORebirth email verification plumbing was later prepared and
validated in
`docs/project/EMAIL_DELIVERY_PRODUCTION_EVIDENCE_20260815.md`, but production
forum notifications remain blocked until a real SMTP provider is selected,
provider-supplied DNS is added, MyBB SMTP is configured, and received-message
SPF/DKIM/DMARC headers are proven.

## Backup / restore / upgrade

Current backup coverage for launch includes:

1. MyBB database SQL dump.
2. Shared MyBB config/settings.
3. Identity Bridge config.
4. Uploads, avatars, attachments, cache, and secrets tarball.
5. Checksums for backup artifacts.

Restore procedure:

1. Stop or isolate `ao-rebirth-forum`.
2. Restore the MyBB database dump to `aorebirth_mybb`.
3. Restore shared forum files under `/opt/ao-rebirth/forum/shared`.
4. Verify `config.php`, `settings.php`, plugin config, uploads, avatars,
   attachments, and secrets permissions.
5. Restore or relink `/opt/ao-rebirth/forum/current` if the release symlink
   changed.
6. Start/recreate only `ao-rebirth-forum` if needed.
7. Verify public HTTPS `/`, CSS, sensitive path denial, native registration
   disabled, Identity Bridge plugin presence, SSO issue/redeem, and existing
   UID mapping reuse.

Upgrade procedure:

1. Re-check the current official stable MyBB release and checksum.
2. Read release and security notes.
3. Back up DB/files.
4. Stage the upgrade away from production.
5. Verify AORebirth Identity Bridge compatibility.
6. Preserve custom CSS/template changes.
7. Apply MyBB upgrade files.
8. Run the MyBB upgrade process.
9. Remove installer/upgrade files as required.
10. Revalidate SSO and native-registration disablement.
11. Revalidate public forum pages, permissions, seed content, and sensitive
    path denial.
12. Roll back from the pre-upgrade backup if required.

Permanent Identity Bridge regression gate for every MyBB upgrade:

- plugin lint
- SSO issue/redeem
- replay rejection
- expiration rejection
- existing UID mapping reuse
- fresh mapping
- native registration disabled
- DB isolation

## Health monitoring

Lightweight checks used for launch:

- `https://forum.ao-rebirth.com/` returns `200`.
- `https://forum.ao-rebirth.com/aorebirth-forum-launch.css` returns `200`.
- `/install/` returns `404`.
- `/inc/config.php` returns `403`.
- `/.env` returns `403`.
- `/secrets/` returns `404`.
- `ao-rebirth-forum` container is running.
- `ao-rebirth-website` container is healthy.
- `ao-rebirth-accountbroker.service` is active.
- `ao-rebirth-db-backup.timer` is active.

Forum health is not a dependency for AO game service health.

## Acceptance results

- Guest forum index: PASS.
- Guest navigation to AORebirth Home/Register/Login/My Account/Downloads:
  PASS.
- Native MyBB registration disabled: PASS.
- Public CSS load: PASS.
- Sensitive path denial: PASS.
- Registered user SSO through AORebirth website: PASS with a disposable
  controlled account.
- Registered user create-thread path: PASS; the temporary thread/post was
  removed.
- Moderator web-action acceptance: BLOCKED. A disposable moderator-account
  attempt reached the moderation-action stage but did not complete sticky/close
  proof through web moderation URLs. The temporary thread/post was removed and
  temporary `AORM*` users were demoted back to Registered.
- Administrator acceptance: PARTIAL. Public Admin CP is HTTPS and requires
  username/password plus the configured secret PIN. Admin credentials were not
  used or exposed in this stage, so full Admin CP user/group/board management
  was not browser-tested.
- Email transport: BLOCKED for production notification use until DNS mail
  records and authenticated SMTP are configured.
- SSO replay/expiry/DB-isolation regression: not modified in this stage; frozen
  production evidence remains
  `docs/project/MYBB_FORUM_SSO_PRODUCTION_EVIDENCE_20260815.md`.

## Production services observed

- `ao-rebirth-forum`: running.
- `ao-rebirth-website`: running/healthy.
- `ao-rebirth-mysql`: running/healthy.
- `ao-rebirth-accountbroker.service`: active.
- `ao-rebirth-db-backup.timer`: active.
- `ChatEngine`: inactive at observation time.
- `LoginEngine`: inactive at observation time.
- `ZoneEngine`: inactive at observation time.

## Launch status

BLOCKED.

Remaining concrete blockers:

1. Complete live moderator acceptance with a controlled moderator account and a
   verified MyBB moderation workflow for sticky, close/open, move, and report
   handling.
2. Complete full Admin CP acceptance with authorized admin credentials without
   exposing secrets.
3. Configure and prove reliable forum email transport if launch requires email
   notifications: authenticated SMTP plus SPF, DKIM, and DMARC for
   `ao-rebirth.com`.
