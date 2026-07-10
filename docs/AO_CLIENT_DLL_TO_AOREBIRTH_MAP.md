# AO Client DLL To AORebirth Map

## Scope And Snapshot

This report cross-references the replacement-client Ghidra corpus in
`C:\Users\Mike\Documents\AO stripdown\decompile_report` against the AORebirth
checkout as inspected on 2026-07-09. All 40 per-DLL summaries currently report
`ghidra_complete`. The normalized index contains 161,076 functions, 26,877 RTTI
rows, 83,984 vtable rows, 1,216 suspicious casts, and the associated imports,
exports, callgraphs, and focused rebuild maps.

This is an ownership and compatibility map, not a claim that client internals
belong on the server. Client rendering, physics, pathfinding, GUI, audio, patching,
resource-cache implementation, and CRT behavior are explicitly classified as
client-only. Server recommendations are limited to packet framing, identities,
types, ordering, state transitions, template/resource IDs, and validation.

Evidence authority for AORebirth behavior is:

1. Official full-duplex live capture.
2. Direction-appropriate official capture.
3. Private-server capture, clearly labeled.
4. Current-client Ghidra evidence with function/callgraph/decompiler/RTTI support.
5. Current AORebirth source and tests as implementation evidence, not protocol truth.

Confidence labels in this report mean:

- **confirmed**: the named code/type/edge is directly present in Ghidra, or the
  AORebirth behavior is capture-backed and live validated.
- **strong inference**: multiple evidence forms support the conclusion, but a wire
  field, exact trigger, or client state boundary is not directly observed.
- **weak inference**: a useful search lead only; do not implement from it alone.

## Forty-DLL Ownership Index

