# Current Task

## Active

Integrate the validated Linux deployment lane with current AORebirth master,
then synchronize the disabled Ubuntu installation without creating platform
divergence or exposing player services prematurely.

## Current checkpoint

- Windows remains the authoritative development and acceptance platform under
  `docs/project/DEVELOPMENT_AUTHORITY.md`.
- The native .NET 10 Linux lane for ChatEngine, LoginEngine, full Core,
  PlayfieldLoader, and ZoneEngine is integrated with the current capture-backed
  Arete master source.
- Linux source inventories have been regenerated from the integrated legacy
  project files; the Windows-hosted Linux build and Stage 8 offline smoke pass.
- Prior Ubuntu proof covers native disabled-service validation only. LoginEngine
  and ZoneEngine remain disabled/inactive and loopback-only; ZoneEngine Stage 9
  validation uses listener-free lifecycle mode rather than production gameplay.
- The unified account architecture remains a design boundary documented in
  `docs/project/UNIFIED_ACCOUNT_ARCHITECTURE.md`; no account schema or production
  authentication change is part of this synchronization.

## Remaining gates

- Complete the authoritative Windows mandatory integration gate on the merged,
  clean tree.
- Publish immutable Linux packages from that validated source and run the
  approved Ubuntu build/disabled-service verification workflow.
- Prove multi-engine ordering, official-client login and retry/error behavior,
  character-count semantics, and sustained multiplayer operation before public
  production promotion.

## Constraints

- Do not change packet, gameplay, authentication, or database semantics merely
  for Linux.
- Do not create Linux-only core implementations or production-only source edits.
- Preserve generated-combat evidence, exact source inventories, and fail-closed
  quarantine behavior.
- Keep MySQL and ISCom private. Do not enable or expose LoginEngine, ChatEngine,
  or ZoneEngine until their documented production gates pass.
- Do not launch the AO client without explicit current authorization.
