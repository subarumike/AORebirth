@echo off
cd /d "%~dp0..\AORebirth\Built\Debug"
set CSC=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe
"%CSC%" /nologo /out:_tmp_probe_buckethead_nanos.exe /r:AORebirth.Core.dll /r:AORebirth.Enums.dll /r:Utility.dll /r:MsgPack.dll /r:AORebirth.Interfaces.dll /r:AORebirth.Database.dll /r:AORebirth.ObjectManager.dll /r:AORebirth.Stats.dll /r:SmokeLounge.AOtomation.Messaging.dll /r:Cell.Core.dll /r:AORebirth.Core.Exceptions.dll %~dp0_tmp_probe_buckethead_nanos.cs
if errorlevel 1 exit /b 1
_tmp_probe_buckethead_nanos.exe 300439 300440
