# Current Task

VALIDATED: restored the maintained standalone Windows launcher and packaging
workflow for public product source `d4285053660ccfe9b01cee6ed656af6b78b84e11`,
which contains the authorized shutdown and claim-root isolation repair.

Scope: four-engine lifecycle and readiness with WorldEngine first/last; current
owned-local-database migration/preflight handling; exact measured engine outputs
and admitted GameData; a bundled loopback PHP host for Daily Rewards; focused
offline regressions; a new replacement ZIP and local packaged startup followed
by clean shutdown.

The October standalone ZIP and existing user configuration remain preserved.
Gameplay and protocol changes, production and live schemas are outside this task.
No deployment is authorized. Keep private content, dependency inputs,
package artifacts and release evidence outside public commits.

The scoped public repair passed five focused regressions and Windows startup
with four zero exit codes on shutdown. The replacement package passed staged
integrity verification, 20 focused offline cases and local startup/shutdown
using its bundled runtimes and own database. Local Daily GET/POST passed; the
four game engines exited zero. Automated checks do not record Mike's Windows or
live-client acceptance. Daily's referenced image gap is recorded in the
packager README.
