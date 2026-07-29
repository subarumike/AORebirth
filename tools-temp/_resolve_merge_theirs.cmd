@echo off
cd /d "%USERPROFILE%\source\repos\AORebirth"
REM During merge: import GitHub (theirs) for conflicted mission files
git checkout --theirs -- "AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging.Tests/MissionRollSemanticsTests.cs"
git checkout --theirs -- "AORebirth/Server/ZoneEngine/Core/MessageHandlers/MissionEntranceInteractionHandler.cs"
git checkout --theirs -- "AORebirth/Server/ZoneEngine/Core/Missions/MissionOfferCompatibility.cs"
git checkout --theirs -- "AORebirth/Server/ZoneEngine/Core/Missions/MissionOfferTextBuilder.cs"
git checkout --theirs -- "AORebirth/Server/ZoneEngine/Core/Missions/MissionRollService.cs"
git checkout --theirs -- "docs/ai/CURRENT_TASK.md"
git add "AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging.Tests/MissionRollSemanticsTests.cs"
git add "AORebirth/Server/ZoneEngine/Core/MessageHandlers/MissionEntranceInteractionHandler.cs"
git add "AORebirth/Server/ZoneEngine/Core/Missions/MissionOfferCompatibility.cs"
git add "AORebirth/Server/ZoneEngine/Core/Missions/MissionOfferTextBuilder.cs"
git add "AORebirth/Server/ZoneEngine/Core/Missions/MissionRollService.cs"
git add "docs/ai/CURRENT_TASK.md"
git status --short | findstr /b "UU UA UD DU AA"
echo Remaining conflict markers above if any
git commit -m "Merge origin/master into local master (import remote; keep WIP stashed)"
echo COMMIT_EXIT=%ERRORLEVEL%
git log -5 --oneline
git status --short --branch
