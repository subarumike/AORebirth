@echo off
setlocal EnableExtensions DisableDelayedExpansion

set "ROOT=%~dp0"
set "SRC=%ROOT%src"
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
  echo [AORoomSpaceFix] ERROR vswhere.exe not found.
  exit /b 1
)

set "VSROOT="
set "VSRESULT=%TEMP%\AORoomSpaceFix-vswhere-%RANDOM%-%RANDOM%.txt"
"%VSWHERE%" -latest -products * -version "[18.0,19.0)" -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath > "%VSRESULT%"
if errorlevel 1 (
  del /Q "%VSRESULT%" >nul 2>nul
  echo [AORoomSpaceFix] ERROR Visual Studio discovery failed.
  exit /b 1
)
set /p VSROOT=<"%VSRESULT%"
del /Q "%VSRESULT%" >nul 2>nul
if not defined VSROOT (
  echo [AORoomSpaceFix] ERROR Visual Studio 18 C++ x86 tools were not found.
  exit /b 1
)
if not exist "%VSROOT%\VC\Auxiliary\Build\vcvars32.bat" (
  echo [AORoomSpaceFix] ERROR vcvars32.bat not found under "%VSROOT%".
  exit /b 1
)

call "%VSROOT%\VC\Auxiliary\Build\vcvars32.bat" >nul
if errorlevel 1 (
  echo [AORoomSpaceFix] ERROR Visual Studio x86 environment setup failed.
  exit /b 1
)

set "BUILD_ROOT=%TEMP%\AORoomSpaceFix-%RANDOM%-%RANDOM%"
if exist "%BUILD_ROOT%" (
  echo [AORoomSpaceFix] ERROR temporary build directory collision: "%BUILD_ROOT%".
  exit /b 1
)
mkdir "%BUILD_ROOT%"
if errorlevel 1 (
  echo [AORoomSpaceFix] ERROR could not create "%BUILD_ROOT%".
  exit /b 1
)

echo [AORoomSpaceFix] Building x86 static-CRT proxy...
pushd "%BUILD_ROOT%"
cl /nologo /std:c++17 /O2 /GL /Gy /EHsc /W4 /WX /MT ^
  /DUNICODE /D_UNICODE /DWIN32_LEAN_AND_MEAN /DNOMINMAX ^
  /I"%SRC%" /LD ^
  "%SRC%\crash_dump.cpp" ^
  "%SRC%\dllmain.cpp" ^
  "%SRC%\logging.cpp" ^
  "%SRC%\gui_rect_fix.cpp" ^
  "%SRC%\randy_color_fix.cpp" ^
  "%SRC%\roomspace_fix.cpp" ^
  "%SRC%\version_proxy.cpp" ^
  /link /DLL /MACHINE:X86 /LTCG /OPT:REF /OPT:ICF /DYNAMICBASE /NXCOMPAT ^
  /guard:cf /Brepro /DEF:"%SRC%\version_proxy.def" ^
  /OUT:"%BUILD_ROOT%\version.dll" bcrypt.lib user32.lib
if errorlevel 1 (
  popd
  echo [AORoomSpaceFix] ERROR proxy build failed.
  exit /b 1
)

echo [AORoomSpaceFix] Building and running offline wrapper self-test...
cl /nologo /std:c++17 /O2 /GL /Gy /EHsc /W4 /WX /MT ^
  /DUNICODE /D_UNICODE /DWIN32_LEAN_AND_MEAN /DNOMINMAX ^
  /I"%SRC%" ^
  "%SRC%\logging.cpp" ^
  "%SRC%\roomspace_fix.cpp" ^
  "%SRC%\self_test.cpp" ^
  /link /MACHINE:X86 /LTCG /OPT:REF /OPT:ICF /DYNAMICBASE /NXCOMPAT ^
  /guard:cf /Brepro /OUT:"%BUILD_ROOT%\AORoomSpaceFixSelfTest.exe" bcrypt.lib
if errorlevel 1 (
  popd
  echo [AORoomSpaceFix] ERROR self-test build failed.
  exit /b 1
)
"%BUILD_ROOT%\AORoomSpaceFixSelfTest.exe"
if errorlevel 1 (
  popd
  echo [AORoomSpaceFix] ERROR offline wrapper self-test failed.
  exit /b 1
)

echo [AORoomSpaceFix] Building and running proxy forwarding self-test...
cl /nologo /std:c++17 /O2 /GL /Gy /EHsc /W4 /WX /MT ^
  /DUNICODE /D_UNICODE /DWIN32_LEAN_AND_MEAN /DNOMINMAX ^
  "%SRC%\proxy_self_test.cpp" ^
  /link /MACHINE:X86 /LTCG /OPT:REF /OPT:ICF /DYNAMICBASE /NXCOMPAT ^
  /guard:cf /Brepro /OUT:"%BUILD_ROOT%\ProxyForwardingSelfTest.exe"
