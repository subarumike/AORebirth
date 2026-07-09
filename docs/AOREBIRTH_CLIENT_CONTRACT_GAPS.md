# AORebirth Client Contract Gaps

## Interpretation Rules

This matrix combines the 2026-07-09 `ghidra_complete` 40-DLL corpus with current
AORebirth source, tests, project decisions, and existing capture reports. A
function or RTTI name is never used as a packet layout by itself. Where a capture
and a decompiler inference differ, the direction-appropriate capture wins.

The word **generation** below means a correlated world/playfield transition, not
an inferred wire field. No exact client generation field has been proven.

## Protocol Stack Comparison

| Stage | Client evidence | Client contract supported by evidence | AORebirth implementation | Gap/status |
|---|---|---|---|---|
| Send size | `Connection.dll+0x0000186c` calls `Message_t::MessageSizeGet` before send | Length belongs to the serialized message sent | `MessageSerializer.Serialize` writes final stream length into the header at byte offset 6 | matched for outbound |
| Receive block | `Connection.dll+0x000019ba` processes control/compression and calls `MessageProtocol.dll::DataBlockToMessage` | Decode only a complete bounded block | Shared AOtomation deserialization requires an exact declared/actual envelope before login or zone dispatch | matched for one-buffer/one-message transport; batching remains unsupported |
| Family select | `MessageProtocol.dll+0x00001e81` selects Operator/Ping/N3/Text/System | Unknown families fail before gameplay dispatch | `PacketInspector.FindSubType` returns null for an unknown discriminator | matched |
| Verify | same function calls `Message_t::Verify` after construction | A constructed message still must pass central validation | shared preflight verifies header/declared size/discriminator bounds, then requires complete known-body consumption | size/consumption boundary matched; semantic verify rules remain unknown |
| Top-level dispatch | `Interfaces.dll+0x00002a9e` branches by family and sends N3 to the N3 module | Family state precedes subtype state | `ZoneClient` publishes a concrete `MessageWrapper<T>` to MemBus | matched architecture |
| N3 record parse | `N3.dll+0x000062fd` constructs queued `n3InfoItemRemote_t` records from a `BinaryStream` | N3 bodies are typed and independently parsed | AOtomation concrete classes/custom serializers | matched where modeled |
| N3 write | `N3.dll+0x00007762` calls each IIR writer | C2S field order is owned by the concrete IIR | per-message AOtomation serializers | capture-dependent |
| AO consumer | `Gamecode.dll+0x000171b2` forwards decoded AO N3 state/signals | AO game state changes only after decode | ZoneEngine handlers/services/direct builders | matched architecture |

### Envelope Validation Findings

- AOtomation's N3 header is 16 bytes. `HeaderSerializer` reads `MessageId`,
  `PacketType`, `Unknown`, `Size`, `Sender`, and `Receiver`.
- Outbound length is deterministic: `MessageSerializer.Serialize` rewrites the
  length at offset 6 after serializing the body.
- Inbound `MessageSerializer.Deserialize` now requires at least the complete
  16-byte header and exact equality between `Header.Size` and stream length.
- `PacketInspector` checks the bounds of each discriminator before reading it,
  including the four-byte N3 discriminator at byte offset 16.
- Known body serializers must finish exactly at the declared end. Under-consumed
  trailing data is rejected rather than published.
- Login and zone `GetMessageNumber` diagnostics return 0 when bytes 16-19 are
  unavailable, so short input remains inside the deserialize rejection path.
- Unknown subtypes and thrown deserializers fail closed and are not published.
- Count, slot, and identity validation is distributed across individual handlers;
  there is no shared inbound contract layer.

### AO-DLL-001 Completion Record

`AO-DLL-001` was completed offline on 2026-07-09 against the evidence boundary at
`Connection.dll+0x000019ba` and `MessageProtocol.dll+0x00001e81`.

