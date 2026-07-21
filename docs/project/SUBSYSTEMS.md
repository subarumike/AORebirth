# ZoneEngine Subsystems

Gameplay systems live in their own folder under `AORebirth/Server/ZoneEngine/Core/<System>/`.
Do not grow new mail/pets/bank/quest logic inside `Playfields/Playfield.cs`.

## Layout (Zone runtime)

```
ZoneEngine/Core/
  Navigation/    - Global NPC chase planning/following and playfield providers
  Mail/          ← Mail Terminal ecosystem (active)
  GMI/           ← Global Market vault (MarketSend deposit; web UI separate)
  Perks/         ← TrainPerk / UsePerk / AddPerkAction (session-trained; capture-backed)
  Arete/         ← Arete dialogue/quests (existing pattern)
  Playfields/    ← World space / visibility / population only
  MessageHandlers/  ← Thin handlers OR move handler into the subsystem folder
  Pets/          ← Next extraction target (still scattered as Pet*.cs today)
```

## What belongs in a subsystem

| Keep in subsystem | Leave elsewhere |
| --- | --- |
| Runtime service (`MailRuntimeService`) | Shared inventory/stats helpers (`InventoryItemRules` mail flags OK) |
| System message handler (`MailMessageHandler`) | Wire models/serializers in `AOtomation.Messaging` |
| System-specific rules/constants | Playfield spawn/visibility |

Messaging contracts (`MailMessage`, serializer) stay in `AOtomation.Messaging` — they are the shared protocol layer, not Zone gameplay.

## Workflow so pulls do not wipe work

1. **Commit the subsystem before every `git pull`.** Uncommitted subsystem folders get dropped by rebase.
2. **Always merge, never rebase local work onto origin:**  
   `git pull --no-rebase origin master`
3. Prefer one focused commit per subsystem (`Mail: …`, `Pets: …`) so conflict ownership is obvious.
4. Push after the subsystem commit so GitHub is the backup (not only the local machine).

## Extraction rule for agents

When starting or continuing work on Mail, Pets, Insurance, Bank, Trade, etc.:

1. Put new code under `Core/<System>/`.
2. Do not add more gameplay orchestration into `Playfield.cs`.
3. Update `ZoneEngine.csproj` Compile includes for the new paths.
4. Document the move in `docs/ai/CURRENT_TASK.md`.
