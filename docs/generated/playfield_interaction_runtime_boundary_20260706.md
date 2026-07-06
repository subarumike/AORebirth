# Playfield Interaction Runtime Boundary - 2026-07-06

This checkpoint documents the interaction/use dispatch slice moved out of
`GenericCmdMessageHandler` and behind the Playfield runtime boundary.

## Boundary Added

`PlayfieldInteractionRuntimeService` owns GenericCmd `Use` dispatch ordering for:

- Rex B18D interaction use
- inventory/backpack use
- private-city guest-key generator use
- private-city controller use
- direct corpse and dead-NPC corpse use
- captured grid terminal and grid-enter terminal use
- surgery clinic use
- pooled static dynel vendor/trade fallback
- statel fallback use

`GenericCmdMessageHandler` now extracts/logs the request and delegates `Use`
routing to `Playfield.TryHandleGenericCmdUse`, which routes through
`PlayfieldRuntimeSystems`.

## Still Owned Outside

The individual interaction handlers still own their existing behavior:

- packet construction and acknowledgement packets
- teleport/grid destination logic
- surgery clinic credit/nano/implant behavior
- inventory/container algorithms
- corpse loot/credit/container behavior
- private-city controller and guest-key behavior
- statel event execution

## Explicit Non-Goals

This slice does not change:

- packet serialization or packet ordering
- DB loading or object construction
- inventory algorithms or credits logic
- combat logic
- NPC/player lifecycle internals