- **Files:** AOtomation `Serialization/MessageSerializer.cs` and
  `Serialization/PacketInspector.cs`; login and zone client receive diagnostics;
  `MessageEnvelopeValidationTests.cs`; and its test project entry.
- **Families:** all AOtomation families routed through login/zone serialization:
  System, Text, N3, Ping, Operator, and InitiateCompression. Nested subtype keys
  use the same bounds rule.
- **Exact rejection rules:** stream must be seekable; actual length must be at
  least 16; declared signed size at offset 6 must be at least 16 and equal actual
  length; each discriminator must fit; and a known body must consume all bytes.
- **Unknown/opaque exception:** a valid unknown family or subtype still returns
  null because no known serializer can define its body boundary. No known packet
  or opaque-payload serializer is exempt from full consumption.
- **Dispatch/state result:** malformed packets are logged by the existing client
  catch path after receive-buffer cleanup. Deserialization does not return, so
  neither client reaches its bus publish and no gameplay handler-owned state is
  mutated. Normal body-deserializer exceptions and unknown warnings are preserved.
- **Batching result:** current login/zone receive code treats one buffered input as
  one message. Concatenated frames are therefore deterministic trailing data and
  are rejected without partial dispatch; batching/reassembly was not added.
- **Tests:** 15 focused tests plus the complete 145-test messaging suite cover
  exact valid frames, both size mismatch directions, short header/key/body,
  trailing data, unknown subtype, no dispatch/mutation, concatenated frames,
  deterministic repeat rejection, captured packets, and existing subsystem
  contracts.
- **Remaining uncertainty:** the client `Message_t::Verify` call can include
  semantic validation not recoverable from the framing evidence. This completion
  claims only the evidenced envelope, discriminator, and consumption boundary.

## Implemented Family Coverage Index

This index covers the current ZoneEngine handler surface plus direct packet
builders that participate in the requested workflows. "Model only" means the
AOtomation type exists but no general ZoneEngine handler owns the behavior.

