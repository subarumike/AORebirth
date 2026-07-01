# GenericCmd Remaining Route Audit

Generated: 2026-06-30 22:24 local

Scope: no-behavior-change audit after the `GenericCmdAction.Use` route extractions in `AORebirth/Server/ZoneEngine/Core/MessageHandlers/GenericCmdMessageHandler.cs`.

## Summary

`GenericCmdMessageHandler` is now mostly a dispatcher for `Use` routes, but it still directly implements `UseItemOnItem`. The remaining direct behavior is small enough to extract safely without changing current packet, stat, item, or statel behavior.

## Remaining Actions

| Action | Id | Current behavior in handler | Side effects | Risk | Recommendation |
| --- | ---: | --- | --- | --- | --- |
| `Get` | `1` | Empty branch, then break. | None. | Low. | Leave as no-op in dispatcher. |
| `Drop` | `2` | Empty branch, then break. | None. | Low. | Leave as no-op in dispatcher. |
| `Use` | `3` | Dispatcher chain through extracted route handlers. Rex B18D observation remains first. | Delegates to per-system handlers; Rex path can still acknowledge directly. | Medium. | Do not change in this task. |
| `UseItemOnItem` | `5` | Directly gets the source item from a character-owned inventory page, sets `secondaryitemtemplate`, then routes the second target to pooled `StaticDynel` `OnUseItemOn` or statel `OnUseItemOn`. | Mutates character stat `secondaryitemtemplate`; can perform static dynel event; can call statel event. No GenericCmd ack is sent in this branch. | Medium. | Extract now into a dedicated handler. |

## Selected Extraction Candidate

Selected candidate: `UseItemOnItem`.

Reason:

- It is the only remaining direct non-empty non-Use action branch.
- It has a clear action match: `GenericCmdAction.UseItemOnItem`.
- Its side effects are isolated to the current item lookup/stat mutation and second-target event dispatch.
- Extraction can preserve all existing assumptions, including target indexing, no ack behavior, pooled dynel lookup, and statel fallback.

## Behavior To Preserve

- Source item lookup remains:
  - page identity type from `client.Controller.Character.Identity.Instance`
  - page identity instance from `(int)message.Target[0].Type`
  - slot from `message.Target[0].Instance`
- `secondaryitemtemplate` remains set to the source item `LowID`.
- `secondaryitemtype` remains untouched.
- If `Pool.Instance.Contains(message.Target[1])`, the target is fetched as a `StaticDynel` from the character playfield.
- If the static dynel has an `OnUseItemOn` event, that event is performed.
- If the second target is not in the pool, `client.Controller.UseStatel(message.Target[1], EventType.OnUseItemOn)` is called.
- No GenericCmd ack is added.

## Extraction Recommendation

Create:

- `UseItemOnItemInteractionHandler`
- `UseItemOnItemInteractionRules`

Keep `GenericCmdMessageHandler` as the action dispatcher and move only the current `UseItemOnItem` branch body.
