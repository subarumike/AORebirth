# ZoneEngine_New supported gameplay gap closure — 2026-09-08

## Decision boundary

Starting candidate: `05c8d4ef7429e1ed765a19bd63ace0e21a2b29e5` on
`codex/zoneengine-new-gameplay-reconciliation` in the existing isolated
`AORebirth-zoneengine-new-full-integration` worktree. Starting master and merge base:
`6e90dda030774726aa2060acb9edb756ea1f635c`. The primary checkout is not the integration
workspace. Its pre-existing untracked files remain outside this change.

This is a safe, tested closure checkpoint, **not a complete Legacy replacement**.
The supported contracts below still contain concrete regressions; build success
cannot authorize the conditional master switch. Production deployment is separately
prohibited even if a later candidate becomes master-ready.

`ZONEENGINE_NEW_MASTER_READY=NO`

`MASTER_SWITCH_PERFORMED=NO`

The candidate's normal wrappers select `ZoneEngine_New`; the unchanged master and
production are not thereby switched. Legacy remains an explicit candidate rollback.

## Implemented source boundaries

- A playfield-owned accepted placement authority constructs **22 NPC actors**:
  Scarlett, six Subway merchants, Stan and Sarah, Marco and Lorelei, and eleven
  ungated Thrak/Aban garden vendors. Three accepted Arete world vending machines
  are separate static dynels, not fabricated NPCs. Two exact Arete quest props
  connect the Strongbox and shop-thief remains. No source hash, name, nearest-level,
  coordinate-proximity or generic combat fallback is introduced.
- Exact factories preserve the accepted level/health/appearance and source-specific
  stat overrides. Combat-unresolved social/vendor actors are not suppressed and
  do not acquire New's generic unarmed attack. Their shared Character death/corpse
  authority is retained; no new loot table, respawn schedule or combat AI is invented.
- Dialogue owns the exact player, transport, registered NPC object and playfield
  lifetime. Five typed packet handlers enter the existing owner dispatcher. Content
  is packaged offline; a dialogue action delegates to authored quest or shop services.
  Sixteen of the 43 registered Legacy dialogue identities have an implemented and
  activated domain route. Lorelei's shop is active but her specialized dialogue is
  not. Zyvania's accepted transport option remains blocked, not text-only success.
- Stan/Strongbox/factory and Sarah/remains/DNA armor hand-ins use existing ordered
  inventory/mission/stat transactions. Staged item reference and slot ownership,
  terminal state, reconnect and unknown-commit quarantine prevent replayed rewards.
  Accepted DOJA and Tailor adapters use the same services, not a dialogue-owned DAO.
- Captured stock is a sealed, one-time snapshot. Opening, offer mutation and commit
  each revalidate the exact accepted actor/machine, transport and world. An exact
  already-loaded static machine can receive its accepted stock only before any
  generation/open, with matching identity/template/position/heading; it is not
  overwritten. Missing catalog endpoints cannot produce a partial or random shop.
  The real packaged item catalog resolves all 19 commercial NPC shops and three
  standalone shops (1,335 stock rows), preserving each template's actual buy/sell
  modifiers. Scarlett, Stan and Sarah are quest hand-in actors, not shop vendors.
- Sparrow's exact nested child and attribute-to-skill/resource nano contributions
  are restored without ghost NCU rows, extra charges or guessed termination effects.
  Beacon Warp and Team Beacon Warp use exact catalog timing/cost, existing team
  ownership and a one-shot post-commit recipient projection.
- Cross-playfield movement now performs source departure and destination arrival
  on their respective owner ticks. A finite session handoff retains exact actor,
  transport and persistence authority through rollback, collision and shutdown.
- Generated mission key possession and cleanup admit the six existing owned
  top-level inventory pages, including bank, with full row checks. Repeated corpse
  use reproduces accepted close/reopen/acknowledgement timing while preserving
  the single durable currency claim and original corpse lifetime.

## Explicit reopenings

| Existing area reopened | Why required | Concrete proof |
| --- | --- | --- |
| NPC/session binding | Authored transactions had no trusted world actor entry point | `AcceptedNpcActivationService`, exact `ScarlettDalquistSpawn` and Arete source identities; copied names/IDs alone are rejected by object-reference tests |
| Accepted shop completion | A shop opened before owner replacement could otherwise complete afterward | `TradeService.IsCurrentAcceptedShop`; replacement/foreign-world/closed-session purchase tests |
| Static vendor capability activation | `LoadStaticDynels` precedes accepted actor activation and can already own the exact world machine | `SpawnService.CreateStaticDynel`, `Playfield.StartHeartbeat`; exact existing-object adoption regression test |
| Nano specialization and derived bonuses | Exact Sparrow child and shared attribute trickle were missing from the documented checkpoint | [Nano gap matrix](ZONEENGINE_NEW_NANO_GAP_CLOSURE.md), N01/N09 reproductions |
| Cross-playfield transfer | Synchronous reciprocal transfers could acquire source and destination tick locks in opposite orders | `ZoneSession.TransferToPlayfield`, finite `PlayfieldTransfer`, reciprocal barrier and writer-close tests |
| Generated artifact cleanup/key possession | Legacy searches all owned top-level pages, while checkpoint New checked main inventory only | [Mission gap matrix](ZONEENGINE_NEW_MISSION_GAP_CLOSURE.md), all-six-page and disposable owned-bank fixtures |
| Corpse repeated-use UI | Coalescing repeated uses omitted accepted close/reopen acknowledgements | Same mission matrix and `GeneratedMissionCorpseInteractionTests`; durable claim timing unchanged |

