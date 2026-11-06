# AORebirth Build and Release Contract

Status: Permanent project policy. Apply this contract to every governed release
build, publishing and release attempt. It supersedes older workflow descriptions
that allow unaccepted Linux release work, separate Linux content reconstruction
or moving accepted inputs.
Repository `AGENTS.md` remains the agent-behavior authority.

Ordinary Windows `dotnet build` and Visual Studio Build compile public source
without private release configuration, content access, provenance or acceptance
state. They cannot create release acceptance or authorize publishing/deployment.
Explicit `AORebirthGovernedRelease=true`, active governed build capabilities and
the existing release entrypoints retain private profile validation. Publishing
always remains guarded; Linux release/package generation remains online-only.

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

1. Fetch and synchronize current public Windows master; require
   LOCAL_WINDOWS_SOURCE_SHA == PUBLIC_MASTER_SHA.
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

## Authorities and roles

| Role | Authority and permitted responsibility |
| --- | --- |
| Public product source | `https://github.com/subarumike/AORebirth`, branch `master`, is the only product/runtime source authority. |
| `PUBLIC_PRODUCT_CHECKOUT` | The configured local Windows integration/build checkout; synchronize it from public master before each Windows build. Local edits do not create a competing product authority. |
| `PRIVATE_CONTENT_UPSTREAM` | The configured private upstream authority for private GameData/content changes. Its exact repository binding stays in private governance. |
| `CANONICAL_WINDOWS_GAMEDATA` | The reconciled private dataset used by Windows, including required coexisting private/generated content and canonical repairs. A cached tree is not upstream authority. |
| `PRIVATE_LINUX_CHECKOUT` / `PRIVATE_LINUX_REPOSITORY` | The private derived source, allowlisted Linux build/packaging/service adaptations, tooling and release provenance. They do not own independent product behavior or content. |
| `ONLINE_LINUX_BUILDER` | The existing private GitHub Actions builder for Linux binaries and deployable live-server packages. |

Private repository bindings, infrastructure, credentials, protected GameData,
build artifacts and release receipts remain private. Public documentation uses
the role names above instead of publishing those private details. Existing
deployment-tooling access and publication rules remain in force.

## Windows preparation and acceptance

1. Fetch public master and record the exact `PUBLIC_MASTER_SHA`.
2. Synchronize the Windows checkout to that SHA, preserving unrelated work.
   Before compiling, require `LOCAL_WINDOWS_SOURCE_SHA == PUBLIC_MASTER_SHA`.
   Uncommitted product/runtime changes are not an accepted substitute.
3. Fetch/check `PRIVATE_CONTENT_UPSTREAM` and record `CONTENT_DUMP_SHA` on every
   build/release attempt. Missing access or an unknown current upstream SHA
   fails closed, even when a cached content tree exists.
4. Compare the fetched dump content and SHA with the recorded basis of canonical
   Windows GameData. Reconcile every approved addition, modification and removal
   before building Windows. Apply upstream-owned removals against their recorded
   ownership/basis; do not prune unrelated private/generated content. Preserve
   legitimate coexisting content and canonical repairs. An unexplained conflict,
   stale replacement or unreviewed content delta blocks the build.
5. Produce the deterministic complete `WINDOWS_GAMEDATA_MANIFEST` using the
   shared inventory/hash algorithm. Cover the complete selected dataset, not
   only the files changed in this release. Record content reconciliation and
   source provenance without publishing protected content.

Approved authored runtime catalogs use the existing reconciliation helper's
private `PRIVATE_CATALOG_ADOPTIONS` profile mapping. The three chest runtime paths
`Loot/MissionChestRKAllowlist.json`, `Loot/MissionChestSeasonalExclusions.json` and
`Loot/MissionChestSpecialExclusions.json` remain eligible together;
`QuestRewards.json`, `ObjectStateMachines.json` and runtime-consumed
`Playfields/{numeric-id}/Spawns.json` are independently eligible. Each row binds
`PATH`, an owning-checkout ignored `STAGED_PATH`, its approved `SHA256`, and
`APPROVAL` containing `BY=Mike`, ignored `INSTRUCTION_PATH`, the actual user
instruction's `INSTRUCTION_SHA256`, and a literal `TEXT` excerpt naming the
policy. Use private paths only in the ignored profile. Normal preparation and
hash-approved reconciliation adopt these bytes after upstream merging. Chest and
object-state-machine catalogs replace only absent/empty public placeholders or
retain identical approved bytes; quest rewards preserve every existing table.
Spawn repairs require approval naming the complete relative path and an existing
nonempty valid spawn catalog; they may add dictionary fields only, preserving
every prior field, scalar type/value, list length and placement order. Reject
unexplained private changes and empty replacement catalogs. Local replay
retains exact instruction/before/after payloads; portable evidence carries bounded
identities only. Never reset canonical state or copy policies directly into the
selected data tree to bypass reconciliation. Generated-items, source verification
and exact-input Windows acceptance requirements remain unchanged.
6. Build Windows locally from the exact public source and refreshed canonical
   GameData. Use the established build and startup/readiness workflows.
7. Stop after Windows build/startup readiness and hand the build to Mike for
   testing. Do not begin Linux release work or silently accept the build.