| Original DLL | Evidence role | AORebirth owner or analogue | Classification |
|---|---|---|---|
| `N3.dll` | N3 IIR parsing, dynels, playfields, spaces, teleport activation | AOtomation N3 messages/serializers; ZoneEngine playfield, visibility, teleport, corpse services | Direct server-contract evidence |
| `Gamecode.dll` | AO-specific message types, dispatch consumers, world init, combat/inventory RTTI | ZoneEngine handlers/services; item, nano, city, vendor, quest code | Direct server-contract evidence |
| `Vehicle.dll` | Position, rotation, velocity, room-space, steering, surface alignment | `CharDCMoveMessageHandler`, controllers, follow/movement services | Mixed; fields/states relevant, physics client-only |
| `GUI.dll` | Login/character-selection UI, windows, damage text | Login/Chat/Zone responses only; no GUI implementation owner | Client-only except visible result IDs/order |
| `MessageProtocol.dll` | Data-block family decoding and `Message_t::Verify` | AOtomation `MessageSerializer`, `PacketInspector`, custom serializers | Direct framing/verification evidence |
| `ACE.dll` | Transport/event-loop support and timing | Core TCP infrastructure | Supporting implementation evidence; mostly client-only |
| `AFCM.dll` | Frame/timer support | No server analogue | Client-only |
| `Awesomium.dll` | Embedded web UI | No server analogue | Client-only |
| `BinaryIO.dll` | Primitive binary I/O | AOtomation `StreamReader`/`StreamWriter` | Supporting serializer evidence |
| `BinaryStream.dll` | Client binary-stream reads/writes | AOtomation serializers | Supporting packet-layout evidence |
| `BlockDatabase.dll` | Client resource database blocks/cache | Item/nano data files and AORebirth SQL only at ID/template boundary | Client loader is not applicable |
| `city.dll` | City objects and city data | private-city services, city packets, org/city SQL | Mixed; identity/template effects relevant |
| `Collision.dll` | Client collision | wall/statel transition checks only where server authoritative | Client physics not applicable |
| `Connection.dll` | Send/receive framing, compression/control, protocol conversion | `ZoneClient`, `MessageSerializer` | Direct transport-contract evidence |
| `DatabaseController.dll` | Identity-keyed `DbObject_t` retrieval and blob decoding | Item/nano loaders, DAOs, playfield content providers | Template/identity evidence only |
| `dbDebug.dll` | Client database diagnostics | Logging only | Client-only |
| `DeltaTimer.dll` | Client frame clock | Server timers/schedulers are independent | Client-only |
| `DisplaySystem.dll` | Rendering and viewport | No server analogue | Client-only |
| `Fanatic.dll` | Client support subsystem | No proven server contract | Client-only/uncertain |
| `FXS.dll` | Effects | Effect/nano IDs emitted by server | IDs relevant; effect engine client-only |
| `GameData.dll` | Playfield/resource blob types | item/nano data, playfield definitions, SQL content | Template/ID evidence |
| `icudt42.dll` | ICU data | No server analogue required | Client/runtime dependency |
| `Image.dll` | Image decoding | No server analogue | Client-only |
| `InstanceManager.dll` | Client instance/resource ownership | Pool and playfield registries are functional analogues only | Supporting lifecycle evidence |
| `Interfaces.dll` | Top-level message dispatch, redirects, authentication, GUI/resource bridges | login/zone dispatch, handler bus, serializers | Direct dispatch evidence |
| `ldb.dll` | Localized text lookup | feedback/chat/category/message IDs | IDs relevant; loader client-only |
| `mfc100.dll` | Microsoft UI/runtime | No server analogue | Client/runtime dependency |
| `mpir.dll` | Multiprecision support | No identified server contract | Client/runtime dependency |
| `mss32.dll` | Audio | No server analogue | Client-only |
| `msvcp100.dll` | C++ runtime | No server analogue | Runtime dependency |
| `msvcr100.dll` | C runtime, RTTI, timing/random imports | No protocol owner; RTTI calls support type evidence | Evidence support only |
| `PATCHW32.DLL` | Patching | No server analogue | Client-only |
| `PathFinder.dll` | Client pathfinding | NPC server movement planning is independent | Client algorithm not applicable |
| `randy31.dll` | Rendering/game support, historically crash-prone | No server analogue | Client-only |
| `references.dll` | Reference/ownership support | No direct server contract identified | Supporting/uncertain |
| `ResourceManager.dll` | Identity-keyed cache and fallback | item/nano caches and content data providers | Template/ID evidence only |
| `SandyInterface.dll` | Game-zone activation bridge | Zone/playfield selection and redirect | Readiness concept relevant; implementation client-only |
| `serialize.dll` | General serialization support | AOtomation serializers | Supporting packet evidence |
| `SIMPlayer.dll` | Simulated/player-side state | player/NPC controller behavior is only a functional analogue | Mixed; do not copy client simulation |
| `Utils.dll` | Common timing and utility support | General utilities | Client-only unless a specific ID/format is proven |

## Protocol And Dispatch Evidence