if errorlevel 1 (
  popd
  echo [AORoomSpaceFix] ERROR proxy forwarding self-test build failed.
  exit /b 1
)
"%BUILD_ROOT%\ProxyForwardingSelfTest.exe" "%BUILD_ROOT%\version.dll"
if errorlevel 1 (
  popd
  echo [AORoomSpaceFix] ERROR proxy forwarding self-test failed.
  exit /b 1
)

echo [AORoomSpaceFix] Building and running deployment helper self-test...
cl /nologo /std:c++17 /O2 /GL /Gy /EHsc /W4 /WX /MT ^
  /DUNICODE /D_UNICODE /DWIN32_LEAN_AND_MEAN /DNOMINMAX ^
  "%SRC%\deploy_tool.cpp" ^
  /link /MACHINE:X86 /LTCG /OPT:REF /OPT:ICF /DYNAMICBASE /NXCOMPAT ^
  /guard:cf /Brepro /OUT:"%BUILD_ROOT%\AORoomSpaceFixDeploy.exe" bcrypt.lib
if errorlevel 1 (
  popd
  echo [AORoomSpaceFix] ERROR deployment helper build failed.
  exit /b 1
)
"%BUILD_ROOT%\AORoomSpaceFixDeploy.exe" --self-test
if errorlevel 1 (
  popd
  echo [AORoomSpaceFix] ERROR deployment helper self-test failed.
  exit /b 1
)

dumpbin /headers "%BUILD_ROOT%\version.dll" > "%BUILD_ROOT%\headers.txt"
findstr /C:"14C machine (x86)" "%BUILD_ROOT%\headers.txt" >nul
if errorlevel 1 (
  popd
  echo [AORoomSpaceFix] ERROR version.dll is not PE32 x86.
  exit /b 1
)

dumpbin /exports "%BUILD_ROOT%\version.dll" > "%BUILD_ROOT%\exports.txt"
findstr /C:"17 number of functions" "%BUILD_ROOT%\exports.txt" >nul
if errorlevel 1 (
  popd
  echo [AORoomSpaceFix] ERROR version.dll does not have exactly 17 functions.
  exit /b 1
)
findstr /C:"17 number of names" "%BUILD_ROOT%\exports.txt" >nul
if errorlevel 1 (
  popd
  echo [AORoomSpaceFix] ERROR version.dll does not have exactly 17 export names.
  exit /b 1
)
for %%E in (GetFileVersionInfoA GetFileVersionInfoByHandle GetFileVersionInfoExA GetFileVersionInfoExW GetFileVersionInfoSizeA GetFileVersionInfoSizeExA GetFileVersionInfoSizeExW GetFileVersionInfoSizeW GetFileVersionInfoW VerFindFileA VerFindFileW VerInstallFileA VerInstallFileW VerLanguageNameA VerLanguageNameW VerQueryValueA VerQueryValueW) do (
  findstr /R /C:"[ ]%%E$" "%BUILD_ROOT%\exports.txt" >nul
  if errorlevel 1 (
    popd
    echo [AORoomSpaceFix] ERROR missing export %%E.
    exit /b 1
  )
)

dumpbin /dependents "%BUILD_ROOT%\version.dll" > "%BUILD_ROOT%\dependents.txt"
findstr /I /C:"VCRUNTIME" /C:"MSVCP" /C:"ucrtbase.dll" /C:"api-ms-win-crt-" "%BUILD_ROOT%\dependents.txt" >nul
if not errorlevel 1 (
  popd
  echo [AORoomSpaceFix] ERROR dynamic Visual C++ runtime dependency detected.
  exit /b 1
)
popd

where.exe tar.exe >nul 2>nul
if errorlevel 1 (
  echo [AORoomSpaceFix] ERROR Windows tar.exe was not found.
  exit /b 1
)

set "STAGE=%BUILD_ROOT%\package"
set "VERIFY_STAGE=%BUILD_ROOT%\package-verify"
mkdir "%STAGE%"
if errorlevel 1 (
  echo [AORoomSpaceFix] ERROR could not create fresh package staging.
  exit /b 1
)

copy /B "%BUILD_ROOT%\version.dll" "%STAGE%\version.dll" >nul
if errorlevel 1 goto :stage_copy_failed
copy /B "%BUILD_ROOT%\AORoomSpaceFixDeploy.exe" "%STAGE%\AORoomSpaceFixDeploy.exe" >nul
if errorlevel 1 goto :stage_copy_failed
copy /Y "%ROOT%Install.cmd" "%STAGE%\Install.cmd" >nul
if errorlevel 1 goto :stage_copy_failed
copy /Y "%ROOT%Uninstall.cmd" "%STAGE%\Uninstall.cmd" >nul
if errorlevel 1 goto :stage_copy_failed
copy /Y "%ROOT%PACKAGE-README.txt" "%STAGE%\README.txt" >nul
if errorlevel 1 goto :stage_copy_failed
copy /Y "%ROOT%LICENSES\AOReloaded-MIT.txt" "%STAGE%\AOReloaded-MIT.txt" >nul
if errorlevel 1 goto :stage_copy_failed

