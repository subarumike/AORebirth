# Agent Rules

## Authority and instruction precedence

`AGENTS.md` is the repository-wide authority for agent behavior. A nested
`AGENTS.md`, if present, must also be followed for files within its subtree; it
may add narrower requirements but may not weaken higher-precedence rules.

Instruction precedence, highest to lowest:

1. external system and user instructions;
2. this root `AGENTS.md`, supplemented by applicable nested `AGENTS.md` files;
3. `docs/project/DEVELOPMENT_AUTHORITY.md`;
4. `docs/project/PROJECT_STATE.md`;
5. `docs/ai/CURRENT_TASK.md`, for the active task only;
6. `docs/project/KNOWN_DECISIONS.md`;
7. architecture and subsystem documentation;
8. workflow and code-standard documentation;
9. task-specific evidence documents;
10. historical documentation.

Current authoritative documents override stale historical descriptions.
Historical reports never redefine current architecture. `AI_START_HERE.md` is
the single startup index.

## PATCH MODE default and FULL RELEASE MODE escalation

<!-- patch-release-policy:start -->
PATCH MODE is the default for routine AORebirth patches. This policy governs
release scope and supersedes older unconditional full-build, full-copy, broad-test
and repeated-baseline-review instructions. It does not change product authority,
input identity, Windows acceptance, privacy or deployment authorization.

FULL RELEASE MODE is permitted only when Mike explicitly requests it or a
material architecture, schema, build-system, engine-topology or other release-wide
compatibility change actually requires it. Record the changed paths, material
effect and necessary broader work before escalation. Unrelated inherited failures,
a new source SHA, a receipt-format issue or a missing incremental-tooling feature
never silently escalates PATCH MODE.

PATCH MODE follows this sequence:

1. Fetch current public master and record its tip. Normally synchronize
   Windows to that tip. An explicitly approved ZoneEngine-only runtime patch
   may use the exact selected ancestor after replaying the existing private
   patch plan and verifying its dependency closure and unchanged GameData and
   schema. Require LOCAL_WINDOWS_SOURCE_SHA == PUBLIC_MASTER_SHA, where the
   latter is the selected source for that patch.
2. Check the configured PRIVATE_CONTENT_UPSTREAM on every build. Reconcile only
   actual approved GameData additions, modifications and removals; preserve
   legitimate private runtime repairs and generated runtime content.
3. Build/test Windows first within the changed dependency/subsystem scope.
   Existing explicit authorization requirements for automated suites and client
   control still apply. Stop for Mike's exact-input Windows acceptance.
4. Freeze the accepted source SHA, content-dump SHA, GameData manifest and
   build/startup receipt. Never reuse acceptance for a changed tuple.
5. Use dependency analysis and incremental detection in the existing private
   GitHub Actions online builder to build only impacted Linux engines/projects
   and transitive dependencies. Reuse verified unchanged engine outputs.
6. Produce a delta package containing changed binaries/dependencies, changed or
   added GameData, and an explicit GameData removal list (empty when no removals).
   Do not download/reconstruct an entire previous release merely to create a patch.
7. With explicit deployment authorization, deploy that immutable delta; validate
   the actual-database backup, required migration state, affected service readiness
   and dependent health, provenance, package integrity and final manifest match.
   No local Docker/WSL or production Linux build is an alternative.

Whole-tree manifest consistency describes the complete logical target, not a
requirement to copy or reconstruct every file. Derive the target deterministically
from a verified base plus the accepted delta and removal list; reuse validated
unchanged entries/assets. Require Windows == Linux == live complete GameData
manifests. Never preserve an unexplained Linux-only overlay, omit a removal,
prune unrelated private content or substitute an unchecked cache for authority.

Record a known baseline failure once with its subsystem/input fingerprint,
failure signature and evidence. Reuse that record when the subsystem and relevant
inputs are unchanged. Do not rerun unrelated inherited failures, investigate them
as new regressions, or use them to justify broad gameplay/cave/client suites.
PATCH MODE fails only for:

| Failure condition | PATCH MODE result |
| --- | --- |
| NEW_FAILURE | FAIL |
| CHANGED_BASELINE_FAILURE | FAIL |
| FAILURE_IN_PATCH_TOUCHED_CODE_OR_DATA | FAIL |
| REQUIRED_RELEASE_CONTRACT_FAILURE | FAIL |
| UNCHANGED_UNRELATED_BASELINE_FAILURE | RECORD_AND_CONTINUE |

Declare an explicit test/check timeout before execution, using the check's
expected workload. Silence is not a hang. Do not interrupt a quiet test before
its declared timeout expires. On expiry retain diagnostics and stop only the
owned process. Broad suites require patch relevance and existing authorization;
FULL RELEASE MODE does not erase baseline classifications or timeout discipline.

Canonical GameData admits only explicitly identified runtime-consumed content:
loader/configuration/schema-backed paths and reviewed generated runtime outputs.
An extension such as JSON alone is not proof of runtime use. Cache, research
output, reports, documentation/README files, screenshots, temporary exports,
build artifacts, logs, backups and other non-runtime material must fail admission
or be deterministically excluded BEFORE manifest/package generation. Unknown or
unclassified paths fail closed; reject path/symlink escapes. Excluded material
stays outside canonical GameData. Do not silently delete unique evidence or
rewrite an accepted package; any cleanup delta uses normal governed ownership,
review and an explicit removal list.

Use available GitHub Actions caching for package restore, build intermediates,
unchanged private assets and unchanged GameData. Cache keys/provenance must bind
relevant source/dependency/content identities and reject stale or corrupt reuse.
Automatically derive impact, manifests, source audits and receipt metadata from
verified inputs and actual execution. Do not stage policy/receipt files manually
outside the governed pipeline or fabricate approvals, hashes, measurements or
acceptance. Fresh upstream checks, source parity and trust-boundary validation
remain mandatory even on a cache hit.

Optimize the shortest safe path: Windows accepted -> GitHub Actions incremental
build -> delta package -> deployment. Record phase timings and cache decisions
automatically; reuse unchanged verified evidence instead of hour-long manual
release investigations. Broader FULL RELEASE work must remain tied to its
recorded material trigger or Mike's explicit request.

These are governance requirements, not a claim that every existing wrapper
already implements selective execution, caching or runtime-content filtering.
If an entrypoint cannot satisfy PATCH MODE, identify its exact limitation and
the smallest required tooling correction; do not bypass a release gate, invent
unsupported switches, silently perform heavyweight work or relabel it FULL.
<!-- patch-release-policy:end -->

