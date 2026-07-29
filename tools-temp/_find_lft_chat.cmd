@echo off
setlocal
set "T=C:\Users\nermi\.cursor\projects\c-Users-nermi-source-repos-AORebirth\agent-transcripts"
rg -n -e LFT -e LookingForTeam -e TeamSearch -e "Looking For Team" -e "unknown message 1500" -e "unknown message 1502" -e "case 1500" -e "case 1502" "%T%" --glob "*.jsonl"
