# Grid Terminal C0040320 Destination Mapping

## Scope

Correct the destination evidence for Borealis `Terminal:C0040320`.

## Evidence

- Current-client statel data `AORebirth/Built/Debug/playfields.dat` contains `PF 800`, `Terminal:C0040320`, template `95350` (`Enter The Grid`) at `(636.4026, 66.8100, 728.8094)`.
- Its `OnUse` `TeleportProxy2` maps to `PF 152`, destination index `0`, destination instance `C0000098`.
- `PF 152` contains `Terminal:C0000098`, template `95351` (`Exit the Grid`) at `(170.0766, 4.6002, 240.9393)`, heading `(0, 0, 0, 1)`.
- Read-only `teleports` DB inspection found no terminal override for `Terminal:C0040320`; the only `C0040320` DB row uses a non-terminal statel type and points to playfield `2063`, so it does not apply to this grid terminal.

## Corrected Capture Attribution

- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260621-091447/events.log:255-256` shows the local player used `Terminal:C002028F`, not `Terminal:C0040320`.
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260621-091447/events.log:321-322` shows the server echo for that same `Terminal:C002028F` use.
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260621-091447/events.log:645-646` shows the resulting local grid spawn at `(234.3062, 3.7750, 212.8138)` with heading `(0, 1, 0, -4.371139E-08)`.
- Nearest `PF 152` exit anchor to that captured landing is `Terminal:C04E0098`, template `95351`, at `(237.5000, 4.2000, 217.4000)`.
- `Terminal:C0040320` appears later in the same capture as a spawned Borealis statel (`events.log:2925-2926`) and as other players' use events, but that capture does not prove a local `C0040320` landing transform.

## Result

`Terminal:C0040320` must not use the captured `(234.3062, 3.7750, 212.8138)` landing. For the private server, it should route through its own `TeleportProxy2` destination and spawn adjacent to `Terminal:C0000098` at `(170.0766, 4.6002, 243.4393)` with heading `(0, 0, 0, 1)`, pending live smoke confirmation.
