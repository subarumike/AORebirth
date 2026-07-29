@echo off
cd /d "%USERPROFILE%\source\repos\AORebirth"
git stash list
echo ====
git stash pop
echo POP_EXIT=%ERRORLEVEL%
git status --short --branch
echo ====
git diff --name-only --diff-filter=U
