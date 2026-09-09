# ZoneEngine_New dialogue gap closure

Baseline: `05c8d4ef7429e1ed765a19bd63ace0e21a2b29e5`, isolated
`codex/zoneengine-new-gameplay-reconciliation`; primary master, production and client untouched.

## Reopened boundary and proving blocker

```text
EXISTING_AREA_REOPENED=Authored quest entry routing and daily-cycle acceptance only
WHY_REQUIRED=Accepted NPC interactions cannot reach the checkpoint's durable Stan/Scarlett methods
BLOCKER_THAT_PROVED_IT=New has no KnuBot open/answer/close/trade handlers or dialogue owner; terminal DOJA state cannot authorize another daily generation
```

The checkpoint's inventory, trade and authored transaction coordinators remain authoritative.
Dialogue must never grant items, apply rewards, commit SQL, clear terminal history, or retry an
uncertain commit independently. New world authority is an exact `NpcCharacter` binding supplied
by `AcceptedNpcActivationService`, not a name, captured number, nearby template or generic NPC.

## Exact baseline inventory

`ContentDrivenNpcDialogueRouter.cs:646–691` registers **43** NPC dialogue identities.
`AreteFrameworkBootstrap.cs:43–58` loads twelve checked-in manifest trees, containing **44**
dialogue NPC definitions. The additional Prince Creehan definition (`SimpleChar:78CCD541`) has
no registration in the compiled router: data presence alone is not an accepted activation route.
Dialogue JSON owns text, options, node transitions and capture provenance; it does **not** own
spawn placement, template, level or stat variants. Those must come from the separately audited
NPC activation sources. No raw capture is needed at runtime.

Baseline classification for the 43 registered rows below: **LEGACY_ONLY** (accepted compiled
dialogue behavior; no New dialogue owner/handler). A row becomes **ACCEPTED_BUT_DISCONNECTED**
for activation only when the NPC audit establishes its exact world binding. A text-only port is
not full quest parity: specialized side effects listed below require real domain adapters.

All rows open through inbound `KnuBotOpenChatWindow` (or the existing NPC use route), answer
through `KnuBotAnswer`, and close through `KnuBotCloseChatWindow`. Response text/options are
the unchanged referenced JSON, not placeholders. Each node's exact `Index`/`NextNodeId` owns
the transition; a hidden `(Continue after trade)` option must not become a fake Goodbye.

