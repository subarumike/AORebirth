@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "SCRIPT_DIR=%~dp0"
for %%I in ("%SCRIPT_DIR%..") do set "REPO_ROOT=%%~fI"

set "NESTED_ROOT=%REPO_ROOT%\tools-temp\external\aosharp-github"
set "BOOTSTRAP_MAIN=%NESTED_ROOT%\AOSharp.Bootstrap\Main.cs"
set "BOOTSTRAP_STD_STRING=%NESTED_ROOT%\AOSharp.Common\Unmanaged\DataTypes\StdString.cs"
set "CAPTURE_MAIN=%REPO_ROOT%\tools-temp\AOSharpLiveCapture\Main.cs"
set "PATCH_FILE=%REPO_ROOT%\tools-temp\AOSharpLiveInjector\patches\capture-safe-bootstrap.patch"
set "INJECTOR_PROJECT=%REPO_ROOT%\tools-temp\AOSharpLiveInjector\AOSharpLiveInjector.csproj"
set "INJECTOR_SOURCE=%REPO_ROOT%\tools-temp\AOSharpLiveInjector\Program.cs"
set "INJECTOR_EXE=%REPO_ROOT%\tools-temp\AOSharpLiveInjector\bin\Debug\AOSharpLiveInjector.exe"
set "SELF_TEST_OUTPUT=%TEMP%\aosharp-live-injector-build-test-%RANDOM%%RANDOM%.txt"
set "SELF_TEST_PLUGIN=%INJECTOR_EXE%.self-test-must-not-exist"

if not exist "%NESTED_ROOT%\.git" (
    echo FAILED: AOSharp nested source repository is missing: "%NESTED_ROOT%"
    exit /b 1
)

if not exist "%BOOTSTRAP_MAIN%" (
    echo FAILED: AOSharp Bootstrap source is missing: "%BOOTSTRAP_MAIN%"
    exit /b 1
)

if not exist "%BOOTSTRAP_STD_STRING%" (
    echo FAILED: AOSharp StdString source is missing: "%BOOTSTRAP_STD_STRING%"
    exit /b 1
)

if not exist "%CAPTURE_MAIN%" (
    echo FAILED: AOSharpLiveCapture source is missing: "%CAPTURE_MAIN%"
    exit /b 1
)

if not exist "%INJECTOR_SOURCE%" (
    echo FAILED: AOSharpLiveInjector source is missing: "%INJECTOR_SOURCE%"
    exit /b 1
)

if not exist "%PATCH_FILE%" (
    echo FAILED: capture-safe Bootstrap patch is missing: "%PATCH_FILE%"
    exit /b 1
)

git -C "%NESTED_ROOT%" apply --check --reverse "%PATCH_FILE%" >nul 2>nul
if not errorlevel 1 goto verify_contract

git -C "%NESTED_ROOT%" apply --check "%PATCH_FILE%" >nul 2>nul
if errorlevel 1 (
    echo FAILED: AOSharp Bootstrap is partially patched or no longer matches the capture-safe contract.
    exit /b 1
)

git -C "%NESTED_ROOT%" apply "%PATCH_FILE%"
if errorlevel 1 (
    echo FAILED: could not apply the capture-safe AOSharp Bootstrap patch.
    exit /b 1
)

:verify_contract
git -C "%NESTED_ROOT%" apply --check --reverse "%PATCH_FILE%" >nul 2>nul
if errorlevel 1 (
    echo FAILED: capture-safe AOSharp Bootstrap patch verification failed.
    exit /b 1
)

