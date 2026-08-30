# AORebirth Project State

Updated: 2026-08-28

This file is the concise current source of truth. The pre-cleanup long-form
state is preserved at
`docs/archive/project/PROJECT_STATE_PRE_BASELINE_CLEANUP_20260801.md`; subsystem
completion matrices and dated evidence retain detailed provenance.

## Acceptance baseline

- Development-only ACG placement visualization: the checksum-pinned
  `18.8.62_EP1` portable atlas is imported deterministically into lazy
  per-playfield shards with 32,805 primary records, 32,737 additional points,
  65,542 coordinates, 4,016 native ACG keys, 459 populated playfields, and the
  explicit PF103/PF615/PF4805 malformed boundaries. `Off` is the default,
  Release builds reject activation, one configured current playfield is loaded,
  and inert placeholders retain native-key visual evidence and full source
  provenance without changing Legacy or production spawn authority. Evidence
  grades remain one `ExactOfficial`, four capture-correlated, and 4,011
  unresolved. See `docs/reference/ACG_DEVELOPMENT_PLACEHOLDERS.md`.
  Capture-correlated and unresolved development entities use the explicit
  `default_monster.cir` CatMesh `26884`; FDQO retains exact CatMesh `15222`.

- ACG placement/spawn-policy schema: the official `18.8.62_EP1`
  ResourceDatabase type-`1000014` corpus is the authoritative placement layer.
  All 32,805 decoded `HashSpawnPoint_t` records now have exact raw bytes,
  versioned field boundaries, native accessor evidence, per-field statistics,
  and official-decode-only placement readiness. The 16 scalar fields contain
  proven position, radius, rotation midpoint/width, ACGHash, level range,
  respawn chance/time, and serialized-section presence; three further fields
  are strongly corroborated flag/range roles and one optional byte remains
  unknown. The former 507,976-byte trailing opaque region is fully decoded as
  `PlayfieldDistrictInfo_t::GetZoneToDistrictIndex`; only 45 inactive
  allocation-slack bytes remain opaque. All 32,805 known rows are placement
  ready, while resources 103, 615, and 4805 remain explicit resource-level
  parser limits without synthetic rows. Evidence and the lossless catalog are
  under `docs/generated/acg_placement_schema` and governed by
  `docs/reference/ACG_PLACEMENT_SCHEMA.md`.

- AO Spawn Population Reconstruction: the first deterministic population layer
  now keeps 32,805 official ACG placements, 3,472 captured runtime observations,
  server-selected MonsterData/archetypes, and transient runtime identities on
  explicit evidence axes. It produces 18,423 topology/runtime population
  records: zero exact-placement associations, 766 local-population
  associations, 1,397 playfield-population associations, 1,029 unassociated
  observations, and 280 conflicts. Its playfield/district/ACGHash buckets and
  25-metre spatial components are analytical indexes, not proven Funcom
  grouping structures. Population association never claims exact-row
  ownership; its 22 identity-ready rows are optional runtime enrichment and
  never gate official placement validity. Evidence, readiness inventory,
  reuse statistics, and Leet/PF4582/Borealis studies are under
  `docs/generated/spawn_populations`.

- ACG-to-MonsterData resource-chain audit: both verified `18.8.62_EP1` and
  `18.8.62_EP2` clients expose byte-identical ACG, PlayfieldDynels, CATMesh, and
  MonsterData records through one active B-tree view. Across 460,193 active
  resources, 32,805 ACG placements, and 1,470 MonsterData records, no direct or
  indirect official static ACG-to-MonsterData edge exists. The proven client
  spawn path instead consumes server-authored SimpleChar stat 359 as resource
  instance `1040023:<MonsterData>`, so the accepted relation is
  `SERVER_RUNTIME_ASSOCIATION`. The separate 1040005 Nano stat-359 references
  are spell/morph data and do not establish placement identity. Evidence and
  deterministic catalogs are under
  `docs/generated/acg_monsterdata_resource_audit`.

- Official NPC visual archetype census: the verified `18.8.62_EP1` client
  resource chain now catalogs all 1,470 MonsterData records against 856
  referenced CATMesh resources. It yields 1,360 exact complete visual
  signatures and 750 broader structural base-model families; MonsterData does
  not prove an enemy-only subset, so no single guessed `ARCHETYPE_COUNT` is
  declared. The census associates 3,470 of 3,472 retained runtime observations
  through MonsterData/CATMesh evidence without requiring exact placement
  identity. All 32,805 ACG placement associations remain explicitly unresolved
  because the official client contains no ACGHash-to-MonsterData resolver.
  Leet and Heckler case studies, top-20 reuse, source hashes, unknown
  relationships, and deterministic digest are published in
  `docs/generated/enemy_archetypes/enemy-archetype-census-report.md`.

- NPC identity bridge evidence acquisition: the preserved Arete live sample
  `20260827-213046` proved clean raw capture but exposed polling-heavy client
  enrichment and a first-discovery evidence-floor defect. The repaired explicit
  `--npc-identity-bridge` AOSharp mode now records direct runtime and loaded
  playfield model state, runtime NPC identity, raw SCFU/Stat
  references, client-visible stats, world position/rotation, native zone/cell,
  and pointer-diagnostic lifecycle lineage inside deterministic zone epochs.
  Event-first dirty state, bounded retries, ten-field client reads, duplicate
  suppression, and throttled serialization replace 2,202 repeated full-stat
  snapshots. Offline replay can reconstruct omitted packet links only by exact
  raw key, epoch, runtime identity, and lineage window. Optional resolver
  consumption remains fail-closed for stale epochs, absent/derived identities,
  ACGHash, unproven transforms, and existing duplicate ambiguity. The client
  audit found no NPC-specific official model/template ID, stable official
  district ID, or proven world-to-placement transform. The live sample proves
  no unique placement. Evidence: `docs/reference/NPC_IDENTITY_BRIDGE_CAPTURE.md`
  and `docs/evidence/ARETE_NPC_IDENTITY_BRIDGE_FAILED_CAPTURE_20260827.md`.

