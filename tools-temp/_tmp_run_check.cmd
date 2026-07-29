@echo off
C:\xampp\mysql\bin\mysql.exe -u root cellao_codex_clean -N -e "SELECT HEX(stats) FROM staticdynels WHERE Instance=14428396;" > C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_stats.hex
cd /d C:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug
C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe /nologo /r:Utility.dll /r:SmokeLounge.AOtomation.Messaging.dll /r:MsgPack.dll /out:check_stats.exe C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_itemcheck\Program.cs
if errorlevel 1 exit /b 1
check_stats.exe
