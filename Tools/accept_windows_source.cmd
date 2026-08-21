@echo off
setlocal EnableExtensions

set "EXPECTED_SHA="
set "RUN_BUILD=1"
set "RUN_MANDATORY_GATE=0"

:parse_args
if "%~1"=="" goto :args_done
if "%~1"=="--expected-sha" (
    set "EXPECTED_SHA=%~2"
    shift
    shift
    goto :parse_args
)
if "%~1"=="--skip-build" (
    set "RUN_BUILD=0"
    shift
    goto :parse_args
)
if "%~1"=="--mandatory-gate" (
    set "RUN_MANDATORY_GATE=1"
    shift
    goto :parse_args
)
if "%~1"=="--help" goto :usage_ok
goto :usage_error

:args_done
if "%EXPECTED_SHA%"=="" goto :usage_error

for /f "usebackq delims=" %%I in (`git rev-parse --show-toplevel`) do set "REPOSITORY_ROOT=%%I"
if "%REPOSITORY_ROOT%"=="" goto :git_failed
pushd "%REPOSITORY_ROOT%" || exit /b 1

for /f "usebackq delims=" %%I in (`git rev-parse HEAD`) do set "ACTUAL_SHA=%%I"
if "%ACTUAL_SHA%"=="" goto :git_failed

echo AO_REBIRTH_SOURCE_SHA=%ACTUAL_SHA%
echo EXPECTED_SOURCE_SHA=%EXPECTED_SHA%
if /i not "%ACTUAL_SHA%"=="%EXPECTED_SHA%" (
    echo SOURCE_SHA_MATCH=FAIL
    echo WINDOWS_ACCEPTANCE=FAIL
    popd
    exit /b 10
)
echo SOURCE_SHA_MATCH=PASS

git diff --quiet --
if errorlevel 1 goto :dirty
git diff --cached --quiet --
if errorlevel 1 goto :dirty
echo TRACKED_SOURCE_CLEAN=PASS

git diff --check
if errorlevel 1 (
    echo GIT_DIFF_CHECK=FAIL
    echo WINDOWS_ACCEPTANCE=FAIL
    popd
    exit /b 12
)
echo GIT_DIFF_CHECK=PASS

call Tools\generate_capture_backed_npc_combat_inventory.cmd --check
if errorlevel 1 (
    echo GENERATED_COMBAT_INTEGRITY=FAIL
    echo WINDOWS_ACCEPTANCE=FAIL
    popd
    exit /b 13
)
echo GENERATED_COMBAT_INTEGRITY=PASS

set "BUILD_RESULT=NOT_RUN"
if "%RUN_BUILD%"=="1" (
    call Tools\build_aorebirth_debug.cmd
    if errorlevel 1 (
        echo BUILD=FAIL
        echo WINDOWS_ACCEPTANCE=FAIL
        popd
        exit /b 20
    )
    set "BUILD_RESULT=PASS"
)
echo BUILD=%BUILD_RESULT%

set "TEST_RESULT=NOT_RUN"
if "%RUN_MANDATORY_GATE%"=="1" (
    call tools\run_mandatory_integration_gate.cmd
    if errorlevel 1 (
        echo TESTS=FAIL
        echo WINDOWS_ACCEPTANCE=FAIL
        popd
        exit /b 30
    )
    set "TEST_RESULT=PASS"
)
echo TESTS=%TEST_RESULT%

if not exist build-verify mkdir build-verify
set "SHORT_SHA=%ACTUAL_SHA:~0,8%"
set "EVIDENCE=build-verify\windows-acceptance-%SHORT_SHA%.env"
> "%EVIDENCE%" echo AO_REBIRTH_SOURCE_SHA=%ACTUAL_SHA%
>> "%EVIDENCE%" echo EXPECTED_SOURCE_SHA=%EXPECTED_SHA%
>> "%EVIDENCE%" echo SOURCE_SHA_MATCH=PASS
>> "%EVIDENCE%" echo TRACKED_SOURCE_CLEAN=PASS
>> "%EVIDENCE%" echo GIT_DIFF_CHECK=PASS
>> "%EVIDENCE%" echo GENERATED_COMBAT_INTEGRITY=PASS
>> "%EVIDENCE%" echo BUILD=%BUILD_RESULT%
>> "%EVIDENCE%" echo TESTS=%TEST_RESULT%
>> "%EVIDENCE%" echo BUILD_PLATFORM=windows
>> "%EVIDENCE%" echo CONFIGURATION=Debug
>> "%EVIDENCE%" echo BUILD_TIMESTAMP_LOCAL=%DATE% %TIME%
>> "%EVIDENCE%" echo WINDOWS_ACCEPTANCE=PASS

echo WINDOWS_ACCEPTANCE_EVIDENCE=%EVIDENCE%
echo WINDOWS_ACCEPTANCE=PASS
popd
exit /b 0

:dirty
echo TRACKED_SOURCE_CLEAN=FAIL
echo WINDOWS_ACCEPTANCE=FAIL
popd
exit /b 11

:git_failed
echo WINDOWS_ACCEPTANCE=FAIL
exit /b 2

:usage_error
echo WINDOWS_ACCEPTANCE=FAIL
echo usage: Tools\accept_windows_source.cmd --expected-sha ^<sha^> [--skip-build] [--mandatory-gate]
exit /b 2

:usage_ok
echo usage: Tools\accept_windows_source.cmd --expected-sha ^<sha^> [--skip-build] [--mandatory-gate]
exit /b 0