- NPC placement identity resolution: the deterministic
  `NpcPlacementIdentityResolver` now expands all 32,805 normalized official
  placements and 146 retained field paths, quantitatively tests coordinate and
  district-transform hypotheses, distinguishes row runtime epochs from frozen
  capture resource labels, clusters all 3,325 observations conservatively, and
  emits only non-mutating promotion eligibility. The current proof result is
  zero unique placements: 680 radius/proxy candidate sets remain ambiguous,
  2,365 observations are unmatched, and 280 phase/mapping or repeated-lineage
  conflicts are rejected. Direct X/Y/Z is the strongest diagnostic; PF4582 exact X/Z
  repetition strongly corroborates those axes and scale, but Y remains
  different and no full coordinate transform, runtime-to-official-base map,
  district map, template join, or placement-specific runtime identifier is
  proven. Borealis Guide and Guard appearances remain exact, with one PF954
  radius candidate each after the PF3081 mixed-zone capture is partitioned,
  but neither is unique. All 3,325 promotion records remain blocked, ACGHash is
  excluded from runtime identity, and no runtime NPC definition changed.
  Evidence: `docs/reference/NPC_PLACEMENT_IDENTITY_RESOLVER.md`.

- NPC observation harvesting foundation: the deterministic database-wide
  `NpcObservationHarvester` now replays retained raw SCFU and ordinary Stat
  packets, preserves exact appearance arrays and field provenance, rejects AO's
  `1234567890` unset sentinel from authoritative state, inventories all 358
  accepted captures across repository and historical roots without pruning,
  and reconciles 3,472 captured NPC observations against 32,805 governed
  official placements. Exact playfield plus float32-coordinate matching found
  no unique placement bridges; official-radius containment identified 299
  heuristic candidate sets that remain explicitly ambiguous, with 3,173
  observations unmatched. All promotion candidates therefore remain blocked
  and no runtime NPC definition was mass rewritten. The PF3081 Borealis Guide
  and Guard appearance regressions pass;
  ordinary Stat replay passes; seven historical captures retain explicit raw
  integrity/SCFU replay failures rather than being treated as field-complete.
  Evidence schema and workflow: `docs/reference/NPC_OBSERVATION_HARVESTER.md`.

- Playfield hydration Stage 0/1: the current mixed loader and `Playfield`
  constructor remain the sole production authority behind a narrow legacy-only
  composition seam. Static definition, validation, provenance, canonical
  SHA-256, comparison, and runtime-materialization contracts are present, but no
  DAO shadow hydrator, allowlist, new runtime configuration, database change, or
  production behavior change is enabled. Evidence:
  `docs/architecture/PLAYFIELD_HYDRATION_SOURCE_INVENTORY.md` and
  `docs/architecture/PLAYFIELD_HYDRATION_MIGRATION.md`.

- First-party bot service foundation: AORebirth now has a storage-neutral bot principal,
  versioned service credential, scoped authorization, revocation-aware session, structured
  audit, and per-bot rate-limit model. A dedicated versioned HMAC-authenticated loopback TCP
  boundary connects BotService contracts to a narrow ChatEngine adapter for tells,
  organization chat, and channels without using player passwords, legacy Funcom auth,
  public chat framing, or ISCom. Account Broker additions are contracts only; no bot schema,
  persistence endpoint, deployment, or public WebSocket gateway exists yet. Port `6996`
  remains semantically and operationally dedicated to ZoneEngine ISCom.

- DailyLogin VGTP routing audit: the AORebirth website now hosts the DailyLogin
  web app and its claim endpoint returns live account board state, but the
  in-game client still opens `vgtp://uwg.daily.icc-rk/index.app` through the
  official AO browser/VGTP path. A global hosts/DNS override is rejected because
  it would hijack official Rubi-Ka/RK2019 DailyLogin traffic. The correct fix is
  process-local, dimension-aware routing in the private client patch /
  `version.dll` layer. The newer AORebirth private client patch source has been
  reconciled into authoritative Windows `master` at
  `Tools\AOClientRoomSpaceGuard\ProxyDll`. Source commit `b60b7ca6` builds a
  versioned v2 combined patch containing both crash-repair/RoomSpace protection
  and endpoint-aware AORebirth login-key behavior. The currently published
  installer and installed local DLL remain the earlier accepted v1 lineage and
  were not replaced. DailyLogin routing must wait until v2 passes real
  disposable-client acceptance, then be implemented only in the canonical
  client-patch path with the exact VGTP interception point.
  Evidence: `docs/evidence/CLIENT_PATCH_SOURCE_PROVENANCE_20260817.md` and
  `docs/evidence/DAILYLOGIN_VGTP_DIMENSION_ROUTING_20260817.md`.