| Packet family | Direction | AORebirth owner | Evidence and current status |
|---|---|---|---|
| `ZoneLogin` | C2S | `ZoneLoginMessageHandler`, `ZoneClient.CreateCharacter` | implementation-defined login entry; no packet-specific Ghidra layout claim |
| `FullCharacter` | S2C | `FullCharacterMessageHandler` | version 26 confirmed; nested/trailing unknown sections unresolved |
| `CharInPlay` | C2S and server fanout | `CharInPlayMessageHandler`, visibility services | key-only subclass recovered; ordering capture-backed, fixed sleep unsupported |
| `SimpleCharFullUpdate` | S2C | `Packets/SimpleCharFullUpdate`, visibility services | distinct RTTI type plus extensive captures; primary visible character/NPC introduction |
| `N3Teleport` | S2C | `TeleportMessageHandler`, transfer service, custom serializer | body repaired and playtested; exact meaning of several serializer fields remains named `Unknown` |
| `PlayfieldAnarchyF` | S2C | `PlayfieldAnarchyFMessageHandler` | definite current-client structure mismatch; do not rewrite without generator-payload fixture |
| `Despawn` | S2C | `DespawnMessageHandler`, corpse/transfer cleanup | captured/playtested for current paths |
| `DropDynel` | S2C | `DropDynelMessageHandler` | recovered model exists; runtime use must remain event/capture specific |
| `AppearanceUpdate` | S2C | `AppearanceUpdateMessageHandler` | visible post-introduction update; packet-specific Ghidra layout not newly proven here |
| `CharacterInfoPacket` | S2C | `CharacterInfoPacketMessageHandler` | identity lookup response; current behavior only |
| `Stat` | S2C | `StatMessageHandler` | live layout is identity, base byte, count, then stat/value pairs |
| `WeatherControl` | S2C | `WeatherControlMessageHandler` | client-visible environment; no server gap inferred from DLL names |
| `ResearchUpdate` | S2C | `ResearchUpdateMessageHandler` | handler present; not covered by strong packet-specific corpus evidence |
| `ResearchRequest` | C2S | `ResearchRequestMessageHandler` | handler present; validation remains implementation-defined |
| `OrgClient` | C2S | `OrgClientMessageHandler` | handler present; organization semantics need capture, not Ghidra name inference |
| `CharDCMove` | C2S then S2C fanout | `CharDCMoveMessageHandler` | 54-byte recovered body and float tail locked; finite-value validation missing |
| `FollowTarget` | both | `FollowTargetMessageHandler`, NPC movement services | captured coordinate path used; correction variants remain capture-gated |
| `CharacterAction` | both | `CharacterActionMessageHandler` | broad action multiplexer; individual action semantics vary in confidence |
| `GenericCmd` | both | `GenericCmdMessageHandler` and interaction routers | target/type routing central to corpses, containers, doors, terminals, city/grid |
| `LookAt` | C2S | `LookAtMessageHandler` | target request; identity/type/range validation is route specific |
| `Skill` | C2S | `SkillMessageHandler` | present; packet-specific corpus evidence not sufficient for stronger claims |
| `SocialActionCmd` | C2S | `SocialActionCmdMessageHandler` | present; visible fanout depends on current implementation |
| `Action` | S2C | `BackpackContainerActionMessageHandler` | emits captured/compatibility open and close action identities for container windows |
| `CityControllerWindowClose` | both | `CityControllerWindowCloseMessageHandler` | private-city controller window close route; target/session semantics are capture specific |
| `ClientContainerAddItem` | C2S | `ClientContainerAddItemMessageHandler`, inventory service | captured bank/backpack request body; source/target validation implemented |
| `ClientMoveItemToInventory` | C2S | corresponding handler, inventory/corpse services | captured full source identity and target placement; success paths live validated |
| `ContainerAddItem` | both | corresponding handler, inventory/corpse services | captured success acknowledgement body; rejection contract incomplete |
| `InventoryUpdate` | S2C | `InventoryUpdateMessageHandler`, container/corpse code | open-state/count/entry data; backpack/corpse flows capture-backed |
| `InventoryUpdated` | S2C | `InventoryUpdatedMessageHandler` | serializer behavior lacks a targeted current runtime capture |
| `Bank` | S2C | `BankMessageHandler` | bank slot placements repaired and live validated |
| `ChestItemFullUpdate` | S2C | corresponding handler | backpack/container introduction; capture-backed ordering |
| `AddTemplate` | S2C | `AddTemplateMessageHandler` | item/template introduction; exact use is operation specific |
| `TemplateAction` | S2C | `TemplateActionMessageHandler` | captured in equip/implant/reward paths; action values require fixture evidence |
| `SimpleItemFullUpdate` | S2C | corresponding handler | static/item object introduction; identity/template correctness is critical |
| `VendingMachineFullUpdate` | S2C | corresponding handler | vendor introduction; SQL/template stock owns content |
| `ShopUpdate` | S2C | corresponding handler | shop inventory/result state; capture and SQL validation stronger than DLL names |
| `Trade` | both | `TradeMessageHandler`, inventory service | player/vendor/OFAB-like transaction routing; live validation exists for core buy/sell/trade |
| `KnuBotTrade` | C2S | `KnuBotTradeMessageHandler` | dialogue/vendor trade branch |
| `KnuBotStartTrade` | S2C | corresponding handler | trade-window start |
| `KnuBotFinishTrade` | C2S | corresponding handler | transaction completion request |
| `KnuBotRejectedItems` | S2C | corresponding handler | explicit result family; exact rejection values are route/capture dependent |
| `Attack` | both | `AttackMessageHandler`, playfield combat | start/cancel state separate from damage; invalid target clears and replies |
| `StopFight` | both | `StopFightMessageHandler`, death cleanup | clears fighting target/tick and broadcasts stop |
| `CastNanoSpell` | S2C | `CastNanoSpellMessageHandler`, `PlayerController.CastNano` | client function/type confirmed; validation/scheduling gaps remain |
| `AttackInfo` | S2C direct builder | playfield and NPC combat coordinators | capture-locked envelope; several result fields remain partially semantic |
| `MissedAttackInfo` | model/direct use as applicable | combat code | type and captured shape known; comprehensive trigger policy not established |
| `HealthDamage` | model/direct use as applicable | combat/status paths | do not add to normal hits; prior local tests produced duplicate text |
| `SpecialAttackWeapon` | S2C direct builder | `AttackMessageHandler`, `ClientConnected` | startup arrays/fields contain hardcoded unknown values |
| `Feedback` | S2C | `FeedbackMessageHandler` | client localization IDs; visible text capture is required for meaning |
| `ChatText` | S2C | `ChatTextMessageHandler` | visible result; not a substitute for protocol acknowledgement |
| `ChatServerInfo` | S2C | corresponding handler | system/chat metadata |
| `ChatCmd` | C2S | corresponding handler | command parsing, not an N3 world-state contract |
| `VicinityChat` | C2S | corresponding handler | local chat fanout |
| `KnuBotOpenChatWindow` | S2C | corresponding handler | dialogue window start |
| `KnuBotAppendText` | S2C | corresponding handler | dialogue text payload |
| `KnuBotAnswerList` | S2C | corresponding handler | visible options |
| `KnuBotAnswer` | C2S | corresponding handler | selected option routing |
| `KnuBotCloseChatWindow` | both | corresponding handler | dialogue close state |

