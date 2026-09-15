# Build and acceptance boundary

Windows development and testing remain authoritative. Shared gameplay source is
maintained here. Production-platform builds and deployment implementation are
maintained privately and consume an exact Windows-accepted source commit.

SharedBuild contains only Windows-required compatibility adapters, source
inventories and test fixtures. Windows acceptance validates compiled metadata,
runtime source completeness, mandatory tests and durable-state behavior.

The private build independently verifies target-platform compilation, runtime
tests, package identity and deployment readiness. Windows acceptance alone does
not authorize a production release. Both source and private tooling identities
must be recorded. Running services are not modified by repository maintenance.

Operational records and private implementation details are retained outside the
public repository. Historical source identities are preserved in the private
archive during the coordinated history cleanup.