- Linux login/inventory follow-up: after live validation of
  `login-hydration-b1c61405`, the DailyLogin runtime path issue was corrected
  in shared Windows-authoritative source by deriving claim roots from
  `AO_REBIRTH_DAILY_LOGIN_CLAIMS_ROOTS` or
  `AO_REBIRTH_ZONE_STATE_DIR/daily-login/claims`, while retaining legacy XAMPP
  roots only for Windows runtime compatibility. Live read-only SQL for
  `Nanotechnica` proved the previous zero-row inventory query used the wrong
  column: startup inventory rows are keyed by `ContainerType=39` and normal
  inventory page `ContainerInstance=104`, not by `ContainerInstance=39`.
  Focused DailyLogin validation passed, the Windows debug build passed,
  AOtomation messaging passed 1018/1018, Linux ZoneEngine publish plus offline
  smoke passed, and production ZoneEngine is now deployed as
  `dailylogin-path-360b3002` with startup/database preflights passing and port
  `7501` active. Evidence:
  `docs/evidence/LOGIN_INVENTORY_DAILYLOGIN_FOLLOWUP_20260817.md`.
- Crash-reconnect zombie-session fix: Windows-authoritative source commit
  `fe6617b3bcd1d3806eddd4dbbb91e9c6680ef499` was validated locally, published
  through the governed Linux ZoneEngine path, deployed as immutable production
  release `reconnect-fe6617b3`, and accepted with live official-client testing.
  The first reconnect under 30 seconds was immediately playable without an extra
  relog, the old timer deadline had no effect, reconnect after the timeout
  passed, fast reconnect repeated 3/3, ZoneEngine had no restart, and final
  `ONLINE_COUNT=0`. Later Windows `master` commit
  `5d0a84960df961e504f8761da46521d9968b8cd8` only adds client-patch changes and
  does not require a ZoneEngine redeploy. Evidence:
  `docs/evidence/CRASH_RECONNECT_LIVE_ACCEPTANCE_20260818.md`.
- Unified account database/schema phase: live production metadata for `login`
  and `characters` has been audited read-only against MySQL 8.4.10, normal
  playable `login.Flags=0` is proven, no nonzero pending flag is approved, and
  the repository now contains an Account Broker identity schema proposal plus a
  validation SQL artifact. The identity schema validates against the local
  Windows development MySQL database with legacy short-username representation
  preserved. Production website account routes and MyBB forum SSO are now
  deployed through the Account Broker; public forum HTTPS is pending DNS.
- Account Broker foundation: the first internal trusted-side broker library is
  implemented with an injected database boundary, identity-first idempotent
  provisioning, existing-account linking by stable `login.Id`, future external
  mapping support for MyBB UID linkage, split username policy for new
  registration versus legacy linking, and AO password creation through
  `LoginEncryption.GeneratePasswordHash()`. Account Broker validation passes
  28/28 in Debug and Release. No public route or production deployment exists
  yet.
- Unified account Windows-local flow: a loopback-only Account Broker HTTP
  service now exposes health, CSRF, registration, login, current-session,
  logout, and minimal local register/login/member pages. The service uses the
  broker as the only account authority; it does not reactivate legacy PHP
  account pages and does not let website code query `login.Password`. Sessions
  are server-side random-token sessions with HttpOnly/SameSite cookies, logout
  invalidation, CSRF protection, and lightweight registration/login rate
  limiting. Debug and Release unified-flow validation pass 41/41. Production
  public account routes are promoted through the broker; MyBB 1.8.40 is
  installed as a broker-backed identity consumer.
- Public unified account flow: `ao-rebirth.com/register`, `/login`, `/account`,
  and `/logout` are enabled through the Linux Account Broker release
  `9a176f6f` on the trusted Docker bridge address `172.18.0.1:7510`. A
  controlled production test account was created through public `/register`;
  website wrong-password, correct-password, account page, logout, duplicate,
  invalid-input, rate-limit, and broker-unavailable isolation checks pass.
  Database proof shows exactly one identity row, one identity email row, one
  `login` row, one linked game mapping, and normal non-GM account flags for the
  controlled account. Real LoginEngine protocol acceptance now passes against
  production `2.24.96.30:7500`: correct credentials reach `CHARACTER_LIST` and
  wrong credentials reach `LOGIN_ERROR`. Exposed MySQL root and
  `aorebirth_stage6` credentials were rotated, old values were rejected, and
  ChatEngine/LoginEngine/ZoneEngine/AccountBroker are active after redeploying
  LoginEngine and ZoneEngine release `account-gates-20260815-001`. Legacy PHP
  account endpoints remain blocked. The official GUI client was not launched by
  this agent. Controlled public/account/forum acceptance identities were later
  retired after zero-character/zero-post proof by disabling identities and
  mappings and rotating their game `login.Password` hashes.
- MyBB forum cutover-safe production state: before Hostinger DNS was added, the
  production forum vhost/container
  route works with a host override, HTTP redirects to HTTPS, sensitive MyBB
  paths are blocked, native MyBB registration remains disabled, the Identity
  Bridge plugin is active, the approved 40-row traditional board structure is
  live, forum cookies are no longer configured for the parent
  `.ao-rebirth.com` domain, MyBB DB credentials are denied game/identity DB
  access, forum-container failure does not affect the website/account/game
  services, current forum/proxy/broker logs do not contain `aor_sso` URL
  `code=` query strings, and cutover backup
  `/opt/ao-rebirth/database/backups/mybb-cutover-20260815T091336Z` exists.
  The website Forum SSO entry point now submits the one-time code to MyBB by
  POST instead of placing it in the callback URL query string.
