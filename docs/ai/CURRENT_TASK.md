# Current Task

## Active

Public unified account flow is enabled on `ao-rebirth.com` through the
AORebirth Account Broker. Real AO-client acceptance with the generated
production test account remains the only unproven player-facing gate.

## Current checkpoint

- Password authentication is restored and proven in Debug and Release.
- The proposed identity schema now validates against the local Windows
  development MySQL target.
- The first internal Account Broker foundation is implemented and validated.
- The loopback Account Broker HTTP service now exposes local registration,
  login, current-session, logout, and health endpoints.
- Windows-local unified account flow validation passes in Debug and Release.
- Production Account Broker release `9a176f6f` is deployed and healthy on
  `172.18.0.1:7510`.
- Public `/register`, `/login`, `/account`, and `/logout` are enabled on
  `ao-rebirth.com`.
- Public registration created a controlled production account through the
  broker only; database proof shows one identity row, one linked `login` row,
  one linked game mapping, and normal non-GM account flags.
- Website wrong-password, correct-password, account, logout, duplicate,
  validation, rate-limit, and broker-unavailable failure paths passed.
- Legacy PHP account routes remain blocked.
- MyBB installation, SSO, and forum provisioning remain out of scope.

## Remaining gates

- Perform official AO-client login acceptance with the controlled production
  test account stored on the VPS at `/tmp/aor_acceptance_identity.txt`, without
  printing credentials.
- Rotate affected DB secrets because a diagnostic container-env command printed
  MySQL secret values into the local task transcript.
- Commit/push only when explicitly instructed.
- Next account stage requires explicit approval for MyBB bridge/forum
  provisioning work.

## Constraints

- Do not redesign the AO login protocol.
- Do not replace the existing password-hash format.
- Do not change character ownership in this stage.
- Do not perform destructive database operations.
- Do not enable legacy website registration/login pages.
- Do not install MyBB.
- Do not launch the AO client without explicit current authorization.