| Subsystem | Original evidence | Recovered item | Source evidence | Confidence | Apparent client expectation | AORebirth owner and current behavior | Status | Validation | Smallest safe follow-up |
|---|---|---|---|---|---|---|---|---|---|
| Outbound framing | `Connection.dll+0x0000186c` | `Connection_t::Send(uint, Message_t*)`; calls `MessageSizeGet`, then raw send | `decompile_report/evidence/Connection.dll/functions.csv`, `decompile.c`, `callgraph_edges.csv` | confirmed | Serialized length must agree with the bytes sent | `AORebirth.Core.Components.MessageSerializer.Serialize`; writes final length at header offset 6, then `ZoneClient.SendCompressed` queues it | matched | Serialize captured bodies and assert declared length equals buffer length | Add an explicit envelope invariant test for every recovered fixture |
| Inbound framing | `Connection.dll+0x000019ba` | `Connection_t::Receive`; network-order length/control handling and call to `DataBlockToMessage` | same Connection evidence files | confirmed | A complete bounded data block is decoded before dispatch | `ZoneClient.OnReceive` copies `_remainingLength` and catches deserialize exceptions | partially matched | Truncated, short, overlong, and declared-size mismatch tests | Preflight minimum and declared size before message-number access |
| Family decode | `MessageProtocol.dll+0x00001e81` | `DataBlockToMessage`; selects message family and calls `Message_t::Verify` | `decompile_report/evidence/MessageProtocol.dll/functions.csv`, `decompile.c`, `callgraph_edges.csv` | confirmed | Unknown family or failed verification must not reach gameplay dispatch | AOtomation `PacketInspector` returns null for unknown subtype; `ZoneClient` rejects null/deserialize exceptions, but body consumption is not verified | partially matched | Known, unknown, truncated, and trailing-byte fixture tests | Add strict envelope verification and consumption diagnostics without changing layouts |
| Top-level dispatch | `Interfaces.dll+0x00002a9e` | `Client_t::ProcessMessage`; ping/system/N3 branches, redirect/auth reads, unknown errors | `decompile_report/evidence/Interfaces.dll/functions.csv`, `decompile.c`, `callgraph_edges.csv`, `strings.csv` | confirmed | Family dispatch precedes N3 subtype dispatch; redirect/auth are separate stateful paths | `ZoneClient` deserializes, wraps the concrete body, and publishes to the MemBus; `ZoneServer` subscribes inbound handlers by attribute | matched at architecture level | Handler subscription audit and unknown-family test | Add a test that every inbound-capable handler is subscribed exactly once |
| N3 ingest | `N3.dll+0x000062fd` | `n3Engine_t::AddNetWorkMessage`; constructs `n3InfoItemRemote_t` records from `BinaryStream` | `decompile_report/evidence/N3.dll/functions.csv`, `decompile.c`, `callgraph_edges.csv` | confirmed | N3 payloads are typed records, not arbitrary raw blobs | AOtomation resolves concrete N3 body types and custom serializers | matched for modeled families | Round-trip and captured-body tests in `N3RecoveredContractTests` | Extend fixtures to each server-emitted direct builder |
| N3 send block | `N3.dll+0x000076d1` | `n3EngineClient_t::SendNetWorkMessage` | same N3 evidence files | confirmed | N3 records are placed in a transport data block | `MessageSerializer` plus `ZoneClient.SendCompressed` create the envelope | matched at functional boundary | Captured complete-frame serialization | Keep transport tests separate from body tests |
| IIR write | `N3.dll+0x00007762` | `n3EngineClient_t::SendIIRToServer`; invokes `n3InfoItemRemote_t::Write` | same N3 evidence files | confirmed | Client request bodies follow each IIR writer's field order | AOtomation message serializers decode client requests | partially matched by family | Existing captured C2S bodies; add bounds/rejection tests | Inventory and movement fixtures first |
| AO N3 consumer | `Gamecode.dll+0x000171b2` | `n3EngineClientAnarchy_t::ToClientN3Message` | `decompile_report/evidence/Gamecode.dll/functions.csv`, `decompile.c`, `callgraph_edges.csv` | strong inference | AO-specific N3 messages feed game state/signals after generic N3 decode | ZoneEngine handlers and direct packet builders own AO state changes | partially matched | Per-family capture plus client-visible smoke | Use only named downstream calls; do not name unresolved helper functions |

## Dynel And World Lifecycle Evidence

