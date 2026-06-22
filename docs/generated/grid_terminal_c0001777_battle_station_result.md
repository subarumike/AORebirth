# Battle Station Grid Terminal C0001777

## Scope

Fix `Terminal:C0001777` use in playfield `6007` so it transfers back to the grid instead of stopping after statel lookup.

## Evidence

- Packed playfield data: `AORebirth/Datafiles/playfields.dat`
- Source statel: playfield `6007`, identity `Terminal:C0001777`, template `95350` (`Enter The Grid`)
- Source position: `(217.2726, 6.020095, 182.065)`
- Source heading: `(0, 0.4733359, 0, 0.880882)`
- OnUse function: `TeleportProxy2`
- OnUse arguments: identity type `51102`, destination playfield `152`, destination door index `0`
- Requirement 1: `Computer Literacy > 19`
- Requirement 2: `isfightingme == 0`

## Destination

- Destination playfield: `152`
- Destination statel: `Terminal:C0000098`
- Destination template: `95351` (`Exit the Grid`)
- Destination base position: `(170.0766, 4.600221, 240.9393)`
- Destination heading: `(0, 0, 0, 1)`
- Existing `TeleportProxy2` forward clearance: `2.5`
- Implemented landing: `(170.0766, 4.600221, 243.4393)`

## Root Cause

`Terminal:C0001777` has a valid `TeleportProxy2` event to playfield `152`, but legacy `teleportproxy2` resolves the destination with `GetDoor(C0000098)`. The destination object in packed playfield data is `Terminal:C0000098`, not `Door:C0000098`, so the generic statel route does not complete the transfer.

## Implementation Note

The fix is scoped to `Terminal:C0001777` in playfield `6007`. It preserves the captured/statel requirements and routes directly to the destination terminal transform in playfield `152`.
