# Current Task

## Active

Build a parallel Linux deployment lane for AORebirth in
`D:\AO_Rebirth_Linux_Build` while preserving the existing Windows/.NET
Framework solution as the reference build.

## Current checkpoint

- Branch: `codex/linux-parallel-build`.
- Linux SDK lane: .NET 10, intended for Ubuntu 24.04.
- Messaging, Cell.Util, MsgPack.Mono, and Translations compile from guarded
  linked source/resource inventories.
- Assembly identity, MsgPack byte-vector/round-trip, translation-resource, and
  unchanged Windows debug-build validation pass.
- This is compile feasibility only; no engine is Linux-deployable yet.

## Next slice

Port Cell.Core and Utility. Resolve modern NLog/resource handling and replace or
isolate the Windows performance-counter path without changing Windows behavior.
Then advance through the dependency closure documented in
`LinuxBuild/PORTING_PLAN.md` toward ChatEngine as the first server milestone.

## Constraints

- Do not convert or replace legacy project files.
- Do not change packet behavior or database schemas.
- Preserve assembly boundaries, names, versions, and strong-name identities.
- Require Windows regression and cross-runtime parity checks for shared-source
  compatibility changes.
- Ubuntu runtime and service validation require the VPS SSH connection details.
