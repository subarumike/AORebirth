@echo off
REM Opens Git Bash and runs pull/merge/commit/push for pet work.
setlocal
set "REPO=c:\Users\nermi\source\repos\AORebirth"
set "BASH="
if exist "C:\Program Files\Git\bin\bash.exe" set "BASH=C:\Program Files\Git\bin\bash.exe"
if exist "C:\Program Files\Git\git-bash.exe" set "BASH=C:\Program Files\Git\git-bash.exe"
if "%BASH%"=="" (
  echo Git Bash not found. Install Git for Windows, or run manually in Git Bash:
  echo   cd /c/Users/nermi/source/repos/AORebirth
  echo   bash tools/git_push_pet_work.sh
  exit /b 1
)
cd /d "%REPO%"
"%BASH%" -lc "bash tools/git_push_pet_work.sh"
endlocal
