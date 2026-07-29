@echo off
git show 6f88f211:AORebirth/Server/ZoneEngine/Core/MessageHandlers/CharacterInfoPacketMessageHandler.cs > tools-temp\_info_handler_6f88.cs
findstr /n "internal void Send FindByIdentity npcfamily" tools-temp\_info_handler_6f88.cs
echo ==== DIFF vs HEAD working tree ====
git diff HEAD -- AORebirth/Server/ZoneEngine/Core/MessageHandlers/CharacterInfoPacketMessageHandler.cs