findstr /C:"public const int CaptureSafeContractVersion = 5;" "%BOOTSTRAP_MAIN%" >nul
if errorlevel 1 goto contract_marker_failed
findstr /C:"GetCaptureSafeSingletonName(inChannelName)" "%BOOTSTRAP_MAIN%" >nul
if errorlevel 1 goto contract_marker_failed
findstr /C:"ShouldInstallCaptureCommandHook(_captureSafeMode" "%BOOTSTRAP_MAIN%" >nul
if errorlevel 1 goto contract_marker_failed
findstr /C:"hook = LocalHook.Create(" "%BOOTSTRAP_MAIN%" >nul
if errorlevel 1 goto contract_marker_failed
findstr /C:"using (StdString tokenized = StdString.Create())" "%BOOTSTRAP_MAIN%" >nul
if errorlevel 1 goto contract_marker_failed
findstr /C:"TryLogTypedChatInput(chatInput)" "%BOOTSTRAP_MAIN%" >nul
if errorlevel 1 goto contract_marker_failed
findstr /C:"AOSharpLiveCapture.typed-chat.log" "%BOOTSTRAP_MAIN%" >nul
if errorlevel 1 goto contract_marker_failed
findstr /C:"TryLogChatSocket(" "%BOOTSTRAP_MAIN%" >nul
if errorlevel 1 goto contract_marker_failed
findstr /C:"AOSharpLiveCapture.chat-socket.log" "%BOOTSTRAP_MAIN%" >nul
if errorlevel 1 goto contract_marker_failed
findstr /C:"WsSend_Hook" "%BOOTSTRAP_MAIN%" >nul
if errorlevel 1 goto contract_marker_failed
findstr /C:"plugin load refused." "%BOOTSTRAP_MAIN%" >nul
if errorlevel 1 goto contract_marker_failed
findstr /C:"IsCaptureReadySignaled()" "%BOOTSTRAP_MAIN%" >nul
if errorlevel 1 goto contract_marker_failed
findstr /C:"SignalCaptureBootstrapReady();" "%CAPTURE_MAIN%" >nul
if errorlevel 1 goto contract_marker_failed
findstr /C:"ready.Set();" "%CAPTURE_MAIN%" >nul
if errorlevel 1 goto contract_marker_failed
findstr /C:"AOSharpCaptureBootstrap_" "%CAPTURE_MAIN%" >nul
if errorlevel 1 goto contract_marker_failed
findstr /C:"_capture_safe" "%CAPTURE_MAIN%" >nul
if errorlevel 1 goto contract_marker_failed
findstr /C:"PollTypedChatLog" "%CAPTURE_MAIN%" >nul
if errorlevel 1 goto contract_marker_failed
findstr /C:"AOSharpLiveCapture.typed-chat.log" "%CAPTURE_MAIN%" >nul
if errorlevel 1 goto contract_marker_failed
findstr /C:"PollChatSocketLog" "%CAPTURE_MAIN%" >nul
if errorlevel 1 goto contract_marker_failed
findstr /C:"AOSharpLiveCapture.chat-socket.log" "%CAPTURE_MAIN%" >nul
if errorlevel 1 goto contract_marker_failed
findstr /C:"private const int NativeObjectSize = 0x18;" "%BOOTSTRAP_STD_STRING%" >nul
if errorlevel 1 goto contract_marker_failed
findstr /C:"[FieldOffset(20)]" "%BOOTSTRAP_STD_STRING%" >nul
if errorlevel 1 goto contract_marker_failed
findstr /C:"WaitForCaptureBootstrapReady(channelName" "%INJECTOR_SOURCE%" >nul
if errorlevel 1 goto contract_marker_failed
findstr /C:"pipe?.Disconnect();" "%INJECTOR_SOURCE%" >nul
if errorlevel 1 goto contract_marker_failed
rem DelayedExpansion eats bare !; caret preserves != for findstr.
findstr /C:"CaptureSafeContractVersion ^!= 5" "%INJECTOR_SOURCE%" >nul
if errorlevel 1 goto contract_marker_failed

pushd "%REPO_ROOT%"
MSBuild.exe tools-temp\AOSharpLiveInjector\AOSharpLiveInjector.csproj /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
set "BUILD_EXIT=!ERRORLEVEL!"
popd
if not "!BUILD_EXIT!"=="0" (
    echo FAILED: AOSharpLiveInjector build failed.
    exit /b 1
)

if not exist "%INJECTOR_EXE%" (
    echo FAILED: AOSharpLiveInjector build did not produce the expected executable.
    exit /b 1
)

if exist "%SELF_TEST_PLUGIN%" (
    echo FAILED: injector self-test sentinel unexpectedly exists: "%SELF_TEST_PLUGIN%"
    exit /b 1
)

"%INJECTOR_EXE%" --self-test --plugin "%SELF_TEST_PLUGIN%" > "%SELF_TEST_OUTPUT%" 2>&1
set "SELF_TEST_EXIT=!ERRORLEVEL!"
findstr /X /C:"PASS: capture-safe bootstrap provides fail-closed isolated capture chat commands without native GUI rewriting." "%SELF_TEST_OUTPUT%" >nul 2>nul
set "SELF_TEST_MATCH=!ERRORLEVEL!"
if not "!SELF_TEST_EXIT!"=="0" goto self_test_failed
if not "!SELF_TEST_MATCH!"=="0" goto self_test_failed
del /q "%SELF_TEST_OUTPUT%" >nul 2>nul

echo PASS: capture-safe AOSharpLiveInjector built and verified. No client was launched or injected.
exit /b 0

:contract_marker_failed
echo FAILED: capture-safe AOSharp Bootstrap contract marker is missing.
exit /b 1

:self_test_failed
echo FAILED: capture-safe AOSharpLiveInjector self-test failed.
if exist "%SELF_TEST_OUTPUT%" findstr /C:"FAIL:" /C:"PASS:" "%SELF_TEST_OUTPUT%" 2>nul
if exist "%SELF_TEST_OUTPUT%" del /q "%SELF_TEST_OUTPUT%" >nul 2>nul
exit /b 1