| NPC | Content identity suffix (`SimpleChar:`) | PF | Accepted content / quest / vendor linkage |
| --- | --- | --- | --- |
| Rex Larsson | 782DE568 | 6553 | Arete/rex-larsson; Rex/Marcus chain coordinator and QFU |
| Marcus Stone | 782DE567 | 6553 | Arete/marcus-stone; Rex/Marcus chain coordinator and trade |
| Flint Novak | 78E0FC64 | 6553 | Arete/flint-novak; Kneecapping chain |
| Alex Gibbs | 78E0FC61 | 6553 | Same pack; Kneecapping/robot-brain trade and vendor |
| ICC Immigration Officer Bill | 78E0FC66 | 6553 | Same pack; immigration trade |
| Stan Goodman | 78E0FC65 | 6553 | Same pack; lockpick/factory chain and NPC trade |
| Sarah Greene | 78E0FC69 | 6553 | Same pack; Sarah quest/trade |
| Vernon Godfray | 78E0FC68 | 6553 | Same pack; Vernon quest/trade |
| Dr. Mason | 78E0FC6C | 6553 | Same pack; Mason quest/trade |
| Lorelei the Bartender | 78E0FC6B | 6553 | Same pack; Lorelei/Lolly quest, trade and vendor |
| Lolly the Reet | 7985CAEC | 6553 | Same pack; Lorelei/Lolly quest |
| Shipping Manifest Terminal | 78E0FC6A | 6553 | Same pack; shipping-manifest trade |
| Marco Spida | 78E0FC81 | 6553 | Same pack; accepted nano-package vendor |
| Vaughn Hammond | 78E0FC73 | 6553 | Same pack; Vaughn trade |
| Windcaller Karrec | 796360BB | 655 | Subway/windcaller-karrec; durable offering quest/trade |
| Annoying Dude | 796360BD | 655 | Same pack; active Karrec quest gate |
| Maddy Cardile | 796360BC | 655 | Same pack; active Karrec quest gate |
| Tailor | 79135F51 | 127 | Subway/tailor; first/repeat-open nodes, measurement grant |
| Scientist Veronica Escobar | 787B54B2 | 4310 | Thrak/garden-key; analyzer/key chain and recovery |
| Prophet Yutt Thrak | 78D280F6 | 4311 | Same pack; analyzer/insignia chain |
| Hypnagogic Urga-Lum Thrak | 79758F3A | 4677 | Same pack; quest-stage gate and soul/key trade |
| Dreaming Silvertail | 797652A0 | 4310 | Same pack; Thrak/Aban soul-trade gate |
| Craig-Or of Furious Fists | 79758F3F | 4677 | Thrak/garden-vendors; exact business option opens shop |
| Craig-Or of Preservation | 79758F3E | 4677 | Same pack and vendor branch |
| Craig-Or of Flaming Barrels | 79758F3B | 4677 | Same pack and vendor branch |
| Craig-Or of Gear & Ammo | 79758F3C | 4677 | Same pack and vendor branch |
| Craig-Or of Protection | 79758F3D | 4677 | Same pack and vendor branch |
| Son-Len, Official of Power | 79758F40 | 4677 | Same pack; completed garden-key gate, accepted refusal chat |
| Or-Mada of Furious Fists | 7A2013B7 | 4676 | Aban/garden-vendors; exact business option opens shop |
| Or-Mada of Protection (near Preservation) | 7A2013B4 | 4676 | Same pack; distinct accepted identity, no name merge |
| Or-Mada of Preservation | 7A2013B5 | 4676 | Same pack and vendor branch |
| Or-Mada of Flaming Barrels | 7A2013B8 | 4676 | Same pack and vendor branch |
| Or-Mada of Protection (near Gear) | 7A2013B6 | 4676 | Same pack; distinct accepted identity, no name merge |
| Or-Mada of Gear & Ammo | 7A2013B9 | 4676 | Same pack and vendor branch |
| El-Mada, Official of Consistency | 7A2013BA | 4676 | Same pack; Aban garden-key gate, no invented refusal chat |
| Scarlett Dalquist | 7A18B924 | 7010 | Doja/nascense-chip; exact one-slot turn-in trade |
| Dr. Rosenblatt | 7A18D419 | 4310 | Nascence/rosenblatt-hiathlin; existing Hiathlin/disc/nano chains |
| Scientist Drake Rodriguez | 7A1E3C24 | 4001 | Nascence/life-dialogs; bracer/Donna quest |
| Joshua Falker | 7A18D424 | 4310 | Same pack; silvertail/chimera quests |
| Scientist Donna Red | 7A18D4B1 | 4310 | Same pack; Ancient Device/Aban garden-key start |
| Ecclesiast Aban Fala | 7A1B033F | 4312 | Same pack; accepted quest gate, device/insignia trade |
| Sipius Aban Lux-Wei | 7A2013BC | 4676 | Same pack; accepted quest gate and artifact trade |
| Zyvania Bagh | 7976BCF3 | 655 | Andromeda/zyvania-bagh; accepted dialogue-triggered transport to PF716 |

Arete's fourteen registrations preserve the existing default-enabled
`AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING` gate; Tailor preserves
`AO_REBIRTH_ENABLE_SUBWAY_TAILOR_DIALOGUE_ROUTING`. Other router registrations are ungated.
The fixed PF/source identity values above are from the router and its named InteractionRules;
Windcaller, Tailor and Aban additionally require the existing runtime-bound spawn registries.

### Additional compiled routes, not silently counted as registered content

