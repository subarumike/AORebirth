# Build and acceptance boundary

Windows development and testing remain authoritative. Shared gameplay source,
Linux portability, build projects and reusable deployment implementation are
maintained here. Both platforms consume one exact Windows-accepted public-master
commit. Linux-private master mirrors that SHA without additional commits.

SharedBuild contains only Windows-required compatibility adapters, source
inventories and test fixtures. Windows acceptance validates compiled metadata,
runtime source completeness, mandatory tests and durable-state behavior.

The Linux build independently verifies target-platform compilation, runtime
tests, package identity and deployment readiness. Windows acceptance alone does
not authorize a production release. Both application and release-tooling identities
must be the same accepted public-master SHA. Running services are not modified
by repository maintenance.

Operational records, secrets and server-specific configuration remain external.
Historical source identities and prior releases are preserved for rollback.
No private application patch or assembled source commit may enter a release.