Mail/service terminals, private-city controllers, surgery clinics, grid terminals,
doors, and guest-key generators are not separate universal packet families in the
current server. They route primarily through `GenericCmd`, `CharacterAction`,
`ClientMoveItemToInventory`, `TemplateAction`, `N3Teleport`, city/org packets, and
normal result messages. Each target must still be checked by identity type,
playfield, route ownership, and capture-specific preconditions.

## Login, World Entry, And Zoning Contract

### Client Readiness Evidence

The strongest Ghidra chain is:

1. `n3PlayfieldFullUpdateIIR_t::ReadSubClass` at `N3.dll+0x00029c24`
   reads fields and creates a `DbObject_t`.
2. `PollStatus` at `N3.dll+0x00029aec` is a separate vtable phase.
3. `Activate` at `N3.dll+0x00029b0e` obtains the playfield, adds the root, and
   calculates water height.
4. `Gamecode.dll+0x00016e94` then accesses playfield identity/proxy, tilemap,
   ground, water, child objects, and activates the game zone.
5. `N3.dll+0x0000c8aa` and `+0x0000d9d8` show room lookup/room-space update as
   client spatial work.
6. `n3TeleportIIR_t::Activate` at `N3.dll+0x00029f87` applies destination
   position/rotation before `StartTeleport`.

This proves staged client readiness. It does not prove that the server must send
tilemap, room graph, ground, or water blobs, and it does not expose an exact wire
generation field.

### AORebirth Sequence Comparison

