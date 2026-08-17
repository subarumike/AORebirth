@echo off
setlocal EnableExtensions DisableDelayedExpansion

set "ROOT=%~dp0"
set "SRC=%ROOT%src"
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
  echo [AORebirthClientPatch] ERROR vswhere.exe not found.
  exit /b 1
)

set "VSROOT="
set "VSRESULT=%TEMP%\AORebirthClientPatch-vswhere-%RANDOM%-%RANDOM%.txt"
"%VSWHERE%" -latest -products * -version "[18.0,19.0)" -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath > "%VSRESULT%"
if errorlevel 1 (
  del /Q "%VSRESULT%" >nul 2>nul
  echo [AORebirthClientPatch] ERROR Visual Studio discovery failed.
  exit /b 1
)
set /p VSROOT=<"%VSRESULT%"
del /Q "%VSRESULT%" >nul 2>nul
if not defined VSROOT (
  echo [AORebirthClientPatch] ERROR Visual Studio 18 C++ x86 tools were not found.
  exit /b 1
)
if not exist "%VSROOT%\VC\Auxiliary\Build\vcvars32.bat" (
  echo [AORebirthClientPatch] ERROR vcvars32.bat not found under "%VSROOT%".
  exit /b 1
)

call "%VSROOT%\VC\Auxiliary\Build\vcvars32.bat" >nul
if errorlevel 1 (
  echo [AORebirthClientPatch] ERROR Visual Studio x86 environment setup failed.
  exit /b 1
)

set "BUILD_ROOT=%TEMP%\AORebirthClientPatch-%RANDOM%-%RANDOM%"
if exist "%BUILD_ROOT%" (
  echo [AORebirthClientPatch] ERROR temporary build directory collision: "%BUILD_ROOT%".
  exit /b 1
)
mkdir "%BUILD_ROOT%"
if errorlevel 1 (
  echo [AORebirthClientPatch] ERROR could not create "%BUILD_ROOT%".
  exit /b 1
)

echo [AORebirthClientPatch] Building x86 static-CRT proxy...
pushd "%BUILD_ROOT%"
cl /nologo /std:c++17 /O2 /GL /Gy /EHsc /W4 /WX /MT ^
  /DUNICODE /D_UNICODE /DWIN32_LEAN_AND_MEAN /DNOMINMAX ^
  /I"%SRC%" /LD ^
  "%SRC%\crash_dump.cpp" ^
  "%SRC%\dllmain.cpp" ^
  "%SRC%\gui_rect_fix.cpp" ^
  "%SRC%\logging.cpp" ^
  "%SRC%\login_key_patch.cpp" ^
  "%SRC%\randy_color_fix.cpp" ^
  "%SRC%\roomspace_fix.cpp" ^
  "%SRC%\version_proxy.cpp" ^
  /link /DLL /MACHINE:X86 /LTCG /OPT:REF /OPT:ICF /DYNAMICBASE /NXCOMPAT ^
  /guard:cf /Brepro /DEF:"%SRC%\version_proxy.def" ^
  /OUT:"%BUILD_ROOT%\version.dll" bcrypt.lib user32.lib
if errorlevel 1 (
  popd
  echo [AORebirthClientPatch] ERROR proxy build failed.
  exit /b 1
)

echo [AORebirthClientPatch] Building and running offline wrapper self-test...
cl /nologo /std:c++17 /O2 /GL /Gy /EHsc /W4 /WX /MT ^
  /DUNICODE /D_UNICODE /DWIN32_LEAN_AND_MEAN /DNOMINMAX ^
  /I"%SRC%" ^
  "%SRC%\logging.cpp" ^
  "%SRC%\login_key_patch.cpp" ^
  "%SRC%\roomspace_fix.cpp" ^
  "%SRC%\self_test.cpp" ^
  /link /MACHINE:X86 /LTCG /OPT:REF /OPT:ICF /DYNAMICBASE /NXCOMPAT ^
  /guard:cf /Brepro /OUT:"%BUILD_ROOT%\AORebirthClientPatchSelfTest.exe" bcrypt.lib
