# ZoneEngine_New full-integration candidate evidence

Status: IN PROGRESS / NOT APPROVED FOR MASTER OR PRODUCTION.

## Provenance and protected state

Starting Windows master/origin master:
`6e90dda030774726aa2060acb9edb756ea1f635c`.
Integration base: `307e87670f9d26b50b1ed26e019684726600c52c`, containing the prior
Delmus reconciliation. Master is an ancestor of that base; no merge conflict was
needed to create this worktree. Candidate branch:
`codex/zoneengine-new-full-integration`. Worktree:
`C:\Users\Mike\Documents\AORebirth-zoneengine-new-full-integration`.

Primary checkout untracked work (`Tools/NPC_Inspect/`,
`docs/reference/enemy-templates/`, `quest example from PRK.txt`) was preserved.
Other integration/mission worktrees were not changed. Production binaries,
services, DB, migrations, public configuration and client processes were untouched.

## Root causes and repairs

- The previous reconciliation compiled NewEngine separately while governed
  Windows/Linux defaults still selected Legacy. Candidate defaults now select
  New; Legacy requires explicit selection, with ownership-aware rollback.
- New startup could bootstrap/create/apply schema and prompt. These classes are
  removed; a read-only shared readiness assembly and separate acknowledged
  operator tool own distinct responsibilities. Legacy schema startup mutation is
  also removed. See the transition plan for the exact three-migration order.
- Invalid ZoneIP could fall back to a public bind. New now uses the shared
  Loopback/Public bind policy; advertised ZoneIP and configured ports validate
  separately. New .NET configuration loading fails closed on both OSes.
- Headless failure previously waited for console input and returned success.
  New now returns nonzero, reports schema/package states, handles shutdown files
  and SIGTERM, and emits systemd READY only after initialization/listen succeeds.
- New did not hold the established cross-process online ownership lease.
  The same lease contract is reused without loading the Legacy runtime.
  Closed/late/duplicate session handling, failed-spawn online cleanup, durable
  character/stat snapshot transactions, and indeterminate-commit quarantine are
  covered by focused tests and disposable DAO checks.
- Trades acknowledged completion before independent participant writes.
  Inventory, uploaded nanos and both participants' cash now commit through one
  DAO transaction before success packets. Definite failure retains the offer and
  cash; indeterminate commit quarantines persistence instead of risking a retry.
  Shop purchases cannot charge for memory-only overflow delivery.
- HashSpawnSystem accepted extracted placement hashes and substituted `AAAA`
  when an identity was missing. Automatic hash spawn now requires the exact
  accepted placement, activation/behavior/identity authority and an explicit
  evidenced New template bridge. Missing bridges/templates remain blocked.
- Linux source omission and SQL counts are replaced/supplemented with exact
  source/asset identity, casing and hash-set checks plus negative fixtures.
- DotNetZip is replaced with BCL ZLibStream for the existing compressed item
  slices and matching extractor writer. Removing DotNetZip also removes its
  transitive vulnerable Drawing 4.7.0 dependency chain. Redundant CodePages
  package references are removed while .NET's CP1252 provider remains registered.
  Frozen old-format zlib bytes and CP1252 decoding are regression-tested.

## Configuration and ownership mapping

| Concern | New Windows / Linux contract | Legacy / transition note |
| --- | --- | --- |
| Database | `AO_REBIRTH_MYSQL_CONNECTION` overrides Config.xml; SQLType MySql | No new gameplay-to-SQL dependency; repositories own parameterized commands |
| Database target | `AO_REBIRTH_EXPECTED_DATABASE` must match the connection and selected database; mandatory in the MySql deployment profile | Wrong-target validation and normal startup refuse before writes |
| Schema operator | Separate `AO_REBIRTH_MIGRATION_CONNECTION` and tool | No engine assembly references operator; no startup migration/recovery |
| Config source | `AO_REBIRTH_CONFIG_PATH`, otherwise packaged Config.xml | No workstation/production path embedded in New |
| ZoneIP | Concrete advertised IPv4, not listener bind policy | Existing LoginEngine redirect contract retained |
| Bind | `AO_REBIRTH_BIND_MODE`, unset Loopback; invalid values refused | Public remains explicit; no public change performed |
| ZonePort / CommPort | Exact configured valid ports | No port renumbering or fallback on invalid values |
| Chat / ISCom | Config ChatIP/CommPort; shared ISCom link | Portable .NET keepalive, no Windows-only IOControl on Linux |
| Login ownership | Existing `AO_REBIRTH_SESSION_OWNERSHIP_DIR` lock contract | One character authority through reconnect/transfer; no invented ticket fields |
| Logging | NLog plus explicit stdout/stderr paths; secrets not echoed | Operator tool error output uses stable classifications/numbers |
| World | Packaged `GameData` only; no runtime repository/client path search | Offline extraction is a build input, never client RDB runtime access |
| Locality | Existing shared Locality settings and New policy | Not a new license to activate unproven NPCs |
| Lifecycle | One New root service container, playfield child scopes, explicit registrations | Legacy not loaded alongside New; explicit rollback must be exclusive |

