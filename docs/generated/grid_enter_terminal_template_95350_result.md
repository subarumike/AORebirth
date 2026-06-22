# Grid Enter Terminal Template 95350

## Scope

Handle current-client `Enter The Grid` statels that use `TemplateId 95350` and `TeleportProxy2` to grid playfield `152`.

## Evidence

Source: packed playfield data `AORebirth/Datafiles/playfields.dat`.

Observed broken/tested terminals:

| Playfield | Identity | Template | Position | Requirements | Destination |
| --- | --- | --- | --- | --- | --- |
| `800` | `Terminal:C0040320` | `95350` (`Enter The Grid`) | `(636.4026, 66.8100, 728.8094)` | statel `TeleportProxy2` requirements | `PF 152`, index `0`, instance `C0000098` |
| `6007` | `Terminal:C0001777` | `95350` (`Enter The Grid`) | `(217.2726, 6.020095, 182.065)` | `Computer Literacy > 19`, `isfightingme == 0` | `PF 152`, index `0`, instance `C0000098` |
| `556` | `Terminal:C002022C` | `95350` (`Enter The Grid`) | `(1804.096, 14.07634, 619.4999)` | `Computer Literacy > 80`, `isfightingme == 0` | `PF 152`, index `0`, instance `C0000098` |

Destination evidence:

| Playfield | Identity | Template | Base Position | Heading | Landing |
| --- | --- | --- | --- | --- | --- |
| `152` | `Terminal:C0000098` | `95351` (`Exit the Grid`) | `(170.0766, 4.600221, 240.9393)` | `(0,0,0,1)` | `(170.0766, 4.600221, 243.4393)` |

The landing applies the same `2.5` forward clearance used by legacy `teleportproxy2`.

## Root Cause

The packed statel data points these terminals at destination instance `C0000098` in playfield `152`. Legacy `teleportproxy2` resolves the destination with `GetDoor(C0000098)`, but the packed destination object is `Terminal:C0000098`, template `95351`, not a `Door`. The result is the client stopping after statel lookup.

The prior captured landing `(234.3062, 3.7750, 212.8138)` is not a `Terminal:C0040320` landing. Capture `20260621-091447` shows local use of `Terminal:C002028F`, then grid spawn near `Terminal:C04E0098`; keep that capture tied to `C002028F` only.

Capture `20260622-003221` proves additional terminal-specific landings even though the inspected source statels all expose the same raw `TeleportProxy2` args `[51102,152,0,0]`. The captured routes are documented in `docs/generated/grid_terminal_live_capture_20260622_result.md` and must remain terminal-specific.

## Implementation

The server now handles `TemplateId 95350` terminals whose `OnUse` event has `TeleportProxy2` to playfield `152`. It preserves the statel requirements and failure feedback, resolves destination statels by instance plus `TemplateId 95351`, and applies live-captured/user-submitted route entries for terminals whose exact grid landing has been captured. `Terminal:C002022C` and `Terminal:C0040320` now have user-submitted landing evidence documented in `docs/generated/grid_terminal_live_capture_20260622_result.md`.
