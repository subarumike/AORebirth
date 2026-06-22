# Workflow

## First Checks

Run:

```cmd
git status --short --branch
```

Identify dirty files before editing. Do not revert user or previous-agent work unless Mike explicitly asks.

## Known Workflow First

- Before exploratory commands, check the project AI docs for the documented workflow.
- For known workflows, use the documented command first.
- If a wrapper exists, use the wrapper.
- Do not bypass wrappers with hand-rolled command chains unless the wrapper itself is broken and the task is specifically to repair it.
- If no wrapper exists for a recurring task, create the smallest practical `cmd.exe` wrapper only when that is within the task scope, then document that wrapper as the approved entrypoint.
- If the documented command is missing, ambiguous, or outdated, stop and report the documentation gap. Do not improvise a discovery session.

## Command Syntax Safety

- Do not improvise shell syntax. Use repository-approved command forms, documented wrappers, and simple shell-safe commands. Malformed command syntax is an agent workflow violation and must be prevented, not merely corrected after failure.
- Agents must not run probe commands, line-count probes, empty-pattern commands, placeholder commands, or shell-syntax experiments unless Mike explicitly requests that investigation. Required file inspection must use known-good targeted read commands only. A malformed probe command is an agent workflow violation even if it causes no repo change. Reporting the bad command afterward is not enough; prevention is required.
- Do not run line-count probes just to prove a file exists or estimate file size.
- Do not run `find`, `findstr`, `rg`, `grep`, `dir`, or similar commands with empty patterns, placeholder arguments, or syntax experiments.
- Required workflow-doc reads must use known-good targeted read commands only.
- If a file needs to be inspected, read the relevant section directly using a known-good command form.
- A malformed probe command is still an agent workflow violation even if it caused no repo change.
- Malformed command syntax is an agent workflow violation and an agent execution error, not a project blocker. Malformed search, find, rg, grep, dir, or line-count commands are agent execution errors, not project blockers. If a command fails because of quoting, shell syntax, escaped characters, regex syntax, or path quoting, immediately rerun the task once with a simpler command form.
- Search commands must be shell-safe. For ripgrep on Windows/cmd workflows, prefer repeated `-e` patterns instead of complex quoted regex strings.
- Good:

```bat
cmd /d /c rg -n -e "PatternOne" -e "PatternTwo" AORebirth\Server
```

- Bad:

```bat
rg -n 'PatternOne|PatternTwo' "AORebirth/Server"
rg -n "(PatternOne|PatternTwo)" "path with nested quoting"
```

- Do not combine fragile quoting with paths containing spaces. Use simpler searches, narrower paths, or multiple safe commands instead.
- Keep output compact. Do not dump full files, full logs, broad recursive output, or noisy command output into chat or the context window. Use targeted searches, line-numbered snippets, and concise summaries.
- If a malformed command happened, final reporting must include the failed command category, the corrected safe command form used, and confirmation that the malformed command did not change repo state. This reporting is required after prevention failed, but it does not excuse the workflow violation. Do not paste a giant output dump.

## Command Budget And Context Protection

- Protect the context window as a project resource.
- For known workflow startup, use at most one command to start the tool.
- Use at most one optional command to verify expected output only if required.
- Use at most one optional command to inspect a targeted failure log only if the start command fails.
- Do not rediscover known commands with repo-wide searches, directory sweeps, process sweeps, tasklist sweeps, repeated log reads, or source-code inspection.
- Prefer exact known paths over discovery commands.
- Prefer targeted file reads over broad searches.
- Do not paste large command output, long transcripts, repeated directory listings, tasklist output, full logs, or broad search results into chat.
- When a command is expected to be noisy, redirect output to a local log file and summarize only the result, exact command, relevant path, or smallest relevant error.

## Command Permissions