## Packet and gameplay audit: unresolved promotion gates

Both platforms compile the same governed AOtomation packet source and NewEngine
handler registrations; the exact source guard detects platform/source omissions.
Serialization tests remain required. No packet layout or new authentication field
was invented. `ZoneLoginMessage` contains CharacterId only in the shared model;
the prior cookie TODO cannot safely be fixed by making up wire fields. This is
not proof of stronger authentication than the existing contract.

The actual New dispatcher has ZoneLogin plus eleven gameplay handlers:
CharDCMove, CharacterAction, CharInPlay, LookAt, Attack, StopFight, GenericCmd,
ClientMoveItemToInventory, ClientContainerAddItem, Trade and Text. The router
rejects duplicate handlers and drops unsupported decoded bodies. A compiled
packet model does not mean its gameplay route is implemented.

| Area | Evidence / disposition |
| --- | --- |
| Nano casting | Legacy CharacterAction.CastNano calls Controller.CastNano; New CharacterAction lacks that case |
| Teams | Legacy CharacterAction handles invites/replies/kick/leave/leadership; New lacks those branches |
| Mission gameplay | Legacy mission services and request/use routes are not wired into New's smaller dispatch/GenericCmd path |
| Delete/split/use-on-item/reload/perks | Legacy CharacterAction branches exist; New currently only handles a small action subset; no guessed replacement semantics |
| Inventory | Carried/bank/container identity and dirty persistence paths retained; nine trade/shop failure/success tests and actual DAO rollback exercise durable boundaries |
| Stack semantics | Stored StackCount is preserved by migration/DAO; split/merge gameplay parity is NOT established by that proof |
| Movement / locality | New owns cell/tick/movement systems; no Legacy listener subscriptions loaded, but no live-client parity proof |
| NPC activation | All 199 currently authorized placements lack the explicit New template/evidence bridge; zero automatic hash spawns are authorized by the New gate. Raw hash/coordinates/template availability are not authority; visible Legacy NPC parity is not achieved |
| Accepted encounter combat | New uses generic DamageCalculator, not the accepted generated enemy-combat preparation/quarantine contract used by Legacy Subway encounters |

These are known functional migration gaps, not build failures to suppress. Master
promotion must not silently replace the accepted Legacy gameplay with missing
features or fabricated profiles. No `FULLY_INTEGRATED=YES` claim is justified yet.

## World supply investigation

A clean checkout contains tracked root GameData catalogs/items.dat but excludes
`AORebirth/GameData/Playfields/` via .gitignore. The primary checkout's ignored
PF4582 folder held only metadata and a height image; it was not a complete package.

The existing `Tools/extract-rdb-tilemaps.cmd` self-test passed. Without an explicit
source it correctly refused an unset AO_CLIENT_PATH. Using the installed
`D:\Funcom\Anarchy Online` as a READ-ONLY offline resource input succeeded:

```cmd
cmd /d /c Tools\extract-rdb-tilemaps.cmd --ao-path "D:\Funcom\Anarchy Online" --skip-monster-data --skip-items-dat --output-dir C:\Users\Mike\Documents\AORebirth-zoneengine-new-full-integration\AORebirth\GameData\Playfields
```

Initial PF4582 extraction: 9 files. Full extraction: 4,701 new files, 9 retained,
0 failed (922 tilemap artifacts, 1,260 district/spawn artifacts, 2,528 dat artifacts).
No game process was launched and no installed file was modified. The existing
root catalogs/items.dat were deliberately not overwritten.

