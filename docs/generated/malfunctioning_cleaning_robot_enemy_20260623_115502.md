# Malfunctioning Cleaning Robot Enemy Capture

Capture folder:

`tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260623-115502`

Implemented enemy:

- Name: `Malfunctioning Cleaning Robot`
- Captured playfield: `1044525`
- Level: `1`
- Health: `12/12`
- MonsterData: `297023`
- MonsterScale: `200`
- VisualFlags: `31`
- RunSpeedBase: `5`
- NPCFamily: `1019`
- Death action key: `500`

Captured behavior path:

1. Player starts attack against `SimpleChar:78FD608C`.
2. Player damage is applied to the robot.
3. Robot sends attack/follow/movement state back toward the player.
4. Robot health updates are visible after damage.
5. Robot stops fighting.
6. Robot sends `CharacterAction Death`.
7. Dead robot despawns on the existing 10 second dead-NPC cleanup path.

Implementation notes:

- The robot is registered as a focused combat-test archetype using the captured static values.
- Existing combat handling supplies attack, damage, NPC follow, health updates, stop-fight, death action, and delayed despawn.
- The captured corpse/loot replacement body is not implemented here; the robot uses the captured missing/placeholder visual id only as the static catmesh/displaycatmesh value and is excluded from known corpse visual mapping.
- No broad AI, reward, loot, or multi-enemy system was added.
