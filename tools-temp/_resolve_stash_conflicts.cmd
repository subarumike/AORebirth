@echo off
cd /d "%USERPROFILE%\source\repos\AORebirth"
REM stash apply: --theirs = stash (local WIP), --ours = current HEAD (merged github)

REM LFT / team WIP from stash
git checkout --theirs -- "AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/CharacterActionType.cs"
git checkout --theirs -- "AORebirth/Server/ChatEngine/Lists/TeamLevelRanges.cs"
git checkout --theirs -- "AORebirth/Server/ChatEngine/PacketHandlers/LftSearch.cs"
git checkout --theirs -- "AORebirth/Server/ZoneEngine/Core/MessageHandlers/CharacterActionMessageHandler.cs"

REM Mission / dungeon imports from merged HEAD
git checkout --ours -- "AORebirth/Server/ZoneEngine/Core/MessageHandlers/QuestAlternativeMessageHandler.cs"
git checkout --ours -- "AORebirth/Server/ZoneEngine/Core/Missions/MissionCompleteService.cs"
git checkout --ours -- "AORebirth/Server/ZoneEngine/Core/Missions/MissionOfferCompatibility.cs"
git checkout --ours -- "AORebirth/Server/ZoneEngine/Core/Missions/MissionOfferTextBuilder.cs"

git add "AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/CharacterActionType.cs"
git add "AORebirth/Server/ChatEngine/Lists/TeamLevelRanges.cs"
git add "AORebirth/Server/ChatEngine/PacketHandlers/LftSearch.cs"
git add "AORebirth/Server/ZoneEngine/Core/MessageHandlers/CharacterActionMessageHandler.cs"
git add "AORebirth/Server/ZoneEngine/Core/MessageHandlers/QuestAlternativeMessageHandler.cs"
git add "AORebirth/Server/ZoneEngine/Core/Missions/MissionCompleteService.cs"
git add "AORebirth/Server/ZoneEngine/Core/Missions/MissionOfferCompatibility.cs"
git add "AORebirth/Server/ZoneEngine/Core/Missions/MissionOfferTextBuilder.cs"

echo Remaining UU:
git diff --name-only --diff-filter=U
echo FINDSTR markers:
findstr /s /n /c:"<<<<<<<" "AORebirth\Server\ChatEngine\PacketHandlers\LftSearch.cs" "AORebirth\Server\ChatEngine\Lists\TeamLevelRanges.cs" "AORebirth\Server\ZoneEngine\Core\MessageHandlers\CharacterActionMessageHandler.cs" "AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Messages\N3Messages\CharacterActionType.cs" 2>nul