These are bounded correctness changes, not stylistic reorganization or a second
ownership/persistence system. No database schema file changes belong to this batch.

## Supported Legacy / New parity matrix

These are source-contract dispositions, not live-client certification. Test-suite
and exact-SHA execution results are recorded separately after the source freezes.

| Domain | Supported Legacy contract | New contract | Parity status | Intentional difference | Tests | Remaining gap / master blocker |
| --- | --- | --- | --- | --- | --- | --- |
| Sessions | One live owner per character; disconnect/reconnect | Exact transport, actor and global registration ownership | NEWENGINE_STRONGER | Stale owners cannot mutate a replacement | Session/ownership/transfer tests | No newly identified session contract blocker after coordinated gates |
| Playfield loading | Existing packaged geometry/static content and accepted placements | Same pinned package; explicit accepted actor adapters | REGRESSION | No guessed native-hash bridge | Package/official authorization/accepted actor tests | Broader accepted actor factories remain disconnected; see NPC row |
| NPC activation | Compiled world modules, fixed source rows and contextual factories | 22 exact static social/vendor actors plus existing generated mission actors | REGRESSION | Unresolved combat is separate from actor acceptance | Accepted NPC, merchant, garden and prop tests | Full accepted actor ledger/consumer expansion not complete; source inventory is not claimed exhaustive |
| Combat | Exact ordinary/boss profile, aggression, patrol, nano, corpse and loot contracts | Existing generated-mission policies; native explicit-level checks | REGRESSION | Never replace an accepted profile with generic fists | Captured resolver, mission combat, generated combat guard | 322 Subway and 167 Temple ordinary bindings still lack the full corresponding New runtime consumers |
| Dialogue | 43 registered accepted NPC identities, specialized quest/transport/vendor effects | 16 fully routed identities; exact per-player/NPC sessions | REGRESSION | Reject an unported action instead of publishing success | Dialogue and actual garden business/Use tests | 27 accepted specialized routes remain disconnected, including Zyvania transport and garden-key officials |
| Missions | Generated lifecycle, owned key access, durable rewards and authored chains | Existing SQL generated lifecycle; bank cleanup, corpse UI, Stan and Sarah routes | REGRESSION | No implicit occupied Legacy sidecar import | Generated lifecycle, corpse, authored/dialogue, disposable bank rollback | Remaining authored chains depend on the concrete disconnected dialogue/actor adapters, not theoretical retail generation |
| Teams | Team membership/ownership and supported selected/all-member warp | Existing team runtime plus exact owner-tick warp | PARITY | Stale team/session/generation work fails closed | Team and TeamWarp tests | No new team blocker; no team-wide dialogue ownership is introduced |
| Nano/morph | Existing supported specialization/function graphs | Supported checkpoint paths plus Sparrow, trickle and two warps | REGRESSION | No ghost child duration or unsupported resistance guess | NanoService, MorphNano, real catalog, TeamWarp tests | Six definite contracts N02/N05/N06/N07/N08/N10 remain; N03 is conditional on affected persisted rows. Exact contracts in nano matrix |
| Inventory | Existing exact owned item movement, use, upload and durable grants | Existing transactional inventory retained | NEWENGINE_STRONGER | Commit precedes success; unknown outcomes quarantine | Inventory and actual shop/quest transaction tests | No new general inventory blocker in this scope |
| DOJA | Existing accepted active quest and exact chip hand-in | Exact Scarlett dialogue/item transaction | LEGACY_BUG_NOT_PORTED | No false accepted QFU for an already completed nonrepeatable mission | Completed journal, dialogue and DAO rollback tests | A new repeat-cycle identity is future work, not permission to reset mission history |
| Trading | Existing player trade and accepted shop purchases/sales | Transactional trade plus sealed captured stock and live endpoint fences | NEWENGINE_STRONGER | No stale actor commit or unrelated random-stock fallback | Player trade, Subway, Arete standalone and garden shop tests | Gated/summoned vendor activation remains with its specific unported domain |
| Rewards | Supported mission/corpse/authored rewards exactly once | Existing commit/ledger authority; Sarah added | NEWENGINE_STRONGER | No success before commit or retry after unknown commit | Authored, mission, shop and disposable transaction tests | Unported authored chains are dialogue regressions, not duplicated ledger infrastructure |
| Corpses | Existing death swap, supported generated currency/use UI and actor-specific presentation | Shared death route; durable generated claim plus repeat-use UI | UNKNOWN | No guessed generated item pool or replacement death pipeline | Corpse interaction/lifecycle/reward and exact wire tests | Generated currency/UI is reconciled; general `Corpse.BuildSpawnMessage` still carries Biofreak-specific animation/texture constants, so social/ordinary corpse visual parity is not claimed |
| Persistence | Supported character, item and mission state | Existing normalized SQL and snapshots; exact bank CAS extension | NEWENGINE_STRONGER | No opaque parallel store or destructive import | DAO/schema/disposable rollback/restart tests | Occupied Legacy sidecars need separately governed production-transition import, not a master code gate |
| Shutdown/restart | Owned work cancellation and supported state reload | Existing drain/snapshot boundary plus finite transfer recovery | NEWENGINE_STRONGER | Late/stale ownership cannot seize another actor | Shutdown, transfer, nano restore and disposable engine restart | Exact-current-SHA acceptance required; prior checkpoint PASS is not reused |