This supplies 4,710 files / 264,151,897 extracted bytes. The reviewed manifest and
extraction provenance are committed under `docs/generated/playfields/`; the raw
files and immutable archive stay ignored. Archive SHA256:
`6b3c06c234923d07a8d476b710d650e620aafd0775602c2e38160af8bdc69b26`.
File-index SHA256:
`aa7dca68a698a720c7781039d92b680d7eb62d89577d83e2ab9c1959149efb67`.
The shared read-only runtime validator checks every path, size and SHA against
the pinned manifest. The offline importer refuses nonempty targets, verifies the
whole archive and staged tree, and rejects missing/extra/case/link/traversal
entries. Seventeen package fixtures, real import and deterministic re-export
passed. Linux acceptance requires an explicit archive outside its cleaned
checkout; there is no implicit download or production/client path search.
See `docs/project/PLAYFIELD_PACKAGE_SUPPLY.md` for commands and provenance limits.
Raw extraction does not promote NPC identities/behavior. Schema/lifecycle,
clean-checkout/source-SHA and gameplay acceptance remain separate claims.

## Validation evidence

Validation is still being assembled. Counts/results below are the latest
completed observations, not final source-SHA acceptance:

- Governed generated-combat integrity: PASS,
  `9d7fe7bd3b8a4808dde4b999fd3ce009db33f83e1c299e753b92a88b299b01a6`.
- Source/package guard: PASS, including four negative asset sets and two actual
  omitted-source/platform-drift fixtures.
- Production deployment Bash fixtures (local fake services only): PASS 56/56.
- RDB exporter self-test and complete offline extraction: PASS.
- Complete Windows build and all 12 mandatory gate stages: PASS; New focused
  tests 89/89; AOtomation 1,127/1,127; DAO guard seven registered SQL sites,
  seven existing exceptions, zero new violations. No new DAO exception was added.
- Disposable MySQL: PASS for missing/current/incompatible schema, wrong expected
  database, normal startup refusal with unchanged DB, explicit ordered migration,
  idempotence and failed-second-copy rollback/retry. Character snapshot and
  two-party trade atomic commit/late-failure rollback passed against actual MySQL.
- Disposable loopback runtime startup, clean shutdown and restart: PASS, with
  schema/data fingerprints unchanged. No production connection was used.
- New dependency audit: zero vulnerable direct/transitive packages reported.
- Windows exact-SHA and same-SHA Linux acceptance: NOT YET PASSED.
- Live-client/gameplay acceptance: NOT PERFORMED and not authorized.

Disposable harness deletes only its own exact labeled Docker container/network
and temporary files. It compares schema definitions and table rows before/after;
transaction rollback tests exclude only MySQL's nontransactional AUTO_INCREMENT
reservation counters, not application data or explicit item identity sequence.

## Files inspected and changed

Inspected: the two integration request attachments; mandatory startup/project
instructions; prior integration/master ancestry/status; New Program, startup,
network/session/dispatch, GameData/HashSpawn/SpawnService, inventory/trade and DAO
paths; shared ownership/config/ISCom contracts; Legacy Login handoff and actual
CharacterAction branches; SQL and schema paths; Windows launch/build/acceptance;
Linux projects/inventory/publish/systemd/release fixtures; approved RDB extractor
and raw/exported PF4582 metadata; governed official placement model/catalog.

Changed groups: New runtime/schema/lifecycle/trade/activation gates and tests;
shared schema/ownership/config/ISCom; explicit migration tool/disposable harness;
Windows wrappers; Linux source/package/release/rollback infrastructure; dependency
reader/writer replacements; this evidence and transition/workflow/project docs.
The final commit diff is the exact file list; generated combat/official placement
artifacts were not manually edited. Extracted world files remain ignored.

Workflow deviations during investigation included read-only missing-path guesses,
an oversized single-line JSON search result and one WSL-vs-Git-Bash invocation
failure; these were execution errors, not repository blockers, and changed no
project/production data. The documented Git Bash command subsequently passed.

## Production boundary and next decision

The schema migration/release/rollback plan is
`docs/project/ZONEENGINE_NEW_TRANSITION_PLAN.md`. It intentionally contains no
approved production SHA because gameplay acceptance is not complete. The pinned
world supply is resolved. Resolve evidenced New profile mappings and required
gameplay routes, then
rerun the entire Windows suite followed by Linux at the same exact SHA. Only
then may master be advanced without force push. Existing production stays intact.