"%BUILD_ROOT%\AORoomSpaceFixDeploy.exe" write-manifest "%STAGE%"
if errorlevel 1 (
  echo [AORoomSpaceFix] ERROR exact package manifest generation failed.
  exit /b 1
)
"%BUILD_ROOT%\AORoomSpaceFixDeploy.exe" verify-package "%STAGE%"
if errorlevel 1 (
  echo [AORoomSpaceFix] ERROR staged package verification failed.
  exit /b 1
)

set "TEMP_ZIP=%BUILD_ROOT%\AORoomSpaceFix-v1.zip"
tar.exe -a -c -f "%TEMP_ZIP%" -C "%STAGE%" AOReloaded-MIT.txt AORoomSpaceFixDeploy.exe Install.cmd README.txt SHA256SUMS.txt Uninstall.cmd version.dll
if errorlevel 1 (
  echo [AORoomSpaceFix] ERROR package ZIP creation failed.
  exit /b 1
)

mkdir "%VERIFY_STAGE%"
if errorlevel 1 (
  echo [AORoomSpaceFix] ERROR could not create package verification directory.
  exit /b 1
)
tar.exe -x -f "%TEMP_ZIP%" -C "%VERIFY_STAGE%"
if errorlevel 1 (
  echo [AORoomSpaceFix] ERROR package ZIP extraction verification failed.
  exit /b 1
)
"%BUILD_ROOT%\AORoomSpaceFixDeploy.exe" verify-package "%VERIFY_STAGE%"
if errorlevel 1 (
  echo [AORoomSpaceFix] ERROR packaged ZIP payload verification failed.
  exit /b 1
)

set "ARTIFACT_ROOT=%ROOT%artifacts"
if not exist "%ARTIFACT_ROOT%" mkdir "%ARTIFACT_ROOT%"
if errorlevel 1 (
  echo [AORoomSpaceFix] ERROR could not create artifacts directory.
  exit /b 1
)
set "ZIP=%ARTIFACT_ROOT%\AORoomSpaceFix-v1.zip"
copy /B /Y "%TEMP_ZIP%" "%ZIP%" >nul
if errorlevel 1 (
  echo [AORoomSpaceFix] ERROR could not publish the verified package ZIP.
  exit /b 1
)

set "PUBLISHED_DIR=%ARTIFACT_ROOT%\AORoomSpaceFix-v1"
set "PUBLISHED_TMP=%ARTIFACT_ROOT%\AORoomSpaceFix-v1.tmp"
if exist "%PUBLISHED_TMP%" rmdir /S /Q "%PUBLISHED_TMP%"
if exist "%PUBLISHED_TMP%" (
  echo [AORoomSpaceFix] ERROR could not clear stale extracted package staging.
  exit /b 1
)
mkdir "%PUBLISHED_TMP%"
if errorlevel 1 (
  echo [AORoomSpaceFix] ERROR could not create extracted package staging.
  exit /b 1
)
tar.exe -x -f "%ZIP%" -C "%PUBLISHED_TMP%"
if errorlevel 1 (
  echo [AORoomSpaceFix] ERROR extracted package publication failed.
  exit /b 1
)
"%BUILD_ROOT%\AORoomSpaceFixDeploy.exe" verify-package "%PUBLISHED_TMP%"
if errorlevel 1 (
  echo [AORoomSpaceFix] ERROR extracted package publication verification failed.
  exit /b 1
)
if exist "%PUBLISHED_DIR%" rmdir /S /Q "%PUBLISHED_DIR%"
if exist "%PUBLISHED_DIR%" (
  echo [AORoomSpaceFix] ERROR could not replace stale extracted package.
  exit /b 1
)
move /Y "%PUBLISHED_TMP%" "%PUBLISHED_DIR%" >nul
if errorlevel 1 (
  echo [AORoomSpaceFix] ERROR could not publish extracted package.
  exit /b 1
)

echo [AORoomSpaceFix] PASS package="%ZIP%"
echo [AORoomSpaceFix] PASS extracted package="%PUBLISHED_DIR%"
echo [AORoomSpaceFix] AO was not launched and no client directory was changed.
exit /b 0

:stage_copy_failed
echo [AORoomSpaceFix] ERROR package staging copy failed.
exit /b 1
