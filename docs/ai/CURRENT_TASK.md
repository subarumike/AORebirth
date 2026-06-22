# Current Task

## Active Task

GM organization creation bypass for local testing without a six-character team.

## Scope

- Add a GM-only chat command that creates an organization through `OrganizationDao.CreateOrganization`.
- Assign the caller to the new organization as rank `0`.
- Do not enable the full legacy `OrgClient.Read` handler or bypass through packet behavior.
- Do not modify database schema or run destructive database operations.

## Repo Evidence

- `OrgClient.Read` has legacy command `1` organization creation logic, but the active inbound `OrgClientMessageHandler` does not route full legacy org behavior.
- `/set` only changes numeric stats and cannot create the organization row or leader record.

## Implementation Notes

- Prefer `/command createorg <organization name>` so macro/chat command wrapper behavior remains compatible.
- Accepted aliases may include `makeorg` and `orgcreate`.
- The command should reject callers already in an organization to avoid orphaning existing org membership.

## Validation Plan

- `git diff --check`
- repo-approved build
- `cmd /d /c restart-engines.cmd`
- User gameplay testing: run `/command createorg <organization name>` on a GM character not already in an org.

## Validation Result

- `git diff --check`: PASS.
- First `tools\build_aorebirth_debug.cmd`: blocked only by running `ZoneEngine` PID `25584` locking `ZoneEngine.exe`.
- `stop-engines.cmd`: PASS, used only to clear the build lock.
- Second `tools\build_aorebirth_debug.cmd`: PASS.
- `restart-engines.cmd`: PASS.
- User gameplay testing required for `/command createorg <organization name>`.