8. Record `WINDOWS_USER_ACCEPTED=YES` only after Mike explicitly accepts that
   exact Windows build. The acceptance receipt must bind `PUBLIC_MASTER_SHA`,
   `CONTENT_DUMP_SHA`, `WINDOWS_GAMEDATA_MANIFEST` and the exact Windows
   build/startup receipt hash. Preserve the explicit user-acceptance evidence.

Successful automated checks, an agent-authored acceptance flag, historical
approval and deployment authorization are not substitutes for Mike's explicit
Windows acceptance. Mike operates the AO client; build tooling does not launch
or control it without a separate explicit request.

## Freeze the accepted release

After acceptance, freeze the exact source/dump/manifest tuple and its hash-bound
acceptance receipt. Record `ACCEPTED_PUBLIC_MASTER_SHA` as the accepted public
source identity and preserve the distinct derived Linux/package source SHAs.

Public master and private content development may continue. Mandatory fresh
upstream checks record newly observed tips, but newer source or content belongs
to the next release. Do not silently move the accepted tuple or rebuild its
canonical data from a newer tip. Only Mike's explicit re-reconciliation request
starts a new Windows synchronization, content reconciliation, build/readiness and
user-acceptance cycle. Changed inputs invalidate the prior acceptance.

## Linux and live-server package

1. Validate the explicit Windows acceptance and frozen inputs before Linux
   release work. Reconcile Linux product source to the exact accepted public
   SHA. Inventory all differences and allow only explicit Linux
   build/packaging/service adaptations that preserve public product semantics.
2. Require `LINUX_PUBLIC_BASE_SHA == ACCEPTED_PUBLIC_MASTER_SHA` and
   `PUBLIC_RUNTIME_DIFFS=0`. Private derived commits do not make Linux an
   independent product authority; runtime equality is not waived by tooling or
   content allowances.
3. Supply the exact Windows-accepted logical GameData result through only its
   actual delta and explicit removal list. Reuse verified unchanged content;
   do not recopy/reconstruct the entire tree or create a separate Linux dataset.
4. Use the existing private GitHub Actions online builder only. Do not build
   Linux or live-server files locally through Docker/WSL, on production, or
   through an alternate builder. GitHub Actions produces the validated Linux
   binaries and deployable live-server package from the frozen inputs.
5. Produce `LINUX_GAMEDATA_MANIFEST` with the same complete deterministic
   algorithm and require equality with `WINDOWS_GAMEDATA_MANIFEST`.
6. Build/publish only the impacted engine/project dependency closure. Reuse
   verified unchanged engine outputs and evidence; validate the complete target
   inventory, package integrity, affected/dependent startup/readiness, provenance
   and GameData completeness. WorldEngine remains included when required by the
   current topology. Apply the PATCH MODE baseline and touched-failure rules.
7. Push validated Linux results only to the verified private Linux destination.
   Stop there unless deployment is explicitly authorized.

## Authorized deployment and live verification

Explicit Windows acceptance and build/package approval do not authorize a
deployment. When Mike separately authorizes deployment, use the existing normal
deployment workflow and its approved tooling-access exception.

Install the exact accepted online-built Linux binaries and the complete accepted
GameData delta: all additions, modifications and removals, together with required
governed migrations/configuration. Changed-file/incremental transfer is allowed
only when it yields the complete accepted result. A partial content subset is
not an accepted deployment.

After deployment, compute `LIVE_GAMEDATA_MANIFEST` from the complete actual
selected live dataset using the same deterministic algorithm. Require:

```text
LOCAL_WINDOWS_SOURCE_SHA == PUBLIC_MASTER_SHA
LINUX_PUBLIC_BASE_SHA == ACCEPTED_PUBLIC_MASTER_SHA
PUBLIC_RUNTIME_DIFFS=0
WINDOWS_ACCEPTED_GAMEDATA_MANIFEST == WINDOWS_GAMEDATA_MANIFEST
WINDOWS_ACCEPTED_GAMEDATA_MANIFEST == LINUX_GAMEDATA_MANIFEST == LIVE_GAMEDATA_MANIFEST
```

Keep the accepted inputs and resulting source/content manifests in private
provenance. An old package, deployment checkout or running release does not
redefine product/content authority. Mike performs final live client acceptance.

## Fail-closed validation

Refuse the affected build, Linux release or deployment when:

- public master cannot be fetched or Windows source does not match the current
  public-master SHA before its build;
- private content upstream is inaccessible, its current SHA is unknown, or its
  approved changes have not been reconciled before Windows build;
- required canonical private/generated content or a repair would be silently
  lost, or a content conflict/removal has no approved ownership decision;
- Mike has not explicitly accepted the exact Windows build, or acceptance
  evidence does not match the frozen source/dump/manifest/receipt hashes;
- Linux product source differs from the accepted public master or has an
  unexplained/unallowlisted adaptation;
- Linux consumes a different GameData manifest or an independently reconstructed
  dataset;
- GitHub Actions is bypassed for Linux/live-server binaries or package files;
- a deployment lacks explicit authorization or would omit an accepted content
  addition, modification or removal;
- the computed live manifest differs from the accepted Windows/Linux manifest.

Use the existing repository-owned gates and their regression checks. Never
manufacture a receipt, relabel historical evidence, suppress an unknown mismatch
or change product behavior merely to satisfy a release validator.