- `NewCharacterStartAreaSelectionRuntime` intercepts answer/close before ordinary NPC dialogue.
  This is a distinct creation flow, not an accepted NPC identity inferred from the content list.
- `Scripts/InfoBot.cs` and `Scripts/PerkResetService.cs` attach concrete KnuBots;
  `PerkResetServiceProviderSpawn.cs` is an actual world factory. Their supported activation and
  dependencies require their own classification; arbitrary GM `ChatCommands/Npc.cs` script
  attachment is not evidence of another accepted placed NPC.
- `PlayfieldDbMobSpawnRuntimeService.cs` can attach an explicitly configured script. Database
  script configuration is not reconstructed from names, and no production DB is read here.
- Prince Creehan remains **UNPROVEN** for runtime activation despite checked-in dialogue data.

## Bounded repair plan and acceptance

1. Reuse the validated content registry and existing pure dialogue transition service. Keep
   specialized quest conditions/actions outside the session owner; no no-op reward success.
2. Pin one exact player, transport session, NPC object and playfield object. Reject stale,
   replaced, dead, quarantined, distant or unregistered endpoints. Remove queued dialogue and
   staged trades on disconnect, zone, despawn, death and shutdown; teams never own sessions.
3. Preserve accepted Open/Append/AnswerList/Close fields, prompt segments, player substitution,
   wire option ordering, trade-hold suppression and nonblocking owner-tick packet pacing.
4. Bridge exact Stan/Scarlett nodes to existing `AuthoredQuestService` transactions. Stage the
   exact owned item object/location without removing it; completion must revalidate both
   endpoints and that same row. Publish accepted trade only after confirmed durable commit.
5. Bridge only explicitly supported vendor nodes to the accepted actor's real shop capability.
   Do not mark every NPC with a dialogue tree as a vendor or bypass garden-key gates.
6. Preserve the actual common mission terminal-state contract; never manufacture a new DOJA
   cycle or erase reward history merely because Legacy displayed a fresh journal entry.

Validation was pending at this initial inventory checkpoint. The final focused validation is
recorded below; it does not imply that all specialized Legacy quest effects have been ported.

## Implemented checkpoint and daily classification

The first checkpoint's thirteen new dialogue tests and all existing authored tests passed in
the 349-case focused run (overall gate still failed seven independently owned Sparrow tests).
New now has the five KnuBot inbound routes, player/transport/NPC/playfield-owned sessions,
owner-tick pacing, and transfer/logout/despawn/shutdown cleanup. Stan and Scarlett trades call
the existing complete durable inventory/mission/reward transaction before acknowledgement;
stale item, rollback, unknown-commit quarantine and replay are covered. Tailor measurement
grants use the exact existing eight item IDs and atomic inventory boundary. This checkpoint
has explicit domain adapters for fifteen registered NPCs, not forty-three complete quest ports.
Accepted actor activation and real stock capabilities remain separately required.

DOJA daily-repeat is **LEGACY_BUG_NOT_PORTED**, not a newly missing supported runtime feature:
`DojaChipQuestRuntime.cs:151-161` treats `AlreadyApplied` as fresh acceptance and sends a QFU;
`PersistentMissionService.cs:102-113,184-194` returns that status for a **Completed** mission
without reactivating it; `DojaChipQuestRuntime.cs:186-191` still requires **Active** for turn-in.
Cooldown expiry only completes the cooldown mission (`DojaChipQuestRuntime.cs:222-230`). Thus
the accepted Legacy implementation cannot complete a second turn-in either. New's Active-state
guard deliberately suppresses the false journal success, retaining terminal state and reward
history. The common-service regression proves no mutation after two days; the existing New
regression proves no chip consumption, reward or QFU. Real repeat generations are a future
feature, not an excuse to reset fixed-key histories or a New-versus-Legacy readiness blocker.

### Bounded Sarah chain extension

EXISTING_AREA_REOPENED=authored dialogue and item quest transitions

WHY_REQUIRED=Stan's accepted delivery points directly to Sarah, but no New Sarah branch exists.

