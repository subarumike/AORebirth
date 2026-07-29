@echo off
git grep -n SendTeamInviteRequest 9d0a2cd9 -- AORebirth
git grep -n SendTeamMember 9d0a2cd9 -- AORebirth/Server/ZoneEngine
git grep -n "Parameter2 == 17" 9d0a2cd9 -- AORebirth
git grep -n TeamMemberLeft 9d0a2cd9 -- AORebirth/Libraries AORebirth/Server/ZoneEngine
