@echo off
setlocal EnableExtensions DisableDelayedExpansion

rem Builds the client patch as an AOPlugins plugin: artifacts\aoplugin\aorebirth-patch.dll and the launcher files in
rem artifacts\aoplugin\aorebirth-patch\ - copy both into the client's AOPlugins\ folder (next to AOPlugins'
rem version.dll). AOPLUGINS_DIR: a checkout of https://github.com/McAndersDK/aoplugins (for include\aop_plugin.h).

set "ROOT=%~dp0"
set "SRC=%ROOT%src"
set "PATCH_VERSION=2"
set "SOURCE_SHA=unknown"
if not defined AOPLUGINS_DIR set "AOPLUGINS_DIR=%ROOT%..\..\..\..\AOPlugins"
if not exist "%AOPLUGINS_DIR%\include\aop_plugin.h" (
  echo [AORebirthClientPatch] ERROR aop_plugin.h not found under "%AOPLUGINS_DIR%\include" - set AOPLUGINS_DIR.
  exit /b 1
)

set "GITRESULT=%TEMP%\AORebirthClientPatch-git-%RANDOM%-%RANDOM%.txt"
git -C "%ROOT%..\..\.." rev-parse --short=12 HEAD > "%GITRESULT%" 2>nul
if not errorlevel 1 set /p SOURCE_SHA=<"%GITRESULT%"
del /Q "%GITRESULT%" >nul 2>nul

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
  echo [AORebirthClientPatch] ERROR vswhere.exe not found.
  exit /b 1
)
set "VSROOT="
set "VSRESULT=%TEMP%\AORebirthClientPatch-vswhere-%RANDOM%-%RANDOM%.txt"
"%VSWHERE%" -latest -products * -version "[18.0,19.0)" -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath > "%VSRESULT%"
set /p VSROOT=<"%VSRESULT%"
del /Q "%VSRESULT%" >nul 2>nul
if not defined VSROOT (
  echo [AORebirthClientPatch] ERROR Visual Studio 18 C++ x86 tools were not found.
  exit /b 1
)
call "%VSROOT%\VC\Auxiliary\Build\vcvars32.bat" >nul
if errorlevel 1 (
  echo [AORebirthClientPatch] ERROR Visual Studio x86 environment setup failed.
  exit /b 1
)

set "OUT=%ROOT%artifacts\aoplugin"
set "OBJ=%TEMP%\AORebirthClientPatch-plugin-%RANDOM%-%RANDOM%"
mkdir "%OBJ%" || exit /b 1
if not exist "%OUT%" mkdir "%OUT%" || exit /b 1

echo [AORebirthClientPatch] Building x86 static-CRT AOPlugins plugin...
pushd "%OBJ%"
cl /nologo /std:c++17 /O2 /GL /Gy /EHsc /W4 /WX /MT ^
  /DUNICODE /D_UNICODE /DWIN32_LEAN_AND_MEAN /DNOMINMAX /DAO_REBIRTH_AOPLUGIN ^
  /DAO_REBIRTH_CLIENT_PATCH_VERSION=\"%PATCH_VERSION%\" ^
  /DAO_REBIRTH_CLIENT_PATCH_SOURCE_SHA=\"%SOURCE_SHA%\" ^
  /I"%SRC%" /I"%AOPLUGINS_DIR%\include" /LD ^
  "%SRC%\aoplugin_main.cpp" ^
  "%SRC%\crash_dump.cpp" ^
  "%SRC%\daily_login_routing.cpp" ^
  "%SRC%\dllmain.cpp" ^
  "%SRC%\gui_rect_fix.cpp" ^
  "%SRC%\logging.cpp" ^
  "%SRC%\login_key_patch.cpp" ^
  "%SRC%\playfield_name_fix.cpp" ^
  "%SRC%\randy_color_fix.cpp" ^
  "%SRC%\roomspace_fix.cpp" ^
  /link /DLL /MACHINE:X86 /LTCG /OPT:REF /OPT:ICF /DYNAMICBASE /NXCOMPAT ^
  /guard:cf /Brepro /OUT:"%OUT%\aorebirth-patch.dll" bcrypt.lib user32.lib
set "RESULT=%ERRORLEVEL%"
popd
rmdir /S /Q "%OBJ%" >nul 2>nul
if not "%RESULT%"=="0" (
  echo [AORebirthClientPatch] ERROR plugin build failed.
  exit /b 1
)
del /Q "%OUT%\aorebirth-patch.lib" "%OUT%\aorebirth-patch.exp" >nul 2>nul

rem The launcher's pages, dimension server and dimension list (AopLauncherQuery in aoplugin_main.cpp): read from
rem these, the game's own files are left as they are.
if not exist "%OUT%\aorebirth-patch" mkdir "%OUT%\aorebirth-patch" || exit /b 1
copy /Y "%ROOT%AORebirthAnarchyLauncher.url" "%OUT%\aorebirth-patch\AnarchyLauncher.url" >nul || exit /b 1
copy /Y "%ROOT%AORebirthDimensionServer.url" "%OUT%\aorebirth-patch\DimensionServer.url" >nul || exit /b 1
copy /Y "%ROOT%AORebirthDimensions.txt" "%OUT%\aorebirth-patch\dimensions.txt" >nul || exit /b 1
rem More login servers that take the AORebirth key (login_key_patch.cpp): none listed, for setups to add theirs.
copy /Y "%ROOT%AORebirthServers.txt" "%OUT%\aorebirth-patch\servers.txt" >nul || exit /b 1
echo [AORebirthClientPatch] Built "%OUT%\aorebirth-patch.dll" and its launcher files in "%OUT%\aorebirth-patch"
exit /b 0