## Remaining issue disposition

- `SUPPORTED_AND_RECONCILED`: the exact repairs and tests listed above, subject to
  the coordinated final execution results; this does not promote the entire domain.
- `UNPROVEN_AND_BLOCKED`: generic hostile resistance, undefined recipient/timing
  semantics, generated corpse item contents, partial-completion token probability,
  unproven restart-open packet changes and missing mission navigation geometry.
- `FUTURE_WORK`: production occupied-sidecar import; a properly identified repeating
  DOJA cycle; full server-restart pet restoration beyond Legacy's in-process stash.
- `INTENTIONALLY_UNSUPPORTED`: speculative NPC identity/level/combat fallbacks and
  any historical-capture runtime dependency.
- `REQUIRED_REGRESSION`: the specific accepted ordinary/NPC consumers, 27 dialogue
  routes and six definite nano review units above, plus conditional N03 persisted
  morph handling where affected rows exist. These are **not** relabeled
  future work or unproven features merely because this checkpoint does not port them.

The shared general corpse projection is an additional required presentation audit:
its explicit Biofreak constants are not evidence for every accepted NPC. Lifecycle
tests prove one shared corpse and exact ownership cleanup, not universal CFU visual
parity. The dedicated accepted generated-mission corpse projection is unchanged.

The NPC source census separately enumerates compiled modules, fixed source initializers,
combat bindings, official placement authorizations and dynamic factories. Overlapping
views are not additive counts. An unexpanded consumer is assessment-pending, not
proof that accepted data is absent. See [NPC inventory](ZONEENGINE_NEW_NPC_ACTIVATION_INVENTORY.md)
and its machine-readable JSON; [dialogue](ZONEENGINE_NEW_DIALOGUE_GAP_CLOSURE.md),
[nano](ZONEENGINE_NEW_NANO_GAP_CLOSURE.md), [mission](ZONEENGINE_NEW_MISSION_GAP_CLOSURE.md)
and [Subway merchant](ZONEENGINE_NEW_SUBWAY_MERCHANT_GAP_CLOSURE.md) reports retain
source and field-level references. Prior checkpoint reports remain historical.

## Validation and production boundary

Source-level Windows execution for this checkpoint:

- Full governed Windows build and normal New-engine build: PASS.
- New-engine tests: PASS, 420/420, including the real packaged shop catalog and
  pricing; startup validation: PASS. This is 112 more executed cases than the
  starting 308-case checkpoint, not a claim that every additional case is a new
  test method.
- Full mandatory integration gate: PASS, 12/12. Its complete AOtomation suite:
  PASS, 1,129/1,129; generated combat runtime contracts: PASS, 53/53.
- DAO guard/self-tests, generated combat check, pinned playfield package and
  source inventory guard: PASS. No schema file changed.
- Fresh Release/disposable schema, migration, transactional bank cleanup,
  rollback and engine start/stop/restart checks: PASS, with no container/network
  residue and no production contact. That run preceded the final quest-hand-in
  capability metadata correction; final-SHA disposable validation must rerun.

Logs are ignored execution artifacts under `build-verify/zoneengine-gap-closure-*`.
The final `build-verify/zoneengine-gap-closure-final-results.md` records the actual
committed SHA, Windows then Linux execution, exact package/placement hashes and
final branch/master status; it must not substitute a dirty-tree or previous-SHA
result. No current candidate is approved by the previous `05c8d4ef` acceptance.
Any Linux source repair requires a new commit and Windows first.

`PRODUCTION_DATABASE_MODIFIED=NO`

`PRODUCTION_MIGRATIONS_APPLIED=NO`

`PRODUCTION_SERVICES_CHANGED=NO`

`PRODUCTION_DEPLOYMENT_PERFORMED=NO`

`LIVE_CLIENT_TEST_PERFORMED=NO`

`PUBLIC_NETWORK_EXPOSURE_CHANGED=NO`
