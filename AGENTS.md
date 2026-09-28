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

Gameplay and content data must remain data-driven. NPC-specific data, stats,
appearances, IDs, dialogue, quests, rewards, vendor stock, spawn bindings, and
similar game content must not be hard-coded into reusable runtime C# merely to
make behavior work.

Runtime C# implements reusable mechanics, services, loaders, and validation;
individual content definitions belong in the appropriate editable data source.

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

## AUTHORITATIVE SOURCE

Public GitHub `master` is the authoritative AORebirth product/runtime source.

Linux is a DERIVED BUILD of public master.

Linux is NOT an independent gameplay/runtime branch.

Any gameplay, protocol, persistence, content, NPC, mission, combat, item, nano, quest, shop, world, character, database behavior, or other product change must originate in Windows/public master first.

Never implement a product/runtime fix directly in Linux because the Windows implementation does not compile or behave correctly on Linux.

## RESPONSIBILITY SPLIT

AORebirth development responsibilities have changed.

The Linux workstream is now STRICTLY responsible for:

`public Windows/master -> Linux port -> Linux validation -> private Linux Git -> Linux deployment`

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

When it moves:

1. fetch new public master;
2. audit the delta;
3. reconcile the Linux port;
4. retain valid Linux adaptations;
5. drop adaptations superseded by public master;
6. regenerate Linux adaptation/provenance receipts;
7. rerun affected Linux validation.

Do not attempt to freeze or control Windows development.

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

public Windows/master
→ fetch exact SHA
→ port/reconcile to Linux
→ validate Linux
→ push Linux result to private Linux Git
→ package
→ deployment approval
→ deploy Linux

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

1. Fetch current public `origin/master`.
2. Record the exact authoritative SHA.
3. Compare the Linux source/tree against that SHA.
4. Inventory every difference.
5. Classify every difference as:

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

Public Windows/master remains the authoritative product/runtime source.

Every Linux port starts from the exact current public Windows/master SHA.

Required sequence:

public Windows/master implementation
→ Windows validation
→ public master commit/push
→ fetch exact public master SHA
→ reconcile/port that source to Linux
→ Linux build/test
→ push validated Linux port commits to PRIVATE Linux Git
→ Linux package construction/validation
→ deployment approval
→ Linux deployment

Never reverse this sequence. Deployment requires Mike's explicit authorization.

The Linux private repository must preserve traceability to the exact public-master SHA it was derived from.

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

`public Windows/master -> fetch exact authoritative SHA -> port/reconcile to Linux -> validate Linux -> push Linux result to PRIVATE Linux Git -> package/validate -> deployment approval -> deployment`

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
→ fetch new exact public master SHA
→ reconcile/port that new SHA to Linux
→ validate
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

- `D:\AORebirth-fresh\GameData` is Mike's private full GameData dataset.
- `AORebirth\GameData` is the public GitHub distributable/placeholder dataset.
- Differences between private and public catalogs are intentional and are not
  automatically drift.
- Existing runtime configuration `AO_REBIRTH_GAMEDATA_PATH` selects
  external/private GameData.
- Do not redesign GameData staging or selection when that existing mechanism
  satisfies the requirement.
- Never commit private GameData to public GitHub.
- `AO-Content-Dump-main` is extraction, reference, and upstream material; it is
  not the runtime GameData root.

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
- Do not inspect workflow source code unless the workflow itself is being modified.
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