| Subsystem | Original evidence | Recovered item | Source evidence | Confidence | Apparent client expectation | AORebirth owner and current behavior | Status | Validation | Smallest safe follow-up |
|---|---|---|---|---|---|---|---|---|---|
| Dynel construction | `N3.dll+0x00003f80` | `n3Dynel_t::CreateDynel(n3DynelRibosome_i*, uint, DynelDataStatus_e)`; assigns a `Vehicle_t` body | `decompile_report/evidence/N3.dll/functions.csv`, `decompile.c`, `callgraph_edges.csv` | confirmed | A created object has a concrete dynel type/body before it enters space | entity constructors, `PlayfieldObjectMaterializationRuntimeService`, `SimpleCharFullUpdate`, static/corpse builders | partially matched | Spawn/full-update/type fixture and two-client smoke | Add duplicate/type-transition regression fixtures before changing runtime |
| Space entry | `N3.dll+0x00004036` | `n3Dynel_t::AddToSpace(Space_i*)` | same N3 files | confirmed | Object membership is established before normal spatial use | Pool parent identity, `PlayfieldDynelRegistry`, playfield registration/visibility | strong functional match | Registry and visibility ordering tests | Assert parent/playfield identity before registration/fanout |
| Space exit | `N3.dll+0x00004048` | `n3Dynel_t::RemoveFromSpace(Space_i*)` | same N3 files | confirmed | Removal clears old-space membership before reuse/destruction | unregister/dispose/despawn and transfer cleanup | partially matched | zone cleanup, disconnect, corpse despawn, two-client tests | Add behavior-level transfer/dispose callback-order test |
| Visual synchronization | `N3.dll+0x000196bd` | `n3VisualDynel_t::Run`; reads body position/rotation and updates visual mesh/visibility | `decompile_report/evidence/N3.dll/functions.csv`, `decompile.c`, `callgraph_edges.csv` | strong inference | Visible dynels need type-correct body, finite transform, and valid space/room context | `SimpleCharFullUpdate`, `CharDCMove`, follow packets, teleport and appearance packets | partially matched | Captured spawn/move/teleport bodies; finite-value unit tests | Reject non-finite transforms before state mutation or broadcast |
| Runtime type distinction | `Gamecode.dll+0x00015bd0`, `+0x00017bbd`, `+0x00049876` | RTTI casts `n3Dynel_t` to `SimpleChar_t`, `SimpleItem_t`, and `Chest_t` | `decompile_report/evidence/Gamecode.dll/suspicious_casts.csv` | confirmed for cast/type existence | Identity alone is insufficient; the client branches on concrete object class | separate character, static item, container, vendor, corpse, and terminal packet builders | partially matched | Type-specific full-update fixtures and duplicate identity tests | Create a type-transition test matrix for spawn, corpse conversion, and despawn |
| Playfield full update phases | `N3.dll+0x0005b798`, vtable `+0x0003d0cc` | `n3PlayfieldFullUpdateIIR_t`; `PollStatus`, `Activate`, `ReadSubClass`, `WriteSubClass` | `decompile_report/evidence/N3.dll/rtti_types.csv`, `vtables.csv`, `functions.csv`, `decompile.c` | confirmed | Parse, readiness polling, and activation are distinct client phases | `ZoneClientSessionLifecycleCoordinator`, `ClientConnected`, `PlayfieldAnarchyFMessageHandler` | partially matched | Login/respawn capture and lifecycle trace | Repair `PlayfieldAnarchyF` only after exact body/capture agreement |
| Teleport phases | `N3.dll+0x0005c2ac`, vtable `+0x0003e68c`, `+0x00029f87` | `n3TeleportIIR_t`; parse/poll/activate; activation applies position/rotation then calls `StartTeleport` | `decompile_report/evidence/N3.dll/rtti_types.csv`, `vtables.csv`, `functions.csv`, `decompile.c` | confirmed | Destination transform and target playfield data are available before teleport activation | `N3TeleportMessageSerializer`, `PlayfieldTransferRuntimeService`, `TeleportMessageHandler` | matched packet shape; partial lifecycle proof | Existing playtest/capture plus callback-order tests | Lock transfer ordering in a behavior-level unit test |
| AO object type inventory | `Gamecode.dll+0x001bea58`, `+0x001c1f84`, `+0x001c1fd4`, `+0x001c1ffc` | RTTI descriptors for `SimpleCharFullUpdateIIR_t`, `ChestFullUpdateIIR_t`, `CorpseFullUpdateIIR_t`, `DoorFullUpdateIIR_t` | `decompile_report/evidence/Gamecode.dll/rtti_types.csv` | strong inference; RTTI does not prove fields | Each class is a distinct client state transition | SCFU, chest/inventory update, corpse builder, door/statel handlers | mixed | Captured bodies and lifecycle smoke | Keep corpse/door builders first-class; do not substitute a generic dynel packet |

