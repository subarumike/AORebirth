# CellAO Cross-Reference for AO Client Combat/Corpse/Loot Functions

Generated: 2026-05-09 15:05:13 -05:00

Input map: C:\Users\Mike\Documents\Cellao-Clean\docs\reference\client-dll-function-map\ao_client_dll_combat_corpse_loot_readable_functions.csv

Outputs:
- cellao_cross_reference_combat_corpse_loot.csv - all mapped combat/corpse/loot rows with CellAO hit samples.
- cellao_cross_reference_combat_corpse_loot_summary.csv - grouped status counts.
- cellao_cross_reference_high_value.csv - the small set of functions most relevant to current combat/corpse/loot work.

## Status Counts

| status | count |
| --- | ---: |
| DataOnly | 19 |
| ImplementedOrHookedByAlias | 46 |
| MessageModelOnly | 2 |
| Missing | 559 |
| PartialOrUnknown | 12 |

## High-Value Rows

| client function | status | best CellAO area | key hit paths |
| --- | --- | --- | --- |
| N3Msg_DefaultAttack | DataOnly | EnumOrStats | CellAO\Server\ZoneEngine\XML Data\Stats.xml; CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\GameData\CharacterStat.cs; CellAO\Libra... |
| N3Msg_StopAttack | ImplementedOrHookedByAlias | ZoneMessageHandler | CellAO\Server\ZoneEngine\Core\MessageHandlers\StopFightMessageHandler.cs; CellAO\Server\ZoneEngine\Core\Playfields\Playfield.cs; CellAO\Server\ZoneEngine\ZoneEngine.csproj; Cell... |
| N3Msg_SetLootAccess | DataOnly | EnumOrStats | CellAO\Server\ZoneEngine\XML Data\Stats.xml; CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\GameData\CharacterStat.cs; CellAO\Libra... |
| N3Msg_SetLootAccess | DataOnly | EnumOrStats | CellAO\Server\ZoneEngine\XML Data\Stats.xml; CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\GameData\CharacterStat.cs; CellAO\Libra... |
| N3Msg_UseItem | ImplementedOrHookedByAlias | ZoneMessageHandler | CellAO\Server\ZoneEngine\Core\Controllers\NPCController.cs; CellAO\Server\ZoneEngine\Core\Controllers\PlayerController.cs; CellAO\Server\ZoneEngine\Core\MessageHandlers\Characte... |
| N3Msg_UseItem | ImplementedOrHookedByAlias | ZoneMessageHandler | CellAO\Server\ZoneEngine\Core\Controllers\NPCController.cs; CellAO\Server\ZoneEngine\Core\Controllers\PlayerController.cs; CellAO\Server\ZoneEngine\Core\MessageHandlers\Characte... |
| N3Msg_ContainerAddItem | ImplementedOrHookedByAlias | ZoneMessageHandler | CellAO\Server\ZoneEngine\Core\Controllers\NPCController.cs; CellAO\Server\ZoneEngine\Core\Controllers\PlayerController.cs; CellAO\Server\ZoneEngine\Core\MessageHandlers\Containe... |
| N3Msg_ContainerAddItem | ImplementedOrHookedByAlias | ZoneMessageHandler | CellAO\Server\ZoneEngine\Core\Controllers\NPCController.cs; CellAO\Server\ZoneEngine\Core\Controllers\PlayerController.cs; CellAO\Server\ZoneEngine\Core\MessageHandlers\Containe... |
| N3Msg_DefaultActionOnDynel | ImplementedOrHookedByAlias | ZoneMessageHandler | CellAO\Server\ZoneEngine\Core\MessageHandlers\GenericCmdMessageHandler.cs; CellAO\Server\ZoneEngine\ZoneEngine.csproj; CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\sr... |
| N3Msg_DefaultActionOnDynel | ImplementedOrHookedByAlias | ZoneMessageHandler | CellAO\Server\ZoneEngine\Core\MessageHandlers\GenericCmdMessageHandler.cs; CellAO\Server\ZoneEngine\ZoneEngine.csproj; CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\sr... |
| N3Msg_RequestInfoPacket | ImplementedOrHookedByAlias | ZoneMessageHandler | CellAO\Server\ZoneEngine\Core\MessageHandlers\CharacterActionMessageHandler.cs; CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Mess... |
| N3Msg_RequestInfoPacket | ImplementedOrHookedByAlias | ZoneMessageHandler | CellAO\Server\ZoneEngine\Core\MessageHandlers\CharacterActionMessageHandler.cs; CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Mess... |
| N3Msg_GetCurrentFightMode | MessageModelOnly | MessageModel | CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\GameData\FightModeUpdateEntry.cs; CellAO\Libraries\Source\AOtomation\AOtomation.Mess... |
| N3Msg_GetCurrentFightMode | MessageModelOnly | MessageModel | CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\GameData\FightModeUpdateEntry.cs; CellAO\Libraries\Source\AOtomation\AOtomation.Mess... |
| N3Msg_GetCorrectActionID | ImplementedOrHookedByAlias | ZoneMessageHandler | CellAO\Server\ZoneEngine\ChatCommands\Posture.cs; CellAO\Server\ZoneEngine\Core\Controllers\PlayerController.cs; CellAO\Server\ZoneEngine\Core\Functions\GameFunctions\uploadnano... |
| N3Msg_GetCorrectActionID | ImplementedOrHookedByAlias | ZoneMessageHandler | CellAO\Server\ZoneEngine\ChatCommands\Posture.cs; CellAO\Server\ZoneEngine\Core\Controllers\PlayerController.cs; CellAO\Server\ZoneEngine\Core\Functions\GameFunctions\uploadnano... |
| N3Msg_CanAttack | ImplementedOrHookedByAlias | ZoneMessageHandler | CellAO\Libraries\Source\CellAO.Enums\Operator.cs; CellAO\Server\ZoneEngine\Core\MessageHandlers\AttackMessageHandler.cs; CellAO\Server\ZoneEngine\ZoneEngine.csproj; CellAO\Serve... |
| N3Msg_CanAttack | ImplementedOrHookedByAlias | ZoneMessageHandler | CellAO\Libraries\Source\CellAO.Enums\Operator.cs; CellAO\Server\ZoneEngine\Core\MessageHandlers\AttackMessageHandler.cs; CellAO\Server\ZoneEngine\ZoneEngine.csproj; CellAO\Serve... |
| ToClientDynelDead | ImplementedOrHookedByAlias | ServerRuntime | CellAO\Server\ZoneEngine\Core\Playfields\Playfield.cs |
| ToClientDynelDead | ImplementedOrHookedByAlias | ServerRuntime | CellAO\Server\ZoneEngine\Core\Playfields\Playfield.cs |
| GetAttackRange | DataOnly | EnumOrStats | CellAO\Server\ZoneEngine\XML Data\Stats.xml; CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\GameData\CharacterStat.cs; CellAO\Libra... |

## Reading the CSV

- ImplementedOrHookedExact: exact client function/message name appears in a server runtime or handler area.
- ImplementedOrHookedByAlias: a likely CellAO equivalent exists under a different name.
- MessageModelOnly: AOtomation has a message class/serializer model, but ZoneEngine has no obvious handler/runtime path.
- DataOnly: only enum, stat, database, or schema support was found.
- Missing: no sampled code hit found for the generated aliases.

Important: generated documentation/maps are excluded from the search so hits represent existing CellAO code, not this new mapping work.
