# Workflow

## First Checks

Run:

```cmd
git status --short --branch
```

Identify dirty files before editing. Do not revert user or previous-agent work unless Mike explicitly asks.

## Git Pull Safety (subsystems)

- Commit subsystem work **before** pulling. Uncommitted Mail/Pets/etc. gets dropped by rebase.
- Always merge:

```cmd
git pull --no-rebase origin master
```

- Never `git pull --rebase` when you have local gameplay subsystem commits.
- See `docs/project/SUBSYSTEMS.md`.

## Known Workflow First

- Before exploratory commands, check the project AI docs for the documented workflow.
- For known workflows, use the documented command first.
- If a wrapper exists, use the wrapper.
- Do not bypass wrappers with hand-rolled command chains unless the wrapper itself is broken and the task is specifically to repair it.
- If no wrapper exists for a recurring task, create the smallest practical `cmd.exe` wrapper only when that is within the task scope, then document that wrapper as the approved entrypoint.
- If the documented command is missing, ambiguous, or outdated, stop and report the documentation gap. Do not improvise a discovery session.

## Command Syntax Safety

- Do not improvise shell syntax. Use repository-approved command forms, documented wrappers, and simple shell-safe commands. Malformed command syntax is an agent workflow violation and must be prevented, not merely corrected after failure.
- Agents must not run probe commands, line-count probes, empty-pattern commands, placeholder commands, or shell-syntax experiments unless Mike explicitly requests that investigation. Required file inspection must use known-good targeted read commands only. A malformed probe command is an agent workflow violation even if it causes no repo change. Reporting the bad command afterward is not enough; prevention is required.
- Do not run line-count probes just to prove a file exists or estimate file size.
- Do not run `find`, `findstr`, `rg`, `grep`, `dir`, or similar commands with empty patterns, placeholder arguments, or syntax experiments.
- Required workflow-doc reads must use known-good targeted read commands only.
- If a file needs to be inspected, read the relevant section directly using a known-good command form.
- A malformed probe command is still an agent workflow violation even if it caused no repo change.
- Malformed command syntax is an agent workflow violation and an agent execution error, not a project blocker. Malformed search, find, rg, grep, dir, or line-count commands are agent execution errors, not project blockers. If a command fails because of quoting, shell syntax, escaped characters, regex syntax, or path quoting, immediately rerun the task once with a simpler command form.
- Search commands must be shell-safe. For ripgrep on Windows/cmd workflows, prefer repeated `-e` patterns instead of complex quoted regex strings.
- Good:

```bat
cmd /d /c rg -n -e "PatternOne" -e "PatternTwo" AORebirth\Server
```

- Bad:

```bat
rg -n 'PatternOne|PatternTwo' "AORebirth/Server"
rg -n "(PatternOne|PatternTwo)" "path with nested quoting"
```

- Do not combine fragile quoting with paths containing spaces. Use simpler searches, narrower paths, or multiple safe commands instead.
- Keep output compact. Do not dump full files, full logs, broad recursive output, or noisy command output into chat or the context window. Use targeted searches, line-numbered snippets, and concise summaries.
- If a malformed command happened, final reporting must include the failed command category, the corrected safe command form used, and confirmation that the malformed command did not change repo state. This reporting is required after prevention failed, but it does not excuse the workflow violation. Do not paste a giant output dump.

## Small Task Discipline

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

## Command Budget And Context Protection

- Protect the context window as a project resource.
- For known workflow startup, use at most one command to start the tool.
- Use at most one optional command to verify expected output only if required.
- Use at most one optional command to inspect a targeted failure log only if the start command fails.
- Do not rediscover known commands with repo-wide searches, directory sweeps, process sweeps, tasklist sweeps, repeated log reads, or source-code inspection.
- Prefer exact known paths over discovery commands.
- Prefer targeted file reads over broad searches.
- Do not paste large command output, long transcripts, repeated directory listings, tasklist output, full logs, or broad search results into chat.
- When a command is expected to be noisy, redirect output to a local log file and summarize only the result, exact command, relevant path, or smallest relevant error.

## Command Permissions

- Run shell, Git, build, test, validation, and capture commands normally first.
- PowerShell is disallowed for AORebirth build, launch, validation, and live capture workflows.
- `.ps1` wrappers are deprecated for Codex AORebirth workflows. Use `cmd.exe` or Git Bash.
- Do not set `sandbox_permissions` or use `require_escalated` unless the normal command has already failed with a real OS permission error.
- Before retrying with escalation, stop and report the exact command, working directory, target path, and full error text.
- Do not use admin elevation or machine-wide policy changes for routine repo work.

## Build And Engines

After code changes that affect server binaries:

1. Stop engines if running processes are locking build outputs.
2. Build.
3. Restart Chat, Login, and Zone with the root restart wrapper.
4. Check engine status and expected ports only through the existing quick wrapper output.
5. Do not start WebEngine unless explicitly needed.

Stop engines through an approved `cmd.exe` or Git Bash workflow only. Do not run `stop-engines.ps1` from Codex.

Build:

```cmd
cmd /d /c tools\build_aorebirth_debug.cmd
```

Do not use raw AORebirth MSBuild validation with `/m` or MSBuild node reuse. The `cmd.exe` build wrapper resolves `MSBuild.exe` from the latest installed Visual Studio through `vswhere.exe`, kills stale `MSBuild.exe`, `dotnet.exe`, `VBCSCompiler.exe`, and `NuGet.exe` processes, verifies required packages under `AORebirth\packages`, restores packages explicitly before build only when required package folders are missing, then builds `AORebirth.Core`, `LoginEngine`, `ZoneEngine`, `DatabasePreflight`, and `WebEngine`, using:

```cmd
MSBuild.exe <project> /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
```

Legacy build-time NuGet restore through `.nuget\NuGet.targets` has been removed from project files. If required package folders are missing, the wrapper runs explicit solution restore before build with visible progress and timeout handling:

```cmd
MSBuild.exe AORebirth\AORebirth.sln /t:Restore /p:RestorePackagesConfig=true /m:1 /nr:false /v:minimal
```

Do not reintroduce project-level `RestorePackages` targets or `.nuget\NuGet.targets` imports.

If a Codex shell command times out during build validation, do not treat timeout exit code `124` as a build failure until checking for orphaned build child processes and stopping them.

Start engines, stop engines, and check engine status through approved `cmd.exe`
or Git Bash workflows only. Use the read-only root wrapper below for status:

```cmd
cmd /d /c status-engines.cmd
```