- MyBB forum final public production acceptance: PASS on 2026-08-15. Hostinger
  DNS now resolves `forum.ao-rebirth.com A 2.24.96.30` with TTL `300`; Let's
  Encrypt production TLS issued for SAN `forum.ao-rebirth.com`; public HTTP
  redirects to HTTPS; `https://forum.ao-rebirth.com/` returns `200`; CSS, JS,
  and image assets return `200`; sensitive paths remain `403`/`404`; native
  MyBB registration remains disabled; public browser-equivalent AORebirth to
  MyBB SSO creates exactly one UID/mapping and repeat SSO reuses it; replay,
  expired, malformed, and unknown codes are rejected; current URL logs contain
  zero `aor_sso` `code=` query entries; MyBB credentials remain denied game and
  identity DB access; website/forum cookies are Secure/SameSite and session
  cookies are HttpOnly; final controlled accounts were disabled and game
  passwords rotated; final backup is
  `/opt/ao-rebirth/database/backups/mybb-public-acceptance-20260815T094721Z`.
  Forum/account infrastructure acceptance is complete; next forum work should
  be presentation, content, moderation policy, and community launch rather than
  identity-system construction.
- Frozen unified account/forum baseline: established on 2026-08-15 after the
  public production acceptance gates. AORebirth runtime/source baseline:
  `76258f8fc55a8220d63ef11f9aa039139e2870f6`. Website account/forum
  integration baseline:
  `1ecd84fc44457a0ced44b5f0399ead0eeb654ae3`. The unified account
  architecture, MyBB SSO architecture, and public forum infrastructure are now
  frozen; do not redesign identity/auth/forum SSO unless a proven production
  defect requires it. Next account/forum work is presentation, content,
  moderation, email/notification configuration, and launch preparation.
- MyBB forum community-launch preparation: applied on 2026-08-15 without
  changing frozen account/auth/SSO/game architecture. Production now has the
  AORebirth launch CSS layer, AORebirth header navigation, guest
  login/register links routed to AORebirth account pages, final descriptions
  for the approved 40-row board structure, staff seed threads, forum rules,
  account/support/bug-report guidance, read-only official/archive permissions
  for normal users, conservative PM/avatar/signature/attachment settings, and
  launch backups. Guest and registered-user SSO/posting acceptance passed with
  controlled cleanup. Launch remains BLOCKED on live moderator workflow
  acceptance, authorized Admin CP acceptance, and production-grade email
  transport/DNS if notifications are required. Evidence:
  `docs/project/MYBB_FORUM_LAUNCH_READINESS_20260815.md`.
- Email verification source foundation: prepared on 2026-08-15 without
  changing LoginEngine authentication, Account Broker password semantics, AO
  game login behavior, MyBB SSO, or the MyBB password authority boundary. The
  Account Broker now owns hashed email verification tokens, resend superseding,
  token verification, inert authenticated-SMTP configuration, and internal
  website-facing email endpoints. The website now refreshes verification state,
  provides a resend action only for unverified accounts, and uses
  `/verify-email.php#token=...` so verification tokens are not sent in normal URL
  request lines. This earlier foundation block was superseded by the
  2026-08-16 self-hosted production mail acceptance evidence. Evidence:
  `docs/project/EMAIL_DELIVERY_PRODUCTION_EVIDENCE_20260815.md`.
- Email production configuration: advanced on 2026-08-16 without enabling
  AORebirth website/forum outbound mail. Mike selected self-hosted VPS mail
  instead of third-party transactional hosting. The production database was
  backed up at
  `/opt/ao-rebirth/database/backups/email-production-20260816T002205Z`, the
  `account_email_verification_tokens` migration was applied and verified, and
  Account Broker release `email-foundation-20260816-002` is deployed and
  healthy on `172.18.0.1:7510`. The VPS Postfix/Dovecot/OpenDKIM stack now has
  `ao-rebirth.com`, `noreply@ao-rebirth.com`, `forum@ao-rebirth.com`, and DKIM
  selector `aor20260816`; local-only delivery and DKIM signing pass. Account
  Broker and MyBB SMTP are now configured. Hostinger MX/SPF/DKIM/DMARC records
  resolve, Account Broker verification resend to an external mailbox passed,
  and MyBB SMTP notification to an external mailbox passed. The exposed Account
  Broker DB credential and exposed `mail.ao-rebirth.com` TLS key from
  troubleshooting were both rotated. `SubaruMike` clicked the received
  verification link and production now shows the account email as verified.
  Production email is accepted for launch. Evidence:
  `docs/project/EMAIL_PRODUCTION_CONFIGURATION_EVIDENCE_20260816.md`.
- Unified account character display: accepted on 2026-08-16. Account Broker
  release `account-characters-20260816-001` is deployed and healthy on
  `172.18.0.1:7510`. The website `/account` page now shows a read-only My
  Characters section using only the authenticated unified `AOR_IDENTITY`
  session. Website code calls the broker's internal `/api/account/characters`
  endpoint; the broker resolves the identity public ID, validates the active
  linked identity, and queries the live Stage6 `characters` table with
  `characters.Username = CanonicalUsername`. Stage6 has no `playfields` lookup
  table available to the broker, so the page displays playfield IDs rather than
  hardcoding an incomplete catalog. `SubaruMike` route acceptance showed one
  live character, a controlled zero-character identity rendered the empty
  state, no-secret broker access returned `403`, username-tampering input was
  ignored, unauthenticated `/account` redirected, and `/member-index.php`
  remained blocked at the Apache boundary. No schema, character rows,
  LoginEngine behavior, game authentication, or identity/SSO architecture was
  changed.
