# Vendored from Lost-Eden

Source: `Lost-Eden` repository, `Assets/Scripts/Vehicle`, commit
`cbd17258b290e4af85b71c7364e461ac71f8ace7` (2026-09-24).

These files are a reverse-engineered port of the AO client's `Vehicle.dll` /
`Gamecode.dll` / `N3.dll` movement code (`Vehicle_t`, `CharVehicle_t`,
`NPCVehicle_t`, `Path_t`, `PathGuide_t`, `Surface_i`, `n3TilemapSurface_t`,
`CellSurface_t`). Lost-Eden's `Docs/Movement.md` is the write-up behind the
addresses quoted in the comments.

Copied verbatim:

- `Vec3.cs`, `Quat.cs`, `SteeringResult.cs`
- `VehicleSim.cs`, `CharVehicleSim.cs`, `NpcVehicleSim.cs`, `Path.cs`
- `PlayfieldCellSurface.cs`
- `Surface/ISurface.cs`, `Surface/ITileHeightSource.cs`,
  `Surface/ChunkedTileHeightSource.cs`, `Surface/TilemapSurface.cs`,
  `Surface/GridSpace.cs`, `Surface/CellSurface.cs`,
  `Surface/TriangleMeshSurface.cs`

Not copied: the camera vehicles, `VehicleUnityExtensions.cs`,
`WorldCollision.cs` and `PlayfieldTileSurface.cs` (Unity-bound). The server's
replacement for `PlayfieldTileSurface` is
`AORebirth.World.Collision/PlayfieldSurfaceFactory.cs`, which builds the same
surfaces from `PlayfieldCollisionSet`.

Server deviations from the Lost-Eden copy:

- `VehicleSim.DeltaTimeNow` is thread-static. Every playfield heartbeat steps
  its vehicles on its own thread, and stock's process-wide static would be
  shared between them.

The matching Lost-Eden tests live in `../AORebirth.World.Vehicle.Tests`
(camera tests excluded). When refreshing from Lost-Eden, copy the files above,
re-apply the deviations, and update the commit hash.