The status wrapper reads configured ports, resolves listener PIDs through
Windows CIM, and correlates each listener with the exact canonical executable
under `AORebirth\Built\Debug`. It fails closed on missing processes or ports,
wrong owners, multiple listener PIDs, split multi-port ownership, duplicate
engine instances, or unavailable executable ownership. Verified mappings are
ChatEngine `6996` and `7012`, LoginEngine `7500`, ZoneEngine `7501`, and optional
WebEngine `8181`. An absent WebEngine with a closed port is healthy; any partial
or conflicting optional-Web state fails.

Useful read-only modes are:

```cmd
cmd /d /c status-engines.cmd --core
cmd /d /c status-engines.cmd --web-required
cmd /d /c status-engines.cmd --prestart WebEngine
```

Before database-dependent startup, run the read-only preflight:

```cmd
cmd /d /c preflight-database.cmd
```

It requires `AO_REBIRTH_MYSQL_CONNECTION` in the current CMD process, opens
through the production MySQL configuration/connector path, requires
`cellao_codex_clean`, verifies all 34 required tables and read access, and fails
if any `characters.Online` value is nonzero. It performs no writes, migrations,
resets, or schema repair.

After a successful rebuild, restart engines with:

```cmd
cmd /d /c restart-engines.cmd
```

`start-engines.cmd` and `restart-engines.cmd` run preflight before launch;
restart runs it before stopping any healthy engine. Startup verifies exact
launched-PID ownership and rolls back only processes launched by that
invocation. Managed shutdown trusts only PID metadata whose executable path and
start time match and never falls back to killing processes by name.

WebEngine remains excluded from normal startup. Its explicit optional workflow
is:

```cmd
cmd /d /c start-web-engine.cmd
cmd /d /c stop-web-engine.cmd
```

Web startup requires database and stopped-state preflights, the built binary,
the complete manifest-bound PHP 8.5.9 runtime, and the final patched WebCore
tree. It performs no PHP or WebCore download. The wrapper validates, in order,
the database, binary, PHP manifest, complete runtime/modules/INI/real-CGI
contract, WebCore base and compatibility/final manifests, complete final tree,
and process/port ownership before launch and launched-PID verification. The
process validates the configured DB fields without connecting, acquires PHP
then WebCore leases, repeats both full validations before synchronously binding
the listener, and retains both leases for its lifetime.

WebCore uses an offline-only operator import. The only accepted upstream pin is
`765c3850767b63af1cd259bab7f2f7ca3e97adf9`; supply its exact ZIP from a local
path and run from a CMD rooted at the repository:

```cmd
cmd /d /c stop-web-engine.cmd
cmd /d /c import-php-runtime.cmd "C:\local-only\php-8.5.9-nts-Win32-vs17-x64.zip" 8.5.9
cmd /d /c import-webcore-assets.cmd "C:\local-only\CellAO-WebCore-765c3850767b63af1cd259bab7f2f7ca3e97adf9.zip" 765c3850767b63af1cd259bab7f2f7ca3e97adf9
cmd /d /c validate-php-runtime.cmd
cmd /d /c validate-webcore-assets.cmd
cmd /d /c validate-webcore-php.cmd
cmd /d /c Tools\run_web_engine_security_tests.cmd
```

WebEngine must be fully stopped for import. The wrapper enforces the exact
WebEngine stopped-state preflight, and the importer holds an exclusive
runtime/import lease through validation and activation. The command does not
acquire the ZIP and has no URL fallback. The pinned
archive SHA-256 is
`ef297e623040b375e64c543568ca94e44ed7cc59de6fe826ed5e42db95c020ab`;
the expected archive root is
`CellAO-WebCore-765c3850767b63af1cd259bab7f2f7ca3e97adf9`. Import validates the
base 7,140-file tree, applies the exact hash-bound compatibility overlay, and
validates the complete final manifest before activation. See
`docs/project/WEBCORE_ASSET_SUPPLY.md` and
`docs/project/PHP_RUNTIME_SUPPLY.md`.

The selected official runtime is PHP 8.5.9 Windows x64 NTS VS17, archive
SHA-256 `516c2d72231bd035c8a910120834add0ad208098b790b4909b2cbeb93ce135fc`.
All 25 patched PHP files must lint under that exact runtime. Unsafe historical
admin/member/authentication/mutation routes are denied by the host policy.
WebEngine remains development-only: no valid MySQL credential was available
for live semantics, transport is plaintext, and upstream licensing is
unresolved.

## Generated combat cohort

The generated combat inventory, runtime catalog, exact-byte fixtures, active
coverage, formula dataset, and generation manifest are one governed cohort.
Never edit a generated member by hand and never invoke a component generator
against its governed output. Use the coordinator wrapper:

```cmd
cmd /d /c Tools\generate_capture_backed_npc_combat_inventory.cmd --check
cmd /d /c Tools\generate_capture_backed_npc_combat_inventory.cmd --write
cmd /d /c Tools\generate_capture_backed_npc_combat_inventory.cmd --validate-current
cmd /d /c Tools\generate_capture_backed_npc_combat_inventory.cmd --validate-legacy-baseline
cmd /d /c Tools\generate_capture_backed_npc_combat_inventory.cmd --audit-scoped-raw-captures --capture-root "<capture-folder>"
cmd /d /c Tools\generate_capture_backed_npc_combat_inventory.cmd --audit-scoped-raw-captures --capture-root "<capture-folder>" --require-promotable-captures
cmd /d /c Tools\generate_capture_backed_npc_combat_inventory.cmd --audit-combat-capture-readiness
cmd /d /c Tools\generate_capture_backed_npc_combat_inventory.cmd --self-test-governance
```

With no argument, the wrapper performs `--check`. `--write` takes the exclusive
generated-artifact lease, captures immutable primary and auxiliary inputs,
builds the complete candidate, converges active coverage and formula data,
validates every byte/hash/count/identity, then publishes all five artifacts and
the manifest as one recoverable transaction. The manifest is the commit marker
and is replaced last. A changed input before publication or before commit aborts
and preserves or restores the prior complete cohort.