## Playfield, Movement, And Vehicle Evidence

| Subsystem | Original evidence | Recovered item | Source evidence | Confidence | Apparent client expectation | AORebirth owner and current behavior | Status | Validation | Smallest safe follow-up |
|---|---|---|---|---|---|---|---|---|---|
| Playfield activation | `Gamecode.dll+0x00016e94` | `n3EngineClientAnarchy_t::PlayfieldInit`; accesses playfield/proxy identity, tilemap, ground, water, children, and activates game zone | `decompile_report/evidence/Gamecode.dll/functions.csv`, `decompile.c`, `callgraph_edges.csv` | confirmed calls; strong readiness inference | Client-local playfield resources must be ready before stable gameplay | server phases loading/ready/full-character/CharInPlay/InPlay; it cannot load client tilemaps | partially matched | Full-duplex zoning capture with lifecycle trace | Correlate server phase trace to capture; do not emulate tilemap loading |
| Position to room | `N3.dll+0x0000c8aa`, cast callsite `+0x0000c8e9` | `n3Playfield_t::PosToRoom`; `Space_i` to `n3RoomSpace_t` cast and inside-cell lookup | `decompile_report/evidence/N3.dll/functions.csv`, `decompile.c`, `suspicious_casts.csv` | confirmed | Room/space membership can affect visibility and interaction | AORebirth has playfield parentage and server collision/statel checks, not client room graphs | uncertain server relevance | Dungeon two-client capture and room-boundary movement trace | Add diagnostics first; do not build client room graphs on server |
| Room-space rebuild | `N3.dll+0x0000d9d8`, cast callsite `+0x0000da0e` | `n3Playfield_t::UpdateRoomSpace`; room merge/spatial lookup calls | same N3 evidence plus Vehicle callgraph | confirmed | Client room space is rebuilt/updated during playfield changes | no direct AORebirth analogue | client-only except symptoms | Client capture around dungeon transitions | Track stale/invisible symptoms against packet order before server change |
| Velocity | `Vehicle.dll+0x0000a4b1` | `Vehicle_t::SetVel(Vector3_t*)` | `decompile_report/evidence/Vehicle.dll/functions.csv`, `decompile.c` | confirmed | Vehicle/body state recognizes velocity | player `CharDCMove` does not persist an explicit velocity vector; NPC follow has movement state | partially matched/uncertain wire source | Movement captures by state | Capture flying/swimming/vehicle transitions before adding fields |
| Orientation and position | `Vehicle.dll+0x0000d11d`, `+0x0000e21d`, N3 setters `+0x00004061`, `+0x0000409b`, `+0x000040c7` | relative quaternion/position setters | Vehicle and N3 `functions.csv`, `decompile.c` | confirmed | Position and quaternion are coherent and finite | `CharDCMoveMessageHandler` mutates then rebroadcasts raw move type, position, quaternion, tick, and aux floats | partially matched | Recovered movement fixture plus non-finite tests | Add finite transform validation; preserve opaque tail fields |
| Vehicle run/surface | `Vehicle.dll+0x0000e849` | `Vehicle_t::Run(float)` with surface/listener work | Vehicle `functions.csv`, `decompile.c`, `callgraph_edges.csv` | confirmed implementation; weak server inference | Falling/surface behavior is client simulation unless a packet exposes a state | movement modes/stats and server collision checks only | client-only except recognized mode/state | Captures for sit/swim/fly/fall | Document mode bytes; do not copy surface physics |
| Steering | `Vehicle.dll+0x0000a87c`, `+0x0000ab28` | `SteeringSeek`, `SteeringArrive` | Vehicle `functions.csv`, `decompile.c` | confirmed | Client can smooth toward desired movement | NPC server movement/follow is independently authoritative | client-only | Existing NPC chase capture and smoke | Keep server coordinates authoritative; no client steering port |
| Room spatial index | `Vehicle.dll+0x0000746d`, `+0x00007b8c` | `RoomSpace_t::MakeSpatialRoomLookup`, `MergeRoomSpace` | Vehicle `functions.csv`, `decompile.c` | confirmed | Room membership has a client-side spatial index | no server equivalent required | client-only | Symptom-led capture only | No code change without evidence of server-visible room key |
| Vehicle classification | `Gamecode.dll+0x00004098`, `+0x0005aca0`, `+0x0005b6c7` | RTTI casts among `Vehicle_t`, `CharVehicle_t`, `NPCVehicle_t` | `decompile_report/evidence/Gamecode.dll/suspicious_casts.csv` | confirmed type distinctions | Character and NPC vehicle bodies are distinct recognized states | AORebirth has player/NPC controllers and movement modes but no explicit vehicle class contract | missing/uncertain | Vehicle equip/mount and two-client captures | Build fixtures only after exact vehicle state packet is identified |

