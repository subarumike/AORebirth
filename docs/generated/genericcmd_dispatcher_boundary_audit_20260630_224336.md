# GenericCmd Dispatcher Boundary Audit

Generated: 2026-06-30 22:43 local

Scope: no-behavior-change audit of `AORebirth/Server/ZoneEngine/Core/MessageHandlers/GenericCmdMessageHandler.cs` after the GenericCmd route extractions through commit `2be87373`.

## Summary

`GenericCmdMessageHandler` can now be treated as a dispatcher-only boundary for the currently implemented GenericCmd actions.

The handler still performs the inbound GenericCmd log line and owns shared outbound GenericCmd acknowledgement helpers. It no longer directly owns the previously extracted gameplay/object interaction bodies for Rex B18D, inventory/backpack/container use, guest key generation, City Controller use, corpse use, grid terminal use, surgery clinic use, pooled static dynel/vendor use, statel fallback use, or `UseItemOnItem`.

## Current Action Branches

| Action | Id | Current branch behavior | Classification | Behavior ownership |
| --- | ---: | --- | --- | --- |
| `Get` | `1` | Empty branch, then `break`. | No-op. | None in `GenericCmdMessageHandler`. |
| `Drop` | `2` | Empty branch, then `break`. | No-op. | None in `GenericCmdMessageHandler`. |
| `Use` | `3` | Calls extracted handlers in fixed precedence order and breaks when one handles the route. | Dispatch-only, plus inbound logging before the switch. | Extracted handlers own route behavior. |
| `UseItemOnItem` | `5` | Calls `UseItemOnItemInteractionHandler.Default.TryHandle(client, message)`, then `break`. | Dispatch-only. | `UseItemOnItemInteractionHandler`. |

## Current Use Dispatch Order

The current `Use` branch dispatches in this order:

1. `RexB18DInteractionHandler`
2. `InventoryContainerInteractionHandler`
3. `GuestKeyGeneratorInteractionHandler`
4. `CityControllerInteractionHandler`
5. `CorpseInteractionHandler`
6. `GridTerminalInteractionHandler.TryHandleCapturedUse`
7. `GridTerminalInteractionHandler.TryHandleGridEnterUse`
8. `SurgeryClinicInteractionHandler`
9. `StaticDynelInteractionHandler`
10. `StatelInteractionHandler`

This matches the route precedence guarded by `GenericCmdUseRouteClassifier` tests.

## Extracted Handlers Used By Dispatcher

| Handler | Dispatcher call confirmed | Project include confirmed | Notes |
| --- | --- | --- | --- |
| `RexB18DInteractionHandler` | Yes | Yes | First `Use` pre-route. |
| `InventoryContainerInteractionHandler` | Yes | Yes | Inventory item, worn backpack, and open container route. |
| `GuestKeyGeneratorInteractionHandler` | Yes | Yes | Private-city guest key route. |
| `CityControllerInteractionHandler` | Yes | Yes | City Controller `Use`; window close handler is separate. |
| `CorpseInteractionHandler` | Yes | Yes | Direct corpse and dead-NPC corpse use. |
| `GridTerminalInteractionHandler` | Yes | Yes | Captured grid route before generic grid-enter route. |
| `SurgeryClinicInteractionHandler` | Yes | Yes | Surgery clinic use route. |
| `StaticDynelInteractionHandler` | Yes | Yes | Pooled static dynel/vendor `OnUse`/`OnTrade` fallback. |
| `StatelInteractionHandler` | Yes | Yes | Final statel fallback. |
| `UseItemOnItemInteractionHandler` | Yes | Yes | `GenericCmdAction.UseItemOnItem`. |

## Classifier And Test Guardrails

`GenericCmdUseRouteClassifier` remains a pure route-name classifier for `Use` route precedence. The focused GenericCmd route test suite currently covers:

- current route order;
- Rex B18D first precedence;
- private-city guest key targets;
- City Controller modes and target identities;
- corpse route precedence;
- grid and surgery route precedence;
- inventory/backpack/container decisions;
- static dynel pooled fallback decision;
- statel lowest-precedence fallback;
- `UseItemOnItem` action decision.

## Remaining Helpers In GenericCmdMessageHandler

`GenericCmdMessageHandler` still owns shared GenericCmd reply helpers:

- `Acknowledge`
- `AcknowledgeDenied`
- `AcknowledgeWithTarget`
- `AcknowledgeCorpseUse`
- private `Reply` overloads

These are still used as shared outbound helpers by extracted handlers. They are not stale, but they keep `GenericCmdMessageHandler` as both inbound dispatcher and outbound GenericCmd acknowledgement factory.

## Stale Or Cleanup Candidates

No stale private constants or private behavior helpers remain in `GenericCmdMessageHandler`.

Potential cleanup candidates, not changed in this report-only task:

- `GenericCmdMessageHandler` retains broad `using` directives from pre-extraction behavior. They can be cleaned up later by build-verified removal.
- The old comment `TODO: Make this to EntityEnvent or something like this` remains above the using block and no longer describes current dispatcher behavior well.
- Empty XML summary/param comments remain on the handler and ack helpers.
- `GenericCmdUseRouteClassifier` has an explicit final statel fallback check followed by a fallback return of the same route. This is harmless and test-covered but could be simplified later.

## Dispatcher-Only Conclusion

For currently implemented GenericCmd branches, `GenericCmdMessageHandler` is effectively dispatcher-only:

- it logs inbound GenericCmd metadata;
- it selects action branch by `GenericCmdAction`;
- it no-ops `Get` and `Drop`;
- it dispatches `Use` to extracted route handlers;
- it dispatches `UseItemOnItem` to its extracted handler;
- it provides shared GenericCmd ack/reply helpers.

No remaining action branch directly performs gameplay mutation, packet-specific interaction behavior, inventory/container mutation, statel execution, quest progression, shop/trade creation, corpse use, or teleport behavior.

## Recommended Next Cleanup

Recommended next system-separation task outside GenericCmd:

Audit and extract the largest remaining `Playfield.cs` lifecycle responsibility behind a named coordinator and fixture-backed sequence test. The highest-value candidates are NPC death/corpse/despawn flow or same-playfield visibility/SCFU entry flow, because both are packet-order-sensitive and already have lifecycle harness coverage.

Small optional GenericCmd cleanup for later:

- Remove unused `using` directives and stale comments from `GenericCmdMessageHandler` with build validation only.
- Consider moving shared GenericCmd ack/reply helpers into a dedicated `GenericCmdReplyBuilder` or `GenericCmdAcknowledgeService` only if that improves testability without obscuring packet shape.

## Recommended Smoke Tests

Mike should later smoke-test:

- one known statel/door interaction;
- one known inventory/backpack/container use path;
- one known vendor/static dynel use path;
- one known `UseItemOnItem` path if available;
- Rex B18D cargo box interaction if the preview-gated quest path is enabled.