`--check` validates the committed accepted generated-combat artifacts without
reading historical raw captures. `--write` regenerates and publishes generated
artifacts from capture evidence. `--validate-current` remains the strict
historical raw validator and must not be weakened to pass when required capture
roots are unavailable. Use `--validate-legacy-baseline` only as a forensic audit
of the immutable legacy cohort; it may run with zero, some, or all historical
raw roots present and reports the observed availability.
Use
`--audit-combat-capture-readiness` before new combat recapture planning; it is a
non-mutating instrumentation readiness report and must distinguish
`CAPTURE_READY`, `ANALYZER_READY`, and `NOT_PROTOCOL_PROVEN` without claiming a
future capture has already proven values. Use
`--audit-scoped-raw-captures` only with explicit capture roots; it validates the
selected roots, requires validator-grade raw files, rejects sentinel combat
fields, reports `RAW_REVALIDATABLE`, `NEW_RAW_VERIFIED`, or
`BLOCKED_INSUFFICIENT_EVIDENCE`, and does not mutate the legacy cohort. Add
`--require-promotable-captures` when a scoped run is intended to gate promotion;
that flag fails closed on any blocked selected combat candidate. The scoped
audit reports all observed cohorts separately from combat promotion candidates
and does not treat social/vendor-only evidence as ordinary enemy combat. See
`docs/evidence/CAPTURE_BACKED_COMBAT_GOVERNANCE_20260819.md`.

Current-cohort validation and the normal server build use the tracked analyzer
source identity and do not require ignored `bin`/`obj` output from another
checkout. Before `--check` or `--write`, build `AOSharpCaptureAnalyzer` with the
documented command in the Captures section; generation requires the executable,
while validation of the checked-in cohort does not.

Supported build, AOtomation, and mandatory-gate readers route themselves through
the shared generated-artifact read lease. Direct active-coverage and formula
reads of governed inputs do the same and validate a live same-checkout delegated
lease. Do not bypass the lease with copied environment variables or a second
checkout.

Run the focused fault, recovery, delegation, timeout, path-independence, and
fixture-contention suite with:

```cmd
cmd /d /c Tools\run_generated_combat_concurrency_tests.cmd
```

Run the complete clean-worktree sequential/concurrent matrix with:

```cmd
cmd /d /c Tools\stress_generated_combat_pipeline.cmd
```

The stress runner performs two real sequential checks under distinct hash seeds,
two simultaneous real checks, and a held-reader/two-writer transactional fixture.
It fails on artifact drift, generation-identity drift, Git drift, timeout, or
lease/staging/transaction residue. See
`docs/project/GENERATED_COMBAT_PIPELINE.md` for the complete contract.

## Mandatory local integration gate

Run the complete deterministic gate from a clean worktree:

```cmd
cmd /d /c tools\run_mandatory_integration_gate.cmd
```

Prerequisites are CMD, Git with Git LFS, Python, the repository package cache or
normal NuGet restore access, and the .NET Framework build toolchain used by the
approved build wrapper. The gate fails closed on a missing prerequisite,
deterministic engine-management contract failure, generated drift, any
AOtomation or playfield acceptance failure, mission drift, LFS failure, build
failure, or final dirty worktree. Mandatory stage 12 covers offline PHP supply,
WebCore compatibility generation, exact manifests, import/rollback leases,
request/CGI hardening, and conditional real-runtime lint when the ignored local
runtime and payload are installed. It runs with outbound proxy variables directed to a denied loopback
endpoint for proxy-aware clients, and a separate source contract bans network
APIs and acquisition commands from the WebCore asset manager. This is a
deterministic no-network-dependency contract, not an OS-level egress sandbox.
Engine-management tests use injected snapshots, fake database sources, and
temporary PHP/WebCore fixtures; they do not start the AO client, capture
tooling, production engines, access a live database, or require network access.
The checked-in gate requires no real PHP binary or WebCore payload; when both
validated local artifacts are present, the stage additionally runs all 25
syntax lints.

`restart-engines.cmd` is the repo-owned Codex restart entrypoint. Preserve its
preflight-before-stop ordering and do not bypass it with direct lifecycle
script invocation.

### Official Mission-Level Graph

Verify the tracked Helpbot reference and all 1,639 governed runtime detents for
levels 1-149 with:

```cmd
cmd /d /c Tools\helpbot_mission_ql_reference.cmd
```

To reproduce acquisition, download the raw form of pinned AOWiki revision
`44808` from the URL and SHA-256 recorded in
`docs/evidence/data/helpbot-mission-ql-levels-1-149.json`, then run:

```cmd
cmd /d /c Tools\helpbot_mission_ql_reference.cmd --extract-raw "<pinned-raw-wikitext>" --update-graph
```

The command fails unless the raw bytes match the pinned source hash, extraction
finds exactly levels 1-149, and the reconstructed eleven detents reproduce every
published list after adjacent duplicates are removed.

Regenerate the compiled graph from the canonical checked-in CSV with:

```cmd
cmd /d /c tools\generate_mission_level_graph.cmd
```

Verify byte-for-byte reproducibility without writing with:

```cmd
cmd /d /c tools\generate_mission_level_graph.cmd --check
```

The upstream ODS is conflicting legacy provenance and its mission cells after
level 133 were precision-coerced. Do not generate the complete graph from that
ODS. Levels 150-220 remain outside the pinned Helpbot proof boundary.

### Mission QL 1-250 Live Harvest Plan

Regenerate the MissionHarvest plugin's compiled character-level/QL resolver
after an intentional mission-level table change with:

```cmd
cmd /d /c Tools\generate_mission_harvester_ql_table.cmd
```

Build the evidence-only plugin with its exact retained AOSharp SDK through:

```cmd
cmd /d /c Tools\build_mission_offer_harvester.cmd
```

The build fails if the generated 220×11 resolver differs from the governed
mission-level CSV. Codex must not inject or load the resulting DLL; Mike controls
the AOSharp client/plugin lifecycle.

Generate and validate the complete 250-QL assignment, rollability matrix, and
literal in-game command runbook with:

```cmd
cmd /d /c Tools\generate_mission_ql_harvest_plan.cmd
cmd /d /c Tools\generate_mission_ql_harvest_plan.cmd --check
```

The in-game command accepts a target QL directly:

```text
/missionharvest start <targetQL> <requestCount> [intervalSeconds]
/missionharvest status
/missionharvest stop
```

MissionHarvest resolves the first exact matching slot for the current character
level. It must send no request when the target is absent; nearest-QL substitution
is forbidden.

Harvester capture-contract version 2 records a request-time roll-origin snapshot
on every request, cohort, and offer: terminal identity/name, terminal playfield,
local/global coordinates, rotation, and player coordinates. Each offer separately
records the destination playfield/coordinates, all AOSharp `MissionInfo` fields,
reward item low/high IDs and QL, all raw unknown chunks, and the exact
capture-backed mission type for known icons. Unknown icons remain unclassified
with their numeric value preserved. The normalizer retains both the origin and
destination and remains backward-compatible with schema-version-1 journals.