- LoginEngine password authentication: restored after the `f7e9b657`
  username-only regression. `UserCredentialsHandler` again calls
  `CheckLogin.IsLoginCorrect()`, which loads `login.Password` and validates the
  encrypted credential through `LoginEncryption.IsValidLogin()` and
  `PasswordHash.ValidatePassword()`. Debug and Release validation tool runs pass
  14/14, LoginEngine Debug/Release builds pass, database preflight passes, and
  AOtomation messaging passes 1013/1013. No production database, website, Linux,
  MyBB, or public-registration change was made.

- Complete AOtomation suite: PASS (1003/1003).
- Arete regular-mob combat uses a scoped forward reconciliation against the
  current capture corpus. The retired post-cutoff 60/60 script is not restored;
  the current focused gameplay gate validates supported combat behavior and
  expected fail-closed exclusions.
- PF127 Subway acceptance: PASS.
- PF1931 Temple acceptance: PASS.
- PF1931 official-client post-login acceptance: PASS. The static
  `PlayfieldAnarchyF` resource shape is restored, malformed generated-resource
  identities fail closed, all 43 captured internal door statuses remain
  enabled, and Soldier completed entry/residency/exit validation on 2026-08-04.
- Generated mission graph and mission reproducibility: PASS.
- Debug server build: PASS.
- Parallel Linux compile-feasibility lane: Messaging, Cell.Util, MsgPack.Mono,
  Translations, Cell.Core, Utility, Enums, Exceptions, Interfaces,
  ObjectManager, Database, Stats, and Communication build on .NET 10 from guarded linked
  source/resource/content inventories, with a separate Linux-only `Ionic.Zlib`
  compatibility assembly plus an inert identity-compatible `MemBus` adapter.
  Stages 0-4 Windows-hosted compatibility checks,
  exhaustive public/mapping/table contracts, Database/Stats offline behavior,
  exact SQL publish assets, Communication wire/framing/FIFO loopback behavior,
  and the unchanged Windows debug build pass. The first .NET 10 ChatEngine
  executable now builds and publishes with strict configuration, a private
  ISCom bind, headless logging, fail-closed MySQL secret handling, systemd
  readiness notification, and coordinated shutdown. Its Windows/Linux contract,
  listener-free startup/lifecycle, authentication fixture, and both
  framework-dependent and self-contained publish-structure gates pass.
  PlayfieldLoader/full Core are deferred; the three required legacy
  authentication sources are isolated in `AORebirth.Chat.Authentication`.
  Native Ubuntu 24.04.4 x86_64 apphost, exact-case configuration, listener-free
  lifecycle, `Type=notify` readiness with both loopback listeners, and real
  SIGTERM shutdown pass. An isolated, uniquely named/labeled MySQL 8.4 target
  now passes the exact governed 34-table import, restricted runtime-account
  reads/CRUD, production Connector/DAO/password/encrypted-login behavior,
  negative authentication cases, and zero-residue fixture cleanup. ChatEngine
  has a read-only live database preflight in systemd before listener startup.
  The updated test release and unit pass `Type=notify`, loopback-only listeners,
  and clean SIGTERM against that database, then remain disabled/inactive with
  the normal secret-free environment untouched. The website and mail database
  containers, networks, and firewall were unchanged.
- The parallel Linux lane now also builds LoginEngine from its exact 35-source
  inventory with a contained identity-compatible Core slice, pinned MemBus 4.0.1,
  and the six legacy MEF handlers. Stage 7.1 adds fail-closed per-client
  authenticated state, CSPRNG challenge salt, canonical identity and ownership
  guards, same-client FIFO dispatch with bounded drain, and transactional cleanup
  of the governed character-owned data graph. Offline contract/security gates
  pass, as does listener-free disposable MySQL 8.4 security acceptance through
  the production paths with zero fixture residue. The guarded atomic upgrade to
  `stage7-20260809-login-003` and live database preflight, `Type=notify`, exact
  main-PID ownership of `127.0.0.1:7500`, and clean SIGTERM pass. LoginEngine and
  ChatEngine remain disabled/inactive, TCP 7500 is closed, MySQL is healthy and
  bound only to `127.0.0.1:33067`, and existing website/mail containers were
  unchanged.
- Stage 8 adds full `AORebirth.Core`, `PlayfieldLoader`, and `ZoneEngine` .NET
  10 overlays from exact guarded source inventories. ZoneEngine's copied
  XML/JSON/capture content, scripts, and required datafiles are governed by
  checked-in content/runtime-copy inventories and verified by a listener-free
  Stage 8 smoke. Linux excludes the WinForms NBug dependency, preserves JSON
  loader compatibility through a narrow `JavaScriptSerializer` shim, and extends
  the Linux Ionic shim with the diagnostic members ZoneEngine expects.
  ZoneEngine now has Linux-only listener-free startup and lifecycle validation
  modes for exact-case config, closed provider construction, loopback topology,
  required runtime assets, and bounded shutdown-file handling, plus a read-only
  database preflight requiring the expected MySQL database, exact governed
  34-table set, `characters.Online`, and zero online characters. The Linux
  wrapper and Stage 8 smoke pass locally, including child-process validation
  from the build output. Windows Debug passes through the approved build
  wrapper with the governed generated-combat cohort validated first.