BLOCKER_THAT_PROVED_IT=`SarahGreeneQuestRuntime` has compiled acceptance, exact thief-remains
use and armor turn-in through Vernon handoff; the initial New router deliberately rejects Sarah
because playing her text without those effects would falsely claim quest success. The repair
reuses the existing authored DAO transaction and accepted QFU builders, with no schema change.

The added Sarah adapter commits `TalkSarah → FindThief → DeliverArmor → TalkVernon`, the exact
295618 QL200 armor grant, source-row retirement, 296574 QL1 reward and +2229 XP/+1280 credits in
the existing transaction per action. Generic use acknowledgement and NPC trade acceptance are
post-commit callbacks; rollback sends neither. The existing journal builder constants, strings
and three constructors were moved unchanged from `SafeQuestFullUpdateSender.cs` into its already
shared `AuthoredData` partial. No packet layout or generated data was re-created.

Stan's already implemented Strongbox method now accepts a post-commit acknowledgement callback.
Legacy `StanGoodmanQuestRuntime.cs:536-537` proves item slot is `GenericCmd.Target[0]` and the
Strongbox is `[1]`; root binds the exact source prop rather than accepting a matching name or
template alone. Neither Strongbox nor thief recovery may recreate a fresh quest item after its
fixed-key delivery is Completed. Legacy allowed that orphan recovery but cannot reactivate the
mission; the negative tests retain terminal history and the owned lockpick.

Added tests cover exact four-slot Stan trade, Sarah acceptance/recovery/one-slot trade/reward/
Vernon handoff, late rollback, unknown-commit quarantine, recovery acknowledgement ordering,
terminal replay, exact active-only Sarah journal restore, and common DOJA terminal behavior.
The root agent owns actual accepted Stan/Sarah/Scarlett/Subway actors, shop publication and
Strongbox/thief prop bindings; those tests are separate from these domain/session tests.

## Remaining supported dialogue gaps (not disguised as unknown captures)

The sixteen enabled content-domain adapters include eleven general Craig-Or/Or-Mada vendors;
their accepted actor/stock adapters now pass combined activation validation recorded below.
Twenty-seven registered NPCs still require their concrete specialized gate/effect or vendor
adapter at this checkpoint:

| Remaining group | Required existing behavior |
| --- | --- |
| Rex, Marcus, Flint, Alex, Bill | Initial Arete quest coordination, trades, immigration and accepted vendor effects |
| Vernon, Mason, Lorelei, Lolly, Shipping Manifest Terminal, Vaughn | Post-Sarah authored chain roots, item/object interactions, trade and completion effects |
| Karrec, Annoying Dude, Maddy | Durable offering quest, related active-quest gates and trade |
| Veronica, Prophet, Hypnagogic, Dreaming Silvertail | Thrak analyzer/key/soul quest state and exact trade gates |
| Son-Len, El-Mada | Completed garden-key access gate; different accepted refusal behavior |
| Rosenblatt, Rodriguez, Joshua, Donna, Fala, Lux-Wei | Existing Nascence/Aban quest roots, grants, objective progression and trade |
| Zyvania | Existing dialogue-triggered transport and accepted actor activation; not a text-only interaction |

These are **MISSING_REQUIRED_BEHAVIOR**, not a claim that all legacy helper stubs must be ported.
The exact registrations and compiled side-effect callsites are the evidence. InfoBot,
PerkResetService and character-start-area selection also remain separately classified compiled
routes; they are not silently counted as these forty-three NPC registrations. No name-only
identity bridge, placeholder dialogue, arbitrary condition bypass or no-op quest action was added.

## Accepted Arete vendor slice

`AcceptedAreteVendorCatalog` preserves two explicit `AreteLandingSpawn` social actors and their
separate accepted stock providers: Marco `78E0FC81 → VendingMachine:12E77212` has fourteen rows;
Lorelei `78E0FC6B → VendingMachine:12E7720B` has thirty-eight rows. Their exact level 10, health
227, transform, appearance, textures/meshes and Social combat behavior are retained. Lorelei's
shop is supported independently; her specialized dialogue/quest chain remains in the gap table.
Marco's accepted blank root and two close options are emitted unchanged, without inventing the
uncaptured nano explanation. Doctor/Enforcer package opening already uses the authored transaction.