## Permanent build and release contract

Ordinary Windows developer compilation (`dotnet build` or Visual Studio Build)
does not require a private release profile, content access, GameData provenance
or acceptance state. Such compilation does not authorize publishing or release
promotion. `AORebirthGovernedRelease=true` explicitly selects guarded compilation;
active governed-wrapper capabilities also keep compiler validation mandatory.
Publishing and the existing governed release entrypoints retain all private
profile, source/content, acceptance and provenance checks. This distinction
supersedes references below to applying release intake to every ordinary build.

Isolated feature compilation and explicitly requested automated tests use the
existing contract helper's development-only operation:

```cmd
cmd /d /c tools\windows_release_contract.cmd run-feature-validation -- "C:\Program Files\dotnet\dotnet.exe" test AORebirth\Server\ZoneEngine_New.Tests\ZoneEngine_New.Tests.csproj --filter "FullyQualifiedName~MissionChest"
```

For the existing four-engine compilation workflow, use that same operation with
`-- "%ComSpec%" /d /c "<absolute-owning-checkout>\NewZoneEngineBuild\build.cmd"`.
Development tool execution is limited to an explicit owning-checkout
`dotnet run --project Tools/<existing-validation-tool>/<project>.csproj`.
The same development operation also accepts the existing
`Tools\run_mission_dao_validation.cmd --chests-only` and
`Tools\run_newengine_connected_acceptance.cmd --engine <own-Zone-dll> --login-engine <own-Login-exe-or-dll> --chests-only --world-engine <own-World-dll>`
through the absolute owning-checkout CMD wrapper paths. Connected engine paths
must belong to that checkout. The isolated DAO wrapper compiles linked actual
persistence sources; its receipt records the measured tool assembly and no
product hooks when none ran. It never invents compiler observations.
Use the correctly owned ignored private profile; never copy credentials, live
build capabilities, release state or acceptance from another checkout.

This operation requires an isolated `codex/` branch, pins its HEAD plus tracked
and untracked source changes and the selected complete GameData manifest, and
issues its own live process-bound development capability. It records command,
compiler observations, outputs and outcome under the existing ignored state
root. Source/content changes invalidate that validation. Its receipt is marked
`FEATURE_VALIDATION`, `DEPLOYABLE=NO` and `WINDOWS_USER_ACCEPTED=NO`; it cannot
authorize product publishing, release startup, acceptance or Linux release work.
Normal production builds still use the existing approved build wrapper and
require freshly fetched public-master source and reconciled private content.
The explicitly selected runtime patch path uses the same release contract
and the verified scoped compilation it authorizes.
Feature validation never replaces the Windows acceptance and release sequence.

The [build/release contract](docs/project/BUILD_RELEASE_CONTRACT.md) governs
every build and release. Its mandatory sequence is:

`fetch public master and private content -> synchronize Windows -> reconcile all approved content changes -> Windows build/startup -> STOP for Mike -> explicit Windows acceptance -> freeze inputs -> online Linux build/package -> authorized deployment -> verify live manifest`

Public GitHub `subarumike/AORebirth` branch `master` is the only product/runtime
source authority. The local Windows checkout is an integration/build environment,
not an independent authority. Governed Windows builds require
`LOCAL_WINDOWS_SOURCE_SHA == PUBLIC_MASTER_SHA`. Full and ordinary releases
use the freshly fetched master tip. The explicit scoped runtime patch path
records that tip separately as authority and freezes the verified selected
ancestor as `PUBLIC_MASTER_SHA`.

Check the configured private content upstream on every build/release attempt;
unknown or inaccessible upstream content fails closed. Reconcile every approved
addition, modification and removal into the canonical Windows GameData while
preserving legitimate private/generated content and canonical repairs. A cached
tree or older package cannot substitute for this check or redefine authority.

After Windows build/startup readiness, stop for Mike's testing. Automated
validation and deployment authorization do not imply `WINDOWS_USER_ACCEPTED=YES`.
Linux release work must not begin until Mike explicitly accepts the exact
Windows source, content-dump SHA, GameData manifest and build/startup receipt.
Freeze `PUBLIC_MASTER_SHA`, `CONTENT_DUMP_SHA` and `WINDOWS_GAMEDATA_MANIFEST`
for that accepted release. Newer master or content tips belong to the next
release unless Mike explicitly requests re-reconciliation and accepts it again.

Linux consumes that accepted source with `PUBLIC_RUNTIME_DIFFS=0`, apart from
explicitly allowlisted Linux build/packaging/service adaptations, and the exact
Windows-accepted canonical GameData. Do not construct a separate Linux data set.
Use the existing private GitHub Actions online builder for Linux binaries and
live-server packages; local Docker/WSL Linux builds and production rebuilds are
not permitted substitutes.

Deployment requires separate explicit authorization and installs the complete
accepted content delta, including removals. Require the deterministic manifest
chain `WINDOWS_ACCEPTED_GAMEDATA_MANIFEST == LINUX_GAMEDATA_MANIFEST == LIVE_GAMEDATA_MANIFEST`.
Any missing acceptance, unknown identity, source/content mismatch, omitted
content operation or builder bypass fails closed. Private role bindings,
content, credentials, tooling and release evidence remain private.

## Checkout ownership and deployment-tooling access

<!-- deployment-tooling-access:start -->
Normal source work stays in its owning checkout: PUBLIC_PRODUCT_CHECKOUT owns
Windows/public product source; PRIVATE_LINUX_CHECKOUT owns the private derived
Linux source, build and package; APPROVED_DEPLOYMENT_TOOLING_ROOT supplies only
the established production deployment tooling. Exact private role bindings stay
in private governance, never in public commits.

Never silently edit product/runtime source in another checkout. The deployment
tooling checkout is not product authority. Linux adaptations, infrastructure,
history, configuration, credentials and protected GameData remain private.

Explicit deployment authorization itself is sufficient authorization to use
APPROVED_DEPLOYMENT_TOOLING_ROOT; no additional checkout-root waiver is required.
It permits reading the launcher and required supporting deployment scripts and
manifests, executing the approved deployment workflow, and using the existing
production SSH target and credentials already authorized for that deployment.
Do not ask again for a tooling-checkout waiver or an already-authorized connection.

