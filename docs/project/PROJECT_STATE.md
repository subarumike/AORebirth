# AORebirth Project State

Updated: 2026-08-08

This file is the concise current source of truth. The pre-cleanup long-form
state is preserved at
`docs/archive/project/PROJECT_STATE_PRE_BASELINE_CLEANUP_20260801.md`; subsystem
completion matrices and dated evidence retain detailed provenance.

## Acceptance baseline

- Complete AOtomation suite: PASS (998/998).
- Arete regular-mob combat uses a scoped forward reconciliation against the
  current capture corpus. The retired post-cutoff 60/60 script is not restored;
  the current focused gameplay gate validates supported combat behavior and
  expected fail-closed exclusions.
- PF127 Subway acceptance: PASS.
- PF1931 Temple acceptance: PASS.
- PF1931 official-client post-login acceptance: PASS. The static
  `PlayfieldAnarchyF` resource shape is restored, malformed generated-resource
  identities fail closed, all 43 captured internal door statuses remain
  enabled, and Soldier completed entry/residency/exit validation on 2026-08-04.
- Generated mission graph and mission reproducibility: PASS.
- Debug server build: PASS.
- Parallel Linux compile-feasibility lane: Messaging, Cell.Util, MsgPack.Mono,
  Translations, Cell.Core, Utility, Enums, Exceptions, Interfaces,
  ObjectManager, Database, Stats, and Communication build on .NET 10 from guarded linked
  source/resource/content inventories, with a separate Linux-only `Ionic.Zlib`
  compatibility assembly plus an inert identity-compatible `MemBus` adapter.
  Stages 0-4 Windows-hosted compatibility checks,
  exhaustive public/mapping/table contracts, Database/Stats offline behavior,
  exact SQL publish assets, Communication wire/framing/FIFO loopback behavior,
  and the unchanged Windows debug build pass. Core/PlayfieldLoader are audited
  as unused by the first ChatEngine milestone and deferred to Login/Zone. Native
  Ubuntu validation, live disposable-MySQL parity, ChatEngine, and deployment
  are not yet complete.
- Git LFS and Git object integrity: PASS.
- WebEngine offline PHP/WebCore boundary: PASS. The official PHP 8.5.9 x64 NTS
  VS17 runtime and hardened INI are exact-manifest validated; the complete
  7,140-file WebCore corpus and all 25 PHP files are audited, deterministically
  patched, final-manifest validated, and PHP 8.5.9 lint clean. Clean commit
  `f898faa0838cc3918baf29202001e0cc2d0fab56` passed the complete 13-stage gate
  twice unchanged.

## Generated combat authority

The capture corpus and production runtime are authoritative; checked-in
generated projections must reproduce from them and are never edited by hand.
The current deterministic inventory contains 381 sessions, 365 canonical
sessions, 3,269 complete attack chains, 260 certified profiles, 96 runtime-ready
profiles, 309 semantic definitions, 101 runtime-ready definitions, and 1,486
explicitly unresolved observations with zero generator errors.

The active-coverage projection contains 1,534 actors, 1,520 binding records,
and maximum actor index 1,536. It reconciles 559 certified actors and 975
explicitly unresolved actors. The Arete family cohort is 52/96 certified and
the additional Arete binding cohort is 4/17 certified. The transactional
generator and current-cohort validation pass against the generation identity
recorded in the checked-in manifest. Unsupported or conflicting observations
remain fail-closed.

The generated combat surface is now one six-file cohort: five semantic artifacts
plus a manifest commit marker. A multi-reader/single-writer lease protects
supported readers and serializes writers. Primary captures are parsed once into
immutable validated shards; all generator/tool inputs are frozen; active coverage
and formula data converge to one fixed point; and publication is manifest-last,
rollback-capable, and crash-recoverable. The current generation identity is
recorded in `docs/generated/capture_backed_npc_combat_generation_manifest.json`.
Generated output no longer embeds the local checkout path. Runtime catalog,
exact-byte fixtures, and formula semantics are byte-identical to the prior
authority; no supported gameplay behavior changed.

Capture-decoder internal type failures caused by the known Windows interpreter
corruption boundary are retried at the coordinator child boundary; ordinary
deterministic type/schema failures remain fail-closed and are not retried.