- Run shell, Git, build, test, validation, and capture commands normally first.
- PowerShell is disallowed for AORebirth build, launch, validation, and live capture workflows.
- `.ps1` wrappers are deprecated for Codex AORebirth workflows. Use `cmd.exe` or Git Bash.
- Do not set `sandbox_permissions` or use `require_escalated` unless the normal command has already failed with a real OS permission error.
- Before retrying with escalation, stop and report the exact command, working directory, target path, and full error text.
- Do not use admin elevation or machine-wide policy changes for routine repo work.

## Build And Engines

After code changes that affect server binaries:

1. Stop engines if running processes are locking build outputs.
2. Build.
3. Restart Chat, Login, and Zone with the root restart wrapper.
4. Check engine status and expected ports only through the existing quick wrapper output.
5. Do not start WebEngine unless explicitly needed.

Stop engines through an approved `cmd.exe` or Git Bash workflow only. Do not run `stop-engines.ps1` from Codex.

Build:

```cmd
cmd /d /c tools\build_aorebirth_debug.cmd
```

Do not use raw AORebirth MSBuild validation with `/m` or MSBuild node reuse. The `cmd.exe` build wrapper kills stale `MSBuild.exe`, `dotnet.exe`, `VBCSCompiler.exe`, and `NuGet.exe` processes, verifies required packages under `AORebirth\packages`, restores packages explicitly before build only when required package folders are missing, builds `AORebirth.Core` first, then builds `ZoneEngine`, using:

```cmd
MSBuild.exe <project> /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
```

Legacy build-time NuGet restore through `.nuget\NuGet.targets` has been removed from project files. If required package folders are missing, the wrapper runs explicit solution restore before build with visible progress and timeout handling:

```cmd
MSBuild.exe AORebirth\AORebirth.sln /t:Restore /p:RestorePackagesConfig=true /m:1 /nr:false /v:minimal
```

Do not reintroduce project-level `RestorePackages` targets or `.nuget\NuGet.targets` imports.

If a Codex shell command times out during build validation, do not treat timeout exit code `124` as a build failure until checking for orphaned build child processes and stopping them.

Start engines, stop engines, and check engine status through approved `cmd.exe` or Git Bash workflows only. Existing `.ps1` engine launch/status wrappers are deprecated for Codex use until replaced.

After a successful rebuild, restart engines with:

```cmd
cmd /d /c restart-engines.cmd
```

`restart-engines.cmd` is the repo-owned Codex restart entrypoint. It calls the existing approved `stop-engines.cmd` and `start-engines.cmd` wrappers and does not add extra polling, diagnostics, or manual lifecycle commands.

## Database

- Use only `cellao_codex_clean`; this is the active legacy database name retained for local compatibility.
- Do not change schemas without explicit approval.
- Do not wipe or mass-edit data without explicit approval.
- Treat checked-in SQL and runtime DB changes as separate surfaces.

## Captures

- Use AOSharp capture tooling for live packet/data truth.
- Codex runs tools, builds, servers, and captures.
- Mike performs live client playtests.
- Do not ask Mike to run commands inside the game when Codex can run external tooling.
- Do not run PowerShell or `.ps1` live capture wrappers from Codex; use `cmd.exe` or Git Bash workflows.
- Never launch the AO game/client automatically unless Mike explicitly instructs it in the current task.
- Live game testing is manual by Mike. Codex may build, validate, inspect files, or prepare capture tools only within the documented workflow.

### AOSharp Live Capture Startup

Approved startup command:

```cmd
cmd /d /c tools-temp\start-aosharp-live-capture.cmd --title "<AO window title>"
```

Alternative when Mike provides the client process id:

```cmd
cmd /d /c tools-temp\start-aosharp-live-capture.cmd --pid <ao-client-pid>
```

This wrapper is the only approved Codex startup command for AOSharp live capture. It starts the existing AOSharp injector against an already-running AO client and reports only the exact injector command, success or failure, capture output path, and failure log path. It does not launch the AO game/client.