This read/execute exception is limited to the approved tooling directory and
required supporting files that resolve within it. It grants no access through
parent traversal, sibling directories or symlink escapes. It does not permit
product/runtime edits, product rebuilds, unrelated file changes, or treating the
tooling checkout as product authority. Tooling edits require their own task
authorization. Existing workflow approval and private publication rules remain
in force; this exception does not revive retired deployment workflows.

Without explicit deployment authorization, this exception grants no deployment
tooling access or production connection use. Build/package approval, historical
deployment approval and mere possession of credentials are insufficient. A
current instruction not to deploy takes precedence. Separately authorized
governance/tooling maintenance does not authorize a deployment.

The following access contract governs only this deployment exception:

| Action | EXPLICIT_DEPLOYMENT_AUTHORIZATION | NO_DEPLOYMENT_AUTHORIZATION |
| --- | --- | --- |
| READ_LAUNCHER | ALLOW | DENY |
| READ_REQUIRED_SUPPORTING_DEPLOYMENT_FILES | ALLOW | DENY |
| EXECUTE_APPROVED_DEPLOYMENT_WORKFLOW | ALLOW | DENY |
| USE_EXISTING_AUTHORIZED_PRODUCTION_SSH | ALLOW | DENY |
| READ_OUTSIDE_APPROVED_TOOLING_ROOT | DENY | DENY |
| EDIT_PRODUCT_RUNTIME_SOURCE_IN_TOOLING_CHECKOUT | DENY | DENY |
| REBUILD_PRODUCT_SOURCE_IN_TOOLING_CHECKOUT | DENY | DENY |
| MODIFY_UNRELATED_FILES_IN_TOOLING_CHECKOUT | DENY | DENY |
| EDIT_DEPLOYMENT_TOOLING_WITHOUT_SEPARATE_TASK_AUTHORIZATION | DENY | DENY |
| TREAT_TOOLING_CHECKOUT_AS_PRODUCT_AUTHORITY | DENY | DENY |
| PUBLISH_PRIVATE_LINUX_MATERIAL_PUBLICLY | DENY | DENY |
<!-- deployment-tooling-access:end -->

Validate this governance contract with
Tools/tests/test_deployment_tooling_governance.py; it reads policy only and never
executes deployment tooling or contacts production.

## Established build and deployment references

Use [WORKFLOW.md](docs/ai/WORKFLOW.md) for the current Windows commands:
`tools\windows_release_contract.cmd`, `tools\build_aorebirth_debug.cmd`,
`preflight-database.cmd`, `start-engines.cmd`, `status-engines.cmd` and
`stop-engines.cmd`, invoked through `cmd /d /c`. Release startup uses
`start-engines.cmd -ReleaseBuildAttempt` with the measured build receipt.

Before requesting SSH information, inspect the existing private governance,
its deployment-connection instructions and the recorded successful invocation.
Those private instructions bind `PRIVATE_LINUX_CHECKOUT`,
`APPROVED_DEPLOYMENT_TOOLING_ROOT`, the saved connection and current launchers.
Keep their exact locations and connection details out of public commits.

After Mike's exact-input Windows acceptance, use the existing private online
builder and normal deployment operator documented there. Preserve private
GameData and settings, take the required actual-database backup, apply only
required approved migrations, and retain the established rollback material.
Verify required engine health, World registration, source provenance and the
complete Windows/Linux/live GameData manifest chain. In PATCH MODE, rebuild and
restart only the impacted dependency closure; reuse verified unchanged outputs
and evidence. The release contract above governs
acceptance, authorization, verification and rollback; these references add no
alternate build or deployment path.

Generated mission chest content is private, editable GameData:
`Loot/MissionChestRKAllowlist.json`, `Loot/MissionChestSeasonalExclusions.json`
and `Loot/MissionChestSpecialExclusions.json`. Approvals describe positively
reviewed ordinary Rubi-Ka identities, families and QL ranges; exact historical
chest-drop evidence is not required. Missing expansion requirements alone are
not approval. Seasonal and special-purpose exclusions always override approval,
independent of event enablement. Apply these policies only to generated mission
chests; preserve designated seasonal/NPC loot sources and player-owned items.
Never publish private policy records or replace them with public placeholders
when building or deploying. The complete accepted policies belong in the same
Windows/Linux/live content manifest chain.

If CMD cannot resolve installed build tools, inspect the existing SDK/tool
locations and inherited PATH before declaring an installation missing. Correct
only the execution process's PATH when necessary, then invoke the established
wrapper unchanged. Do not change global PATH, create substitute build tokens,
copy acceptance state between checkouts or bypass the public-source checks.

## Existing architecture and scope

Before introducing an abstraction, replacement system, build or configuration
mechanism, staging mechanism, service, wrapper, compatibility layer, or
architecture change:

- inspect the existing implementation;
- identify the existing configuration, runtime, and build mechanisms;
- prove those mechanisms cannot satisfy the requested requirement;
- report that evidence before redesigning anything.

Use this order of preference:

`existing mechanism > configuration > smallest targeted fix > new architecture`

A configuration problem is not authorization for an architecture change. A
failing validation or test is not authorization to modify production code.

Perform only the requested task. Do not perform unrelated cleanup,
refactoring, modernization, architecture redesign, documentation expansion,
test repair, or build-system redesign. Do not turn a narrow task into a
generalized framework.

## AORebirth validation and live acceptance

Do not automatically run fixture, unit, or regression test suites. Run them
only when Mike explicitly requests them.

For normal AORebirth development:

`inspect -> smallest targeted change -> compile/build -> live AO client verification`

Compilation and build validation remain appropriate when needed to produce a
runnable build. Mike performs live AO client verification. Never launch or
control the AO client unless Mike explicitly requests it in the current task.

Do not modify production behavior merely to satisfy stale fixtures, snapshots,
stored hashes, generated inventories, generated reports, or unit-test
expectations. Treat those failures as diagnostic information unless the task
explicitly establishes the test or fixture as an authoritative contract.

## Gameplay content boundary

AORebirth preserves retail Anarchy Online. Runtime C# owns mechanics, protocol,
validation and execution. Select NPCs, items, loot, objectives, rewards,
generation profiles and tunable policy from validated editable GameData.
Resolve references before use. Missing, invalid or unsupported definitions fail
explicitly. Keep incomplete evidence coverage marked provisional, with its
specific gap recorded alongside the affected policy.