The three Alex-area shops are actual standalone VendingMachine dynels, not fabricated NPCs.
All twenty-seven rows remain in exact source order. ICC Tech Supplies is source `12E77208`,
template `300946`, position `(3442.931,12.27642,822.4964)`, quaternion
`(0,0.7057894,0,-0.7084217)` and slot three sealed lockpick `295999` QL1. Those fields come from
`CapturedAreteAlexAreaVendorContentProvider`; root separately registers exact actor references
and owns shop lifecycle/authorization. Existing accepted stock is immutable and not rerolled.

The five catalog tests prove all seventy-nine stock rows, explicit actor appearance, exact
machine owner/identity separation, correct captured orientation, no source-name impersonation,
no copied definition authority and whole-shop rejection for any missing endpoint. A generic
fallback template is present in the negative fixture and cannot authorize a replacement. The
sixth test preserves Marco's exact dialogue behavior. No template interpolation, random stock,
new loot/progression values, database migration or production access was introduced.

## Garden vendor closure

`AcceptedGardenVendorCatalog` contains exactly five Craig-Or and six Or-Mada source placements,
their original immutable stock providers and exact endpoint identities. Both GenericCmd shop-icon
Use and dialogue option zero are accepted by the compiled Legacy handlers; both are covered by
the New actor/session/shop tests. Son-Len `79758F40` and El-Mada `7A2013BA` are absent from this
activation slice: their existing completed-key/owned-key checks cannot be replaced by ordinary
vendor access. Or-Mada Protection `7A2013B4 → 130B7810` and `7A2013B6 → 130B7812` retain separate
positions, orientations and stock despite identical display names.

The garden factories' explicit BART seed is preserved from
`NonPlayerCharacterHandler.cs:181-199` and `SqlTables/mobtemplate.sql:41`, then the exact garden
row overrides are applied. Unset source head/texture fields are not treated as captured zero:
the Legacy factory retains head stat40694, never copies the SQL texture columns, and the garden
clears mesh layers to its one declared mesh. New suppresses its automatic additional head mesh
for this adapter. Craig-Or retains seed speed513; Or-Mada explicitly overrides103. Both compiled
factories quarantine outgoing combat, and their Character.Read timer suppression remains intact.
No generalized BART resolver, name match, nearest-level rule or new enemy profile was introduced.

The seven new garden tests cover all eleven original stock sets row-for-row, typed source and
shop ownership, both accepted shop entry routes, exact appearance/mesh arrays, the two Protection
variants, missing endpoints, stale-actor rejection and exclusion of key-gated officials. Root owns
the common playfield activation composition and exact bound-shop lifecycle. The prior 386-case
gate passed all authored/dialogue cases; one Arete test compared boxed uint expectations against
the existing int Texture.Id property. Its assertion types were corrected without changing values
or runtime behavior. The full gate also had twelve independently owned failures; no overall PASS
is claimed from that run.

### Readiness accounting, not a master-ready claim

| Count | Meaning after this source slice |
| --- | --- |
| 43 | Existing registered content NPCs inventoried above; Prince Creehan remains data-only |
| 16 | Enabled content/domain adapters: Stan, Sarah, Scarlett, Tailor, Marco and eleven general garden vendors |
| 17 | Registered-content actor definitions in accepted activation: previous list plus Lorelei |
| 16 | Intersection with both a supported dialogue-domain adapter and an accepted actor definition |
| 22 | Total accepted social actor definitions in the composed activation catalog, including five Subway shop-only actors |
| 19 + 3 | Exact NPC-owned shop definitions plus three standalone Alex-area machine definitions |
| 27 | Registered specialized dialogue/domain adapters still missing (table above); Lorelei's shop does not complete her quest dialogue |

