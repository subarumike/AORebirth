# Door line of sight

Branch `fix/door-line-of-sight`. Status: builds; not yet verified in the AO client; the new unit test has not been run.

## What changed

Line of sight (`PlayfieldWorldSimulation.HasLineOfSight`) only raycast static world geometry, so NPCs aggroed,
attacked and cast nanos through closed doors. A closed `Door` now blocks any ray that crosses its doorway.

- `Door.BlocksSegment` holds the geometry: the door's plane (normal = rotation * +Z, the same axis the client's
  walk-in check uses) and a doorway extent around the door position.
- `QuestDungeonPlayfield.SpawnDoors` registers each door with the playfield's world simulation.
- `ZoneEngine_New.Tests/DoorLineOfSightTests.cs` covers the geometry.

The check is shared, so players cannot attack through a closed door either.

## Scope

Only quest (mission) dungeons are covered. They are the only playfields where the server creates `Door` dynels and
knows whether a door is open.

Static dungeons such as the subway (playfield 127) are **not** covered. Their rooms list door connections in
`Rooms.json`, but the server spawns no door objects there and has no open/closed state, so line of sight through
those doorways is unchanged. Covering them means spawning server doors in static dungeons, which needs a door
template per playfield and the matching door packets.

## Risks

- **Doorway size is a guess.** `Door.BlocksSegment` uses a half-width of 2 and a half-height of 3 units around the
  door position. A dungeon style with a wider doorway would let rays through at the edges; a much narrower one next
  to an open door less than 2 units away could block that neighbour. The walls around a doorway are static
  collision and still block on their own.
- **Up to 100 ms stale.** Door state goes through the existing line-of-sight cache, so a mob can react up to 100 ms
  after a door closes or opens.
- **Every line-of-sight check loops over the playfield's doors.** A dungeon has tens of doors and results are
  cached, so this should be cheap, but it has not been profiled.
- **Doors are never unregistered.** They live as long as their dungeon instance, which is disposed with its
  simulation. A door removed mid-instance would keep blocking.
- **NPC movement is untouched.** This changes what NPCs can see and attack, not where they can walk. Whether a mob
  already in combat paths through a closed door was not examined.

## Verification still needed

1. Run `DoorLineOfSightTests`.
2. In a quest dungeon: stand behind a closed door with a mob on the other side and confirm no aggro and no
   attacks; open the door and confirm both resume.
3. Confirm attacks across an open doorway and inside a single room are unaffected.
