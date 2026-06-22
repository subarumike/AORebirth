# Grid Line Teleport Pad Result

## Scope

Fix PF `152` grid level pads that move the player up or down inside the grid.

## Evidence

User location on the non-working pad:

`(231.3, 3.8, 132.9)` in PF `152` Grid.

Packed `playfields.dat` identifies the nearby statel:

| Identity | Template | Position | Event | Function |
| --- | --- | --- | --- | --- |
| `Terminal:C01A0098` | `95351` | `(231.229, 3.901, 132.863)` | `OnTargetInVicinity` | `LineTeleport [100001,2228376,0]` |

The same PF `152` data contains other grid level pads using `LineTeleport` with destination playfield argument `0`, meaning the destination is the current playfield rather than literal PF `0`.

## Root Cause

`LineTeleport` interpreted the third statel argument as an absolute playfield id. For grid level pads that pass `0`, AORebirth attempted to resolve destination data from PF `0` instead of PF `152`.

## Fix

`LineTeleport` now resolves destination playfield `0` to the character's current playfield before looking up the destination index. The handler also fails closed when the character, target playfield, destination index, or destination wall segment is invalid.

No generic statel semantics were added.
