# Current Task

## Active Task

Organization creation bypass for local testing without a six-character team.
Follow-up: expose organization city association for local city entry testing.

## Scope

- Add a local-testing chat command that creates an organization through `OrganizationDao.CreateOrganization`.
- Repair `OrganizationDao.CreateOrganization` enough for required organization table fields.
- Assign the caller to the new organization as rank `0`.
- Do not enable the full legacy `OrgClient.Read` handler or bypass through packet behavior.
- Do not modify database schema or run destructive database operations.
- Send `organizations.CityID` as the character info packet city playfield id when a character is in an org.

## Repo Evidence

- `OrgClient.Read` has legacy command `1` organization creation logic, but the active inbound `OrgClientMessageHandler` does not route full legacy org behavior.
- `/set` only changes numeric stats and cannot create the organization row or leader record.
- `organizations.Description`, `Objective`, and `History` are `NOT NULL`; `OrganizationDao.CreateOrganization` previously left them unset.
- `CharacterInfoPacketMessageHandler` previously hardcoded `CityPlayfieldId = 0`, so the client still saw no org city association.
- The tested city entry terminal in playfield `655` maps to destination playfield `1702`.

## Implementation Notes

- Prefer `/command createorg <organization name>` so macro/chat command wrapper behavior remains compatible.
- Accepted aliases may include `makeorg` and `orgcreate`.
- The command should reject callers already in an organization to avoid orphaning existing org membership.
- The command should report organization insert exceptions to the client and ZoneEngine log.
- The command must not require `gmlevel`; this is specifically for local testing where a six-character org team is unavailable.

## Validation Plan

- `git diff --check`
- repo-approved build
- `cmd /d /c restart-engines.cmd`
- User gameplay testing: run `/command createorg <organization name>` on a GM character not already in an org.
- User gameplay testing: retry the city entry after `organizations.CityID` is set for the test org.

## Validation Result

- `git diff --check`: PASS.
- First `tools\build_aorebirth_debug.cmd`: blocked only by running `ZoneEngine` PID `25584` locking `ZoneEngine.exe`.
- `stop-engines.cmd`: PASS, used only to clear the build lock.
- Second `tools\build_aorebirth_debug.cmd`: PASS.
- Follow-up investigation found `/command createorg Testing Org` reached ZoneEngine but created no DB row because organization DAO insert did not satisfy live `organizations` schema.
- Follow-up `git diff --check`: PASS.
- Follow-up first `tools\build_aorebirth_debug.cmd`: compile passed, final copy blocked only by running engine DLL locks.
- Follow-up `stop-engines.cmd`: PASS, used only to clear the build locks.
- Follow-up second `tools\build_aorebirth_debug.cmd`: PASS.
- Follow-up `restart-engines.cmd`: PASS.
- Follow-up authorization check: `createorg` originally required `gmlevel >= 1`; lowered requirement to `0` for the local-testing bypass.
- Follow-up authorization fix `git diff --check`: PASS.
- Follow-up authorization fix `tools\build_aorebirth_debug.cmd`: PASS after stopping running engines.
- Follow-up authorization fix `restart-engines.cmd`: PASS.
- User gameplay testing required for `/command createorg <organization name>`.
- Follow-up city association DB patch: `organizations.Id=1` updated from `CityID=0` to `CityID=1702`; rows changed `1`.
- Follow-up city info packet fix `git diff --check`: PASS.
- Follow-up city info packet first `tools\build_aorebirth_debug.cmd`: compile passed, final copy blocked only by running `ZoneEngine` PID `2756`.
- Follow-up city info packet `stop-engines.cmd`: PASS, used only to clear the build lock.
- Follow-up city info packet second `tools\build_aorebirth_debug.cmd`: PASS.
- Follow-up city info packet `restart-engines.cmd`: PASS.
- Follow-up city terminal live smoke: not observed in fresh ZoneEngine log after restart; user gameplay testing still required.