These are source/contract-fixture counts, not a claim that an AO client or production deployment
has been exercised. Startup must still possess every required stock/template endpoint, retain
exact identity ownership and pass the combined automated gates. **Dialogue readiness remains NO**
while the explicitly supported remaining quest chains or the Zyvania actor are disconnected.

Final source review corrected an initial audit error: Zyvania is not pure text. The Legacy router
calls `ZyvaniaBaghTransportRuntime.TryHandleDialogueAnswer` at `ContentDrivenNpcDialogueRouter.cs:1587-1592`;
that service consumes `zyvania_transport_offer` option zero and teleports to PF716 at
`(808,11.66283,2823)` with heading `(0,-0.9645079,0,0.2640538)`. New has no actor activation or
transport adapter for her, so the previously unreachable text-only allowance was removed.
The negative regression binds an otherwise accepted test actor and still refuses to publish any
conversation. This preserves a visible missing-required classification instead of false success.

### Exact remaining chain owners and missing adapters

The names, identities and playfields in the initial full inventory remain authoritative. These
are concrete compiled consumers, not placeholders inferred from class names:

| Accepted NPC | Existing owner/callsite whose New bridge is missing |
| --- | --- |
| Rex Larsson | `RexMarcusChainCoordinator.OnRexOpen/OnRexAnswer/ResolveRexStartNodeId` (router1845,2078,3819) |
| Marcus Stone | Same coordinator `OnMarcusAnswer/TryBeginMarcusReturnTrade`, wounded-worker return (router1505,2120) |
| Flint Novak | `FlintBioComQuestRuntime.TryHandleDialogueAnswer` (router1515), related accepted trade progression |
| Alex Gibbs | `KneecappingQuestRuntime.TryHandleAlexDialogueAnswer/ResolveAlexStartNodeId` (router1523,3688), robot-brain trade |
| ICC Immigration Officer Bill | `SurveillanceUplinkQuestRuntime.TryHandleBillDialogueAnswer` (router1531), accepted immigration trade |
| Vernon Godfray | `VernonGodfrayQuestRuntime.TryHandleDialogueAnswer/ResolveVernonStartNodeId` (router1555,3703) and item/trade handoffs |
| Dr. Mason | `DoctorMasonQuestRuntime.TryHandleDialogueAnswer/ResolveMasonStartNodeId` (router1563,3708) |
| Lorelei the Bartender | `LoreleiQuestRuntime.TryHandleDialogueAnswer/ResolveLoreleiStartNodeId`, delivery trade (router1572,2455,3751); shop alone is not completion |
| Lolly the Reet | Same Lorelei owner, distinct `ResolveLollyStartNodeId` and cookie trade (router2449,3756) |
| Shipping Manifest Terminal | `ShippingManifestTerminalQuestRuntime.TryHandleDialogueAnswer` (router1580) and exact terminal trade |
| Vaughn Hammond | `VaughnHammondQuestRuntime.IdOfferNodeId/TryBeginVaughnTrade` (router2479,2501) |
| Windcaller Karrec | Durable `WindcallerKarrecTradeAdapter.TryResumeDurableCompletion` plus completed/active/offering gates (router1937,1943) |
| Annoying Dude | Active `WindcallerKarrecQuestRuntime` prerequisite before opening (router1952) |
| Maddy Cardile | Same exact active-quest gate, separate accepted identity (router1952) |
| Scientist Veronica Escobar | `ThrakGardenKeyQuestRuntime` active analyzer recovery and trade (router1985,2651) |
| Prophet Yutt Thrak | Same Thrak owner, device-inspection/insignia/speech state (router3762) |
| Hypnagogic Urga-Lum Thrak | `CanTalkToHypnagogic` silent gate and soul/key stage (router1957) |
| Dreaming Silvertail | Thrak or Aban `CanUseSilvertailSoulTrade` eligibility and exact soul exchange (router2008,2009) |
| Son-Len, Official of Power | `HasCompletedGardenKeyQuest`, captured chat refusal and gated shop (router1964) |
| El-Mada, Official of Consistency | `HasAbanGardenKey`, silent refusal and gated shop (router1972) |
| Dr. Rosenblatt | Hiathlin/Cascading/other disc runtimes, start-node priority, trades and action-lock cleanup (router3168-3218,3711-3726,4001-4004) |
| Scientist Drake Rodriguez | `NascenceLifeRodriguezQuestRuntime.TryGrantBracerOnDialogueOpen` (router1993) and quest handoff |
| Joshua Falker | `NascenceLifeJoshuaFalkerQuestRuntime.AcceptBothKillQuests` (router3276) |
| Scientist Donna Red | `NascenceLifeDonnaRedQuestRuntime.ResolveStartNodeId` (router3731) and Aban device handoff |
| Ecclesiast Aban Fala | `NascenceAbanFalaQuestRuntime.CanTalkToFala/ResolveStartNodeId` (router1996,3736), device/insignia trade |
| Sipius Aban Lux-Wei | Same owner `CanTalkToLuxWei/ResolveLuxWeiStartNodeId` (router2002,3741), artifact trade |
| Zyvania Bagh | `ZyvaniaBaghTransportRuntime.TryHandleDialogueAnswer` / `TryTeleportToNelebEntrance`, sourcePF655 to destinationPF716 |