- Stage 9 publishes ZoneEngine as a self-contained `linux-x64` artifact and
  validates it on native Ubuntu x86_64. Listener-free startup and lifecycle
  modes pass as Linux processes, read-only database preflight passes through
  systemd against the isolated Stage 6 MySQL target, and the disabled systemd
  service demonstrates deterministic start, status, stop, restart, and
  controlled-failure reporting. ZoneEngine remains disabled/inactive after
  validation, TCP 7501 is closed, and no database schema, generated-combat,
  gameplay, or packet behavior was changed.
- Git LFS and Git object integrity: PASS.
- WebEngine offline PHP/WebCore boundary: PASS. The official PHP 8.5.9 x64 NTS
  VS17 runtime and hardened INI are exact-manifest validated; the complete
  7,140-file WebCore corpus and all 25 PHP files are audited, deterministically
  patched, final-manifest validated, and PHP 8.5.9 lint clean. Clean commit
  `f898faa0838cc3918baf29202001e0cc2d0fab56` passed the complete 13-stage gate
  twice unchanged.

## Generated combat authority

The capture corpus and production runtime are authoritative; checked-in
generated projections must reproduce from them and are never edited by hand.
The current deterministic inventory contains 381 sessions, 365 canonical
sessions, 3,269 complete attack chains, 260 certified profiles, 96 runtime-ready
profiles, 309 semantic definitions, 101 runtime-ready definitions, and 1,486
explicitly unresolved observations with zero generator errors.

The active-coverage projection contains 1,534 actors, 1,520 binding records,
and maximum actor index 1,536. It reconciles 559 certified actors and 975
explicitly unresolved actors. The PF6553/PF8009 Arete active cohort contains
100 bindings / 113 actors; 43 certified bindings cover 56 actors and 57 bindings
remain explicit unresolved/quarantined exclusions. The transactional generator,
current-cohort validation, and deterministic second regeneration pass against
identity `041b9dc66bed5ddf2b50277d54232173a1b1d2f80196e721f50c38f138f1f1d5`.
Unsupported or conflicting observations remain fail-closed.

The generated combat surface is now one six-file cohort: five semantic artifacts
plus a manifest commit marker. A multi-reader/single-writer lease protects
supported readers and serializes writers. Primary captures are parsed once into
immutable validated shards; all generator/tool inputs are frozen; active coverage
and formula data converge to one fixed point; and publication is manifest-last,
rollback-capable, and crash-recoverable. The current generation identity is
recorded in `docs/generated/capture_backed_npc_combat_generation_manifest.json`.
Generated output no longer embeds the local checkout path. Runtime catalog,
exact-byte fixtures, and formula semantics are byte-identical to the prior
authority; no supported gameplay behavior changed.

Capture-decoder internal type failures caused by the known Windows interpreter
corruption boundary are retried at the coordinator child boundary; ordinary
deterministic type/schema failures remain fail-closed and are not retried.

The published input descriptor is schema 2 and hashes only durable capture
source, plan, identity, and session-state fields. Private shard descriptors stay
strictly validated inside each primary attempt but do not contaminate the
published identity. Active/formula children receive independent fsynced,
read-back-verified private projections and verify SHA-256 and byte length over
the same bytes they decode with a fully Python-initialized JSON scanner. Active
and formula projections are separate exact consumer inputs, and both preserve
complete `attackInfoPacketIds` arrays rather than sampled or counted evidence.
The frozen ItemDb is likewise verified against its auxiliary snapshot record.
The repository's C# `MessagePackZip` reader extracts exactly the 42 templates
referenced by governed PF127/PF1931 formula inputs into a canonical private JSON
projection. Formula children verify that projection's SHA/length and no longer
parse the full ItemDb in Python. The generated formula values remain unchanged;
its diff is limited to rebuilt analyzer provenance.
After a completed transition, formula equality proves that the next active and
formula pair is identical to the current pair. The coordinator memoizes only
that proven identity transition, preserving the three-round convergence result
while skipping both redundant terminal children. The generated runtime catalog
and fixtures remain byte-identical; other generated changes are provenance and
source-descriptor reconciliation only.
Cohort validation now binds each JSON decode to the manifest SHA/length, reuses
the first parsed object instead of reparsing the 124 MB inventory, and retries
`JSONDecodeError` against the same verified UTF-8 string up to three times. The
same bound applies to impossible stdlib JSON `TypeError` or `AttributeError`
failures only when their traceback proves `json.decoder`/`json.scanner`
ownership; deterministic and unrelated failures fail closed.
Repository-owned acceptance, build, test, and generated-combat wrappers select
a non-embedded 64-bit CPython 3.13.14 runtime through
`Tools/select_python_runtime.cmd`; the selector can be overridden with
`AO_REBIRTH_PYTHON` and rejects the isolated Windows embeddable distribution.
This avoids the locally installed Python 3.12 runtime whose repeated
`python312.dll` access violations prevented stable preflight execution. The
manifest records the selected CPython 3.13.14 binary.
SCFU analyzer provenance is source-backed and fresh-clone portable: ignored
`bin`/`obj` executables and PDBs, which embed absolute checkout paths, are not
published input authority. Current-cohort validation and normal server builds
require only tracked source; actual regeneration privately freezes the locally
built analyzer runtime without adding its machine-specific bytes to the
published generation identity.
Each mandatory gate holds one read lease across all 13 stages, eliminating the
former full inventory parse before and after every filtered acceptance wrapper.

The formula ItemDb reader streams each top-level MessagePack array and retains
only the 42 templates referenced by the governed PF127/PF1931 profiles instead
of all 120,842 templates. Measured peak Python allocation fell from 422,936,105
to 11,169,393 bytes. The governed formula artifact remains byte-identical.