## Database-Wide Official Playfield Placement Import

Import the verified official type-`1000014` placement corpus from the read-only
AO Stripdown extraction with:

```cmd
cmd /d /c Tools\import_official_playfield_placements.cmd --write
```

Validate every pinned source artifact and shard, then verify the checked-in
normalized cohort byte-for-byte without writing with:

```cmd
cmd /d /c Tools\import_official_playfield_placements.cmd --check
cmd /d /c Tools\import_official_playfield_placements.cmd --test
```

The importer verifies all six global source hashes and all 630 source-shard
hashes before rendering. It preserves 32,805 independent official placement
records, including duplicate positions and exact duplicate records, and emits
one normalized shard per resource instance under
`docs\generated\playfields\placements`. The wrapper then regenerates or checks
the offline AORebirth representation/reconciliation inventory across the
official index, `Playfields.xml`, compiled registered content, bounded dynamic
descriptors, and exact PF4582 bridge. Counts that cannot be enumerated offline
remain null. Resources 103, 615, and 4805 remain explicit parser-limited shards
with zero synthetic placements.

The offline AORebirth inventory is declared in
`docs\reference\playfields\aorebirth-playfield-representation-manifest.json`
and rendered to
`docs\generated\playfields\official-playfield-reconciliation.json`. A null
count means the adapter cannot honestly enumerate that dynamic or external
representation offline; it must not be treated as zero.

The generated corpus remains an evidence layer and never authorizes identity or
behavior. The Windows project is the single content-inventory owner and copies
the four canonical global files plus all 630 exact-cased shards to
`Content\Official\PlayfieldPlacements`; the governed Linux inventory is derived
from that project and copies the same files. `ZoneEngine
--validate-official-placements` loads the packaged files relative to the built
binary, verifies every pinned digest and global/per-playfield invariant, and
emits the deterministic `official-placement-build-manifest.json` plus
`PLACEMENT_PROVENANCE.env`. Normal startup and spawn materialization do not
consume the catalog. `ResourceInstance -> PlayfieldId` is accepted only for
this build-validated corpus, with the original resource instance retained. The
source build label `18.8.62_EP1` means the official old-graphics-client
extraction source; it is not a gameplay-content or spawn-content partition.

## PF4582 Authoritative Placement Import

Regenerate the normalized ICC Shuttleport placement catalog and audit report
from the checked-in authoritative source and runtime evidence map with:

```cmd
cmd /d /c Tools\generate_pf4582_placements.cmd
```

Verify byte-for-byte reproducibility, strict source validation, duplicate-position
retention, and fail-closed runtime activation with:

```cmd
cmd /d /c Tools\run_pf4582_placement_tests.cmd
```

The general official placement shard is the authoritative static official
source for PF4582. The specialized PF4582 source, reconciliation report, and
overlay remain the governed `SourceNpcId` crosswalk and historical evidence
layer and must agree with the general shard record-for-record.

The accepted placement source proves 206 placement records. `NpcId` is the
stable AORebirth source-placement key, not a proven native Funcom field.
Candidate respawn timing, names, flags, and unknown fields remain metadata; they
do not authorize movement, combat, loot, scripts, or runtime activation. Only
explicitly mapped existing runtime definitions may be active.

Audit all 38 numeric PF4582 `TemplateHash` groups against the governed evidence
ledger without changing runtime activation:

```cmd
cmd /d /c Tools\audit_pf4582_template_hashes.cmd
cmd /d /c Tools\audit_pf4582_template_hashes.cmd --check
cmd /d /c Tools\audit_pf4582_template_hashes.cmd --test
```

The audit pins its structured inputs, emits deterministic JSON and Markdown, and
fails closed on drift or conflicting evidence. The 24 baseline-unresolved hashes
account for 171 blocked placements. Ten additional blocked Island Reet rows use
the baseline-mapped ISRE hash, so the complete runtime blocked count remains 181.
Its accepted enemy-dossier projection is tracked under `docs/reference/pf4582`
and is tied to the complete raw dossier by SHA-256, allowing `--check` and
`--test` to run in clean or linked worktrees without the ignored capture folder.
Refresh that projection only when a newly accepted complete dossier supersedes
the current source:

```cmd
cmd /d /c Tools\audit_pf4582_template_hashes.cmd --refresh-capture-fixture-from "<accepted-enemy-dossier.json>"
```

No audit classification authorizes activation; promotion requires a separate
task with a stable source key to AO identity/profile bridge. `TemplateHash` is a
legacy AORebirth field name. Official EP1 evidence proves that the represented
value is a packed four-byte `ACGHash_t` scalar/tag, not a cryptographic hash or
a terminal mob-template identity.

Import reconciliation against the governed local 207-record official snapshot,
including the additional blocked `NCNN` record, is generated and checked with:

```cmd
cmd /d /c Tools\reconcile_pf4582_official_source.cmd
cmd /d /c Tools\reconcile_pf4582_official_source.cmd --check
cmd /d /c Tools\reconcile_pf4582_official_source.cmd --test
```

The generated official overlay and `IccShuttleportOfficialPlacementCatalog*.cs`
are evidence/future-generation layers only. They are not consumed by
`IccShuttleportSpawn`; the current runtime catalog remains 206 records, 25
active, and 181 blocked. `NCNN` has no `SourceNpcId`, profile, or activation.

Regenerate and test the corrected structural bridge report with:

```cmd
cmd /d /c Tools\analyze_pf4582_template_identity_bridge.cmd
cmd /d /c Tools\analyze_pf4582_template_identity_bridge.cmd --check
cmd /d /c Tools\analyze_pf4582_template_identity_bridge.cmd --test
```

The current outcome is `STRUCTURAL_SOURCE_AND_CONSUMER_FOUND`, superseding the
historical `NO_BRIDGE_LOCATED` result. This proves the official source record,
parser/native consumer, field locations, vector, and accessors. It does not
prove an `ACGHash_t`-to-mob-template, `MonsterData`, dynel, or AORebirth profile
join. Do not call `GetHash` or `GetHashSpawnPoints` terminal identity consumers.

## AOtomation Messaging Tests

Build and run the legacy MSTest assembly through the repo-owned wrapper:

```cmd
cmd /d /c tools\run_aotomation_messaging_tests.cmd
```

Pass a Visual Studio Test Platform filter directly to run a focused test first:

```cmd
cmd /d /c tools\run_aotomation_messaging_tests.cmd /TestCaseFilter:"FullyQualifiedName=SmokeLounge.AOtomation.Messaging.Tests.PlayfieldLifecycleTraceTests.SubwayThiefCombatContractPreservesLiveEnvelopeMovementAndDeathOrder"
```

The wrapper builds the test project with the repository's single-node MSBuild settings, locates `vstest.console.exe` through Visual Studio Installer's `vswhere.exe`, and then runs the generated .NET Framework 4.8 test assembly. Do not substitute `dotnet test` for this legacy project.

### Ordinary Enemy Level And Respawn Foundation

Run the deterministic shared-model, policy-resolution, scheduler, generation,
exception, exclusion, and population-boundary suite with:

```cmd
cmd /d /c tools\run_aotomation_messaging_tests.cmd /TestCaseFilter:"FullyQualifiedName~SmokeLounge.AOtomation.Messaging.Tests.WorldPopulationFoundationTests"
```

Then run the affected ordinary lifecycle tests with individual focused filters
before the established broader regression suites. Do not combine multiple
filters with an unescaped command-shell pipe.

Eligible PF127 ordinary rows inherit the documented 240-second private-project
policy unless explicit spawn/archetype or group data overrides it. Thief remains
60 seconds; Filth Flea and Bloodcreeper remain explicit 240-second policies.
Future live respawn captures identify exceptions or disputed timing; they are
not required once per ordinary enemy to re-prove the project default. Named,
boss, scripted, summon, pet, temporary-add, vendor, static, container, and
quest-owned content must stay with their explicit owners and cannot inherit the
ordinary policy.

### NPC Chase Navigation Validation

Run the focused shared/PF127 navigation suite first:

```cmd
cmd /d /c tools\run_aotomation_messaging_tests.cmd /TestCaseFilter:"FullyQualifiedName~SmokeLounge.AOtomation.Messaging.Tests.NpcChaseNavigationTests"
```

The global owner is `ZoneEngine.Core.Navigation`. PF127 is the first enabled provider and Vergil is its representative end-to-end case; no capture launcher or client injection is part of this deterministic validation. To enable another playfield, first promote authoritative versioned collision/navigation input, add an `IPlayfieldChaseNavigationProvider`, register it in `PlayfieldChaseNavigationProviderFactory`, add representative collision/route/failure/combat tests, and then perform private-client validation. Do not add enemy-specific pathfinding or reuse PF127 assumptions in another playfield.

## Database

- Use only `cellao_codex_clean`; this is the active legacy database name retained for local compatibility.
- Keep `AORebirth\Config\Config.xml` free of real credentials. Its checked-in connection string is a non-secret placeholder.
- Supply the local MySQL connection string to each engine with the `AO_REBIRTH_MYSQL_CONNECTION` environment variable. The environment value overrides only `MysqlConnection` after normal XML deserialization.
- The override must exist before the CMD process begins because configuration
  and connector state are cached per engine process.
- Run `cmd /d /c preflight-database.cmd` before startup. Exit codes are `10`
  missing override, `11` invalid format, `12` network failure, `13`
  authentication failure, `14` wrong database, `15` missing schema, `16` read
  failure, `17` online characters present, and `18` internal contract failure.
- Run `cmd /d /c Tools\scan_secrets.cmd` before committing configuration or workflow changes. It reports locations, never captured values.
- Do not change schemas without explicit approval.
- Do not wipe or mass-edit data without explicit approval.
- Treat checked-in SQL and runtime DB changes as separate surfaces.

## Captures

- Use AOSharp capture tooling for live packet/data truth.
- Codex runs tools, builds, servers, and captures.
- Mike performs live client playtests.
- Do not ask Mike to run commands inside the game when Codex can run external tooling.
- Do not run PowerShell or `.ps1` live capture wrappers from Codex; use `cmd.exe` or Git Bash workflows.
- Never launch the AO game/client automatically unless Mike explicitly instructs it in the current task.
- Live game testing is manual by Mike. Codex may build, validate, inspect files, or prepare capture tools only within the documented workflow.

### AOSharp Live Capture Startup

Approved startup command:

```cmd
cmd /d /c tools-temp\start-aosharp-live-capture.cmd --title "<AO window title>"
```

Alternative when Mike provides the client process id:

```cmd
cmd /d /c tools-temp\start-aosharp-live-capture.cmd --pid <ao-client-pid>
```

For a one-enemy ten-corpse loot sample, Codex arms validation through the same approved launcher; Mike does not type an in-game capture command:

```cmd
cmd /d /c tools-temp\start-aosharp-live-capture.cmd --title "<AO window title>" --loot-10
```

This wrapper is the only approved Codex startup command for AOSharp live capture. It starts the existing AOSharp injector against an already-running AO client and reports only the exact injector command, success or failure, capture output path, and failure log path. It does not launch the AO game/client. Before target selection it runs a fail-closed capture-safe contract check against the deployed injector and Bootstrap pair. A stale or unsafe binary cannot proceed to injection.

New captures are stored in the repository-level `Captures` folder. The live AO playfield name and resource ID lead each human-readable session folder, while the final compact timestamp remains the unique analyzer-facing capture ID. Example: `Captures\ICC Shuttleport [PF 4582] - 20260818-143201`. The launcher writes the absolute capture root contract before injection. Direct plugin loads without that contract retain the legacy plugin-local `captures` fallback.

Build the capture plugin after capture-tool source changes with:

```cmd
cmd /d /c MSBuild.exe tools-temp\AOSharpLiveCapture\AOSharpLiveCapture.csproj /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
```

For Mike's multi-plugin legacy AOSharp runtime, build the dedicated x86
compatibility plugin directly against that installation's existing
`AOSharp.Core.dll` and `AOSharp.Common.dll`:

```cmd
set MIKE_AOSHARP_RUNTIME=<exact legacy AOSharp runtime directory>
cmd /d /c MSBuild.exe tools-temp\AOSharpLiveCapture\AOSharpLiveCapture.Mike2022.csproj /t:Build /p:Configuration=Release /m:1 /nr:false /v:minimal
```

Load `AOSharpLiveCapture.Mike2022.dll` in the same assembly selection as the
other plugins. Do not also load `AOSharpLiveCapture.dll`. The compatibility
plugin registers `/aocap start|stop|status|flush|mark|snapshot` plus
`/aocap auto on|off|status`. It loads idle so it can coexist with other plugins
without creating capture folders until requested. `/aocap start` begins a manual,
crash-recoverable session and disables automatic continuation. `/aocap stop`
drains and finalizes the current folder, writes `capture-validation.json`, and
stays stopped. Every evidence row is auto-flushed and an atomic checkpoint is
written every two seconds. `/aocap auto on` is the explicit opt-in for immediate
continuous capture and playfield-change rotation; `/aocap auto off` leaves the
current session running until `/aocap stop`.