Before running the wrapper, do not run `rg`, `dir`, `tasklist`, recursive searches, process sweeps, source inspection, build-folder enumeration, or old-log scraping to rediscover how capture startup works. Use the wrapper directly.

Do not inspect AOSharp capture source code, search for command names, enumerate build folders, or read old capture logs unless the wrapper fails or Mike explicitly asks for investigation.

If the wrapper fails, run at most one targeted failure-log inspection command, summarize the smallest relevant failure, identify the likely broken doc, wrapper, missing build output, or runtime prerequisite, then stop unless Mike asked for repair. The approved targeted failure check is:

```cmd
cmd /d /c findstr /C:"ERROR:" tools-temp\AOSharpLiveInjector\bin\Debug\AOSharpLiveInjector-start.log
```

## Live Client Behavior Bugs

For AORebirth bugs involving current AO client behavior, packet flow, UI actions, item movement, inventory, bank, backpacks, shops, trade, missions, NPC interactions, pets, combat actions, or other client/server behavior:

- Treat the live AO client as the authoritative protocol source.
- Treat legacy server code as a partially-correct reference, not proof.
- Do not rely on static audit alone when packet behavior is involved.
- Start with live capture or existing capture review whenever feasible.
- User should only perform in-game actions; Codex must inspect logs/captures itself.
- If capture is not possible, explicitly say so and explain the fallback evidence.
- Repairs must be based on confirmed live packet/message behavior when available.

## Capture-Derived Content

These rules apply to NPC, mob, statel, static dynel, vendor, quest, item, and playfield reconstruction.

- Identity first. The captured AO identity is the primary key.
- Do not choose or replace an object based only on display name, item name, screenshot appearance, nearby objects, spatial proximity, visual similarity, assumed mesh, or guessed relationship.
- Search the complete relevant capture set for the exact identity before declaring evidence missing. Include `events.log`, `packets.hex.log`, `system-messages.log`, `npc-interactions.log`, `inventory-updates.csv`, `enemy-state.csv`, `enemy-state.json`, `vendor-full-updates.csv`, `shop-updates.csv`, and decoded full-update outputs.
- Separate interaction evidence from definition evidence. `GenericCmd Action=Use -> Terminal:56D9B4AF` proves only that the identity was used; template, mesh, name, position, rotation, stat blob, and event configuration require a full-update packet or another source tied to the same identity.
- Use the evidence hierarchy from `docs/project/KNOWN_DECISIONS.md`: exact identity-linked full-update, exact identity-linked stat/update, exact identity-linked interaction, decoded logs, extracted analysis, screenshots, then names/proximity/nearby objects.
- Do not copy template, mesh, stat blob, position, rotation, or events from a nearby identity unless the capture explicitly proves the relationship.
- Do not test alternate templates or mesh overrides because the current object looks wrong. Stop, search all captures for the exact identity, locate full-update/stat evidence, and rebuild from that evidence.
- Keep evidence extraction, data creation, visual smoke, use/interact routing, objective progression, mission completion, and rewards as separate tasks.
- Fail closed when exact identity evidence or required full-update fields are missing, conflicting, unknown, or only supported by name/appearance.

Before editing SQL or runtime data, state:

- exact captured identity;
- capture folders searched;
- full-update evidence found;
- fields that are confirmed;
- fields that remain unresolved;
- files and rows that will change.

Also provide this evidence table before any SQL or game-data edit:

| Field | Proposed value | Exact identity | Capture folder | Packet/log source | Confidence |
| --- | --- | --- | --- | --- | --- |

Local SQL/data patches must include exact rows affected, pre-apply verification query, apply command, post-apply verification query, rollback query, and confirmation that no unrelated rows changed.

## Evidence

Use this source order:

1. Official live capture.
2. Private-server capture as shape/reference evidence.
3. AO stripdown source/contracts.
4. Local code facts.

Do not patch packet-sensitive behavior from visual symptoms alone.