| Transition | Current AORebirth behavior | Evidence status | Gap or required guard |
|---|---|---|---|
| Login request | `ZoneLogin` begins character loading and creates/reconnects the character | implementation owner clear | malformed/duplicate request policy not capture-mapped |
| Playfield selection | `ZoneClient.CreateCharacter` enters playfield loading, resolves playfield, removes an NPC/player identity collision, reads character/stats | strong object-lifecycle match | pool/playfield lookup path has redundant parent-scoped lookup in transfer path |
| Ready block | session coordinator enters `ReadyBlock` | strong server-side model | not correlated to a client generation token |
| Join introduction | joining `SimpleCharFullUpdate` is announced before full-character boundary | capture/lifecycle trace supported | lock with behavior-level two-client fixture |
| Full state | inventory/current playfield/private-city pre-block, then `FullCharacter` version 26 | version confirmed; nested sections partial | unknown arrays/integers/trailing fields remain unresolved |
| Playfield-specific ready data | private-city ready block is sent after full character | capture-specific | no generic claim for all playfields |
| Existing visibility | each existing character is sent as SCFU then `CharInPlay`; joining character is announced in same order | strong match | needs executable packet-order fixture, not only source assertions |
| Appearance/action state | appearance and special-attack state follow the full-character boundary | partial | `SpecialAttackWeapon` values are not fully explained |
| Client ready | inbound `CharInPlay` sleeps one second, announces, clears `Starting`, clears changed flags | key/body confirmed, delay not confirmed | replace only after readiness/capture fixture |
| Zoning begin | transfer disables timers, enters `Zoning`, sends teleport, announces old-world despawn, applies state, resolves target, disposes, redirects | strong ownership, partial order proof | fixed 200 ms/1000 ms sleeps and no generation correlation |
| Zoning completion | next connection/load returns through playfield loading and ready block | explicit phase model | no duplicate/late-message generation rejection |
| Local grid teleport | current-playfield grid route avoids full reconnect | capture/user verified | preserve as a distinct path |

Unsupported or premature behavior: fixed sleeps are not readiness evidence;
emitting new dynels before full update/visibility boundaries would be premature;
replaying a second introduction for the same identity/type without a preceding
removal is unsupported. Missing behavior: explicit generation correlation and an
executable stale/duplicate packet test. Duplicated behavior: the transfer
destination resolver performs a discarded parent-scoped pool lookup after
`ZoneServer.PlayfieldById`, which already creates/returns a playfield.

## Dynel And Visibility Lifecycle Matrix

| Stage | Client expectation supported by evidence | AORebirth owner/current behavior | Status | Validation need |
|---|---|---|---|---|
| Construct | concrete object class/body before space entry | constructors/materialization and type-specific full-update builders | partial | per-type fixture |
| Register parent space | playfield/space relationship is valid | Pool parent and `PlayfieldDynelRegistry` | strong functional match | parent identity assertion |
| Full update | class-specific initial state before normal use | SCFU, SimpleItem, Chest, Corpse, Door/Vendor builders | mixed by type | captured full body |
| Enter visibility | introduced object becomes visible after full state | player path explicitly uses SCFU then `CharInPlay` | matched for players | two-client executable order test |
| Incremental update | transform/stats/appearance target an existing typed object | movement/stat/appearance/follow packets | partial | reject unknown/stale identity |
| Targetability | client can cast dynel to required class | handlers use typed Pool/registry lookups | partial | wrong-type identity tests |
| Interaction ready | full update/container/vendor/terminal state precedes request/response | route-specific services | mixed | operation fixtures |
| Death | combat stops and death state precedes corpse visibility | NPC/player death services stop combat/motion, send death state, schedule corpse | capture/live validated in tested scope | preserve order |
| Corpse conversion | corpse is a distinct identity/type, not the dead `SimpleChar` reused | register corpse, then send `CorpseFullUpdate` | matched | collision allocator test |
| Loot | corpse session opens, moves item, persists, marks consumed | corpse access and inventory services | capture/live validated | failure fixture |
| Despawn | object is removed from visible/typed state | current capture-backed `Despawn`, registry/pool disposal | partial across all types | zone/disconnect/two-client test |
| Zone cleanup | old-space membership cleared before new-world use | transfer dispose and reconnect | partial | stale-identity generation test |
| Resync | duplicate full update should not create wrong class/identity | no general resync contract | uncertain | capture duplicate/reconnect scenario |

Likely symptom links, not diagnoses: wrong full-update type can produce a
non-interactive object or client crash; missing introduction order can produce an
invisible entity; stale parent/registry state can leave an entity after zoning;
reused corpse identity/handle can open the wrong loot session; duplicated SCFU/
`CharInPlay` can produce two-client visibility anomalies.

## Movement And Vehicle Contract

