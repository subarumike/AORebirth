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
- Core/PlayfieldLoader are proven unused by ChatEngine beyond an ignored cache
  initialization and are deferred to LoginEngine/ZoneEngine; Windows behavior
  remains unchanged.
- This is compile feasibility only; no engine is Linux-deployable yet.
- Native Ubuntu execution remains pending VPS connection details.

## Next slice

Port ChatEngine as the first deployable engine. Exclude the audited-unused
Core/PlayfieldLoader path in the Linux lane, replace NBug/WinForms startup,
canonicalize runtime paths and configuration packaging, and add bounded
headless shutdown before Ubuntu validation.

## Constraints

- Do not convert or replace legacy project files.
- Do not change packet behavior or database schemas.
- Preserve assembly boundaries, names, versions, and strong-name identities.
- Require Windows regression and cross-runtime parity checks for shared-source
  compatibility changes.
- Ubuntu runtime and service validation require the VPS SSH connection details.
