# Agent Rules

## Authority and instruction precedence

`AGENTS.md` is the repository-wide authority for agent behavior. A nested
`AGENTS.md`, if present, must also be followed for files within its subtree; it
may add narrower requirements but may not weaken higher-precedence rules.

Instruction precedence, highest to lowest:

1. external system and user instructions;
2. this root `AGENTS.md`, supplemented by applicable nested `AGENTS.md` files;
3. `docs/project/DEVELOPMENT_AUTHORITY.md`;
4. `docs/project/PROJECT_STATE.md`;
5. `docs/ai/CURRENT_TASK.md`, for the active task only;
6. `docs/project/KNOWN_DECISIONS.md`;
7. architecture and subsystem documentation;
8. workflow and code-standard documentation;
9. task-specific evidence documents;
10. historical documentation.

Current authoritative documents override stale historical descriptions.
Historical reports never redefine current architecture. `AI_START_HERE.md` is
the single startup index.

## Existing architecture and scope

Before introducing an abstraction, replacement system, build or configuration
mechanism, staging mechanism, service, wrapper, compatibility layer, or
architecture change:

- inspect the existing implementation;
- identify the existing configuration, runtime, and build mechanisms;
- prove those mechanisms cannot satisfy the requested requirement;
- report that evidence before redesigning anything.

Use this order of preference:

`existing mechanism > configuration > smallest targeted fix > new architecture`

A configuration problem is not authorization for an architecture change. A
failing validation or test is not authorization to modify production code.

Perform only the requested task. Do not perform unrelated cleanup,
refactoring, modernization, architecture redesign, documentation expansion,
test repair, or build-system redesign. Do not turn a narrow task into a
generalized framework.

## AORebirth validation and live acceptance

Do not automatically run fixture, unit, or regression test suites. Run them
only when Mike explicitly requests them.

For normal AORebirth development:

`inspect -> smallest targeted change -> compile/build -> live AO client verification`

Compilation and build validation remain appropriate when needed to produce a
runnable build. Mike performs live AO client verification. Never launch or
control the AO client unless Mike explicitly requests it in the current task.

Do not modify production behavior merely to satisfy stale fixtures, snapshots,
stored hashes, generated inventories, generated reports, or unit-test
expectations. Treat those failures as diagnostic information unless the task
explicitly establishes the test or fixture as an authoritative contract.

## Gameplay content boundary

Gameplay and content data must remain data-driven. NPC-specific data, stats,
appearances, IDs, dialogue, quests, rewards, vendor stock, spawn bindings, and
similar game content must not be hard-coded into reusable runtime C# merely to
make behavior work.

Runtime C# implements reusable mechanics, services, loaders, and validation;
individual content definitions belong in the appropriate editable data source.

## Development authority

- Public GitHub `master` is the authoritative source.
- Windows/public master is developed and accepted first.
- Linux is a derived build of the same accepted source.
- Linux-specific changes are limited to actual Linux operating-system, build,
  and deployment requirements.
- Do not make Linux-only gameplay/runtime changes or introduce gameplay/runtime
  changes directly on the Linux deployment.
- `ZoneEngine_New` is the active zone runtime. Legacy `ZoneEngine` is retired;
  historical references do not make it active.

## GameData authority

- `D:\AORebirth-fresh\GameData` is Mike's private full GameData dataset.
- `AORebirth\GameData` is the public GitHub distributable/placeholder dataset.
- Differences between private and public catalogs are intentional and are not
  automatically drift.
- Existing runtime configuration `AO_REBIRTH_GAMEDATA_PATH` selects
  external/private GameData.
- Do not redesign GameData staging or selection when that existing mechanism
  satisfies the requirement.
- Never commit private GameData to public GitHub.
- `AO-Content-Dump-main` is extraction, reference, and upstream material; it is
  not the runtime GameData root.

- This agent works only on AORebirth. Never work on AO Rebuild or AO stripdown, and never switch to either workspace.
- Read `AI_START_HERE.md` first.
- Ground work in repository files.
- Never rely on old chat history.
- Run `git status --short --branch` before editing.
- After creating a commit, push it to the configured remote; do not leave commits local-only unless Mike explicitly asks for a local-only commit.
- After committing and pushing, include a small Discord-ready summary Mike can post.
- Do not guess packet behavior.
- Do not change database schemas without explicit approval.
- Do not perform destructive database operations.
- Use documented workflow commands first for known workflows; do not rediscover known build, engine, capture, or validation commands.
- Do not improvise shell syntax. Use repository-approved command forms and wrappers; malformed command syntax is an agent workflow violation and must be prevented, not merely corrected after failure.
- Agents must not run probe commands, line-count probes, empty-pattern commands, placeholder commands, or shell-syntax experiments unless Mike explicitly requests that investigation. Required file inspection must use known-good targeted read commands only. A malformed probe command is an agent workflow violation even if it causes no repo change. Reporting the bad command afterward is not enough; prevention is required.
- Malformed search, find, rg, grep, dir, or line-count commands are agent execution errors, not project blockers.
- For Windows/cmd searches, prefer shell-safe `rg` forms with repeated `-e` patterns over complex quoted regex strings, especially when paths contain spaces.
- Protect the context window: avoid command spam, large logs, repeated searches, and noisy transcripts.
- Never launch the AO game/client unless Mike explicitly instructs it in the current task.
- For AOSharp live capture startup, use only the approved `cmd.exe` wrapper documented in `docs/ai/WORKFLOW.md`.
- For mission-terminal / mission-lifecycle capture **analyze and implement**,
  use the repository-relative analyzer build/run commands documented in
  `docs/ai/WORKFLOW.md`. From the repository root, the analyzer command is
  `cmd /d /c tools-temp\AOSharpMissionCaptureAnalyzer\bin\Debug\AOSharpMissionCaptureAnalyzer.exe "<capture-folder>"`.
  If the executable is absent, use the documented repository-relative MSBuild
  command first. Ground implementation in `mission-flow.replay.log`. Do not use
  a user-profile absolute path or start with ad-hoc log searches.