if errorlevel 1 (
  popd
  echo [AORebirthClientPatch] ERROR self-test build failed.
  exit /b 1
)
"%BUILD_ROOT%\AORebirthClientPatchSelfTest.exe"
if errorlevel 1 (
  popd
  echo [AORebirthClientPatch] ERROR offline wrapper self-test failed.
  exit /b 1
)

echo [AORebirthClientPatch] Building and running proxy forwarding self-test...
cl /nologo /std:c++17 /O2 /GL /Gy /EHsc /W4 /WX /MT ^
  /DUNICODE /D_UNICODE /DWIN32_LEAN_AND_MEAN /DNOMINMAX ^
  "%SRC%\proxy_self_test.cpp" ^
  /link /MACHINE:X86 /LTCG /OPT:REF /OPT:ICF /DYNAMICBASE /NXCOMPAT ^
  /guard:cf /Brepro /OUT:"%BUILD_ROOT%\ProxyForwardingSelfTest.exe"
if errorlevel 1 (
  popd
  echo [AORebirthClientPatch] ERROR proxy forwarding self-test build failed.
  exit /b 1
)
"%BUILD_ROOT%\ProxyForwardingSelfTest.exe" "%BUILD_ROOT%\version.dll"
if errorlevel 1 (
  popd
  echo [AORebirthClientPatch] ERROR proxy forwarding self-test failed.
  exit /b 1
)

echo [AORebirthClientPatch] Building and running deployment helper self-test...
cl /nologo /std:c++17 /O2 /GL /Gy /EHsc /W4 /WX /MT ^
  /DUNICODE /D_UNICODE /DWIN32_LEAN_AND_MEAN /DNOMINMAX ^
  "%SRC%\deploy_tool.cpp" ^
  /link /MACHINE:X86 /LTCG /OPT:REF /OPT:ICF /DYNAMICBASE /NXCOMPAT ^
  /guard:cf /Brepro /MANIFEST:EMBED /MANIFESTINPUT:"%SRC%\deploy_tool.manifest" ^
  /OUT:"%BUILD_ROOT%\AORebirthClientPatchDeploy.exe" bcrypt.lib
if errorlevel 1 (
  popd
  echo [AORebirthClientPatch] ERROR deployment helper build failed.
  exit /b 1
)
"%BUILD_ROOT%\AORebirthClientPatchDeploy.exe" --self-test
if errorlevel 1 (
  popd
  echo [AORebirthClientPatch] ERROR deployment helper self-test failed.
  exit /b 1
)

dumpbin /headers "%BUILD_ROOT%\version.dll" > "%BUILD_ROOT%\headers.txt"
findstr /C:"14C machine (x86)" "%BUILD_ROOT%\headers.txt" >nul
if errorlevel 1 (
  popd
  echo [AORebirthClientPatch] ERROR version.dll is not PE32 x86.
  exit /b 1
)

dumpbin /exports "%BUILD_ROOT%\version.dll" > "%BUILD_ROOT%\exports.txt"
findstr /C:"17 number of functions" "%BUILD_ROOT%\exports.txt" >nul
if errorlevel 1 (
  popd
  echo [AORebirthClientPatch] ERROR version.dll does not have exactly 17 functions.
  exit /b 1
)
findstr /C:"17 number of names" "%BUILD_ROOT%\exports.txt" >nul
if errorlevel 1 (
  popd
  echo [AORebirthClientPatch] ERROR version.dll does not have exactly 17 export names.
  exit /b 1
)
for %%E in (GetFileVersionInfoA GetFileVersionInfoByHandle GetFileVersionInfoExA GetFileVersionInfoExW GetFileVersionInfoSizeA GetFileVersionInfoSizeExA GetFileVersionInfoSizeExW GetFileVersionInfoSizeW GetFileVersionInfoW VerFindFileA VerFindFileW VerInstallFileA VerInstallFileW VerLanguageNameA VerLanguageNameW VerQueryValueA VerQueryValueW) do (
  findstr /R /C:"[ ]%%E$" "%BUILD_ROOT%\exports.txt" >nul
  if errorlevel 1 (
    popd
    echo [AORebirthClientPatch] ERROR missing export %%E.
    exit /b 1
  )
)

