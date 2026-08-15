# Current Task

## Active

Implement the first usable Windows-local unified account flow through the
Account Broker without enabling production account creation.

## Current checkpoint

- Windows remains the authoritative development and acceptance platform under
  `docs/project/DEVELOPMENT_AUTHORITY.md`.
- Password authentication is restored and proven in Debug and Release.
- The proposed identity schema now validates against the local Windows
  development MySQL target.
- The first internal Account Broker foundation is implemented and validated.
- The loopback Account Broker HTTP service now exposes local registration,
  login, current-session, logout, and health endpoints.
- Windows-local unified account flow validation passes in Debug and Release.
- Production `/register` and `/login`, legacy PHP account routes, MyBB
  installation, SSO, forum provisioning, production schema deployment, and Linux
  deployment remain out of scope.

## Remaining gates

- Commit/push only when explicitly instructed.
- Keep production database, production website routes, Linux services, and MyBB
  unchanged.
- Next public-account stage requires explicit approval for production migration,
  restricted broker credentials, broker hosting/API route, website route
  integration, and MyBB bridge work.

## Constraints

- Do not redesign the AO login protocol.
- Do not replace the existing password-hash format.
- Do not change character ownership in this stage.
- Do not perform destructive database operations.
- Do not apply production schema changes.
- Do not enable legacy website registration/login pages.
- Do not install MyBB.
- Do not launch the AO client without explicit current authorization.