- Report files inspected.
- Report files changed.
- Report validation performed.
- Keep `docs/ai/CURRENT_TASK.md` focused on active work only.
- Keep `docs/project/PROJECT_STATE.md` updated when stable project status changes.

## ACTIVE WORKFLOW DISCIPLINE (MANDATORY)

- When an approved workflow, wrapper, launcher, startup command, capture command, build command, validation command, or investigation workflow already exists and has previously succeeded, use it immediately.
- Do not rediscover workflows.
- Do not revalidate workflows.
- Do not search the repository for workflows that are already documented.
- Do not inspect workflow source code unless the workflow itself is being modified.
- Assume approved workflows remain valid until they fail.

For capture-backed tasks:

- When Mike provides an AOSharpLiveCapture capture directory or path, treat that as confirmation that the capture is complete and closed. Analyze it immediately; do not ask whether it is closed or finalized unless Mike explicitly says the capture is still running.
- When a workflow document explicitly identifies the approved command, wrapper, launcher, or script, use that command directly. Do not perform any repository search to verify, locate, confirm, inspect, or rediscover it.
- When Mike explicitly requests a new capture, launch the approved AOSharp
  capture workflow immediately. Mike performs the live gameplay action unless
  he explicitly instructs the agent otherwise in the current task. Stop and
  analyze that capture through the approved workflow.
- When Mike provides a completed capture path, analyze it immediately; do not
  launch or stop another capture.
- For an explicit new-capture task, the first operational command should
  normally be the approved capture launcher.
- If the task explicitly says "perform a capture", "start capture", or "run capture",
  the approved capture launcher should be the first task-related command executed.

Do not spend command budget on these unless the approved workflow has already failed:

- `rg`
- `grep`
- `findstr`
- `dir`
- `tree`
- `Get-ChildItem`
- repository-wide searches
- workflow discovery
- workflow archaeology
- documentation archaeology
- source-code archaeology

Active task discipline:

- The active task is defined only in `docs/ai/CURRENT_TASK.md`.
- Stay within that task and the user's current request.
- Do not classify, alter, or propose work on unrelated dirty files.

Failure exception:

- If the approved workflow fails, explain the failure, investigate only the failure, and then immediately return to the active task.

## SMALL TASK DISCIPLINE (MANDATORY)

For small docs-only or workflow-rule edits, use the smallest sufficient action. If the prompt names the exact target files, do not rediscover them; inspect only the named files and the smallest relevant sections.

For small scoped tasks:

- Use at most three pre-edit commands.
- Use at most two validation commands.
- Use at most one final status command.
- Do not search memory files, generated docs, project state, or the broader repo.
- Do not inspect source code, build scripts, logs, captures, or unrelated docs.
- Do not build, launch the game, start live capture, start or stop engines, or use PowerShell.

Progress update discipline:

- Use at most one short pre-edit progress line and one compact final response.
- Do not narrate obvious steps such as staging, validation starting, commit starting, push starting, or final status checking unless something fails or user action is needed.

Stop-after-success rule:

- Once the requested change is made, validation passes, and the commit/push is complete, stop. Do not perform extra audits, cleanup, refactoring, project-state edits, generated-doc updates, exploratory searches, or additional verification not requested by the task.

## OUTPUT DISCIPLINE (MANDATORY)

- Work silently whenever possible.
- Report conclusions, not investigation steps.
- Do not narrate reasoning or exploratory actions.
- Do not output "Ran X commands" messages.
- Do not print command output unless it directly supports a finding or an error.
- Do not dump entire files into chat.
- Do not use commands that print entire files unless explicitly requested.
- For successful builds and tests, report PASS only.
- For failed builds and tests, report only relevant failure information.
- When reporting findings, provide only file path, line numbers, and conclusion.
- Do not provide play-by-play investigation updates.
- Keep status updates limited to Findings, Files modified, Validation status, and Blockers.
- Final task reports should contain only Root cause, Files changed, Validation performed, Commit hash, and Remaining risks.