dumpbin /dependents "%BUILD_ROOT%\version.dll" > "%BUILD_ROOT%\dependents.txt"
findstr /I /C:"VCRUNTIME" /C:"MSVCP" /C:"ucrtbase.dll" /C:"api-ms-win-crt-" "%BUILD_ROOT%\dependents.txt" >nul
if not errorlevel 1 (
  popd
  echo [AORebirthClientPatch] ERROR dynamic Visual C++ runtime dependency detected.
  exit /b 1
)
popd

call :sign_binary "%BUILD_ROOT%\version.dll"
if errorlevel 1 exit /b 1
call :sign_binary "%BUILD_ROOT%\AORebirthClientPatchDeploy.exe"
if errorlevel 1 exit /b 1

where.exe tar.exe >nul 2>nul
if errorlevel 1 (
  echo [AORebirthClientPatch] ERROR Windows tar.exe was not found.
  exit /b 1
)

set "STAGE=%BUILD_ROOT%\package"
set "VERIFY_STAGE=%BUILD_ROOT%\package-verify"
mkdir "%STAGE%"
if errorlevel 1 (
  echo [AORebirthClientPatch] ERROR could not create fresh package staging.
  exit /b 1
)

copy /B "%BUILD_ROOT%\version.dll" "%STAGE%\version.dll" >nul
if errorlevel 1 goto :stage_copy_failed
copy /B "%BUILD_ROOT%\AORebirthClientPatchDeploy.exe" "%STAGE%\AORebirthClientPatchDeploy.exe" >nul
if errorlevel 1 goto :stage_copy_failed
copy /Y "%ROOT%AORebirthAnarchyLauncher.url" "%STAGE%\AORebirthAnarchyLauncher.url" >nul
if errorlevel 1 goto :stage_copy_failed
copy /Y "%ROOT%AORebirthDimensionServer.url" "%STAGE%\AORebirthDimensionServer.url" >nul
if errorlevel 1 goto :stage_copy_failed
copy /Y "%ROOT%Install.cmd" "%STAGE%\Install.cmd" >nul
if errorlevel 1 goto :stage_copy_failed
copy /Y "%ROOT%Uninstall.cmd" "%STAGE%\Uninstall.cmd" >nul
if errorlevel 1 goto :stage_copy_failed
copy /Y "%ROOT%PACKAGE-README.txt" "%STAGE%\README.txt" >nul
if errorlevel 1 goto :stage_copy_failed
copy /Y "%ROOT%LICENSES\AOReloaded-MIT.txt" "%STAGE%\AOReloaded-MIT.txt" >nul
if errorlevel 1 goto :stage_copy_failed

"%BUILD_ROOT%\AORebirthClientPatchDeploy.exe" write-manifest "%STAGE%"
if errorlevel 1 (
  echo [AORebirthClientPatch] ERROR exact package manifest generation failed.
  exit /b 1
)
"%BUILD_ROOT%\AORebirthClientPatchDeploy.exe" verify-package "%STAGE%"
if errorlevel 1 (
  echo [AORebirthClientPatch] ERROR staged package verification failed.
  exit /b 1
)

set "TEMP_ZIP=%BUILD_ROOT%\AORebirthClientPatch-v1.zip"
tar.exe -a -c -f "%TEMP_ZIP%" -C "%STAGE%" AOReloaded-MIT.txt AORebirthClientPatchDeploy.exe AORebirthAnarchyLauncher.url AORebirthDimensionServer.url Install.cmd README.txt SHA256SUMS.txt Uninstall.cmd version.dll
if errorlevel 1 (
  echo [AORebirthClientPatch] ERROR package ZIP creation failed.
  exit /b 1
)

