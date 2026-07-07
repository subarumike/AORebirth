# Playfield Runtime Decomposition Checkpoint - 2026-07-06

## Summary

This checkpoint stops feature movement and records the current `Playfield` runtime decomposition state after the death/respawn, statel transition, and object materialization boundaries.

No runtime behavior is changed by this checkpoint.

## Completed Runtime Boundaries

- `PlayfieldRuntimeSystems`: facade boundary for runtime services, registry, content, provider, and sequencing dependencies.
- `PlayfieldDynelRegistry`: playfield-local dynel lookup, typed views, range queries, static dynel views, and local registration/unregistration.
- `PlayfieldContentDataProvider`: static dynel definitions, statel resolution, vendor statel filtering, and collision-capable statel filtering.
- `PlayfieldContentCoordinator` plus content modules: content registration and content-owned DB spawn suppression.
- `PacketSequencingCoordinator`: session ready/full-character/visibility/private-city/zoning-entry sequencing ownership without packet construction.
- `ZoneClientSessionLifecycleCoordinator`: session phase ownership and transition validation.
- `InventoryContainerRuntimeService`: inventory, backpack, bank, corpse loot transfer, quest reward, vendor, tradeskill, and related container orchestration.
- `NPCRuntimeService`: NPC spawn/activation, home state, patrol/combat tick delegation, aggro/combat start/stop/death, reward hook, corpse/despawn timing, and captured robot orchestration.
- `PlayerCombatRuntimeService`: player attack start, cancel/stop, combat tick, invalid-target cleanup, and death-side combat cleanup orchestration.
- `PlayfieldTimedLifecycleRuntimeService`: heartbeat lifecycle sequencing for corpse, credit, dead-NPC, regeneration, combat, patrol, follow, and collision callback order.
- `PlayfieldLifecycleRuntimeService`: playfield-transfer cleanup sequencing.
- `PlayfieldPlayerDeathRespawnRuntimeService`: player death/respawn packet-state callback order.
- `PlayfieldInteractionRuntimeService`: GenericCmd use routing order.
- `PlayfieldRewardRuntimeService`: quest/reward callback orchestration.
- `PlayfieldObjectLifecycleRuntimeService`: object removal, corpse despawn cleanup, and predicate-based corpse despawn orchestration.
- `PlayfieldCorpseAccessRuntimeService`: corpse use/access and corpse loot transfer sequencing.
- `PlayfieldStatelTransitionRuntimeService`: statel collision/contact/grace and Montroyal/private-city transition orchestration.
- `PlayfieldObjectMaterializationRuntimeService`: startup materialization order for DB mobs, content registration, vendors, static dynels, and final registry refresh.

## Intentionally Still Owned By Playfield

- Public playfield entry points and MemBus transport/publish/send callbacks.
- Packet construction, packet serialization calls, and direct packet emission callbacks.
- Character stat mutation and stat math.
- Teleport packet construction, destination playfield lookup, same-playfield teleport handling, and cross-playfield handoff mechanics.
- Runtime object construction callbacks, including `StaticDynel`, `NPCController`, mob instantiation, and KnuBot script creation.
- DAO calls for DB mob spawn rows and DB mob spawn stat rows.
- Vendor spawning implementation.
- Corpse storage dictionaries, corpse identity allocation, corpse inventory-handle allocation, and corpse loot-item identity allocation.
- Corpse loot rolling, item materialization, credit math, and cash persistence.
- Player/NPC death packet builders and corpse full-update packet builders.
- Combat damage, weapon/range/timing, movement-to-target, and attack packet construction.
- Visibility broadcast, SCFU send callbacks, and playfield-local packet fanout.
- Remaining global/cross-playfield `Pool` exceptions: shutdown scans, global counts, and teleport playfield handoff.

## Remaining High-Risk Domains To Defer

- Combat algorithm extraction: damage, weapon selection, range/timing, attack packet construction, and NPC combat movement remain tightly coupled to packet-visible behavior.
- Corpse loot/credit internals: loot table selection, item materialization, credit mutation, corpse inventory packet construction, and persistence remain behavior-sensitive.
- Teleport/zoning handoff: packet emission, coordinate/heading mutation, client detach/dispose, playfield lookup/creation, and redirection remain interleaved. Guardrails now cover statel collision routing, Montroyal/private-city entry and exit callback order, same-playfield local teleport send before coordinate mutation, cross-playfield zoning entry before teleport send, destination lookup/handoff order, and the mechanics that must remain in `Playfield` for now.
- Packet construction ownership: many packet builders remain intentionally near Playfield callbacks until fixture-backed packet-shape tests exist.
- DB/data loading and import behavior: DAO calls and loader/import semantics should not move without a separate data-provider/import boundary plan.
- Visibility packet fanout: current registry and sequencing guardrails exist, but broadcast mechanics still carry packet-order risk.

## Recommended Next 3 Implementation Slices

1. Add a corpse loot/credit packet-state guardrail before any extraction.
   - Target current `RollCorpseLootItems`, `ScheduleCorpseCreditAward`, `AwardCorpseCredits`, `SendCorpseInventoryUpdate`, and corpse loot packet callbacks.
   - Do not move loot algorithms or packet construction in the first slice.

2. Audit combat packet/damage boundary before extracting algorithms.
   - Target player and NPC attack packet construction, damage announcement, health damage, weapon-slot selection, range/timing, and movement-to-target callbacks.
   - Prefer guardrail coverage first; extract only if a cohesive orchestration seam is obvious.

3. Revisit teleport/zoning only after a behavior-preserving extraction plan exists.
   - Keep packet construction, `PlayfieldById` handoff, coordinate/heading mutation, client detach/dispose, same-playfield local teleport, and `ZoneRedirectionMessage` send in `Playfield` until a fixture-backed packet-state harness exists.
   - Do not extract packet emission from this path based only on string-order guardrails.

## Validation Scope For This Checkpoint

Required validation remains:

- `cmd /d /c git diff --check`
- Focused `PlayfieldLifecycleTraceTests`
- Focused `GenericCmdUseRouteClassifierTests`
- `cmd /d /c tools\build_aorebirth_debug.cmd`
- `cmd /d /c restart-engines.cmd`
