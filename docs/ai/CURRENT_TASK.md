# Current Task

## Active Task

Modernize the AORebirth legacy NuGet restore workflow.

## Current Scope

- Do not change gameplay or server behavior for this task.
- Remove legacy project-level build-time NuGet restore wiring.
- Keep existing `packages.config` package versions unchanged.
- Use single-node MSBuild with node reuse disabled.
- Kill stale `MSBuild`, `dotnet`, `VBCSCompiler`, and `NuGet` processes before each build.
- Build `AORebirth.Core` first, then `ZoneEngine`.
- Preserve the real failing MSBuild exit code.
- Print visible progress before every major build step.
- Use `cmd.exe` only for this modernization; do not use PowerShell, Git Bash, or `.ps1` wrappers.
- Restore packages explicitly before MSBuild only when required package folders are missing.
- Do not run implicit project-level restore targets during build.

## Validation Plan

- Run `cmd /d /c tools\build_aorebirth_debug.cmd`.
- Run `git diff --check`.
- Commit only build workflow/tooling/doc changes for this task.
