# Agent Rules

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
- Do not improvise shell syntax. Use repository-approved command forms and wrappers; malformed quoting, escaping, regex, or path syntax must be prevented, not merely corrected after failure.
- Agents must not run probe commands, line-count probes, empty-pattern commands, placeholder commands, or shell-syntax experiments unless the task explicitly requires them. Required file inspection must use known-good targeted read commands only. A malformed probe command is an agent workflow violation even if it causes no repo change. Reporting the bad command afterward is not enough; prevention is required.
- Malformed search, find, rg, grep, dir, or line-count commands are agent execution errors, not project blockers.
- For Windows/cmd searches, prefer shell-safe `rg` forms with repeated `-e` patterns over complex quoted regex strings, especially when paths contain spaces.
- Protect the context window: avoid command spam, large logs, repeated searches, and noisy transcripts.
- Never launch the AO game/client unless Mike explicitly instructs it in the current task.
- For AOSharp live capture startup, use only the approved `cmd.exe` wrapper documented in `docs/ai/WORKFLOW.md`.
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

- When a workflow document explicitly identifies the approved command, wrapper, launcher, or script, use that command directly. Do not perform any repository search to verify, locate, confirm, inspect, or rediscover it.
- Launch the approved AOSharp capture workflow immediately.
- Reproduce the gameplay action.
- Stop the capture.
- Analyze the capture.
- The first operational command should normally be the approved capture launcher.
- If the task explicitly says "perform a capture", "start capture", "run capture", or references a known capture workflow, the approved capture launcher should be the first task-related command executed.

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

- Stay on the active gameplay task.
- Do not switch to repo cleanup.
- Do not switch to architecture reviews.
- Do not switch to modernization work.
- Do not switch to documentation cleanup.
- Do not classify unrelated dirty files.
- Do not propose unrelated future work.

Failure exception:

- If the approved workflow fails, explain the failure, investigate only the failure, and then immediately return to the active task.

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