The plugin retains the complete inbound and outbound raw stream in
`packets.hex.log` and `raw-packets.csv`. It directly projects raw
`FollowTarget`, `SetPos`, `StopMovingCmd`, and `CharDCMove` packets into
`movement-packets.csv`, raw `SimpleCharFullUpdate` packets into
`scfu-appearance.csv`, current AOSharp dynels plus exact raw entity/corpse
evidence into `world-snapshot.csv`, player position/stats/evades/armor/buffs/
weapons into `player-combat-context.csv`, and attack-boundary distance evidence
into `aggro-observations.csv`. While a session is active, the compatibility
plugin samples the AOSharp live dynel set every 500 milliseconds and writes
baseline, appeared, and disappeared observations with player/entity position
brackets and distances to `visibility-observations.csv`. For every non-local
character in the installed runtime's `DynelManager.Characters` set, each complete
sample writes a `CLIENT_STATE` row containing the native-client
`SimpleChar.IsInLineOfSight` and `SimpleChar.IsInPlay` values. It also emits
`LOS_GAINED`, `LOS_LOST`, `INPLAY_GAINED`, or `INPLAY_LOST` when either value
changes. Raw Despawn rows preserve the packet identity without presenting stale
coordinates as current evidence. These are three separate evidence channels:
gamecode line-of-sight/in-play state, AOSharp dynel-set presence, and server
removal packets. AOSharp does not expose a per-dynel renderer/frustum visibility
property, so none of these rows alone proves that pixels were drawn.
Stop-time validation reports coverage for raw,
spawn identity, world/player context, periodic presence, LOS/in-play state,
movement, combat start, NPC-to-player and unprovoked aggro, death/corpse, and
identity-linked loot. A projection gap with an intact raw stream remains an
offline-decode issue rather than an automatic recapture request. The packet log
uses the canonical format consumed by the repository decoders. The legacy 2022
runtime still lacks newer AOSharp APIs required by the remaining optional
in-process geometry projections; the repository analyzer remains responsible
for those projections.

Build the injector and its capture-safe Bootstrap only through:

```cmd
cmd /d /c tools-temp\build-aosharp-live-injector.cmd
```

Capture-safe injection installs one isolated chat-input hook only after acquiring the per-client duplicate-injection guard. It recognizes only `/aocap` and `/aosmoke`, passes every other command to the client unchanged, and does not use AOSharp's native 131-byte GUI rewrite or its `GetCommand` hook. The native `StdString` allocation is fixed at the required 24-byte layout and deterministically disposed after every typed line. AOSharpLiveCapture itself signals readiness only after initialization and both command registrations succeed; the injector fails on a bounded readiness timeout instead of reporting a half-loaded capture, and disconnect unloads an unready Bootstrap so a retry is not blocked. Comprehensive capture starts automatically, and Mike can control it directly in game with:

```text
/aocap start
/aocap stop
```

The remaining typed commands are `/aocap mark <text>`, `/aocap status`, `/aocap flush`, `/aocap snapshot`, `/aocap dynels [force]`, and `/aocap fight start|stop|auto on|auto off|status`. `/aosmoke` commands are also available. The external request wrapper remains an offline fallback and must not be used to launch the AO client:

```cmd
cmd /d /c tools-temp\control-aosharp-live-capture.cmd start
cmd /d /c tools-temp\control-aosharp-live-capture.cmd stop
cmd /d /c tools-temp\control-aosharp-live-capture.cmd mark "respawn-start"
cmd /d /c tools-temp\control-aosharp-live-capture.cmd flush
cmd /d /c tools-temp\control-aosharp-live-capture.cmd snapshot
```

The wrapper writes a same-directory temporary request and atomically moves it into place. It refuses to overwrite a pending or in-process request. The capture launcher clears stale control artifacts only immediately before a fresh injection.

The default capture always records the comprehensive raw packet superset in independently auto-flushed `packets.hex.log` and `raw-packets.csv`; it never narrows recording by focus, enemy type, marker, or validation mode. Either raw sink, or their complete union, is sufficient for offline recovery. It also directly decodes raw `SimpleCharFullUpdate` packets into reusable SCFU evidence and promotes NPC evidence into `enemy-full-updates.csv`, `enemy-state.csv`, `enemy-dossier.json`, `enemy-movement.csv`, `movement-packets.csv`, `enemy-combat.csv`, `enemy-stat-updates.csv`, `npc-lifecycle.csv`, `corpse-full-updates.csv`, `enemy-respawns.csv`, `inventory-updates.csv`, and `corpse-loot-observations.csv`. `enemy-state.csv` rows include source direction, packet sequence, message type, and evidence source. Loot reconstruction canonicalizes padded and unpadded numeric corpse identities before joining inventory, transfer, lifecycle, and generation evidence. External markers such as `control-aosharp-live-capture.cmd mark "respawn-start"` and launcher modes such as `--loot-10` only label the session or add acceptance requirements; they must never filter, suppress, or narrow captured evidence. A marked respawn capture validates incomplete unless the required respawn is correlated. A `--loot-10` capture validates incomplete if fewer than ten initial corpse snapshots or more than one enemy type is present, while still recording the same comprehensive superset. Final capture validation must report incomplete when corpse presence or inventory was observed without a successfully decoded identity-linked `CorpseFullUpdate`.

If either raw sink contains the raw packet but a decoder, identity join, or promoted export fails, the gameplay capture remains intact. Treat an incomplete projection as an offline-reconstruction task whenever `recaptureRequired=false`, including when `processingAllowed=false` or `offlineDecodeRequired=true`; repair and rerun the offline decoder instead of asking Mike to repeat gameplay. Recapture only when the raw evidence itself is missing or incomplete, including an undrained teardown boundary.

For the current Subway loot boundary, no new capture is requested. If later sampling is approved, the remaining corpus-wide minimum is eight strict complete Bloodcreeper loot outcomes and three strict complete Disobedient Bot loot outcomes. Open every corpse, retain every item transfer and complete empty inventory, and keep the capture loot-only: do not repeat combat, geometry, LOS, navigation, chase, leash, or respawn evidence. A session-local `--loot-10` target exceeds these corpus-wide minimums unless a separately bounded target requires it.