## Combat, Inventory, And Resource Evidence

| Subsystem | Original evidence | Recovered item | Source evidence | Confidence | Apparent client expectation | AORebirth owner and current behavior | Status | Validation | Smallest safe follow-up |
|---|---|---|---|---|---|---|---|---|---|
| Combat family types | `Gamecode.dll+0x001c1084`, `+0x001c17f4`, `+0x001c1edc`, `+0x001c20d8`, `+0x001c2164` | RTTI descriptors for cast, attack, attack info, miss, and special attack IIRs | `decompile_report/evidence/Gamecode.dll/rtti_types.csv` | strong inference; no formula/layout proof | Start, result, miss, and special events are separate client messages | attack/stop handlers, combat tick, nano controller, death/corpse services | partially matched | Existing capture-locked envelopes and combat smoke | Preserve separate events; capture semantics before changing values |
| Nano visual start | `Gamecode.dll+0x0001b5d4` | `n3EngineClientAnarchy_t::N3Msg_CastNanoSpell` | `decompile_report/evidence/Gamecode.dll/functions.csv`, `decompile.c` | confirmed function; fields not inferred | Cast start is distinct from completion/duration | `CharacterActionMessageHandler` calls `PlayerController.CastNano`, which sends `CastNanoSpell`, blocks for delays, finishes, subtracts nano, sets duration | partially matched; unsafe validation gaps | Invalid nano/target/current-nano tests plus targeted capture | Add no-crash validation before any scheduler/formula work |
| Inventory family types | `Gamecode.dll+0x001be4a0`, `+0x001be5d4`, `+0x001be600`, `+0x001be624` | RTTI descriptors for inventory update, client container add/get/move | `decompile_report/evidence/Gamecode.dll/rtti_types.csv` | strong inference; RTTI does not prove fields | Open/update and move requests are distinct operations | AOtomation recovered serializers and `InventoryContainerRuntimeService` | matched where captured; partial elsewhere | `N3RecoveredContractTests`, bank/backpack/corpse live smoke | Add rejection/corrective-response captures and tests |
| Container add consumer | `Gamecode.dll+0x00028433` | `n3EngineClientAnarchy_t::N3Msg_ContainerAddItem` | `decompile_report/evidence/Gamecode.dll/functions.csv`, `decompile.c` | confirmed function; strong identity/order inference with captures | A successful move is made visible by a container-add result referencing source, target, and slot | `ContainerAddItemMessageHandler` and centralized inventory service | matched for bank/backpack/corpse captures | Captured body tests and persistence/relog smoke | Build an explicit failure-result dictionary before changing rejection paths |
| Identity-keyed resources | `DatabaseController.dll+0x0000102e`, `+0x000012fc` | `GetDbObject(Identity_t*)`, `DbObject_t::DecodeBlob` | `decompile_report/evidence/DatabaseController.dll/functions.csv` | confirmed | Client resolves emitted identities/template IDs to typed resources | Item/nano loaders, DAOs, content providers emit IDs; client performs resource lookup | functional boundary matched | Startup data validation and known-ID fixtures | Validate every emitted template/resource ID exists in server data |
| Resource cache/fallback | `ResourceManager.dll+0x000028e5`, `+0x0000298b`, `+0x000029e8` | `SetDatabase`, `GetCached(Identity_t*)`, `GetFallback(TypeID_e, TypeID_e)` | `decompile_report/evidence/ResourceManager.dll/functions.csv` | confirmed | Invalid/mismatched IDs may fall back client-side but should not be relied on | AORebirth caches `items.dat`/`nanos.dat` and SQL templates | partially matched | Missing-template startup audit | Add report-only referential-integrity checks first |
| Typed playfield resources | `GameData.dll+0x0000be90`; `N3.dll+0x0001804e`, `+0x0001965d` | `RDBInfoObject_t::ReadBlob`; casts to `RDBTilemap_t` and `MeshMeta_c` | GameData `functions.csv`; N3 `suspicious_casts.csv` | confirmed types/casts | Playfield and visual IDs select typed client resources | playfield/static dynel definitions and SCFU visual/template fields | partially matched | Known capture rows versus local definitions | Validate type-appropriate IDs, not client blob decoding |
| AO playfield/city resources | `Gamecode.dll+0x00121edb`, `+0x00122729`, `+0x001231d3`, `+0x001232ca`, `+0x0012e520` | casts from `DbObject_t` to building, city, playfield, dynel-loader, house-template types | `decompile_report/evidence/Gamecode.dll/suspicious_casts.csv` | confirmed type distinctions | City/playfield object identities must resolve to the expected resource class | private-city/grid/statel/static-dynel content and SQL | partially matched | Identity-linked capture and content validator | Extend content validators with expected resource-kind metadata |
| Localized result IDs | `ldb.dll+0x00001183`, `+0x00001fc3` | `LDBface::MapMDBfileINT`, `LDBface::GetText` | `decompile_report/evidence/ldb.dll/functions.csv` | confirmed client implementation; weak semantic mapping | Feedback/chat IDs are interpreted through client localization | feedback/chat handlers emit category/message IDs | uncertain by ID | Capture visible text with emitted IDs | Maintain a capture-backed ID dictionary; do not ship client LDB logic |
| Login UI effects | `GUI.dll+0x0000da5c`, `+0x000150b9` | character-select login confirm and login reply slots | `decompile_report/rebuild/gui_map.md`, GUI `functions.csv` | confirmed client functions; weak packet detail | Login replies and character list state drive distinct GUI transitions | LoginEngine/ZoneLogin/redirect flows | partially mapped | Login/selection capture | Use packet captures; GUI names alone do not prove reply fields |
| Damage text randomness | `GUI.dll+0x0004b530`, `+0x0004b532` | `rand` calls in `RenderTextModule_t::DamageTextMessage` | `decompile_report/rebuild/timing_random_map.md`, GUI `random_api_refs.md` | confirmed | Visual placement randomness is client-owned | no server owner | client-only and not applicable | none | Do not reproduce on server |
| Frame timing | `DeltaTimer.dll+0x00001162`, `+0x00001170` | `timeGetTime` in `WrapperTime_t::ReadSystemTime` | timing map and DeltaTimer `random_api_refs.md` | confirmed | Client frame time is independent of server delays | server uses timers and several blocking `Thread.Sleep` calls | client-only anchor; server sleeps unsupported | scheduler/unit tests and capture timing | Replace sleeps only after packet/readiness timing is captured |