| Field/transition | Client evidence | Current AORebirth behavior | Contract conclusion | Gap/test |
|---|---|---|---|---|
| Position | N3/Vehicle relative-position setters | `CharDCMove` converts and stores coordinates, then fans them out | required, known layout | reject NaN/infinity before mutation |
| Heading/quaternion | N3/Vehicle rotation setters | all four components are stored/rebroadcast | required, known layout | finite/non-zero guard; do not rename fields |
| Tick and aux values | recovered 54-byte `CharDCMove` body | preserves `Unknown1`, `AuxA`, `AuxB` | required opaque passthrough | captured round-trip already exists |
| Raw move byte | captured/recovered movement | normalized for server controller but raw value rebroadcast | good separation | add normalization table tests |
| Relative/global distinction | client has explicit relative setters and body global getters | server stores playfield-local coordinates | partial | do not add global field without capture |
| Parent space | dynel add/remove-space and room casts | playfield parent identity | required logical invariant | test cross-playfield stale movement rejection |
| Velocity | `Vehicle_t::SetVel` | no explicit player velocity vector in current request | recognized client state, wire source unknown | capture before adding |
| Surface/room | client Run/room-space methods | server collision/statel checks only | client-only algorithm | no port |
| Vehicle class | RTTI distinguishes vehicle/char/NPC vehicle | no explicit vehicle contract | missing/uncertain | mount/vehicle capture needed |
| Sit/sleep/lounge | current AORebirth compatibility normalization and stats | preserved across logout/login in tested flow | recognized server state | keep existing capture-backed behavior |
| Swim/fly/fall | client simulation evidence only | movement modes/stats may represent them | uncertain | state-specific two-client capture |
| Teleport | distinct IIR activation | distinct server transfer/local-grid paths | matched concept | transfer-order behavior test |

## Combat Contract

| Operation | Preconditions/current checks | State mutation and packet order | Rejection/cleanup | Status and gap |
|---|---|---|---|---|
| Attack start | target must resolve in same playfield and not be dialogue-suppressed/immune | set selected/fighting target, reset tick, engage NPC, send special-attack context, broadcast `Attack` | invalid/immune clears combat and replies `Attack` with no target | partial; exact special context unresolved |
| Attack stop | active character | clear fighting target, reset tick, broadcast `StopFight` | same path used by explicit stop | strong match |
| Combat tick | fighting target must exist and pass validity checks | calculate local damage, send `AttackInfo`, update health, death path if needed | invalid target clears tracking | implementation validated; formulas are not client-proven |
| Hit | valid tick | `AttackInfo` is the normal hit visible event | no duplicate `HealthDamage` | capture-backed policy; preserve |
| Miss/evade/parry | message types/captured envelopes exist for some outcomes | trigger coverage is not comprehensive | unresolved result mapping | partial/missing; capture required |
| Absorb/reflect/shield | RTTI proves reflect/shield families exist | no complete local trigger/value contract established | unresolved | missing/uncertain |
| Special attack | hardcoded `SpecialAttackWeapon` records sent at start/login | client receives context before/with attack | unknown values not validated | partial; capture fixture required |
| Nano start | `CharacterAction.CastNano` supplies nano ID/target | sends `CastNanoSpell`, blocks attack delay, sends finish/duration, subtracts nano, blocks recharge | missing ID/target/skill/cost checks can throw or underflow | material gap |
| Death | target reaches death path | mark dead, stop attackers and victim combat/motion/follow, death animation, rewards, corpse scheduling, despawn | duplicate death/pending corpse guards exist | capture/live validated in tested scope |
| Corpse | known visual/template needed | distinct corpse identity, register before full update, delayed cleanup | collision after allocator wrap not guarded | partial low-frequency risk |
| Player respawn | death action/save point/fallback | player corpse/status, teleport, fresh world stream/CharInPlay | old reclaim packet intentionally unused | capture-backed current path |

Do not change damage formulas, `AttackInfo` values, or add `HealthDamage` to normal
hits from this report. Ghidra confirms class separation, not authoritative formulas.

