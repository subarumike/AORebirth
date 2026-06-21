# Current Task

## Active Task

Fix the recurring AORebirth MSBuild hang/timeout validation workflow.

## Current Scope

- Do not change gameplay or server behavior for this task.
- Add a repo build wrapper that is safe for Codex validation runs.
- Use single-node MSBuild with node reuse disabled.
- Kill stale `MSBuild`, `dotnet`, and `VBCSCompiler` processes before each build.
- Build `AORebirth.Core` first, then `ZoneEngine`.
- Preserve the real failing MSBuild exit code.
- Print visible progress before every major build step.
- Use `cmd.exe` or Git Bash only; do not use PowerShell or `.ps1` wrappers.
- Disable legacy build-time NuGet restore from `.nuget\NuGet.targets`; restore explicitly before MSBuild only when required packages are missing.

## Validation Plan

- Run `cmd /d /c tools\build_aorebirth_debug.cmd`.
- Run `git diff --check`.
- Commit only build workflow/tooling/doc changes for this task.