mkdir "%VERIFY_STAGE%"
if errorlevel 1 (
  echo [AORebirthClientPatch] ERROR could not create package verification directory.
  exit /b 1
)
tar.exe -x -f "%TEMP_ZIP%" -C "%VERIFY_STAGE%"
if errorlevel 1 (
  echo [AORebirthClientPatch] ERROR package ZIP extraction verification failed.
  exit /b 1
)
"%BUILD_ROOT%\AORebirthClientPatchDeploy.exe" verify-package "%VERIFY_STAGE%"
if errorlevel 1 (
  echo [AORebirthClientPatch] ERROR packaged ZIP payload verification failed.
  exit /b 1
)

set "ARTIFACT_ROOT=%ROOT%artifacts"
if not exist "%ARTIFACT_ROOT%" mkdir "%ARTIFACT_ROOT%"
if errorlevel 1 (
  echo [AORebirthClientPatch] ERROR could not create artifacts directory.
  exit /b 1
)
set "ZIP=%ARTIFACT_ROOT%\AORebirthClientPatch-v1.zip"
copy /B /Y "%TEMP_ZIP%" "%ZIP%" >nul
if errorlevel 1 (
  echo [AORebirthClientPatch] ERROR could not publish the verified package ZIP.
  exit /b 1
)

set "PUBLISHED_DIR=%ARTIFACT_ROOT%\AORebirthClientPatch-v1"
set "PUBLISHED_TMP=%ARTIFACT_ROOT%\AORebirthClientPatch-v1.tmp"
if exist "%PUBLISHED_TMP%" rmdir /S /Q "%PUBLISHED_TMP%"
if exist "%PUBLISHED_TMP%" (
  echo [AORebirthClientPatch] ERROR could not clear stale extracted package staging.
  exit /b 1
)
mkdir "%PUBLISHED_TMP%"
if errorlevel 1 (
  echo [AORebirthClientPatch] ERROR could not create extracted package staging.
  exit /b 1
)
tar.exe -x -f "%ZIP%" -C "%PUBLISHED_TMP%"
if errorlevel 1 (
  echo [AORebirthClientPatch] ERROR extracted package publication failed.
  exit /b 1
)
"%BUILD_ROOT%\AORebirthClientPatchDeploy.exe" verify-package "%PUBLISHED_TMP%"
if errorlevel 1 (
  echo [AORebirthClientPatch] ERROR extracted package publication verification failed.
  exit /b 1
)
if exist "%PUBLISHED_DIR%" rmdir /S /Q "%PUBLISHED_DIR%"
if exist "%PUBLISHED_DIR%" (
  echo [AORebirthClientPatch] ERROR could not replace stale extracted package.
  exit /b 1
)
move /Y "%PUBLISHED_TMP%" "%PUBLISHED_DIR%" >nul
if errorlevel 1 (
  echo [AORebirthClientPatch] ERROR could not publish extracted package.
  exit /b 1
)

set "SETUP_RC=%BUILD_ROOT%\AORebirthClientPatchSetup.rc"
set "SETUP_RES=%BUILD_ROOT%\AORebirthClientPatchSetup.res"
set "SETUP_TEMP=%BUILD_ROOT%\AORebirthClientPatchSetup-v1.exe"
set "SETUP_EXE=%ARTIFACT_ROOT%\AORebirthClientPatchSetup-v1.exe"
set "SETUP_SRC=%SRC:\=/%"
set "SETUP_STAGE=%STAGE:\=/%"
(
  echo 1 24 "%SETUP_SRC%/setup_tool.manifest"
  echo 101 RCDATA "%SETUP_STAGE%/AORebirthAnarchyLauncher.url"
  echo 102 RCDATA "%SETUP_STAGE%/AORebirthDimensionServer.url"
  echo 103 RCDATA "%SETUP_STAGE%/version.dll"
) > "%SETUP_RC%"

