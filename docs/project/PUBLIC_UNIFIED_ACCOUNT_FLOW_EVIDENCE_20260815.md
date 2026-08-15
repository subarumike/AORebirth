# Public Unified Account Flow Evidence — 2026-08-15

## Scope

This evidence covers the first public AORebirth website account flow on
`https://ao-rebirth.com`:

- `/register`
- `/login`
- `/account`
- `/logout`

MyBB/forum provisioning remains out of scope. Legacy PHP account endpoints stay
blocked. Account creation is routed through the AORebirth Account Broker; PHP
does not insert directly into the game `login` table.

## Deployed broker build

- AORebirth source commit: `9a176f6fb3b20b589e09f0bbfdd95ea048423062`
- Linux artifact: `LinuxBuild/artifacts/accountbroker/linux-x64/self-contained`
- Production release target:
  `/opt/ao-rebirth/accountbroker/releases/9a176f6f`
- Current symlink:
  `/opt/ao-rebirth/accountbroker/current -> /opt/ao-rebirth/accountbroker/releases/9a176f6f`
- systemd service: `ao-rebirth-accountbroker.service`
- health: `active`, `GET http://172.18.0.1:7510/health -> 200`
- bind: `172.18.0.1:7510`
- firewall: one `7510/tcp` allow rule scoped to the Docker bridge network

The broker was configured with:

- `AOREBIRTH_ACCOUNT_BROKER_URL=http://172.18.0.1:7510/`
- `AOREBIRTH_ACCOUNT_BROKER_ALLOW_PRIVATE_BIND=true`
- `AOREBIRTH_ACCOUNT_BROKER_TRUSTED_PROXY_CIDRS=172.18.0.0/16`

The trusted-proxy CIDR is required so website-container requests are rate
limited by the forwarded client identity instead of collapsing to one bridge IP.

## Website route state

Production route checks after enabling `ACCOUNT_FEATURES_ENABLED=true`:

- `/register -> 200`
- `/login -> 200`
- unauthenticated `/account -> 302`
- `/forum.php -> 200`
- legacy `/register.php -> 403`
- legacy `/process-login.php -> 403`
- website container health: `healthy`

Apache clean routes map only the new public account pages. Legacy WebCore
mutation/login pages remain denied.

## Root cause found during cutover

The first public POST tests failed with the website message:

`Account services are temporarily unavailable.`

Broker reachability from the website container was healthy. The failure was in
`ao/includes/account-broker.php`: PHP stream requests returned broker response
bodies, but the helper did not reliably capture response headers/status/cookies
through `$http_response_header`/`http_get_last_response_headers()` in the
deployed PHP 8.3 container. Because broker CSRF is cookie-bound, the helper
could not complete CSRF-backed register/login calls and returned status `0`,
which the public form mapped to service unavailable.

Fix: `aor_broker_request()` now uses PHP cURL when available and keeps the
stream-wrapper implementation only as a fallback. The production PHP container
has cURL enabled. Probe after the fix:

- broker helper CSRF: `ok`
- direct helper `/api/csrf` status: `200`
- helper registration status: `201`

## Public acceptance result

A controlled production test account was created through public
`https://ao-rebirth.com/register`. The generated username/password/email were
stored only on the VPS at `/tmp/aor_acceptance_identity.txt` for follow-up
manual AO-client testing and were not copied into the repository or this
document.

Acceptance checks:

- new public registration: `302`, no validation/service errors
- wrong-password website login: `200`, expected incorrect-password message
- correct-password website login: `302`
- authenticated `/account`: `200`
- account page shows username label: yes
- account page shows game account label: yes
- account page shows linked state: yes
- logout: `302`
- post-logout `/account`: `302`
- duplicate registration failure path: expected duplicate message
- invalid username failure path: expected validation message
- invalid email failure path: expected validation message
- password mismatch failure path: expected validation message
- login rate-limit path: expected "Too many attempts" message

Database linkage proof for the controlled account:

- `IDENTITY_ROWS=1`
- `IDENTITY_EMAIL_ROWS=1`
- `LOGIN_ROWS=1`
- `MAPPING_LINKED_ROWS=1`
- `LOGIN_NORMAL_FLAGS_ROWS=1`

This proves the public website flow created one AORebirth identity, one linked
game `login` row, and one linked identity/game mapping with normal non-GM
account flags.

## Broker-unavailable isolation

`ao-rebirth-accountbroker.service` was stopped briefly and then restarted.

Observed while broker was stopped:

- AccountBroker: `inactive`
- public registration POST: `200` with the expected temporarily-unavailable
  message
- LoginEngine service: `active`
- ChatEngine service: `active`
- ZoneEngine service: `active`
- LoginEngine listener `7500`: present
- ZoneEngine listener `7501`: present

Observed after restart:

- AccountBroker: `active`
- broker health: `200`

This proves website account creation fails closed when the broker is down and
does not take the game engines offline.

## Validation performed

Windows/local gates:

- Account identity schema runner: PASS
- AccountBrokerValidation Debug: PASS 28/28
- AccountBrokerValidation Release: PASS 28/28 after serial rerun
- LoginAuthenticationValidation Debug: PASS 14/14
- LoginAuthenticationValidation Release: PASS 14/14
- UnifiedAccountFlowValidation Debug: PASS 34/34
- UnifiedAccountFlowValidation Release: PASS 34/34
- `Tools/run_database_preflight_tests.cmd`: PASS
- `Tools/run_aotomation_messaging_tests.cmd`: PASS 1013/1013
- `WebEngine.exe /self-test-web-request-policy`: PASS 15/15
- AORebirth `git diff --check`: PASS
- Website changed PHP lint: PASS
- Website `git diff --check`: PASS with line-ending warnings only

Production gates:

- Account Broker Linux self-contained publish: PASS
- Account Broker systemd active/health: PASS
- website clean-route checks: PASS
- public register/login/account/logout browser-flow checks: PASS
- duplicate/invalid/mismatch failure checks: PASS
- login rate-limit failure check: PASS
- broker-unavailable isolation: PASS
- website and broker log forbidden-string scan for password/hash/salt/env-secret
  terms: PASS, zero matches in checked tails

## Remaining risk

Real AO official-client login with the controlled production account was not
executed by this agent. The website and database evidence proves the account row
and password path were created through the broker, and Windows LoginEngine
validation proves correct and wrong passwords against the AO password verifier.
Final player-facing proof still requires logging into the normal AO client with
the generated production test account or adding a dedicated protocol-level
LoginEngine acceptance tool.

Operational note: during DB proof investigation, a diagnostic `docker exec ...
env` command printed MySQL container environment values into the local task
transcript. The values are not repeated here and were not written to project
files, but the affected DB secrets should be rotated after this cutover.