## Inventory And Container Operation Matrix

| Operation | Client request / identities / slots | Required state | Server response and order | Persistence and visible result | Status/gap |
|---|---|---|---|---|---|
| Ordinary inventory move | `ClientMoveItemToInventory`; full source identity and target placement | owned source page/item; valid destination/equip requirements | mutate/hot-swap, send move ack, equip/appearance packets as applicable | `BaseInventory.Write`; item appears once | live validated; some rejection paths silent |
| Equip/unequip | same request; inventory/equipment page slot | implant access/requirements when applicable | weapon definition, unequip/equip, container ack, skill/appearance recalculation | immediate write; relog validated | matched tested scope |
| Bank open | terminal `GenericCmd Use`, then S2C `Bank` | valid bank interaction route | bank slots with real placements | read-only open; positions survive reopen | matched/live validated |
| Bank deposit | `ClientContainerAddItem`, source `Inventory:<slot>`, target bank identity for character | matching target, source item, free bank slot | add destination, remove source with rollback, `ContainerAddItem` ack | write after ack construction; relog validated | matched success; rejection result unknown |
| Bank withdraw | `ClientMoveItemToInventory`, source bank page/slot | owned source and free/valid target | move and container ack | write; relog validated | matched tested scope |
| Backpack open | `GenericCmd Use` against worn/inventory item | resolved `Container:<id>` page; open state tracked | introduction if needed, `ChestFullUpdate`, `InventoryUpdate`, then GenericCmd success ack | contents visible; reopen/zone/relog validated | matched |
| Backpack close | close use/action for container | open page | close action/ack path | visual window closes; page remains persisted | matched tested scope |
| Move into backpack | `ClientContainerAddItem`, inventory source, container target | page exists, source slot/item valid, not bag-in-bag, free slot | destination add, source remove with rollback, `ContainerAddItem` | write; item visible in bag | matched success |
| Move out of backpack | `ClientMoveItemToInventory`, `Backpack:<handle/slot>` | handle maps to owned container page, source item, inventory target | add destination, remove source with rollback, ack | write; relog validated | matched success |
| Corpse open | `GenericCmd Use` target `Corpse` | corpse exists, not expired, correct target/type | `InventoryUpdate` for corpse session, then success ack | loot window visible | matched/capture-backed |
| Corpse item loot | `ClientMoveItemToInventory` or compatible container add source | active corpse, unlooted item, unique/free-slot checks | add inventory and persist, send `ContainerAddItem`, then mark loot consumed | item persists; no duplicate | live validated |
| Corpse credits | corpse session/use | credits present and not paid | delayed normal stat change; no duplicate manual text | cash persists | live validated |
| Corpse close/despawn | session empty/open timeout | corpse state | later `Despawn` | window/object disappears | capture-backed short delay |
| Vendor/shop open | `GenericCmd`/trade target vendor | valid vendor and template/stock | `VendingMachineFullUpdate`, `ShopUpdate`, trade window packets | stock visible | content/template integrity remains important |
| Vendor buy/sell | `Trade` operations and item identities | funds, stock/item ownership, slot availability | transaction result and inventory/stat packets | both inventory/cash persisted; live validated | matched tested scope; unobserved failures remain |
| Player trade | `Trade` plus temporary bags | two players/session/offer state | offer/result packets then transfer | both inventories/cash write; cancel rollback | live validated tested scope |
| OFAB transaction | vendor/trade-shaped flow | OFAB-specific currency/item rules | current handlers/content | visible stock/result | capture and failure-result coverage incomplete |
| Surgery clinic implant | `ClientMoveItemToInventory`; implant/inventory slots | clinic access state and equip checks | captured `TemplateAction` plus `ContainerAddItem` order by equip/unequip | item page and wear state persisted | capture-backed tested path |
| Item creation/reward | server action; template IDs | templates exist, unique/free-slot checks | DTO `TemplateAction`/`ContainerAddItem` or operation-specific packet | write must succeed before progression | partial by feature |
| Item deletion | `CharacterAction` delete or scripted action | owned item/valid slot | mutation and operation-specific result | persistence boundary varies | uncertain failure contract |
| Stack split/merge | `CharacterAction` routes | stackable item, counts and slots valid | local mutation/result | persistence behavior needs focused fixture | partial/uncertain |
| Overflow | `ContainerAddItem` target placement/page | normal inventory full or explicit route | add/result to overflow page | visible overflow item | model exists; broad live coverage limited |
| `InventoryUpdated` | S2C result family | operation specific | current serializer | client refresh effect uncertain | targeted capture required |

