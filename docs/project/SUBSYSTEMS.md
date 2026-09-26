# ZoneEngine_New Subsystems

Gameplay systems live in their own folder under
`AORebirth/Server/ZoneEngine_New/Core/<System>/`. Do not grow new system
orchestration inside `Core/Playfield/Playfield.cs`.

## Layout (Zone runtime)

```
ZoneEngine_New/Core/
  Ai/               - Reusable NPC behavior
  Dialogue/         - Dialogue mechanics and routing
  Inventory/        - Zone inventory mechanics
  Missions/         - Mission mechanics and lifecycle
  Mobs/             - NPC activation and construction
  Movement/         - Character movement authority
  Playfield/        - World space, visibility and population orchestration
  Trade/            - Player trade mechanics
  MessageHandlers/  - Thin zone message dispatch
```

## What belongs in a subsystem

| Keep in subsystem | Leave elsewhere |
| --- | --- |
| System runtime service | Shared inventory/stat primitives |
| Thin system handler or dispatcher | Wire models/serializers in `AOtomation.Messaging` |
| System-specific reusable rules | Playfield world/visibility orchestration |

Messaging contracts and serializers stay in `AOtomation.Messaging`; they are
the shared protocol layer, not Zone gameplay.

## Workflow so pulls do not wipe work

1. **Commit the subsystem before every `git pull`.** Uncommitted subsystem folders get dropped by rebase.
2. **Always merge, never rebase local work onto origin:**  
   `git pull --no-rebase origin master`
3. Prefer one focused commit per subsystem (`Mail: …`, `Pets: …`) so conflict ownership is obvious.
4. Push after the subsystem commit so GitHub is the backup (not only the local machine).

## Extraction rule for agents

When starting or continuing a gameplay subsystem:

1. Put new code under `Core/<System>/`.
2. Do not add more gameplay orchestration into `Playfield.cs`.
3. Local C# files under the project directory are included automatically;
   update `ZoneEngine_New.csproj` only when an external linked source or other
   project metadata actually requires it.
4. Document the move in `docs/ai/CURRENT_TASK.md`.
