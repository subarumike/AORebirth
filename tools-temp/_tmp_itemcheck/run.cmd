@echo off
set CSC=C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe
if not exist "%CSC%" set CSC=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe
if not exist "%CSC%" set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set OUT=C:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug
cd /d C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_itemcheck
"%CSC%" /nologo /r:%OUT%\AORebirth.Core.dll /r:%OUT%\AORebirth.Enums.dll /r:%OUT%\SmokeLounge.AOtomation.Messaging.dll /r:%OUT%\Utility.dll /r:%OUT%\MsgPack.dll /out:%OUT%\check_items.exe Program.cs
if errorlevel 1 exit /b 1
cd /d %OUT%
check_items.exe
