@echo off
cd /d "%USERPROFILE%\source\repos\AORebirth"
git merge origin/master -m "Merge origin/master into local master"
echo MERGE_EXIT=%ERRORLEVEL%
git log -5 --oneline
git status --short --branch
