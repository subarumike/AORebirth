# Current Task

## Active

Build a parallel Linux deployment lane for AORebirth in
`D:\AO_Rebirth_Linux_Build` while preserving the existing Windows/.NET
Framework solution as the reference build.

## Current checkpoint

- Branch: `codex/linux-parallel-build`.
- Linux SDK lane: .NET 10, intended for Ubuntu 24.04.
- Messaging, Cell.Util, MsgPack.Mono, Translations, Cell.Core, and Utility
  compile from guarded linked source/resource inventories.
- A separate Linux-only `Ionic.Zlib` compatibility assembly preserves the
  external compression type boundary.
- Stage 1 Windows-hosted validation passes for assembly identity, resources,
  unsafe readers, buffer/TCP behavior, portable metrics, canonical `Config.xml`,
  NLog output, legacy zlib fixtures, and the unchanged Windows debug build.
- This is compile feasibility only; no engine is Linux-deployable yet.
- Native Ubuntu execution remains pending VPS connection details.

## Next slice

Port Enums, Exceptions, Interfaces, and ObjectManager while preserving their
assembly boundaries and exact source inventories. Then advance through the
dependency closure documented in `LinuxBuild/PORTING_PLAN.md` toward ChatEngine
as the first server milestone.

## Constraints

- Do not convert or replace legacy project files.
- Do not change packet behavior or database schemas.
- Preserve assembly boundaries, names, versions, and strong-name identities.
- Require Windows regression and cross-runtime parity checks for shared-source
  compatibility changes.
- Ubuntu runtime and service validation require the VPS SSH connection details.
