# Current Task

## Active

Establish an audited listener-binding policy for AORebirth production services
and prepare a safe Ubuntu public-mode cutover candidate without creating
platform divergence.

## Current checkpoint

- Windows remains the authoritative development and acceptance platform under
  `docs/project/DEVELOPMENT_AUTHORITY.md`.
- The native .NET 10 Linux lane for ChatEngine, LoginEngine, full Core,
  PlayfieldLoader, and ZoneEngine is integrated with the current capture-backed
  Arete master source.
- Linux source inventories have been regenerated from the integrated legacy
  project files; the Windows-hosted Linux build and Stage 8 offline smoke pass.
- Listener reachability is now an explicit deployment policy:
  `AO_REBIRTH_BIND_MODE=Loopback` keeps LoginEngine and ZoneEngine private, and
  `AO_REBIRTH_BIND_MODE=Public` is required for production public listeners.
- The unified account architecture remains a design boundary documented in
  `docs/project/UNIFIED_ACCOUNT_ARCHITECTURE.md`; no account schema or production
  authentication change is part of this synchronization.

## Remaining gates

- Complete the authoritative Windows mandatory integration gate on the merged,
  clean tree.
- Publish immutable Linux packages from the validated source.
- Prove external reachability for LoginEngine and ZoneEngine only when the
  Ubuntu deployment explicitly sets public mode.
- Complete official-client login, character selection/creation, zone handoff,
  Arete entry, movement, and loopback rollback validation before marking the
  public production cutover accepted.

## Constraints

- Do not change packet, gameplay, authentication, or database semantics merely
  for Linux.
- Do not create Linux-only core implementations or production-only source edits.
- Preserve generated-combat evidence, exact source inventories, and fail-closed
  quarantine behavior.
- Keep MySQL and ISCom private. Public reachability is authorized only through
  the audited LoginEngine/ZoneEngine `AO_REBIRTH_BIND_MODE=Public` deployment
  switch.
- Do not launch the AO client without explicit current authorization.