## Strongest Cross-Project Conclusions

### Confirmed AORebirth Matches

- AORebirth has the same broad decode architecture the client evidence implies:
  envelope decode, concrete family/type resolution, then handler dispatch.
- Unknown and deserialize-failing messages are rejected before MemBus gameplay
  dispatch.
- `FullCharacter` version 26, the `CharInPlay` key-only subclass, the recovered
  `CharDCMove` int-plus-two-float tail, and current `N3Teleport` body are already
  capture/rebuild backed.
- Bank deposit/withdraw, backpack open/movement/persistence, corpse open/item/credit
  loot, and ordinary inventory/equipment persistence have stronger current capture
  and live-smoke evidence than the Ghidra type names. Those implementations remain
  authoritative within their tested scopes.
- Corpse creation is registered before `CorpseFullUpdate`; loot opens through
  `GenericCmd`, moves through `ClientMoveItemToInventory`/`ContainerAddItem`, and
  later despawns. This matches the captured client-visible choreography.
- Same-playfield visibility explicitly sends a full update before `CharInPlay` for
  each introduced character.

### Partial Or Missing Behavior

- AOtomation does not currently expose an equivalent of the client's centralized
  `Message_t::Verify`: declared size and complete body consumption are not checked
  together before dispatch.
- `PlayfieldAnarchyF` remains a documented fixed-layout mismatch against the
  recovered `n3PlayfieldFullUpdateIIR_t` shape. Its nested/generator payload is not
  sufficiently resolved for an immediate runtime rewrite.
