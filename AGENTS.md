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
- Report files inspected.
- Report files changed.
- Report validation performed.
- Keep `docs/ai/CURRENT_TASK.md` focused on active work only.
- Keep `docs/project/PROJECT_STATE.md` updated when stable project status changes.

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