Current New authored definitions load the accepted Arete/flint and Doja manifests; additional
quest owners must use their actual accepted definitions, owner/session gates and the existing
single durable mission/inventory/stat transaction before effects are acknowledged. This audit
does not establish any new schema requirement for the remaining dialogue chains. No approval
blocker is invented: schema changes, if a future concrete slice proves them necessary, still
require separate approval; production access and client control remain outside this task.

## Final focused validation and bounded shared-corpse proof

`Tools\run_zoneengine_new_tests.cmd`: **PASS**, 420 passed, zero failed/skipped, acceptance
PASS. Evidence: `build-verify/zoneengine-gap-closure-final-tests.log:641,643`. This source
checkpoint includes all dialogue/authored, Arete/garden vendor, accepted actor/prop and shop
ownership tests plus the real packaged item-catalog acceptance below. The preceding 419-case
checkpoint remains recorded in `build-verify/zoneengine-gap-closure-verified-tests.log:73,75`.
Full Windows, mandatory and exact-SHA Linux gates are coordinated separately;
this result is not a deployment or client gameplay assertion.

`DialogueTests.RealTeamJoinLeaveAndRejoinCannotTransferDialogueOrStagedRewardOwnership`
uses the real `TeamService`, with synchronous callbacks on the test's single owner thread.
Invitation/acceptance, leave and rejoin do not give a teammate the character's NPC conversation
or staged DOJA chip. Foreign answer/close/stage/finish are rejected without DAO effects; only the
original owner consumes its exact chip and receives the once-only reward. The teammate's own
same-template item and level remain unchanged.

`AcceptedVendorDeathTests` has six passing cases, reusing the real accepted Subway/Marco actor
fixture and actual `SpawnService`, inventory action/move services, trade service and locality:

- Vendor death closes the live shop trade and returns its offered item before the 2.5-second
  corpse delay. Repeated death notifications/ticks create one corpse and one Died event.
- Old NPC/shop playfield ownership and the accepted binding are removed. A replacement registry
  owner survives both registry-only replacement and actual locality registration; its cell,
  observer membership and subsequent announcement remain intact, without a false despawn.
- Existing expiry and opened-empty cleanup remove corpses and close the loot window; unopened
  empty corpses remain. Test-only past deadlines avoid sleeping or changing production clocks.
- The observed corpse lookup explicitly returns missing CATMesh, never an invented visual ID.
  `StubGameData` retains its default throwing behavior for unrelated tests, proven by a separate
  regression. No persistence is performed by these death/cleanup fixtures.