- Server session phases are explicit, but there is no explicit generation token or
  acknowledgement correlated with client parse/poll/activate phases.
- `PlayfieldLifecycleRuntimeService` and inbound `CharInPlay` use fixed blocking
  sleeps. No Ghidra evidence proves those durations or that sleep itself is the
  readiness condition.
- Player movement accepts and rebroadcasts non-finite position/quaternion values.
- Nano casting indexes the nano cache and mutates state without the checks described
  by its own TODOs, and blocks the calling thread for attack/recharge delays.
- Special-attack startup and several combat result fields remain hardcoded or named
  `Unknown`; RTTI confirms message classes but not those values.
- Inventory success paths are well captured, while several invalid/rejected paths
  only log and return. The required corrective packet, if any, needs targeted
  capture evidence.

### Explicit Contradictions And Resolutions

- `RTTI_Type_Descriptor` rows prove class names and distinctions, not packet field
  layouts. Existing capture-locked AOtomation bodies take precedence.
- Historical AORebirth notes saying backpack movement was not implemented are
  superseded by the later committed implementation and live validation in current
  `PROJECT_STATE.md`.
- Recovered `DropDynel`/quit structures do not by themselves override the currently
  captured and playtested `Despawn` path. Use the packet appropriate to the proven
  lifecycle event.
- The client has room graphs, collision, steering, surface alignment, rendering,
  and timing loops. Their existence does not justify server ports; only packet-
  visible state and server-authoritative validation are in scope.

## Validation Lanes

Use the smallest lane that can falsify a claim:

- **Serializer**: captured body round-trip, declared length, truncation, trailing
  bytes, unknown family, count/slot bounds.
- **Lifecycle**: behavior-level callback-order tests plus existing
  `PlayfieldLifecycleTraceTests`; source-text assertions alone are not wire proof.
- **Persistence**: mutation, acknowledgement, write result, close/reopen, zone, and
  relog checks.
- **Two-client**: joiner/existing-player packet order, identity/type, despawn, and
  zone cleanup.
- **Capture required**: unresolved generator bodies, failure acknowledgements,
  generation markers, special-attack semantics, vehicle modes, and room readiness.

The implementation ranking and exact safe slices are maintained in
`Docs/AOREBIRTH_DLL_EVIDENCE_BACKLOG.md`.