The published input descriptor is schema 2 and hashes only durable capture
source, plan, identity, and session-state fields. Private shard descriptors stay
strictly validated inside each primary attempt but do not contaminate the
published identity. Active/formula children receive independent fsynced,
read-back-verified private projections and verify SHA-256 and byte length over
the same bytes they decode with a fully Python-initialized JSON scanner. Active
and formula projections are separate exact consumer inputs, and both preserve
complete `attackInfoPacketIds` arrays rather than sampled or counted evidence.
The frozen ItemDb is likewise verified against its auxiliary snapshot record.
The repository's C# `MessagePackZip` reader extracts exactly the 42 templates
referenced by governed PF127/PF1931 formula inputs into a canonical private JSON
projection. Formula children verify that projection's SHA/length and no longer
parse the full ItemDb in Python. The generated formula values remain unchanged;
its diff is limited to rebuilt analyzer provenance.
After a completed transition, formula equality proves that the next active and
formula pair is identical to the current pair. The coordinator memoizes only
that proven identity transition, preserving the three-round convergence result
while skipping both redundant terminal children. The generated runtime catalog
and fixtures remain byte-identical; other generated changes are provenance and
source-descriptor reconciliation only.
Cohort validation now binds each JSON decode to the manifest SHA/length, reuses
the first parsed object instead of reparsing the 124 MB inventory, and retries
`JSONDecodeError` against the same verified UTF-8 string up to three times. The
same bound applies to impossible stdlib JSON `TypeError` or `AttributeError`
failures only when their traceback proves `json.decoder`/`json.scanner`
ownership; deterministic and unrelated failures fail closed.
Repository-owned acceptance, build, test, and generated-combat wrappers select
CPython 3.13.14 through `Tools/select_python_runtime.cmd`; the selector can be
overridden with `AO_REBIRTH_PYTHON`. This avoids the locally installed Python
3.12 runtime whose repeated `python312.dll` access violations prevented stable
preflight execution. The manifest records the selected CPython 3.13.14 binary.
Each mandatory gate holds one read lease across all 13 stages, eliminating the
former full inventory parse before and after every filtered acceptance wrapper.

The formula ItemDb reader streams each top-level MessagePack array and retains
only the 42 templates referenced by the governed PF127/PF1931 profiles instead
of all 120,842 templates. Measured peak Python allocation fell from 422,936,105
to 11,169,393 bytes. The governed formula artifact remains byte-identical.

## Known generator concurrency debt

The generated-combat migration does not silently generalize to unrelated
pipelines. Mission graph, Arete movement promotion/aggregation/verification,
Subway content generation, legacy loot seed export, and WebCore compatibility
still have documented snapshot or publication gaps. PHP/WebCore parsers also
retain hash-then-reopen windows. Dialogue content currently has read-only loaders
and no executable generator to migrate. These are separate, semantics-preserving
migrations; details and exact source references are recorded in
`docs/evidence/GENERATED_COMBAT_CONCURRENCY_20260802.md`.

## Supported playfields

- Arete regular-mob combat has explicit captured source/profile selectors while
  preserving runtime identity. Exact profiles are active for the supported Alex
  Waste Collector, Garbage Flea, and Cleanmeister cohort; level-1 Cleaning
  Robots; level-6 Desert Reets; level-5/6 Rollerrats; and ICC Peacekeepers.
  Engineer Automaton I, Robotic Guard Dog, and incomplete robot variants remain
  intentionally passive where exact combat evidence is insufficient.
- Rubi-Ka character creation persists its initial Arete location as PF 6553,
  X 3607.6, Y 52.4, Z 785.7 without integer rounding.
- PF127 Subway is complete for its current capture-backed population,
  navigation, combat, lifecycle, loot, vendor, Karrec, zoning, and teardown
  contracts. New behavior still requires capture evidence.
- PF1931 Temple is complete for its current ordinary/named population, dynamic
  doors, combat, lifecycle, loot, and navigation contracts. Unsupported nano
  selectors and unseen loot outcomes remain fail-closed. PF6553 Marcus ambient
  combat also fails closed before attack start while its captured start context
  is incomplete; supported mesh and burning-robot visuals remain active.