Give gameplay components independent responsibilities and typed contracts.
Use existing item, persistence, combat and network services. Centralize
immutable AO protocol identifiers in typed protocol definitions. Historical
compatibility requires versioned scope, exact evidence and regression tests.
Keep recorded packet payloads in offline evidence and test fixtures. Runtime
offer generation uses validated typed definitions and reusable eligibility data.

The [runtime data architecture check](docs/project/ARCHITECTURE.md#data-oriented-gameplay)
runs against proposed changes in CI. A new violation blocks validation even when
compilation succeeds. Inherited findings stay visible as open remediation work.

## Development authority

- Public GitHub `master` is the authoritative source.
- Windows/public master is developed and accepted first.
- Linux is a derived build of the same accepted source.
- Linux-specific changes are limited to actual Linux operating-system, build,
  and deployment requirements.
- Do not make Linux-only gameplay/runtime changes or introduce gameplay/runtime
  changes directly on the Linux deployment.
- `ZoneEngine_New` is the active zone runtime. Legacy `ZoneEngine` is retired;
  historical references do not make it active.

## LEGACY CUTOVER WORKFLOWS ARE RETIRED

The historical ZoneEngine -> ZoneEngine_New cutover is COMPLETE and RETIRED.

`ZoneEngine_New` is the current authoritative zone runtime.

Public GitHub `master` is the current product.

Do NOT treat normal Linux porting or deployment as an engine cutover.

Do NOT resurrect, infer, require, or preserve historical cutover machinery merely because it appears in:

* old reports
* old deployment receipts
* old scripts
* historical branches
* comments
* archived docs
* previous acceptance artifacts
* migration-era tooling
* legacy terminology

Historical cutover artifacts are evidence only.

They are NOT current workflow authority.

## CURRENT LINUX WORKFLOW

The normal Linux workflow, after Mike explicitly accepts and freezes the
Windows release inputs, is:

`accepted public-master SHA + accepted canonical Windows GameData -> port/reconcile to Linux -> online build -> validate -> package -> push to private Linux Git`

That is the Linux engineering workflow.

Do not mix production deployment concerns into Linux source-port work unless Mike explicitly asks to deploy.

## NORMAL LIVE DEPLOYMENT

When Mike explicitly authorizes deployment, treat it as a NORMAL software-version deployment.

Normal deployment means:

1. verify the approved package;
2. stop the required live services;
3. take a current backup of the actual live database if schema changes are involved;
4. apply the migrations required by the accepted public-master release SHA;
5. install the approved online-built Linux package and every accepted GameData
   addition, modification and removal;
6. start services;
7. verify startup, logs, listeners, provenance and equality of live, Linux and
   accepted Windows GameData manifests;
8. Mike performs live AO client acceptance.

Do NOT automatically introduce:

* prepared-schema-cutover workflows
* engine-transition workflows
* legacy/new-engine dual-running assumptions
* special cutover manifests
* previous-release pin systems
* old cutover-specific migration gates
* historical cutover session policing
* old ZoneEngine compatibility checks
* release-transition frameworks
* extra rollback architecture beyond the established normal deployment/backup mechanism

unless Mike explicitly requests that specific mechanism.

## DATABASE MIGRATIONS

Database migrations in public master are ordinary versioned schema changes.

Treat them as:

`current live schema -> migrations required by the accepted public-master release SHA`

Do NOT reinterpret ordinary migrations as evidence that an engine cutover is occurring.

If the accepted public-master release requires migrations:

* identify the outstanding migrations;
* back up the actual live database;
* apply them in their defined order;
* validate the resulting schema;
* continue deployment.

Do not build a new migration framework around them.

## PUBLIC MASTER IS THE PRODUCT

Do not use historical live SHA, historical private Linux SHA, or old release SHA as architectural authority.

They matter only for:

* understanding current live state;
* rollback/recovery;
* deployment delta.

They do NOT define what Linux should become.

Linux must consume the exact public-master SHA explicitly accepted by Mike for
the release, plus only required allowlisted Linux adaptations. A newer master
tip does not silently replace frozen accepted inputs.

## NO WORKFLOW ARCHAEOLOGY

Do not search old reports, historical branches, old receipts, or archived deployment documents to invent requirements for the current workflow.

Only inspect historical material when:

* Mike explicitly asks;
* a current failure cannot be explained from current source/workflow;
* or exact provenance is required.

Current authoritative files and current `AGENTS.md` override historical workflow descriptions.

If old documentation conflicts with this rule, the old documentation is stale.

## SIMPLEST CURRENT WORKFLOW WINS

For Linux porting:

`fetch master -> port -> build -> validate -> package -> private Git`

For deployment:

`backup if needed -> migrations if needed -> deploy -> start -> verify`

Do not expand these into a larger framework without proving the current workflow cannot satisfy the requirement.

## STOP INVENTING GATES

A gate is valid only if it comes from:

* current authoritative source;
* current `AGENTS.md`;
* current documented build/deployment workflow;
* or Mike's explicit instruction.

Do not invent gates from historical process residue.

When an unexpected condition appears, first ask:

`Is this actually required by current master/current Linux deployment?`

If NO:

* record it if useful;
* do not block the task.

## CURRENT ROLE SPLIT

Windows developers:

* own product/runtime development on public master;
* own ordinary Windows defects and architecture cleanup.

Linux workstream:

* ports the Windows-accepted public-master release faithfully;
* fixes Linux-specific build/package/deployment problems;
* deploys the resulting Linux package when authorized.

Linux work does not take ownership of unrelated Windows cleanup.

## REQUIRED BEHAVIOR

If an old workflow/tool tries to force a retired cutover assumption:

STOP USING THAT OLD WORKFLOW.

Do not redesign current source to satisfy it.

Either:

* use the current normal workflow;
* or update the private Linux tooling so it reflects the current normal workflow without changing product semantics.

## AUTHORITATIVE SOURCE

Public GitHub `master` is the authoritative AORebirth product/runtime source.

Linux is a DERIVED BUILD of public master.

Linux is NOT an independent gameplay/runtime branch.

Any gameplay, protocol, persistence, content, NPC, mission, combat, item, nano, quest, shop, world, character, database behavior, or other product change must originate in Windows/public master first.

Never implement a product/runtime fix directly in Linux because the Windows implementation does not compile or behave correctly on Linux.

## RESPONSIBILITY SPLIT

AORebirth development responsibilities have changed.

The Linux workstream is now STRICTLY responsible for:

`public GitHub master -> Linux port -> build -> validate -> package -> private Linux Git`

Normal Linux deployment is a separate step, only when Mike explicitly authorizes it.

Delmus and other Windows developers are responsible for normal Windows/public-master product development.

The Linux workstream must NOT take ownership of ordinary Windows development issues.

Public GitHub `master` remains the authoritative product/runtime source.

Linux consumes and ports that source.

## NO ROUTINE WINDOWS WORK

During Linux-port work, do NOT stop and switch to Windows development merely because public master contains:

* stale tests;
* stale fixtures;
* stale generated contracts;
* architecture-guard findings;
* code-style findings;
* content-policy findings;
* known baseline test failures;
* incomplete Windows cleanup;
* historical technical debt;
* non-Linux regressions already present in public master.

Record them as public-master baseline findings and continue Linux work when they do not prevent a faithful Linux port.

Do not repair those issues on public master as part of normal Linux work.

The Windows developers own them.

## LINUX ACCEPTANCE BASELINE RULE

Linux validation must distinguish:

`PUBLIC_BASELINE_FAILURE`

from:

`LINUX_PORT_FAILURE`

A failure that reproduces on the exact authoritative public-master source and is not caused by a Linux adaptation is a:

`PUBLIC_BASELINE_FAILURE`

It does NOT automatically block the Linux port.

Linux acceptance may proceed with documented public baseline failures when:

1. the same defect/failure is proven on exact public master;
2. the Linux port did not introduce or worsen it;
3. the failure is unrelated to Linux packaging/infrastructure correctness;
4. runtime semantics remain faithful to public master;
5. the acceptance receipt records the failure explicitly.

Never hide, suppress, delete, or misreport baseline failures.

Use explicit results such as:

`PASS_WITH_PROVEN_PUBLIC_BASELINE_EXCEPTIONS`

Do not record plain `PASS` when failures occurred.

## ZERO NEW LINUX FAILURES

The primary Linux acceptance requirement is:

`NEW_LINUX_FAILURES=0`

Any failure introduced by:

* Linux project wiring;
* Linux-specific source adaptation;
* packaging;
* filesystem handling;
* path/case handling;
* native dependencies;
* service/systemd integration;
* Linux configuration;
* private Linux tooling;

must be investigated and resolved before deployment.

Do not classify a failure as baseline without proving it against exact public master.

## ARCHITECTURE / CONTENT GUARDS

An architecture, content, or policy guard failure inherited unchanged from authoritative public master is not automatically a Linux blocker.

Example:

If public master contains a hard-coded-content finding and the Linux port contains the exact same runtime source bytes, classify it as:

`PUBLIC_BASELINE_ARCHITECTURE_FINDING`

Record it and continue Linux work unless it prevents Linux compilation, package construction, startup, or faithful execution.

Do NOT modify the guard merely to make Linux pass.

Do NOT modify public Windows runtime merely because Linux encountered the inherited finding.

Windows developers own the public-side remediation.

## WHEN LINUX MAY TOUCH PUBLIC WINDOWS CODE

The Linux workstream should change public Windows/master only for an ABSOLUTE BLOCKER.

An absolute blocker means a proven defect in authoritative public master that:

* prevents the product from compiling at all in its authoritative form; or
* prevents Linux from faithfully implementing the same runtime semantics without introducing Linux-only behavior; or
* causes a critical product/runtime failure that makes the Linux server fundamentally unusable; and
* cannot correctly be handled as a Linux port/build/package adaptation.

Before touching public master from the Linux workstream:

1. prove the defect exists in public master;
2. prove it is an absolute blocker;
3. prove a Linux-only workaround would create semantic divergence;
4. keep the public fix as small and platform-neutral as possible.

Ordinary failing tests, stale contracts, architecture findings, cleanup opportunities, and technical debt are NOT absolute blockers.

Default action:

`record -> continue Linux port -> leave Windows repair to Windows developers`

## PUBLIC MASTER MOVEMENT

Public master may continue moving while Linux is being ported.

After Mike accepts Windows, retain the exact accepted product/content/manifest
tuple for the release. New public-master or private-content tips belong to the
next release. Record newly observed tips without changing accepted inputs.

Only Mike's explicit re-reconciliation request starts a new Windows
synchronization, full approved content reconciliation, build/startup and user
acceptance cycle. Do not silently advance Linux or reuse the old acceptance
for changed inputs. Freezing release inputs does not freeze Windows development.

## LINUX PORT AUTHORITY

The Linux team may modify the PRIVATE Linux repository for:

* Linux compilation/project wiring;
* build tooling;
* source inventories;
* Linux-specific dependency wiring;
* filesystem/path/case differences;
* native-library support;
* packaging;
* service/systemd integration;
* deployment tooling;
* private acceptance tooling;
* provenance and identity checks.

Those adaptations must preserve authoritative public-master behavior.

## FORBIDDEN LINUX PRODUCT DRIFT

Do not introduce Linux-only changes to:

* gameplay;
* protocol semantics;
* persistence behavior;
* DAO semantics;
* quests;
* NPC behavior;
* combat;
* nanos;
* items;
* shops;
* world/spawns;
* character behavior;
* balancing;
* content;
* authentication semantics.

If Linux behavior would differ from public master, stop and classify the issue.

## PRIVATE LINUX GIT FLOW

Required workflow:

freshly synchronized public Windows/master and canonical private content
→ Windows build/startup readiness
→ Mike explicitly accepts the exact inputs
→ freeze source SHA, dump SHA and Windows GameData manifest
→ port/reconcile to Linux
→ build Linux through the private GitHub Actions online builder
→ validate Linux
→ package/validate
→ push Linux result to private Linux Git

Stop at private Linux Git unless Mike explicitly authorizes normal deployment.

Public Windows/master is the input.

Private Linux Git is the Linux output/history.

Never push private Linux port history to public GitHub.

## CURRENT QUESTJOURNAL FINDING

The current inherited `QuestJournal.cs` architecture-guard finding involving:

`NpcHashType = (IdentityType)0x000111D3`

is NOT part of the Linux workstream unless separately proven to be an absolute runtime blocker.

If the Linux runtime source matches public master, classify this current finding as:

`PUBLIC_BASELINE_ARCHITECTURE_FINDING`

Record it and continue Linux reconciliation/acceptance.

Do not change QuestJournal or the architecture guard during Linux porting for this finding.

## ACCEPTANCE REPORTING

Linux reports must clearly separate:

PUBLIC_BASELINE_FAILURES
PUBLIC_BASELINE_ARCHITECTURE_FINDINGS
LINUX_PORT_FAILURES
NEW_LINUX_FAILURES

Deployment readiness requires:

`NEW_LINUX_FAILURES=0`

and no unresolved Linux-port-specific blocker.

It does NOT require the Linux workstream to repair every pre-existing Windows/public-master defect.

## LINUX-SPECIFIC CHANGES

Linux-specific changes are permitted ONLY when strictly necessary for:

* Linux compilation
* OS/platform APIs
* filesystem/path handling
* case sensitivity
* Linux packaging
* service/systemd integration
* deployment scripts
* Linux configuration/environment wiring
* native-library/platform dependencies
* equivalent Linux infrastructure required to run the SAME public-master behavior

Linux-specific code must preserve public-master semantics.

A Linux port adaptation must not become an alternate implementation of gameplay behavior.

## FORBIDDEN LINUX DRIFT

Do NOT introduce Linux-only:

* gameplay fixes
* gameplay defaults
* content
* NPC behavior
* mission behavior
* combat behavior
* item behavior
* nano behavior
* persistence semantics
* DAO semantics
* protocol behavior
* authentication behavior
* world/spawn behavior
* shop behavior
* quest/dialogue behavior
* balancing
* compatibility fallbacks that change gameplay
* hard-coded game content

If public master has an inherited defect, apply the LINUX ACCEPTANCE BASELINE RULE.
Record proven public baseline findings and continue a faithful Linux port. Only
an ABSOLUTE BLOCKER under WHEN LINUX MAY TOUCH PUBLIC WINDOWS CODE justifies
public-side remediation by the Linux workstream. Never introduce Linux-only
product fixes.

## RECONCILIATION RULE

Before every Linux build:

1. Validate Mike's explicit Windows acceptance and the immutable release inputs.
2. Check the private content upstream and record the observed tip without
   replacing the frozen accepted dump SHA or canonical data.
3. Record the accepted authoritative public SHA and compare the Linux source/tree
   against it; require the accepted Windows GameData manifest unchanged.
4. Automatically derive the complete difference inventory. Reuse verified
   unchanged adaptation classifications; manually review changed or unexplained
   differences rather than repeating the whole release investigation.
5. Classify each changed or unexplained difference as:

VALID_LINUX_PORT
VALID_LINUX_BUILD
VALID_LINUX_PACKAGING
VALID_LINUX_SERVICE
VALID_LINUX_CONFIGURATION
STALE_LINUX_CHANGE
INVALID_RUNTIME_DRIFT
REQUIRES_REVIEW

6. Remove/reconcile stale or invalid drift before treating the Linux build as valid.

Never assume an existing Linux-private change is legitimate merely because it already exists.

## WINDOWS TO LINUX PIPELINE

Public Windows/master remains the only authoritative product/runtime source.
Every Linux port starts from the exact public-master SHA explicitly accepted
by Mike together with the canonical Windows GameData.

Required sequence:

fetch public master and private content upstream
→ synchronize the Windows checkout to the exact current public-master SHA
→ reconcile all approved content additions/modifications/removals
→ deterministic Windows GameData manifest
→ Windows build/startup readiness
→ stop for Mike's explicit Windows acceptance
→ freeze the accepted source SHA, dump SHA and GameData manifest
→ reconcile/port that source to Linux
→ supply the same accepted canonical GameData
→ online Linux build and required target-platform validation
→ Linux package construction/validation
→ push validated Linux port commits to PRIVATE Linux Git

Never reverse this sequence. Stop here unless Mike explicitly authorizes normal deployment under NORMAL LIVE DEPLOYMENT.

The Linux private repository must preserve traceability to the exact public-master SHA it was derived from.
Linux/package and authorized live deployment must preserve the accepted Windows
GameData manifest exactly; an accepted content removal is part of the release.

## EXACT-SHA REQUIREMENT

Every Linux build/report must identify:

PUBLIC_MASTER_SHA
LINUX_SOURCE_SHA
PACKAGE_SOURCE_SHA

The Linux candidate must be traceable to the exact public-master SHA plus explicitly documented Linux-only adaptations.

Do not describe a Linux candidate as synchronized when those identities are unknown.

## GAMEDATA / CONTENT

Game content must remain data-driven.

Do not solve missing Linux content by hard-coding it into C#.

Private/protected GameData may be required for runtime/package operation, but it must not be committed or published merely to make a Linux build pass.

Missing private runtime data is a packaging/deployment problem, not permission to invent replacement content.

## CAPTURE DATA

Historical capture/evidence data is research input only.

Runtime must not consume historical capture directories or packet corpora directly.

Do not copy `D:\AORebirthCaptures` or equivalent capture stores into Linux runtime/package data.

Validated permanent content derived offline from captures may be legitimate GameData.

## PAUSED/UNMERGED BRANCHES

Never include work from an unmerged feature branch in Linux merely because it is locally available.

Only authoritative public master is eligible unless Mike explicitly authorizes a specific exception.

The currently paused mission typed-content branch must NOT be included in Linux until it is merged to public master.

Keep `operations` history separate; do not merge it into the Linux port.

## TERMINOLOGY

Use "Linux" for the AORebirth Linux backend/build.

Do not substitute "Unix" when referring to this project.

## STOP CONDITIONS

STOP and report rather than improvising when:

* Linux requires a gameplay/runtime semantic change
* an ABSOLUTE BLOCKER is proven under WHEN LINUX MAY TOUCH PUBLIC WINDOWS CODE
* Linux differs from public master for an unexplained reason
* required private GameData is missing
* a Linux-only workaround would change behavior
* authoritative SHA cannot be established
* reconciliation would discard unexplained work
* an unmerged branch appears necessary

Do not work around these conditions silently.

## PRIVATE LINUX SETUP AND PUBLICATION BOUNDARY

The AORebirth Linux setup is PRIVATE and must remain private.

Public GitHub `master` is authoritative for AORebirth product/runtime behavior, but that does NOT make the Linux infrastructure public.

The required relationship is:

`fetch public master/private content -> synchronize/reconcile Windows -> Windows build/startup -> Mike explicitly accepts -> freeze exact inputs -> port/reconcile to Linux -> online build -> validate -> package/validate -> push Linux result to PRIVATE Linux Git`

Production deployment is separate and requires Mike's explicit authorization.

Rules:

- The private Linux repository/checkout must remain private.
- Linux-specific adaptations must remain private unless Mike explicitly authorizes otherwise.
- Linux build scripts, packaging details, deployment files, service/systemd files, environment/configuration files, server details, connection details, internal infrastructure, private GameData, build artifacts, acceptance artifacts, deployment receipts, and private Git history must remain private.
- Never push, mirror, merge, publish, or copy the private Linux repository or its Git history into the public AORebirth repository.
- Never publish private Linux infrastructure in public commits, pull requests, issues, logs, reports, or artifacts.
- Existing tracked `LinuxBuild` files already present in public master do NOT authorize publishing additional material from the private Linux setup.
- Public-master gameplay/runtime fixes must still be implemented and accepted on public master first.
- The private Linux checkout then incorporates those authoritative public-master changes.
- Do not implement gameplay/runtime fixes only inside the private Linux repository.
- Read-only inspection of the existing private Linux checkout and its configured remotes is allowed when required for an authorized reconciliation/audit.
- Do not change Linux remotes, remote URLs, remote ownership, or repository visibility during reconciliation unless Mike explicitly authorizes that exact action.
- Do not create a new public Linux repository as a substitute for the existing private setup.
- Do not invent or guess a private remote or connection.
- Do not publish private Linux files merely because the public checkout lacks an equivalent file.
- Private/protected GameData must never be committed or published to public GitHub.
- Capture/evidence stores must never be copied into the public repository or Linux runtime merely for convenience.

## PRIVATE LINUX GIT ROLE

The private Linux Git repository exists to store:

- the Linux port of authoritative public-master source
- legitimate Linux compilation adaptations
- Linux OS/platform adaptations
- filesystem/case-sensitivity adaptations
- Linux packaging
- service/systemd integration
- deployment tooling
- Linux configuration/environment wiring
- native-library/platform adaptations
- private Linux build/acceptance history

It must remain private.

It may later be shared with authorized Linux developers without making the Linux infrastructure public.

It is NOT the authority for independent gameplay/runtime development.

## PRODUCT FIXES DISCOVERED DURING LINUX PORTING

If Linux porting reveals a gameplay, runtime, protocol, persistence, DAO, content, combat, NPC, mission, item, nano, world, authentication, or other product defect:

Do not implement a Linux-only product fix. Classify and record the finding under
the LINUX ACCEPTANCE BASELINE RULE. Ordinary public-side remediation belongs to
Delmus and the Windows developers; continue Linux work when a faithful port is
possible.

Only a proven ABSOLUTE BLOCKER permits the Linux workstream to make the smallest
platform-neutral public fix, following WHEN LINUX MAY TOUCH PUBLIC WINDOWS CODE.
Any product fix must still be accepted on public Windows/master first.

After the Windows developers or the authorized absolute-blocker work complete it:

public master fix/validation/push
→ fetch/synchronize the new exact public-master SHA and reconcile private content
→ Windows build/startup and Mike's explicit acceptance of the new inputs
→ freeze the accepted source/dump/manifest tuple
→ reconcile/port that accepted SHA and canonical data to Linux
→ online build
→ validate
→ package
→ push resulting Linux port to private Linux Git

Never solve product defects only inside private Linux Git.

## REMOTE DIRECTION

The intended logical relationship is:

PUBLIC WINDOWS REMOTE = authoritative source/input
PRIVATE LINUX REMOTE = Linux port destination/output

Never push Linux-port branches, private Linux infrastructure, or Linux-private history to the public AORebirth repository.

Once the private Linux repository is established, the preferred checkout topology is:

- `public` -> public `subarumike/AORebirth` source remote
- `origin` -> private AORebirth Linux Git destination

Do not assume or configure those remote names until the private repository actually exists and is verified.

## PRIVATE LINUX PUSH RULE

For an authorized Linux port/build pipeline, pushing the validated Linux result to the verified private Linux Git repository is an expected pipeline step.

Before any private Linux push:

1. identify the exact destination remote;
2. verify the destination is private;
3. verify it is the intended AORebirth private Linux repository;
4. record the public Windows/master SHA from which the Linux result was derived;
5. verify the task authorizes continuing through the Linux push stage.

If the destination is public, ambiguous, missing, or unverified:

STOP.

Never substitute the public AORebirth repository for the private Linux destination.

## PRIVACY

Keep private:

- Linux-specific private repository/history
- Linux infrastructure
- build/deployment internals
- server/connection details
- service configuration
- protected/private GameData
- credentials
- internal acceptance/deployment receipts
- private build artifacts

Existing public `LinuxBuild` files do not authorize publishing additional private Linux material.

### STOP CONDITIONS

STOP and report if:

- the existing private Linux checkout cannot be identified;
- the private remote cannot be identified;
- destination privacy cannot be verified;
- a workflow would expose private Linux infrastructure publicly;
- reconciliation appears to require publishing private history;
- a public-master fix would require importing private Linux implementation into public source;
- Linux deployment credentials, host details, or other private infrastructure would be exposed.

Do not work around these conditions silently.

## GameData authority

- The configured `PRIVATE_CONTENT_UPSTREAM` is the upstream authority for
  private GameData/content changes. Its exact private binding stays private.
- `D:\AORebirth-fresh\GameData` is the canonical private Windows integration
  dataset derived from that upstream and legitimate coexisting private/generated
  content. It is not an independent upstream authority.
- Fetch/check the upstream on every build/release attempt and record
  `CONTENT_DUMP_SHA`. Before Windows build, compare the current upstream content
  against its recorded canonical basis and reconcile all approved additions,
  modifications and removals. Preserve required canonical repairs and generated
  content; fail closed on unexplained conflicts or unreconciled changes.
- After Mike accepts Windows, freeze the exact source/dump/manifest tuple.
  New upstream tips are next-release inputs unless Mike explicitly requests
  re-reconciliation and accepts the new Windows build.
- `AORebirth\GameData` is the public GitHub distributable/placeholder dataset.
- Differences between private and public catalogs are intentional and are not
  automatically drift.
- Existing runtime configuration `AO_REBIRTH_GAMEDATA_PATH` selects
  external/private GameData.
- Do not redesign GameData staging or selection when that existing mechanism
  satisfies the requirement.
- Never commit private GameData to public GitHub.
- Supply Linux with the exact Windows-accepted canonical dataset. No separate
  Linux reconstruction, stale replacement or partial accepted content delta is
  permitted; Windows/Linux/live manifests must match.
- An upstream checkout is input to reconciliation, not the selected runtime
  GameData root.

- This agent works only on AORebirth. Never work on AO Rebuild or AO stripdown, and never switch to either workspace.
- Read `AI_START_HERE.md` first.
- Ground work in repository files.
- Never rely on old chat history.
- Run `git status --short --branch` before editing.
- After creating a commit, push it to the configured remote; do not leave commits local-only unless Mike explicitly asks for a local-only commit.
- After committing and pushing, include a small Discord-ready summary Mike can post.
- Do not guess packet behavior.
- Do not change database schemas without explicit approval.
- Do not perform destructive database operations.
- Use documented workflow commands first for known workflows; do not rediscover known build, engine, capture, or validation commands.
- Do not improvise shell syntax. Use repository-approved command forms and wrappers; malformed command syntax is an agent workflow violation and must be prevented, not merely corrected after failure.
- Agents must not run probe commands, line-count probes, empty-pattern commands, placeholder commands, or shell-syntax experiments unless Mike explicitly requests that investigation. Required file inspection must use known-good targeted read commands only. A malformed probe command is an agent workflow violation even if it causes no repo change. Reporting the bad command afterward is not enough; prevention is required.
- Malformed search, find, rg, grep, dir, or line-count commands are agent execution errors, not project blockers.
- For Windows/cmd searches, prefer shell-safe `rg` forms with repeated `-e` patterns over complex quoted regex strings, especially when paths contain spaces.
- Protect the context window: avoid command spam, large logs, repeated searches, and noisy transcripts.
- Never launch the AO game/client unless Mike explicitly instructs it in the current task.
- If this task starts or restarts ZoneEngine_New, LoginEngine, ChatEngine, or
  WebEngine, run `cmd /d /c stop-engines.cmd` before the final reply unless Mike
  explicitly asks to leave the engines running.
- `ACTIVE_CHECKOUT_WINS`: before building, starting, or restarting the governed
  LoginEngine, ChatEngine, or ZoneEngine_New set, automatically identify and
  stop positively identified AORebirth backend engines from every local
  AORebirth checkout. Do not ask Mike merely because an identified engine came
  from another checkout. Positive identification requires the exact engine name
  and its canonical `AORebirth\Built\Debug` executable suffix, from which the
  owning checkout is derived. Request graceful shutdown through that checkout
  first; force only that confirmed PID after the bounded shutdown timeout, then
  wait for process exit and port release. Never kill by process name or port
  alone. If an expected port owner cannot be positively identified as one of
  these AORebirth engines, stop the workflow and report it. Never leave backend
  engines from two AORebirth checkouts running simultaneously.
- For AOSharp live capture startup, use only the approved `cmd.exe` wrapper documented in `docs/ai/WORKFLOW.md`.
- For mission-terminal / mission-lifecycle capture **analyze and implement**,
  use the repository-relative analyzer build/run commands documented in
  `docs/ai/WORKFLOW.md`. From the repository root, the analyzer command is
  `cmd /d /c tools-temp\AOSharpMissionCaptureAnalyzer\bin\Debug\AOSharpMissionCaptureAnalyzer.exe "<capture-folder>"`.
  If the executable is absent, use the documented repository-relative MSBuild
  command first. Ground implementation in `mission-flow.replay.log`. Do not use
  a user-profile absolute path or start with ad-hoc log searches.
- Report files inspected.
- Report files changed.
- Report validation performed.
- Keep `docs/ai/CURRENT_TASK.md` focused on active work only.
- Keep `docs/project/PROJECT_STATE.md` updated when stable project status changes.

## ACTIVE WORKFLOW DISCIPLINE (MANDATORY)

- When an approved workflow, wrapper, launcher, startup command, capture command, build command, validation command, or investigation workflow already exists and has previously succeeded, use it immediately.
- Do not rediscover workflows.
- Do not revalidate workflows.
- Do not search the repository for workflows that are already documented.
- Do not inspect workflow source code unless the workflow itself is being modified
  or the authorized deployment-tooling access exception above applies.
- Assume approved workflows remain valid until they fail.

For capture-backed tasks:

- When Mike provides an AOSharpLiveCapture capture directory or path, treat that as confirmation that the capture is complete and closed. Analyze it immediately; do not ask whether it is closed or finalized unless Mike explicitly says the capture is still running.
- When a workflow document explicitly identifies the approved command, wrapper, launcher, or script, use that command directly. Do not perform any repository search to verify, locate, confirm, inspect, or rediscover it.
- When Mike explicitly requests a new capture, launch the approved AOSharp
  capture workflow immediately. Mike performs the live gameplay action unless
  he explicitly instructs the agent otherwise in the current task. Stop and
  analyze that capture through the approved workflow.
- When Mike provides a completed capture path, analyze it immediately; do not
  launch or stop another capture.
- For an explicit new-capture task, the first operational command should
  normally be the approved capture launcher.
- If the task explicitly says "perform a capture", "start capture", or "run capture",
  the approved capture launcher should be the first task-related command executed.

Do not spend command budget on these unless the approved workflow has already failed:

- `rg`
- `grep`
- `findstr`
- `dir`
- `tree`
- `Get-ChildItem`
- repository-wide searches
- workflow discovery
- workflow archaeology
- documentation archaeology
- source-code archaeology

Active task discipline:

- The active task is defined only in `docs/ai/CURRENT_TASK.md`.
- Stay within that task and the user's current request.
- Do not classify, alter, or propose work on unrelated dirty files.

Failure exception:

- If the approved workflow fails, explain the failure, investigate only the failure, and then immediately return to the active task.

## SMALL TASK DISCIPLINE (MANDATORY)

For small docs-only or workflow-rule edits, use the smallest sufficient action. If the prompt names the exact target files, do not rediscover them; inspect only the named files and the smallest relevant sections.

For small scoped tasks:

- Use at most three pre-edit commands.
- Use at most two validation commands.
- Use at most one final status command.
- Do not search memory files, generated docs, project state, or the broader repo.
- Do not inspect source code, build scripts, logs, captures, or unrelated docs.
- Do not build, launch the game, start live capture, start or stop engines, or use PowerShell.

Progress update discipline:

- Use at most one short pre-edit progress line and one compact final response.
- Do not narrate obvious steps such as staging, validation starting, commit starting, push starting, or final status checking unless something fails or user action is needed.

Stop-after-success rule:

- Once the requested change is made, validation passes, and the commit/push is complete, stop. Do not perform extra audits, cleanup, refactoring, project-state edits, generated-doc updates, exploratory searches, or additional verification not requested by the task.

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