rc /nologo /fo "%SETUP_RES%" "%SETUP_RC%"
if errorlevel 1 (
  echo [AORebirthClientPatch] ERROR setup resource build failed.
  exit /b 1
)

cl /nologo /std:c++17 /O2 /GL /Gy /EHsc /W4 /WX /MT ^
  /DUNICODE /D_UNICODE /DWIN32_LEAN_AND_MEAN /DNOMINMAX /DAO_REBIRTH_CLIENT_PATCH_EMBEDDED ^
  "%SRC%\setup_tool.cpp" "%SRC%\deploy_tool.cpp" "%SETUP_RES%" ^
  /link /SUBSYSTEM:WINDOWS /MACHINE:X86 /LTCG /OPT:REF /OPT:ICF /DYNAMICBASE /NXCOMPAT ^
  /guard:cf /Brepro /OUT:"%SETUP_TEMP%" shell32.lib ole32.lib user32.lib bcrypt.lib
if errorlevel 1 (
  echo [AORebirthClientPatch] ERROR setup EXE build failed.
  exit /b 1
)

call :sign_binary "%SETUP_TEMP%"
if errorlevel 1 exit /b 1

copy /B /Y "%SETUP_TEMP%" "%SETUP_EXE%" >nul
if errorlevel 1 (
  echo [AORebirthClientPatch] ERROR could not publish setup EXE.
  exit /b 1
)

echo [AORebirthClientPatch] PASS package="%ZIP%"
echo [AORebirthClientPatch] PASS extracted package="%PUBLISHED_DIR%"
echo [AORebirthClientPatch] PASS setup="%SETUP_EXE%"
echo [AORebirthClientPatch] AO was not launched and no client directory was changed.
exit /b 0

:stage_copy_failed
echo [AORebirthClientPatch] ERROR package staging copy failed.
exit /b 1

:sign_binary
if /I not "%AO_REBIRTH_CODESIGN%"=="1" exit /b 0
if not defined AO_REBIRTH_CODESIGN_THUMBPRINT if not defined AO_REBIRTH_CODESIGN_PFX (
  echo [AORebirthClientPatch] ERROR AO_REBIRTH_CODESIGN=1 requires AO_REBIRTH_CODESIGN_THUMBPRINT or AO_REBIRTH_CODESIGN_PFX.
  exit /b 1
)

set "SIGNTOOL="
for /F "delims=" %%S in ('where.exe signtool.exe 2^>nul') do if not defined SIGNTOOL set "SIGNTOOL=%%S"
if not defined SIGNTOOL (
  echo [AORebirthClientPatch] ERROR signtool.exe was not found.
  exit /b 1
)

if defined AO_REBIRTH_CODESIGN_PFX (
  if not exist "%AO_REBIRTH_CODESIGN_PFX%" (
    echo [AORebirthClientPatch] ERROR AO_REBIRTH_CODESIGN_PFX does not exist.
    exit /b 1
  )
  if defined AO_REBIRTH_CODESIGN_PFX_PASSWORD (
    "%SIGNTOOL%" sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /f "%AO_REBIRTH_CODESIGN_PFX%" /p "%AO_REBIRTH_CODESIGN_PFX_PASSWORD%" "%~1"
  ) else (
    "%SIGNTOOL%" sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /f "%AO_REBIRTH_CODESIGN_PFX%" "%~1"
  )
) else (
  "%SIGNTOOL%" sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /sha1 "%AO_REBIRTH_CODESIGN_THUMBPRINT%" "%~1"
)
if errorlevel 1 (
  echo [AORebirthClientPatch] ERROR Authenticode signing failed for "%~nx1".
  exit /b 1
)

"%SIGNTOOL%" verify /pa /all "%~1" >nul
if errorlevel 1 (
  echo [AORebirthClientPatch] ERROR Authenticode verification failed for "%~nx1".
  exit /b 1
)

echo [AORebirthClientPatch] PASS signed "%~nx1".
exit /b 0
