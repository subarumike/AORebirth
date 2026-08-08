# Current Task

## Active

Build a parallel Linux deployment lane for AORebirth in
`D:\AO_Rebirth_Linux_Build` while preserving the existing Windows/.NET
Framework solution as the reference build.

## Current checkpoint

- Branch: `codex/linux-parallel-build`.
- Linux SDK lane: .NET 10, intended for Ubuntu 24.04.
- Messaging, Cell.Util, MsgPack.Mono, Translations, Cell.Core, Utility, Enums,
  Exceptions, Interfaces, ObjectManager, Database, and Stats compile from
  guarded linked source/resource inventories. Database's 34 SQL Content assets
  are guarded and copied to build/publish output.
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
- This is compile feasibility only; no engine is Linux-deployable yet.
- Native Ubuntu execution remains pending VPS connection details.

## Next slice

Port Communication and Core. Replace or adapt the net40-only MemBus boundary
without changing ISCom ordering/concurrency, retain dynamic-message/MEF
discovery, and keep socket behavior cross-platform. Then advance to
PlayfieldLoader and ChatEngine.

## Constraints

- Do not convert or replace legacy project files.
- Do not change packet behavior or database schemas.
- Preserve assembly boundaries, names, versions, and strong-name identities.
- Require Windows regression and cross-runtime parity checks for shared-source
  compatibility changes.
- Ubuntu runtime and service validation require the VPS SSH connection details.
