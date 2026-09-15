# Current Task

Official-client acceptance of exact candidate 9817b708 is PASS. Actual client:
C:\Funcom\Anarchy Online, 18.8.62_EP2. Login, CharInPlay/chat, zoning,
inventory/equipment, logout/relogin and post-service-restart integrity are accepted.
All four item identities survive; the QL1 shirt remains equipped. The only stat
delta is the explicitly logged normal +6 health regeneration.

Active work: reconcile into a clean Mike-owned master-integration worktree, run
full exact-result Windows/Linux and connected/DAO/schema acceptance, then prepare
final release and rollback manifests. origin/master is still 7be49b22; independently
verified live Login/NewEngine remain cb12160c. No new migrations are required.
Direct master push and production deployment are not authorized. Keep Legacy.
Do not reopen the resolved position fixture issue without a new deterministic failure.
See docs/reports/NEWENGINE_9817B708_OFFICIAL_CLIENT_ACCEPTANCE.md.