### Inventory Rejection Finding

Successful bank/backpack/corpse choreography is well captured. Invalid source,
empty slot, full destination, wrong target, and bag-in-bag paths often log and
return after preventing mutation. That is safe for server state, but it is not
proof that the client receives every required rollback/error packet. Capture each
failure before adding a generic acknowledgement. Chat text is not a protocol
rollback.

## Resource And Template Contract

| Client resource evidence | Server-relevant contract | AORebirth source | Gap/validation |
|---|---|---|---|
| `DatabaseController.dll+0x0000102e` identity lookup | emitted identity/template must resolve to intended resource class | item/nano loaders, DAOs, content definitions | startup referential-integrity report |
| `ResourceManager.dll+0x0000298b` cache and `+0x000029e8` fallback | do not rely on client fallback to hide wrong IDs | `ItemLoader.ItemList`, `NanoLoader.NanoList` | validate missing low/high/nano/template IDs |
| `GameData.dll+0x0000be90` typed blob read | type matters, not only numeric existence | mob/static/vendor/playfield data | expected-resource-kind metadata |
| `N3.dll+0x0001804e` tilemap cast | playfield identity selects client-local tilemap | playfield definitions/teleport target | validate known playfield IDs; no loader port |
| `N3.dll+0x0001965d` mesh metadata cast | visual mesh/template fields must be type appropriate | SCFU, weapon/item/appearance builders | compare identity-linked captures |
| Gamecode city/playfield/house casts | city and mission objects have distinct resource classes | private-city, statel, static-dynel, quest content | validator should flag wrong class/ID pair |
| `ldb.dll` text lookup | category/message IDs produce client-visible localized text | Feedback/ChatText emitters | capture-backed result-code dictionary |
| GUI/Image/FXS | client renders IDs/effects/images | only emitted visual/effect/animation IDs matter | no client loader/rendering port |

Server correctness opportunities are limited to template IDs, item low/high IDs,
nano IDs, visual/mesh IDs, weapon visuals, animation/effect IDs, door/terminal and
playfield object identities, vendor stock references, city/mission object types,
and localized result IDs. Client cache, image, GUI, RDB, LDB, and effect engines
remain out of scope.

## Highest-Confidence Gaps

1. No centralized inbound declared-size/minimum/body-consumption verification
   equivalent to the client `Message_t::Verify` boundary.
2. `PlayfieldAnarchyF` is a known current-client body mismatch, blocked on an exact
   generator-payload/capture fixture.
3. Fixed sleeps stand in for readiness in transfer and `CharInPlay`; their timing is
   unsupported by the DLL evidence.
4. No explicit generation correlation rejects stale/late world messages.
5. Movement accepts non-finite transforms.
6. Nano casting lacks safe ID/target/resource/cost prerequisites and blocks the
   calling thread.
7. Special-attack and several combat result fields remain hardcoded/unknown.
8. Inventory success contracts are strong, but failure acknowledgements are not
   systematically captured.
9. Corpse identity, inventory-handle, and loot-item allocators wrap without checking
   active collisions.
10. Resource/template validation is distributed and does not provide one report of
    invalid outgoing IDs by expected client resource kind.

Implementation slices, blockers, and acceptance criteria are ranked in
`Docs/AOREBIRTH_DLL_EVIDENCE_BACKLOG.md`.
