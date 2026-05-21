# Live Packet Implementation Gaps

This note maps the latest passive live capture back to the current CellAO code.

Capture used:

- `tools-temp\live-pcaps\private-server-quest-batch\2026-05-10_23-44-53`
- Decoded counts: 5812 packets, 536 C2S frames, 1498 S2C frames, 5 clean quest rows.
- Loot-focused comparison capture: `tools-temp\live-pcaps\private-server-loot\2026-05-10_22-35-30`
- Loot capture counts: 7377 packets, 846 C2S frames, 6839 S2C frames, 26 loot body rows, 72 loot drop rows.

## Capture Pattern

The useful live quest/combat/loot packet families from this capture are:

- C2S: `Attack`, `LookAt`, `CharacterAction`, `GenericCmd`, `ClientMoveItemToInventory`, `KnuBotOpenChatWindow`, `KnuBotAnswer`, `KnuBotCloseChatWindow`, `StopFight`.
- S2C: `SimpleCharFullUpdate`, `Stat`, `Despawn`, `AttackInfo`, `StopFight`, `Attack`, `CorpseFullUpdate`, `HealthDamage`, `QuestFullUpdate`, `Quest`, `Feedback`, `ChatText`, `ContainerAddItem`, `InventoryUpdate`, `NewLevel`, `KnuBot*`.

The first clean `QuestFullUpdate` after login replayed active one-shot missions already in the mission window. It should not be treated as a quest-start packet. Later clean `QuestFullUpdate` rows are objective progress or completion updates.

## Implemented Or Partly Implemented

- `Attack`: `Core\MessageHandlers\AttackMessageHandler.cs` accepts the Q attack toggle, sets target/fighting target, and echoes attack state to the playfield. It does not itself perform swing timing or damage.
- `GenericCmd Use`: `Core\MessageHandlers\GenericCmdMessageHandler.cs` now routes corpse use through `TryUseCorpse` / `TryUseDeadNpcCorpse` and acknowledges corpse use with the routed corpse identity.
- `ClientMoveItemToInventory`: `Core\MessageHandlers\ClientMoveItemToInventoryMessageHandler.cs` routes loot-window item moves through `TryLootCorpseItem`.
- `SimpleCharFullUpdate`: `Core\Packets\SimpleCharFullUpdate.cs` is the main visible NPC/mob spawn packet. NPC data depends on `NPCFamily`, `LosHeight`, `MonsterData`, health, level, and movement bytes.
- `Stat`, `ChatText`, `Feedback`, `ContainerAddItem`, `InventoryUpdate`, `Despawn`, `StopFight`, and `KnuBot*` have message-handler classes present.

## Important Gaps

- There is no dedicated `CorpseFullUpdateMessageHandler.cs` in `Core\MessageHandlers`. Corpse creation appears to be owned by playfield/corpse logic, but packet generation should be treated as a first-class server behavior because live death/loot depends on a distinct corpse dynel.
- Combat is split across attack state, controller timers, and playfield death/corpse handling. The live packet sequence shows attack state and damage are separate from corpse creation. Avoid fixing corpse issues inside `AttackMessageHandler` unless the live packet evidence points there.
- Live combat timeline decoding now maps the captured S2C `AttackInfo`, `HealthDamage`, `MissedAttackInfo`, `Attack`, and `StopFight` bodies against the existing AOtomation message classes. The common N3 body layout is identity, one base unknown byte, then message-specific fields. That base byte matters: C2S `Attack` target identities begin after it, and parsing one byte early produces bogus `195:*` targets.
- Packet coverage now distinguishes ZoneEngine server implementation from AOtomation message models. Several important packets, including `AttackInfo`, `CorpseFullUpdate`, and `HealthDamage`, are no longer treated as unknown wire shapes; they are message-model-only gaps that still need ZoneEngine lifecycle ownership.
- Quest support is mostly packet-level/KnuBot-level. The capture proves live progress uses `QuestFullUpdate` plus `Quest`, `Feedback`, `ChatText`, `Stat`, and sometimes inventory/reward packets. CellAO needs a small quest-state service before these one-shot shuttleport missions can be represented cleanly.
- Quest reward export now writes `quest_reward_events.csv`. The known quest batch shows a focused completion sequence around t+789.169: `QuestState` stat changes, `NewLevel`, `Feedback` with category/message ids, `Quest`, `ChatText` `Mission Complete.`, then a follow-up `Quest` packet. Future captures with reward text such as XP/cash chat should land in the same file as `mission_xp_chat` or `mission_reward_chat`.
- `ClientMoveItemToInventory` is decoded as N3 identity, base unknown byte, full source container identity, then 32-bit target placement. The source container instance can encode temporary loot-window page/slot hints such as `Backpack:00700000`, so collapsing it to `Backpack:00000000` loses useful evidence.
- Loot data is structurally modeled in DB tables, but current seed data has no mob `DropHashes` assignments. The working local loot path is deterministic test loot, not real mob drop tables.
- The S2C stat parser must keep using the live layout: identity, one unknown/flags byte, count, then `count * (stat,value)`. Older offset assumptions decode reward and quest state incorrectly.
- The live loot capture confirms the corpse-open path is corpse-session based: `CorpseFullUpdate`, C2S `GenericCmd Use` against the corpse identity, S2C `InventoryUpdate`, C2S `ClientMoveItemToInventory`, then later corpse `Despawn`.

## Safest Next Code Path

1. Keep the capture tooling as source-of-truth validation: `tools-temp\live-data-collector\Test-LiveDataCollector.ps1`.
2. Add a focused server-side corpse packet test around the existing playfield corpse lifecycle. Validate: death triggers `Despawn` of live mob identity, `CorpseFullUpdate` of corpse identity, `GenericCmd Use` opens loot, `ClientMoveItemToInventory` removes selected loot, empty corpse despawns.
3. Only after corpse tests are stable, wire a DB-backed loot roller behind the same `TryUseCorpse` / `TryLootCorpseItem` path.
4. Treat quests separately from combat/loot. Start with replaying one captured `QuestFullUpdate` shape for a local one-shot test quest, then add KnuBot answer routing.

## Automation

Run the offline collector self-check with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools-temp\live-data-collector\Test-LiveDataCollector.ps1
```

Current expected known-capture assertions:

- At least 1000 S2C rows.
- At least 100 C2S rows.
- At least 5 clean quest rows.
- At least 30 packet coverage rows.

The decode pipeline also writes:

- `live_packet_coverage.csv`
- `live_packet_coverage.md`
- `quest_reward_events.csv`
- `live_combat_loot_timeline.csv`
- `live_corpse_sessions.csv`
- `live_combat_loot_timeline.md`

The current known capture reports 44 observed packet families. Use `live_packet_coverage.md` to split those into full server matches, partial server matches, message-model-only packets, and packets with no local model/server match.