The proving fixes are in root-owned `AcceptedNpcActivationService.OnDied` (detach the old exact
object without deleting its replacement) and `LocalityVisibility.Untrack` (reject stale-reference
cleanup before removing identity-indexed observer state). The fixture additions are
`AcceptedVendorDeathTests.cs`, `AcceptedSubwayShopRuntimeTests.cs`, `DialogueTests.cs` and the
explicit opt-in in `TestDoubles.cs`; these do not alter production corpse packets.

Remaining shared-corpse risks are explicit:

- **General corpse visual parity remains UNKNOWN.** `Core/Entities/Corpse.cs` still emits
  Biofreak-specific animation, sex/breed and default texture constants. Passing lifecycle tests
  do not authorize applying those visuals to every accepted vendor/NPC; dedicated generated
  mission corpse contracts are separate and unchanged by this slice.
- **Thrown corpse creation is not repaired or proven recoverable.**
  `Character.CompleteCorpseSwap` clears the pending flag before `SpawnDeathCorpse`, then raises
  Died only after successful creation. An exception can strand a dead actor without its final
  cleanup notification. No test suppresses that exception or claims a retry/rollback contract.
- Normal `SpawnService` expiry/empty cleanup does not prove safety for a separately replaced
  corpse identity: `DespawnLootable` still unregisters by identity. This slice proves stale
  accepted-NPC cleanup only; it does not certify unrelated corpse identity or cash durability.

The 27 missing supported dialogue chains remain blockers despite this green focused gate.
No schema changes, production access, AO client control or new capture promotion occurred.

### Real packaged vendor catalog and pricing acceptance

`AcceptedVendorRealCatalogTests.PackagedRealCatalogResolvesEveryAcceptedShopAndPreservesItsTemplatePricing`
uses the actual production `ItemTemplateCatalog` and packaged repository `GameData/items.dat`.
Its name repository is empty: missing IDs cannot be masked by SQL/name-only stubs or by
`StubCatalog.Add`. All nineteen commercial NPC endpoints plus three standalone machines resolve,
including every low/high endpoint across **1,335 frozen stock rows**. Missing endpoints: **none**.
The test then constructs all twenty-two exact accepted shops with the real templates and checks
their inherited BuyModifier426 and SellModifier427, without the transaction fixture's 100/50
overrides.

The ignored diagnostic artifact is
`AORebirth/Server/ZoneEngine_New.Tests/bin/Debug/net10.0/accepted-vendors-items.audit.json`.
Its packaged source SHA256 matches a direct hash of repository `AORebirth/GameData/items.dat`:
`12B0D9B3AAA55403074BC17457BE9D6A75A10A29D45E6392D77CC4904EE4922F`.

| Actual endpoint/template group | BuyModifier426 | SellModifier427 |
| --- | ---: | ---: |
| Eighteen endpoints using99637,99570,99574,99601,99634,297281,297459 | 4 | 105 |
| Subway source79135F52 / template99572 | 4 | 1000 |
| Marco78E0FC81 / template248371 | 4 | 90 |
| Lorelei78E0FC6B / template297371 | 3 | 100 |
| ICC Tech standalone12E77208 / template300946 | 2 | 105 |

These values are positive actual item metadata, not guessed normalized percentages. All six
relevant Legacy captured-vendor runtime families call the int-template `Vendor` constructor
(`Core/Entities/Vendor.cs:101-108`), which copies the same template stats. The different
hash-template constructor's DAO pricing override at lines92-97 is not this captured-vendor
binding. New `StaticDynel.ApplyTemplateStats` copies the exact template entries; the test proves
the accepted endpoint adapters preserve them. No price formula or vendor metadata was changed.

This additional acceptance first caught two incorrectly marked commercial capability records:
Stan and Sarah were HasVendor despite having quest hand-ins rather than shops. Root corrected
their commercial flags to false, preserving their dialogue and transactional quest-trade routes.
The composed metadata now agrees with the nineteen actual commercial NPC shops, not twenty-one.
Catalog availability and inherited modifier equality are now proven offline; production deployment
and live client shop behavior remain outside this task.