## Known generator concurrency debt

The generated-combat migration does not silently generalize to unrelated
pipelines. Mission graph, Arete movement promotion/aggregation/verification,
Subway content generation, legacy loot seed export, and WebCore compatibility
still have documented snapshot or publication gaps. PHP/WebCore parsers also
retain hash-then-reopen windows. Dialogue content currently has read-only loaders
and no executable generator to migrate. These are separate, semantics-preserving
migrations; details and exact source references are recorded in
`docs/evidence/GENERATED_COMBAT_CONCURRENCY_20260802.md`.

## Supported playfields

- Arete regular-mob combat has explicit captured source/profile selectors while
  preserving runtime identity. Exact profiles are active for the supported Alex
  Waste Collector, Garbage Flea, and Cleanmeister cohort; level-1 Cleaning
  Robots; level-6 Desert Reets; level-5/6 Rollerrats; and ICC Peacekeepers.
  Generated ItemDb Stat 287 attack-range authority fixes supported natural
  melee profiles at 2m, so observed follow/start separations no longer become
  attack reach and supported mobs pursue before attacking. Engineer Automaton I,
  Robotic Guard Dog, and incomplete robot variants remain intentionally passive
  where exact combat evidence is insufficient.
- Rubi-Ka character creation persists its initial Arete location as PF 6553,
  X 3607.6, Y 52.4, Z 785.7 without integer rounding.
- PF4582 ICC Shuttleport has a checked-in authoritative placement layer with
  all 206 source records, 206 unique `NpcId` values, 38 template hashes, and
  all duplicate-position records preserved. Under the explicit practical
  completion authorization, 199 placements now materialize: the original 25,
  10 additional exact `ISRE` Island Reets, and 164 placements from 17 unique
  same-name template mappings. Only the 11 `ISRE` placements receive the
  captured Island Reet combat contract. The 164 additional template-backed
  placements retain their exact template identity, source minimum level, and
  position in Social mode without invented combat, loot, pathing, or respawn
  behavior. Seven unresolved template hashes remain fail-closed. Source names,
  flags, and candidate respawn timing remain placement metadata rather than
  behavioral authority.
- PF4582 now also has a governed, byte-identical local snapshot of the completed
  official EP1 structural investigation. The exact type-1000014 / instance-4582
  resource contains 207 `HashSpawnPoint_t` records across districts of 142 and
  65 records. All 206 accepted records reconcile one-to-one; `NCNN` is the one
  additional official record and is retained as an official blocked placement
  with null `SourceNpcId`, no profile, and no activation. Five exact duplicate
  equivalence groups remain separate, and two legacy guard `SpawnAngle` values
  differ from the official encoded field without preventing deterministic record
  correspondence. The current bridge outcome is
  `STRUCTURAL_SOURCE_AND_CONSUMER_FOUND`, superseding the historical
  `NO_BRIDGE_LOCATED`: the packed four-byte `ACGHash_t` type, parser, native
  field, vector, and accessors are proven, while terminal mob identity, static
  mappings, and runtime hash-to-dynel join remain unresolved. The 207-record
  official overlay authorizes 199 records and blocks eight, including `NCNN`;
  PF4582 runtime materialization remains governed by its specialized catalog.
  The heartbeat corpse-spawn queue now uses one synchronization boundary for
  scheduling and draining, preventing concurrent queue corruption while
  preserving same-key replacements.
- AORebirth now has a deterministic database-wide official static placement
  evidence layer imported from the official `18.8.62_EP1` old-graphics-client
  ResourceDatabase extraction. The source cohort is hash-pinned at 630 unique
  type-`1000014` resource instances: 627 parsed resources, three explicit
  parser-limited resources, 4,146 districts, 32,805 independent placement
  records, and 4,016 canonical `ACGHash_t` tags. Resource instance is exposed as
  playfield ID only under the extraction's 630/630 validated relationship, and
  the original instance remains provenance. The corpus preserves all duplicate
  records and creates no synthetic placement records for resources 103, 615,
  or 4805. The authoritative Windows project and its derived Linux inventory
  copy the same exact-cased corpus into ZoneEngine artifacts. A shared compiled
  validation mode verifies all file, shard, count, PF4582, and activation-policy
  invariants and emits deterministic manifest/provenance evidence. Normal
  startup and spawn materialization do not consume the general database-wide
  catalog. Identity, behavior readiness, and runtime activation remain
  separately governed; PF4582 reconciles exactly to the general 207-row shard
  while its specialized checked-in runtime catalog materializes 199 of 206
  accepted placements and blocks seven.
- PF127 Subway is complete for its current capture-backed population,
  navigation, combat, lifecycle, loot, vendor, Karrec, zoning, and teardown
  contracts. New behavior still requires capture evidence.
- PF1931 Temple is complete for its current ordinary/named population, dynamic
  doors, combat, lifecycle, loot, and navigation contracts. Unsupported nano
  selectors and unseen loot outcomes remain fail-closed. PF6553 Marcus ambient
  combat also fails closed before attack start while its captured start context
  is incomplete; supported mesh and burning-robot visuals remain active.

## Repository health

- Tracked configuration contains placeholders only and supports the ignored
  `AO_REBIRTH_MYSQL_CONNECTION` local environment override.
- The read-only database preflight uses the production configuration/connector
  path, verifies the exact database and 34-table contract, and blocks startup
  when any character is still marked online.
