# Current Task

## Active

Build a parallel Linux deployment lane for AORebirth in
`D:\AO_Rebirth_Linux_Build` while preserving the existing Windows/.NET
Framework solution as the reference build.

## Current checkpoint

- Branch: `codex/linux-parallel-build`.
- Linux SDK lane: .NET 10, intended for Ubuntu 24.04.
- Messaging, Cell.Util, MsgPack.Mono, Translations, Cell.Core, Utility, Enums,
  Exceptions, Interfaces, ObjectManager, Database, Stats, and Communication
  compile from guarded linked source/resource inventories. Database's 34 SQL
  Content assets are guarded and copied to build/publish output.
- A separate Linux-only `Ionic.Zlib` compatibility assembly preserves the
  external compression type boundary.
- Stage 1 Windows-hosted validation passes for assembly identity, resources,
  unsafe readers, buffer/TCP behavior, portable metrics, canonical `Config.xml`,
  NLog output, legacy zlib fixtures, and the unchanged Windows debug build.
- Stage 2 Windows-hosted validation passes exact legacy/Linux public-contract
  parity for all four assemblies plus ObjectManager lifecycle behavior. Unused
  legacy MemBus and Exceptions NLog dependencies are excluded.
- Stage 3 Windows-hosted validation passes Database/Stats semantic contracts,
  mapping/table fixtures, exact modern provider pins, 143 database-free runtime
  checks, and 156 SQL/package artifact checks. Windows Debug remains passing;
  no database connection or schema operation was performed.
- Stage 4 Windows-hosted validation passes Communication public/protected and
  wire contracts plus bounded source/published IPv4-loopback framing/FIFO and
  keepalive checks. The net40-only, source-dead Communication MemBus boundary is
  replaced by an inert identity-compatible Linux assembly.
- PlayfieldLoader's ignored cache initialization and full Core are excluded from
  the first ChatEngine publish. A hidden `AO.Core.Encryption.LoginEncryption`
  dependency is preserved through a contained three-source Linux authentication
  assembly.
- Stage 5 builds and publishes ChatEngine for `linux-x64`. Linux startup uses
  strict exact-case configuration, a private ISCom bind, console/journald
  logging, fail-closed env-only MySQL credentials, bind-readiness checks,
  `Type=notify` readiness, and coordinated SIGTERM/SIGINT shutdown.
- Offline startup validation constructs closed provider objects and a
  listener-free eight-channel topology; it does not open a database or socket.
- Stage 5 Windows/Linux contract, authentication, lifecycle, negative secret,
  and framework-dependent/self-contained publish-structure gates pass locally.
- Native Ubuntu 24.04.4 x86_64 validation passes: the self-contained apphost,
  exact-case startup, listener-free lifecycle, systemd unit, `Type=notify`
  readiness after both loopback listeners, and real SIGTERM shutdown all pass.
  The Stage 6 test release is installed on the test VPS but left
  disabled/inactive.
- Stage 6 provisions a separately named and labeled MySQL 8.4 container,
  volume, network, database, and runtime user. Only `127.0.0.1:33067` is bound;
  the existing AO website and mail databases/networks remain untouched.
- The exact governed 34-file schema imports and verifies on Ubuntu. The runtime
  account has only `SELECT`, `INSERT`, `UPDATE`, and `DELETE` on the disposable
  database.
- A listener-free integration harness passes the production Config/Connector,
  DAO, password-hash, encrypted login-key, account/character ownership, negative
  authentication, and exact cleanup paths with zero fixture residue.
- ChatEngine now has a read-only `--validate-database` mode, and systemd runs it
  before listener startup. The disabled unit passes live DB preflight,
  `Type=notify`, loopback listener checks, and clean SIGTERM. The Stage 6
  validator uses a runtime drop-in and leaves the normal secret-free service
  environment untouched.
- Stage 7 builds LoginEngine from the exact guarded 35-source inventory plus a
  contained `AORebirth.Core` LoginEngine closure. The unchanged legacy MemBus
  adapters use pinned MemBus 4.0.1, and MEF composes the six active handlers.
- Stage 7.1 adds fail-closed per-client authenticated state, a CSPRNG server
  salt, canonical authenticated identity and character-ownership guards,
  same-client FIFO dispatch with a bounded shutdown drain, and transactional
  cleanup of the full governed character-owned data graph.
- The Stage 7 contract and security gates pass offline. The listener-free
  disposable MySQL 8.4 security acceptance also passes against the production
  Config/Connector/DAO paths with zero fixture residue.
- The guarded atomic disabled-service upgrade to
  `stage7-20260809-login-003` passes on Ubuntu 24.04 x86_64. Live database
  preflight, `Type=notify`, exact main-PID ownership of `127.0.0.1:7500`, and
  clean SIGTERM pass. LoginEngine remains disabled/inactive, TCP 7500 is closed,
  and the healthy disposable database remains bound only to loopback.
- Stage 8 now builds full `AORebirth.Core`, `PlayfieldLoader`, and `ZoneEngine`
  on .NET 10 from exact guarded source inventories. ZoneEngine also has guarded
  copied content and runtime-copy inventories for its XML/JSON/capture assets,
  script files, and datafiles. Linux excludes the WinForms NBug dependency,
  supplies a narrow `JavaScriptSerializer` compatibility source, and extends the
  Linux Ionic shim with diagnostic `TotalIn`/`TotalOut` members. ZoneEngine now
  has Linux-only listener-free `--validate-startup` and `--validate-lifecycle`
  modes covering exact-case configuration, closed provider construction,
  loopback topology, required runtime assets, and bounded shutdown-file handling.
  ZoneEngine also has a read-only `--validate-database` mode requiring the
  expected MySQL database, exact governed 34-table set, `characters.Online`, and
  zero online characters before listener startup. The full Linux wrapper and
  Stage 8 smoke pass locally, including child-process validation of startup and
  lifecycle modes from the build output. Windows Debug validation is currently
  blocked before compile by stale generated combat catalog preflight; the
  analyzer dependency now builds, but the governed generator fails because
  captured realm 655 provenance does not contain matching raw-derived SCFU
  metadata.
- Stage 9 publishes ZoneEngine as a self-contained `linux-x64` artifact and
  validates it as a native Ubuntu x86_64 process on the VPS. Listener-free
  startup and lifecycle modes pass natively, the systemd validation service
  installs as disabled, validates the read-only Stage 6 MySQL 34-table preflight,
  demonstrates start/status/stop/restart, reports a controlled invalid
  configuration failure deterministically, and returns to disabled/inactive with
  TCP 7501 closed. Evidence is recorded in
  `docs/evidence/STAGE9_ZONEENGINE_NATIVE_SYSTEMD_20260809.md`.

## Next slice

Continue with multi-engine ordering and official-client end-to-end validation
across LoginEngine, ChatEngine, and ZoneEngine. Keep the proven LoginEngine and
ZoneEngine slices disabled and loopback-only: public exposure remains
unapproved until official-client end-to-end login plus retry/error UX is proven,
character-count semantics are resolved, and a sustained multiplayer soak passes.

## Constraints

- Do not convert or replace legacy project files.
- Do not change packet behavior or database schemas.
- Preserve assembly boundaries, names, versions, and strong-name identities.
- Require Windows regression and cross-runtime parity checks for shared-source
  compatibility changes.
- VPS access is configured outside the repository; never record credentials or
  private-key material here.