## Repository health

- Tracked configuration contains placeholders only and supports the ignored
  `AO_REBIRTH_MYSQL_CONNECTION` local environment override.
- The read-only database preflight uses the production configuration/connector
  path, verifies the exact database and 34-table contract, and blocks startup
  when any character is still marked online.
- Engine health is PID-owned: listener PIDs must resolve to the exact expected
  executables. Managed startup and shutdown never kill by process name alone.
- The approved Debug build includes LoginEngine, preventing creation-path
  changes from restarting against a stale executable.
- Newly created Rubi-Ka characters begin at the supplied Arete arrival point and
  receive a durable, one-time KnuBot choice from the dedicated ICC Shuttleport
  Commander between Arete and ICC Shuttleport. Existing characters and the
  official Shadowlands selector are unaffected; no database schema change is
  required.
- Managed start/stop status probes receive the repository configuration and
  engine directory on every invocation. Shutdown validates PID metadata against
  engine identity, executable path, start time, and released listener ports.
- The mandatory secret scanner rejects likely credentials without echoing
  values. Any credential exposed outside the repository still requires external
  rotation.
- WebEngine no longer downloads PHP, `php.ini`, or WebCore assets. It requires
  the complete official PHP 8.5.9 x64 NTS VS17 archive tree, the exact hardened
  INI, and an offline-imported final `htdocs` tree for CellAO WebCore commit
  `765c3850767b63af1cd259bab7f2f7ca3e97adf9`. PHP and WebCore are held under
  exclusive process-lifetime leases and revalidated before listener creation.
- The pinned WebCore archive is identified by SHA-256
  `ef297e623040b375e64c543568ca94e44ed7cc59de6fe826ed5e42db95c020ab`;
  its manifest covers 7,140 files and 26,648,501 bytes and has SHA-256
  `85c1515d274c2e4051013e89ca6d2a355365d5d01df7d621cc060dfa84e38463`.
- DotNetZip was removed. Archive extraction uses canonical-path containment;
  Zlib-only runtime paths use the isolated Ionic.Zlib package. Npgsql is 4.0.14.
- Three obsolete detached worktrees, the unowned Cursor export, 1,877 tracked
  temporary/decompiled files, and 74,054,821,216 bytes of disposable diagnostics
  and tools were removed after manifests and reachability checks.
- Git contains one 128.83 MiB pack, no loose objects, and no reported garbage
  after full integrity verification and native garbage collection.
- Line endings are explicit: maintained source/data use LF, Windows CMD/BAT
  launchers use CRLF, and binary formats are never normalized.

## Remaining debt boundary

- Rotate the previously exposed database credential externally.
- Perform authorized live WebEngine verification only after a valid disposable
  database credential is supplied. Current validation intentionally makes no
  live database connection and invents no credential.
- Replace or front the plaintext HTTP listener before considering secure-only
  cookies or production exposure. WebEngine remains development-only.
- Resolve the pinned WebCore snapshot's licensing before redistribution or
  production use. No license file was found upstream; integrity validation does
  not grant redistribution rights.
- Review `_tmp_mail_recovery` before any removal.
- Continue catalogued unsupported gameplay only with authoritative evidence;
  do not bulk-implement `NotImplementedException` paths or invent defaults for
  chase, quest deletion, action 59, anarchy playfields, perks, research, PvP,
  towers, teams, organizations, missions, quests, or pets.

## Operational workflow

- Run the complete local gate with `tools\run_mandatory_integration_gate.cmd`.
- Query live engine process/port health with `status-engines.cmd`.
- Validate database readiness with `preflight-database.cmd` before startup.
- Build with `tools\build_aorebirth_debug.cmd`.
- Stop/restart only with the approved root CMD wrappers.
- Supply WebCore assets only through the offline import and validation workflow
  in `docs/project/WEBCORE_ASSET_SUPPLY.md`; never restore a URL-backed archive
  bootstrap.
- Start optional WebEngine only with `start-web-engine.cmd`; it validates the
  database, binary, PHP runtime, and WebCore assets before launch. Stop it with
  `stop-web-engine.cmd`.
- Do not launch the AO client unless Mike explicitly requests it.