Existing capture folders can be retro-decoded without repeating gameplay:

```cmd
cmd /d /c MSBuild.exe tools-temp\AOSharpCaptureAnalyzer\AOSharpCaptureAnalyzer.csproj /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
cmd /d /c tools-temp\AOSharpCaptureAnalyzer\bin\Debug\AOSharpCaptureAnalyzer.exe --self-test
cmd /d /c python tools-temp\AOSharpLiveCapture\decode_npc_lifecycle_capture.py --self-test
cmd /d /c tools-temp\AOSharpCaptureAnalyzer\bin\Debug\AOSharpCaptureAnalyzer.exe "<capture-folder>"
cmd /d /c tools-temp\AOSharpCaptureAnalyzer\bin\Debug\AOSharpCaptureAnalyzer.exe --decode-loot "<capture-folder>"
cmd /d /c python tools-temp\AOSharpLiveCapture\decode_npc_lifecycle_capture.py <capture-folder>
cmd /d /c python tools-temp\AOSharpLiveCapture\decode_movement_capture.py <capture-folder>
```

Run the analyzer first to recover direct SCFU evidence from raw packets, run
`--decode-loot` to recover raw inventory snapshots and item transfers, then run
the lifecycle decoder to rebuild correlated NPC lifecycle and corpse-loot outputs.
Run the movement decoder when movement, idle paths, chase, or range evidence is
needed. It reconciles the packet log and `raw-packets.csv`, so Mike captures whose
packet log uses the alternate line format still retain their movement evidence.

For mission-terminal and mission-lifecycle **analyze and implement**, **ALWAYS**
use the dedicated x86 mission analyzer:

`C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpMissionCaptureAnalyzer\bin\Debug\AOSharpMissionCaptureAnalyzer.exe`

Do not substitute ad-hoc Python/log greps, the geometry `AOSharpCaptureAnalyzer`,
or the legacy server mission decoder as the first analysis step. When Mike hands
a mission capture folder, run this analyzer immediately, then ground implementation
in `mission-flow.replay.log` (and errors log if non-empty).

It shares the live plugin's `MissionFlowCapture` extractor and the current AOSharp
serializer, preserving the 64-bit geometry analyzer's existing architecture:

```cmd
cmd /d /c MSBuild.exe tools-temp\AOSharpMissionCaptureAnalyzer\AOSharpMissionCaptureAnalyzer.csproj /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
cmd /d /c tools-temp\AOSharpMissionCaptureAnalyzer\bin\Debug\AOSharpMissionCaptureAnalyzer.exe --self-test
cmd /d /c tools-temp\AOSharpMissionCaptureAnalyzer\bin\Debug\AOSharpMissionCaptureAnalyzer.exe "<capture-folder>"
```

The analyzer writes `mission-flow.replay.log` and
`mission-flow.replay.errors.log` into the capture folder. A successful replay
has zero errors and retains the raw global ordinal, raw directional sequence,
captured timestamp, direction, and mission identities on every promoted
mission-flow row.

After adding a finalized capture to the Subway enemy combat-contract input list,
regenerate the versioned evidence contract through the repository wrapper:

```cmd
cmd /d /c tools\generate_subway_enemy_combat_contracts.cmd
```

Before running the wrapper, do not run `rg`, `dir`, `tasklist`, recursive searches, process sweeps, source inspection, build-folder enumeration, or old-log scraping to rediscover how capture startup works. Use the wrapper directly.

Do not inspect AOSharp capture source code, search for command names, enumerate build folders, or read old capture logs unless the wrapper fails or Mike explicitly asks for investigation.

If the wrapper fails, run at most one targeted failure-log inspection command, summarize the smallest relevant failure, identify the likely broken doc, wrapper, missing build output, or runtime prerequisite, then stop unless Mike asked for repair. The approved targeted failure check is:

```cmd
cmd /d /c findstr /C:"ERROR:" tools-temp\AOSharpLiveInjector\bin\Debug\AOSharpLiveInjector-start.log
```

## Live Client Behavior Bugs

For AORebirth bugs involving current AO client behavior, packet flow, UI actions, item movement, inventory, bank, backpacks, shops, trade, missions, NPC interactions, pets, combat actions, or other client/server behavior:

- Treat the live AO client as the authoritative protocol source.
- Treat legacy server code as a partially-correct reference, not proof.
- Do not rely on static audit alone when packet behavior is involved.
- Start with live capture or existing capture review whenever feasible.
- User should only perform in-game actions; Codex must inspect logs/captures itself.
- If capture is not possible, explicitly say so and explain the fallback evidence.
- Repairs must be based on confirmed live packet/message behavior when available.

## Capture-Derived Content

These rules apply to NPC, mob, statel, static dynel, vendor, quest, item, and playfield reconstruction.

- Identity first. The captured AO identity is the primary key.
- Do not choose or replace an object based only on display name, item name, screenshot appearance, nearby objects, spatial proximity, visual similarity, assumed mesh, or guessed relationship.
- Search the complete relevant capture set for the exact identity before declaring evidence missing. Include `events.log`, `packets.hex.log`, `system-messages.log`, `npc-interactions.log`, `inventory-updates.csv`, `enemy-state.csv`, `enemy-state.json`, `vendor-full-updates.csv`, `shop-updates.csv`, and decoded full-update outputs.
- Separate interaction evidence from definition evidence. `GenericCmd Action=Use -> Terminal:56D9B4AF` proves only that the identity was used; template, mesh, name, position, rotation, stat blob, and event configuration require a full-update packet or another source tied to the same identity.
- Use the evidence hierarchy from `docs/project/KNOWN_DECISIONS.md`: exact identity-linked full-update, exact identity-linked stat/update, exact identity-linked interaction, decoded logs, extracted analysis, screenshots, then names/proximity/nearby objects.
- Do not copy template, mesh, stat blob, position, rotation, or events from a nearby identity unless the capture explicitly proves the relationship.
- Do not test alternate templates or mesh overrides because the current object looks wrong. Stop, search all captures for the exact identity, locate full-update/stat evidence, and rebuild from that evidence.
- Keep evidence extraction, data creation, visual smoke, use/interact routing, objective progression, mission completion, and rewards as separate tasks.
- Fail closed when exact identity evidence or required full-update fields are missing, conflicting, unknown, or only supported by name/appearance.

Before editing SQL or runtime data, state:

- exact captured identity;
- capture folders searched;
- full-update evidence found;
- fields that are confirmed;
- fields that remain unresolved;
- files and rows that will change.