- Engine health is PID-owned: listener PIDs must resolve to the exact expected
  executables. Managed startup and shutdown never kill by process name alone.
- The approved Debug build includes LoginEngine, preventing creation-path
  changes from restarting against a stale executable.
- Newly created Rubi-Ka characters begin at the supplied Arete arrival point and
  receive a durable, one-time KnuBot choice from the dedicated ICC Shuttleport
  Commander between Arete and ICC Shuttleport. Existing characters and the
  official Shadowlands selector are unaffected; no database schema change is
  required.
- Managed start/stop status probes receive the repository configuration and
  engine directory on every invocation. Shutdown validates PID metadata against
  engine identity, executable path, start time, and released listener ports.
- The mandatory secret scanner rejects likely credentials without echoing
  values. Any credential exposed outside the repository still requires external
  rotation.
- WebEngine no longer downloads PHP, `php.ini`, or WebCore assets. It requires
  the complete official PHP 8.5.9 x64 NTS VS17 archive tree, the exact hardened
  INI, and an offline-imported final `htdocs` tree for CellAO WebCore commit
  `765c3850767b63af1cd259bab7f2f7ca3e97adf9`. PHP and WebCore are held under
  exclusive process-lifetime leases and revalidated before listener creation.
- The pinned WebCore archive is identified by SHA-256
  `ef297e623040b375e64c543568ca94e44ed7cc59de6fe826ed5e42db95c020ab`;
  its manifest covers 7,140 files and 26,648,501 bytes and has SHA-256
  `85c1515d274c2e4051013e89ca6d2a355365d5d01df7d621cc060dfa84e38463`.
- DotNetZip was removed. Archive extraction uses canonical-path containment;
  Zlib-only runtime paths use the isolated Ionic.Zlib package. Npgsql is 4.0.14.
- LoginEngine and ZoneEngine listener exposure is deployment policy, not source
  drift: `AO_REBIRTH_BIND_MODE` defaults to `Loopback` (`127.0.0.1`), accepts
  only explicit `Loopback` or `Public`, fails closed on empty or invalid values,
  and binds public production listeners only when set to `Public` (`0.0.0.0`).
  LoginEngine additionally requires a loopback `ZoneIP` in `Loopback` mode and
  a concrete non-loopback `ZoneIP` in `Public` mode so client handoff cannot
  advertise an unusable bind wildcard or private loopback address.
  ChatEngine public and private ISCom bind rules remain separate; MySQL and
  ISCom are not made public by the listener policy.
- Three obsolete detached worktrees, the unowned Cursor export, 1,877 tracked
  temporary/decompiled files, and 74,054,821,216 bytes of disposable diagnostics
  and tools were removed after manifests and reachability checks.
- Git contains one 128.83 MiB pack, no loose objects, and no reported garbage
  after full integrity verification and native garbage collection.
- Line endings are explicit: maintained source/data use LF, Windows CMD/BAT
  launchers use CRLF, and binary formats are never normalized.

## Remaining debt boundary

- Public LoginEngine TCP 7500 and ZoneEngine TCP 7501 exposure is permitted only
  through explicit `AO_REBIRTH_BIND_MODE=Public` production deployment after the
  repository validation gates pass. `Loopback` remains the default and rollback
  mode.
- Before declaring public production accepted, prove official-client
  end-to-end login, retry/error UX, character list/create/select semantics,
  ZoneEngine handoff, Arete entry, movement, loopback rollback, and sustained
  multiplayer operation.
- Rotate the previously exposed database credential externally.
- Perform authorized live WebEngine verification only after a valid disposable
  database credential is supplied. Current validation intentionally makes no
  live database connection and invents no credential.
- Replace or front the plaintext HTTP listener before considering secure-only
  cookies or production exposure. WebEngine remains development-only.
- Resolve the pinned WebCore snapshot's licensing before redistribution or
  production use. No license file was found upstream; integrity validation does
  not grant redistribution rights.
- Review `_tmp_mail_recovery` before any removal.
- Continue catalogued unsupported gameplay only with authoritative evidence;
  do not bulk-implement `NotImplementedException` paths or invent defaults for
  chase, quest deletion, action 59, anarchy playfields, perks, research, PvP,
  towers, teams, organizations, missions, quests, or pets.

## Operational workflow

- Run the complete local gate with `tools\run_mandatory_integration_gate.cmd`.
- Query live engine process/port health with `status-engines.cmd`.
- Validate database readiness with `preflight-database.cmd` before startup.
- Build with `tools\build_aorebirth_debug.cmd`.
- Stop/restart only with the approved root CMD wrappers.
- Supply WebCore assets only through the offline import and validation workflow
  in `docs/project/WEBCORE_ASSET_SUPPLY.md`; never restore a URL-backed archive
  bootstrap.
- Start optional WebEngine only with `start-web-engine.cmd`; it validates the
  database, binary, PHP runtime, and WebCore assets before launch. Stop it with
  `stop-web-engine.cmd`.
- Do not launch the AO client unless Mike explicitly requests it.
- The approved AOSharp launcher writes new evidence to repository-level
  `Captures` folders named `<area> [PF <resource id>] - <capture id>`; the
  timestamp suffix remains the stable analyzer-facing ID, and historical
  plugin-local captures are not moved automatically.
- AOSharp capture-safety contract 6 prevents failed/empty chat socket reads and
  malformed chat-frame parser exceptions from escaping the native receive hook.
  The governed injector build self-tests the exact `EndOfStreamException`
  containment boundary before publishing a usable binary.