Also provide this evidence table before any SQL or game-data edit:

| Field | Proposed value | Exact identity | Capture folder | Packet/log source | Confidence |
| --- | --- | --- | --- | --- | --- |

Local SQL/data patches must include exact rows affected, pre-apply verification query, apply command, post-apply verification query, rollback query, and confirmation that no unrelated rows changed.

## Evidence

Use this source order:

1. Official live capture.
2. Private-server capture as shape/reference evidence.
3. AO stripdown source/contracts.
4. Local code facts.

Do not patch packet-sensitive behavior from visual symptoms alone.

## Capture Evidence Fixtures

Tracked capture fixtures are derived evidence packages under
`docs/reference/captures`. They intentionally do not vendor the ignored raw
capture folders.

Validate the tracked fixture set before using it as the basis for a gameplay
change:

```cmd
cmd /d /c tools\validate_capture_evidence_fixtures.cmd
```

This is a Windows-lane gate. After it passes, the same tracked fixture files can
be reconciled into the Linux branch before any gameplay code is promoted.

### Raw capture retention authority

`docs/evidence/aosharp_capture_retention.csv` is the tracked source of truth for
whether accepted AOSharp raw evidence must be retained. Its synchronized report
is `docs/generated/aosharp_capture_retention.md`.

Normal inventory regeneration appends every newly accepted capture as
`retain/unreviewed`; it preserves reviewed records and fills a previously blank
digest only when the accepted inventory proves that identity. An identity or
digest mismatch fails closed. A local capture that is absent from the accepted
inventory or retention report is still retained by default.

Only `discard_approved` is discard authority. It requires an evidence digest,
complete analysis and evidence coverage, tracked `used_by` paths, an immutable
raw archive path plus SHA-256 or complete tracked derived artifacts, and an
approval name, date, and reason. Repository references, generated inventory
rows, implementation references, or fixture existence alone are not discard
authority.

Regenerate and validate with:

```cmd
cmd /d /c python Tools\inventory_aosharp_captures.py
cmd /d /c python Tools\inventory_aosharp_captures.py --validate-current
```

The generator has no prune or delete operation. Raw-folder removal is never an
inventory side effect and must not be automated from inferred usage.

## Windows/Linux server repair parity

Server repairs are Windows-authoritative first and Linux-deployed second.

Required workflow for every server repair or gameplay/runtime source change:

1. Land the repair in Windows `master` as committed source.
2. Deploy Linux server binaries only from an exact committed Windows `master`
   SHA.
3. Record the deployed Linux release name and exact source SHA in repository
   evidence or project-state documentation.
4. Before starting the next server repair, compare the current Windows
   `master` server-source delta against the active Linux release source SHA.
5. If Windows contains server-code commits that are not represented in the
   active Linux release, reconcile/deploy that delta before using live Linux
   behavior as acceptance evidence for the next repair.

Client-patch-only and docs-only commits after a deployed server SHA do not
require a Linux server redeploy, but they must be explicitly identified as
non-server changes before declaring Windows/Linux server parity.

## SHA-gated Windows/Linux synchronization

AORebirth has one authoritative source history. Windows remains the development
and acceptance platform; `master` is the integrated source authority; Linux
consumes exact accepted SHAs from controlled build workspaces.

For Windows integration evidence after a commit is on the intended integration
line, run:

```cmd
cmd /d /c Tools\accept_windows_source.cmd --expected-sha <sha>
```

Add `--mandatory-gate` only when the full mandatory integration gate is required
for that acceptance event. The wrapper fails closed on a source SHA mismatch,
tracked-source dirt, `git diff --check`, build failure, or mandatory-gate
failure. It writes non-secret evidence under ignored `build-verify`.
The wrapper also validates raw-independent accepted generated-combat integrity
with `--check`; it must not call the strict historical `--validate-current` gate.

For Linux acceptance, use a controlled disposable or dedicated build workspace,
not a normal developer checkout and not the production runtime directory:

```bash
LinuxBuild/accept-linux-sha.sh --expected-sha <sha> --expected-placement-manifest-sha <windows-manifest-sha256> --workspace /srv/ao-rebirth-linux-acceptance
```

The Linux wrapper fetches origin, checks out the exact SHA detached, resets and
cleans only the sentinel-marked controlled workspace, verifies:

```text
AO_REBIRTH_SOURCE_SHA=<sha>
EXPECTED_SOURCE_SHA=<sha>
SOURCE_SHA_MATCH=PASS
TRACKED_SOURCE_CLEAN=PASS
RESTORE=PASS
BUILD=PASS
TESTS=PASS
PUBLISH=PASS
LINUX_ACCEPTANCE=PASS
```

It then writes `SOURCE_SHA`, `BUILD_PROVENANCE.env`, and
`LINUX_ACCEPTANCE.env` into the published ZoneEngine artifact. These files must
not contain secrets, usernames, tokens, private host addresses, database
credentials, or operational configuration.

The normal ZoneEngine Linux publish wrappers still exist:

```cmd
cmd /d /c LinuxBuild\publish-zoneengine.cmd linux-x64 true
```

```bash
LinuxBuild/publish-zoneengine.sh linux-x64 true
```

They continue to perform source-inventory validation, restore, build, publish,
and Stage 8 offline smoke validation. They also write non-secret publish
provenance with `ACCEPTANCE_RESULT=UNVERIFIED`. A direct publish is build
evidence, not deployment acceptance.

Deploy ZoneEngine only from a Linux-accepted artifact whose provenance matches
the intended source SHA:

```bash
bash upgrade-live-service.sh <verified-publish-dir> <release-id> <expected-source-sha>
```

The deployment gate refuses promotion when `SOURCE_SHA`,
`BUILD_PROVENANCE.env`, or `LINUX_ACCEPTANCE.env` is missing or mismatched, when
`SOURCE_SHA_MATCH`, `TRACKED_SOURCE_CLEAN`, or `LINUX_ACCEPTANCE` is not `PASS`,
when the online-character guard fails, when runtime validation fails, or when
rollback validation fails.

To validate artifact provenance without touching production, use:

```bash
bash upgrade-live-service.sh --validate-artifact-provenance <verified-publish-dir> <expected-source-sha>
```

Do not deploy an implicit branch tip. Do not copy source files between Windows
and Linux. Do not edit production source. If Linux finds a compatibility defect,
repair it through the Windows source tree, validate on Windows, merge to
`master`, then run Linux acceptance on the new exact SHA.
