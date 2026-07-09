# AORebirth DLL Evidence Backlog

## Ranking Method

The order weighs, in sequence: client compatibility impact, evidence confidence,
observed AORebirth weakness, regression risk, implementation size, available
capture fixtures, and ability to validate without launching the client.

Abbreviations: `C` confirmed, `S` strong inference, `W` weak inference; size is
`S/M/L`; risk is the risk of changing current behavior. "Offline" means the slice
can be accepted without launching AO.

| Rank | ID | Slice | Impact | Confidence | Risk | Size | Fixture | Offline | Code-ready |
|---:|---|---|---|---|---|---|---|---|---|
| 1 | `AO-DLL-001` | Strict inbound message envelope preflight (complete 2026-07-09) | High | C/S | Medium | S | Synthetic/current suite | Yes | Complete |
| 2 | `AO-DLL-002` | Recover `PlayfieldAnarchyF` current-client body | High | C mismatch | High | M | Missing | No | No |
| 3 | `AO-DLL-003` | Reject non-finite movement transforms | High | C/S | Low | S | Yes | Yes | Yes |
| 4 | `AO-DLL-004` | Make parent-scoped Pool missing lookup null-safe | High | C source defect | Medium | S | Synthetic | Yes | Yes |
| 5 | `AO-DLL-005` | Remove redundant transfer destination Pool lookup | High | C source path | Low | S | Synthetic | Yes | Yes |
| 6 | `AO-DLL-006` | Collision-safe corpse identities and handles | High | C source risk | Low | M | Synthetic | Yes | Yes |
| 7 | `AO-DLL-007` | Executable playfield-transfer order fixture | High | S | Low | M | Synthetic | Yes | No |
| 8 | `AO-DLL-008` | Map unresolved `FullCharacter` sections | High | C unknowns | High | L | Missing | No | No |
| 9 | `AO-DLL-009` | Replace readiness sleeps with explicit conditions | High | S | High | M | Missing | No | No |
| 10 | `AO-DLL-010` | Correlate and reject stale world generations | High | S | High | M | Missing | Partial | No |
| 11 | `AO-DLL-011` | Two-client visibility order fixture | High | C/S | Low | M | Partial | Yes | No |
| 12 | `AO-DLL-012` | Dynel type-transition and duplicate guards | High | C/S | Medium | M | Partial | Yes | No |
| 13 | `AO-DLL-013` | Idempotent startup object materialization | Medium | C source gap | Medium | M | Synthetic | Yes | No |
| 14 | `AO-DLL-014` | Client error/result code dictionary | High | S | Low | M | Partial | Yes | No |
| 15 | `AO-DLL-015` | Inventory rejection acknowledgement fixtures | High | C success/S gap | Medium | M | Missing | No | No |
| 16 | `AO-DLL-016` | Targeted `InventoryUpdated` capture and serializer lock | Medium | C unknown | Medium | S | Missing | No | No |
| 17 | `AO-DLL-017` | Nano request no-crash prerequisites | High | C source gap | Medium | S | Partial | Yes | No |
| 18 | `AO-DLL-018` | Nonblocking nano cast/recharge scheduler | High | C source gap | High | M | Missing | No | No |
| 19 | `AO-DLL-019` | Special-attack context capture and fixture | Medium | C unknowns | High | M | Missing | No | No |
| 20 | `AO-DLL-020` | Miss/evade/parry/absorb result matrix | Medium | S | High | M | Partial | No | No |
| 21 | `AO-DLL-021` | First-class corpse full-update boundary | Medium | C/S | Medium | M | Yes | Yes | No |
| 22 | `AO-DLL-022` | Outgoing resource/template referential audit | High | C/S | Low | M | Existing data | Yes | No |
| 23 | `AO-DLL-023` | Terminal target/type/playfield guard matrix | Medium | S | Medium | M | Partial | Yes | No |
| 24 | `AO-DLL-024` | Inbound handler subscription/direction test | Medium | C | Low | S | Synthetic | Yes | No |
| 25 | `AO-DLL-025` | Count/slot/index serializer fuzz suite | High | S | Low | M | Synthetic | Yes | No |
| 26 | `AO-DLL-026` | Vehicle and movement-state capture matrix | Medium | C types/W wire | High | M | Missing | No | No |
| 27 | `AO-DLL-027` | Vendor/shop template integrity report | Medium | C/S | Low | M | Existing data | Yes | No |
| 28 | `AO-DLL-028` | `Despawn`/`DropDynel`/resync event dictionary | Medium | C structures/S use | Medium | M | Partial | Partial | No |
| 29 | `AO-DLL-029` | Localized feedback/result ID evidence table | Medium | C mechanism/W meaning | Low | M | Partial | Yes | No |
| 30 | `AO-DLL-030` | Room/readiness symptom diagnostics | Medium | C client code/W server key | Low | S | Missing | Partial | No |

## Detailed Slices

### AO-DLL-001 - Strict Inbound Message Envelope Preflight (Complete 2026-07-09)

**Subsystem:** protocol/framing.

**Evidence anchors:** `Connection.dll+0x000019ba`,
`MessageProtocol.dll+0x00001e81` and its `Message_t::Verify` call;
`MessageSerializer.cs`; `HeaderSerializer.cs`; `ZoneClient.OnReceive`.

**Pre-implementation behavior:** outbound serialization wrote a declared length.
Inbound deserialization did not compare declared and actual lengths or require
complete consumption. `GetMessageNumber` read body bytes before the deserialize
guard.

**Suspected gap:** a short, overlong, or trailing-data frame can fail outside the
intended error path or be accepted without the client's equivalent verify boundary.

**Implemented files:** AOtomation `Serialization/MessageSerializer.cs` and
`Serialization/PacketInspector.cs`; `Server/ZoneEngine/Core/ZoneClient.cs`;
`Server/LoginEngine/CoreClient/Client.cs`; `MessageEnvelopeValidationTests.cs`;
and the messaging test project file.

**Implemented rules:** require a seekable stream and a complete 16-byte header;
read the signed 16-bit declared size at offset 6; reject sizes below 16 or unequal
to the exact received length; bounds-check every family/subtype discriminator;
and require every known serializer to finish exactly at the declared end. This
applies before login or zone bus publication. Existing body-deserializer failures
retain their prior exception path.

**Dispatch and cleanup:** login and zone diagnostics now return message number 0
when bytes 16-19 are unavailable instead of indexing them. Both clients retain
their existing malformed-input cleanup: `_remainingLength` is cleared, the
deserialize exception is logged, the receive callback returns false, and no bus
event or gameplay handler is published. Unknown packets retain the existing
warning/null path.

**Intentional exceptions:** a well-formed unknown family or subtype still returns
null because no body schema exists against which to prove consumption. There is
no known-family trailing-data allowlist: all current known fixtures consume the
complete body. Login and zone do not currently expose frame batching, so two
concatenated valid packets are rejected as one overlong envelope rather than
partially dispatched.

**Tests:** 15 focused envelope tests cover exact Ping and N3 packets, byte-identical
N3 round trip, declared size smaller/larger than available data, a short N3 key,
truncated header/body, known trailing data, unknown N3 subtype, no handler call,
no handler-owned state mutation, concatenated frames, deterministic repeated
rejection, publish ordering, and short diagnostic guards. The complete messaging
suite passes 145/145, including captured `InfoPacket` fixtures and existing
packet/login/world/zoning/inventory/bank/backpack/vendor/corpse/combat/runtime
contracts.

**Remaining uncertainty:** `Message_t::Verify` may enforce semantic invariants
beyond the evidenced size and construction boundary. This slice does not infer
those rules and does not add transport-level fragment reassembly or frame batching.

**Acceptance:** complete. Malformed frames reach the existing logged rejection
path without bus publication or handler-owned state mutation; unknown handling
is preserved; known valid fixtures remain byte-identical; and build/tests pass.

### AO-DLL-002 - Recover PlayfieldAnarchyF Current-Client Body

**Subsystem:** zoning/playfield full update.

**Evidence anchors:** `n3PlayfieldFullUpdateIIR_t` RTTI at
`N3.dll+0x0005b798`, vtable `N3.dll+0x0003d0cc`, read/activate functions
`N3.dll+0x00029c24` and `+0x00029b0e`; existing current-client mismatch report.

**Current behavior:** AORebirth emits a fixed legacy `PlayfieldAnarchyF` structure
with coordinates, two playfield identities, vendor info, and X/Z fields.

**Suspected gap:** the current client expects the recovered playfield-full-update
base and optional opaque generator payload, not that fixed canonical body.

**Targets:** AOtomation `PlayfieldAnarchyFMessage` and serializer,
`PlayfieldAnarchyFMessageHandler.cs`, login/respawn fixtures.

**Smallest safe change:** first capture and model the exact base prefix plus opaque
payload length; only then add a custom serializer behind a fixture-verified path.

**Tests:** byte-exact official login and respawn bodies, empty-generator and
generator-present cases, malformed payload lengths.

**Fixtures:** missing official full-duplex current-client captures.

**Risk:** high; this is a world-init packet and a wrong body can cause white screens
or silent readiness failures.

**Blockers:** exact generator payload framing and official S2C capture.

**Acceptance:** local serialized body matches both official fixtures byte-for-byte
and login/respawn smoke passes. Not code-ready now.

### AO-DLL-003 - Reject Non-Finite Movement Transforms

**Subsystem:** movement/visibility.

**Evidence anchors:** `Vehicle.dll+0x0000d11d`, `+0x0000e21d`;
N3 position/rotation setters; recovered 54-byte `CharDCMove`; current
`CharDCMoveMessageHandler`.

**Current behavior:** position and quaternion are accepted, applied to the server
character, and rebroadcast without finite-value checks.

**Suspected gap:** NaN/infinity can poison range, collision, persistence, or
two-client transform state.

**Targets:** `CharDCMoveMessageHandler.cs`, focused messaging/movement tests.

**Smallest safe change:** reject non-finite coordinate or quaternion components
before `Controller.Move`; preserve raw move byte, tick, and aux floats. Do not add
client physics or reinterpret opaque fields.

**Tests:** each non-finite component, valid identity quaternion, existing recovered
body, and proof that rejected input is neither applied nor fanned out.

**Fixtures:** existing movement fixture is sufficient.

**Risk:** low.

**Blockers:** none.

**Acceptance:** all finite captured movement is unchanged; invalid transforms cause
one bounded log/rejection and no state/fanout.

### AO-DLL-004 - Make Parent-Scoped Pool Missing Lookup Null-Safe

**Subsystem:** dynel/object lookup, combat, interaction.

**Evidence anchors:** client typed dynel casts; `Pool.GetObject<T>(parent, identity)`
at `Pool.cs:374`; callers such as `AttackMessageHandler` explicitly test for null.

**Current behavior:** when the parent/type bucket exists but the identity does not,
`temp` is null and the mismatch exception formats `temp.GetType()`, causing a
`NullReferenceException` instead of a missing-object result.

**Suspected gap:** invalid/stale target identities can bypass handler rejection and
throw during lookup.

**Targets:** `AORebirth.ObjectManager/Pool.cs` and focused Pool/handler tests.

**Smallest safe change:** return null when no entity exists; retain
`TypeInstanceMismatchException` only for an existing entity of the wrong type.
Do not change missing-parent/type semantics in the same slice.

**Tests:** found correct type, missing identity in existing bucket, existing wrong
type, missing type bucket, missing parent; one `Attack` invalid-target path.

**Fixtures:** synthetic Pool entities.

**Risk:** medium because Pool is shared, but the change matches null-checking callers.

**Blockers:** none.

**Acceptance:** missing identity returns null without exception; wrong concrete type
still throws the intended exception; existing callers/tests pass.

### AO-DLL-005 - Remove Redundant Transfer Destination Pool Lookup

**Subsystem:** zoning/teleport destination ownership.

**Evidence anchors:** `n3TeleportIIR_t::Activate` at `N3.dll+0x00029f87`;
`Playfield.ResolveOrCreatePlayfieldTransferDestination`; `ZoneServer.PlayfieldById`.

**Current behavior:** `ZoneServer.PlayfieldById` already returns or creates the
destination. The resolver then performs a discarded parent-scoped Pool lookup and
retains an unreachable null-construction fallback.

**Suspected gap:** unnecessary lookup can throw and obscures the single owner of
playfield creation during a sensitive transition.

**Targets:** `Server/ZoneEngine/Core/Playfields/Playfield.cs` and transfer tests.

**Smallest safe change:** return the server-owned result directly, with an explicit
failure if that documented contract ever returns null. Do not change packet order.

**Tests:** existing destination, newly created destination, null-contract failure,
and callback order unchanged.

**Fixtures:** synthetic server/playfield setup.

**Risk:** low.

**Blockers:** none.

**Acceptance:** one creation/lookup owner, no discarded Pool call, same destination
identity and transfer sequence.

### AO-DLL-006 - Collision-Safe Corpse Identities And Handles

**Subsystem:** corpse lifecycle and container sessions.

**Evidence anchors:** distinct `CorpseFullUpdateIIR_t` RTTI at
`Gamecode.dll+0x001c1fd4`; current allocators in `Playfield.cs`; capture-backed
corpse open/loot/despawn sequence.

**Current behavior:** corpse IDs wrap in a small reserved range; inventory handles
wrap from `0x70` to `0xff`; loot item IDs also wrap. Active maps are not checked.

**Suspected gap:** sustained deaths can reuse an active identity/handle and route a
client loot request to the wrong corpse/session.

**Targets:** `Playfield.cs`, corpse lifecycle tests, combat/corpse smoke assertions.

**Smallest safe change:** bounded collision-aware allocation against active and
pending corpse state; fail explicitly on exhaustion. Preserve wire ranges.

**Tests:** wrap with free entry, wrap with occupied entries, handle collision,
pending corpse collision, exhaustion, and normal sequence unchanged.

**Fixtures:** synthetic corpse states; no client required.

**Risk:** low to medium.

**Blockers:** none.

**Acceptance:** no active identity/handle is reused, exhaustion is deterministic,
and existing corpse fixtures remain byte-identical.

### AO-DLL-007 - Executable Playfield-Transfer Order Fixture

**Subsystem:** zoning lifecycle.

**Evidence anchors:** teleport parse/poll/activate vtable at
`N3.dll+0x0003e68c`; current `PlayfieldTransferRuntimeService` callback order.

**Current behavior:** source-text assertions cover ownership/order, but no focused
behavior test executes callbacks and records the sequence.

**Suspected gap:** refactoring can reorder zoning phase, teleport, old-world
despawn, state application, dispose, and redirect without a failing behavior test.

**Targets:** transfer service and the smallest test assembly that can execute it.

**Smallest safe change:** no runtime change; invoke with recording delegates and
assert the established order and exactly-once behavior, including no-lifecycle fallback.

**Tests:** normal path, null lifecycle callback, resolver failure, and no redirect
after pre-finalize failure.

**Fixtures:** synthetic delegates.

**Risk:** low; test infrastructure may need a narrow internal-access seam.

**Blockers:** choose a test boundary without broad project-reference changes.

**Acceptance:** executable ordering coverage exists and current behavior passes.

### AO-DLL-008 - Map Unresolved FullCharacter Sections

**Subsystem:** login/full character state.

**Evidence anchors:** current-client version 26 decision; unknown arrays/integers
and trailing sections in `FullCharacterMessageHandler`; playfield full-update phases.

**Current behavior:** required version and major inventory/nano/stat blocks are sent;
`Unknown2` through `Unknown13` include empty/default sections.

**Suspected gap:** missing nested client state can surface as later feature or
generation readiness failures.

**Targets:** FullCharacter model, serializer, handler, capture decoder/tests.

**Smallest safe change:** produce a field-offset diff report for one official login
and one relog; do not rename or populate fields until repeated values/semantics agree.

**Tests:** byte/offset fixture and count-bound tests.

**Fixtures:** official full-duplex complete `FullCharacter` frames are required.

**Risk:** high.

**Blockers:** complete frames and nested decode.

**Acceptance:** every byte is named or explicitly opaque, with no speculative semantics.

### AO-DLL-009 - Replace Readiness Sleeps With Explicit Conditions

**Subsystem:** `CharInPlay` and playfield transfer.

**Evidence anchors:** client parse/poll/activate phases; fixed 200 ms and 1000 ms
sleeps in `PlayfieldLifecycleRuntimeService`, plus 1000 ms inbound `CharInPlay` sleep.

**Current behavior:** request/worker threads block for fixed durations.

**Suspected gap:** timing masks an ordering condition, adds latency, and can still
race on slow clients.

**Targets:** lifecycle service, `CharInPlayMessageHandler`, session coordinator.

**Smallest safe change:** first capture timestamps and phase trace; then replace one
sleep at a time with an explicit state/event/queued continuation.

**Tests:** no early fanout, no duplicate continuation, disconnect cancellation.

**Fixtures:** full-duplex zoning/login timing capture missing.

**Risk:** high.

**Blockers:** actual readiness trigger.

**Acceptance:** no fixed sleep remains in the selected path and capture order is unchanged.

### AO-DLL-010 - Correlate And Reject Stale World Generations

**Subsystem:** session/zoning generation transitions.

**Evidence anchors:** separate playfield parse/poll/activate phases; explicit
AORebirth session phases; no proven wire generation field.

**Current behavior:** phases reject illegal server transitions, but handlers do not
carry one explicit server generation token for late old-world work.

**Suspected gap:** delayed callbacks/messages may affect a new playfield session.

**Targets:** session lifecycle coordinator, transfer/visibility queued work, trace tests.

**Smallest safe change:** after capture/design proof, add an internal monotonic
server transition token to queued server work only; do not invent a packet field.

**Tests:** stale callback ignored, current callback accepted, reconnect/zone wrap.

**Fixtures:** synthetic tests plus a late-event capture scenario.

**Risk:** high.

**Blockers:** identify all asynchronous owners and prove no required old work crosses.

**Acceptance:** stale work cannot fan out/mutate the new generation; wire unchanged.

### AO-DLL-011 - Two-Client Visibility Order Fixture

**Subsystem:** visibility.

**Evidence anchors:** typed SCFU/CharInPlay classes, client dynel type casts, current
visibility services and trace assertions.

**Current behavior:** SCFU precedes `CharInPlay` for joiner/existing character paths.

**Suspected gap:** most checks are source/trace oriented rather than serialized
two-client packet queues.

**Targets:** visibility fanout/runtime service and messaging tests.

**Smallest safe change:** serialize captured outputs for A joining B, B joining A,
self-exclusion, and despawn.

**Tests:** exact order, identity, no duplicate, no cross-playfield fanout.

**Fixtures:** existing bodies plus synthetic clients.

**Risk:** low.

**Blockers:** test seam for outbound queues.

**Acceptance:** serialized queue order is executable and deterministic.

### AO-DLL-012 - Dynel Type-Transition And Duplicate Guards

**Subsystem:** object lifecycle.

**Evidence anchors:** `n3Dynel_t::CreateDynel`, add/remove space, RTTI casts to
SimpleChar/SimpleItem/Chest, distinct corpse/door full-update types.

**Current behavior:** Pool/registry key by identity and constructors/builders are
type specific; player/NPC identity collision is handled on login.

**Suspected gap:** there is no one test matrix for same identity/different type,
duplicate full update, corpse conversion, reconnect, and stale unregister.

**Targets:** Pool, registry, materialization, corpse/visibility tests.

**Smallest safe change:** tests first; add a narrow guard only where a duplicate is
demonstrably accepted.

**Tests:** all listed transitions and exact typed-view cleanup.

**Fixtures:** synthetic entities plus current full-update bodies.

**Risk:** medium.

**Blockers:** define legal reconnect reuse versus illegal type replacement.

**Acceptance:** legal reuse remains; illegal duplicate/type collision fails clearly.

### AO-DLL-013 - Idempotent Startup Object Materialization

**Subsystem:** NPC/vendor/static dynel startup.

**Evidence anchors:** client construct/add-to-space contract; current
`PlayfieldObjectMaterializationRuntimeService` loops without an explicit run guard.

**Current behavior:** DB mobs, content, vendors, and static dynels materialize in one
startup call; repeated invocation can attempt duplicate objects.

**Suspected gap:** reload/re-entry/error retry can duplicate identities or throw.

**Targets:** materialization service, runtime systems, startup tests.

**Smallest safe change:** make one playfield startup generation explicitly
idempotent or reject a second call before partial mutation.

**Tests:** call twice, partial delegate failure/retry, identity counts.

**Fixtures:** synthetic definitions.

**Risk:** medium; intentional reload semantics must be preserved if they exist.

**Blockers:** establish whether runtime reload is supported.

**Acceptance:** repeated same-generation call cannot duplicate objects.

### AO-DLL-014 - Client Error/Result Code Dictionary

**Subsystem:** protocol results and localization.

**Evidence anchors:** `ldb.dll` text lookup, GUI-visible replies, Feedback/ChatText/
KnuBot rejection families, existing captures.

**Current behavior:** result IDs and ad hoc logs/chat are distributed across handlers.

**Suspected gap:** success/failure semantics cannot be audited consistently.

**Targets:** a generated/reference Markdown or CSV report and capture decoder; no
runtime change first.

**Smallest safe change:** catalog packet family, direction, category/message/action
ID, visible text, source capture, authority, and emitting handler.

**Tests:** duplicate/conflicting ID report and source-path validation.

**Fixtures:** existing captures.

**Risk:** low.

**Blockers:** some visible text may need manual capture annotation.

**Acceptance:** every implemented rejection points to known evidence or `unknown`.

### AO-DLL-015 - Inventory Rejection Acknowledgement Fixtures

**Subsystem:** inventory/bank/backpack/corpse.

**Evidence anchors:** inventory IIR RTTI; captured success choreography; current
handlers that log/return for invalid/full/wrong-target cases.

**Current behavior:** mutation is prevented, but several rejection paths send no
known corrective result.

**Suspected gap:** the client may retain a ghost drag, stale slot, or open transaction.

**Targets:** live capture plan, inventory service, recovered contract tests.

**Smallest safe change:** capture one failure per operation; lock body/order before
adding any response.

**Tests:** no mutation, exact failure packet, original slots remain.

**Fixtures:** official full-duplex failure captures required.

**Risk:** medium.

**Blockers:** client launch/capture by Mike.

**Acceptance:** each selected failure has a fixture-backed result or proven silence.

### AO-DLL-016 - Targeted InventoryUpdated Capture And Lock

**Subsystem:** inventory refresh.

**Evidence anchors:** recovered inventory class family and existing mismatch report.

**Current behavior:** handler/model exists; current runtime body is not capture-locked.

**Suspected gap:** field/count interpretation may be legacy.

**Targets:** capture decoder, message serializer, `N3RecoveredContractTests`.

**Smallest safe change:** capture and add byte-exact test before runtime use changes.

**Tests:** serialize/deserialize, malformed count.

**Fixtures:** missing.

**Risk:** medium.

**Blockers:** targeted operation that emits the family.

**Acceptance:** exact current-client body fixture and documented trigger.

### AO-DLL-017 - Nano Request No-Crash Prerequisites

**Subsystem:** nanos/combat.

**Evidence anchors:** `Gamecode.dll+0x0001b5d4`, `CastNanoSpellIIR_t` RTTI,
`CharacterAction.CastNano`, `PlayerController.CastNano` direct dictionary index.

**Current behavior:** unknown nano ID can throw; target, current nano, skill, lock,
and resource checks are incomplete.

**Suspected gap:** malformed or unavailable casts can crash/mutate invalid state.

**Targets:** CharacterAction handler, player controller, nano tests.

**Smallest safe change:** after rejection-result evidence, use `TryGetValue`, validate
target existence/type and nonnegative cost before any send/mutation.

**Tests:** missing ID, missing/wrong target, insufficient nano, valid known nano.

**Fixtures:** known surgery/normal cast plus rejection capture.

**Risk:** medium.

**Blockers:** exact client-visible rejection/finish behavior.

**Acceptance:** invalid casts never throw or mutate and client UI exits cast state correctly.

### AO-DLL-018 - Nonblocking Nano Cast/Recharge Scheduler

**Subsystem:** nanos/timing.

**Evidence anchors:** distinct cast start/finish/duration messages; client timing is
client-owned; two `Thread.Sleep` calls in `PlayerController.CastNano`.

**Current behavior:** handler thread blocks for attack and recharge delay.

**Suspected gap:** throughput and disconnect/target-death races.

**Targets:** player combat/nano runtime service and lifecycle cancellation.

**Smallest safe change:** after AO-DLL-017 and timing capture, move one cast into a
cancellable scheduled state machine.

**Tests:** disconnect, target death, recast lock, exactly-once finish/cost/effect.

**Fixtures:** full-duplex timed cast.

**Risk:** high.

**Blockers:** prerequisite/rejection and timing contracts.

**Acceptance:** no blocking sleep and capture-equivalent event order.

### AO-DLL-019 - Special-Attack Context Capture And Fixture

**Subsystem:** combat/special attacks.

**Evidence anchors:** special-attack RTTI descriptors; hardcoded records/unknowns in
`AttackMessageHandler` and `ClientConnected`.

**Current behavior:** fixed MA/dimach/brawl-like records and top-level unknown values
are emitted independent of character equipment/skills.

**Suspected gap:** incorrect client buttons, recharge, or animation context.

**Targets:** capture tooling/report, message fixtures, then builder.

**Smallest safe change:** capture login and attack start for two distinct characters;
do not alter values first.

**Tests:** byte-exact arrays/counts and character-state comparison.

**Fixtures:** missing official S2C.

**Risk:** high.

**Blockers:** captures.

**Acceptance:** every emitted field is fixture-backed or explicitly opaque.

### AO-DLL-020 - Miss/Evade/Parry/Absorb Result Matrix

**Subsystem:** combat results.

**Evidence anchors:** RTTI for miss/reflect/shield/special result families and
existing private-server combat captures.

**Current behavior:** hit path is established; comprehensive alternate outcome
trigger/value mapping is absent.

**Suspected gap:** wrong/missing combat text and animation for non-hit outcomes.

**Targets:** capture report, combat coordinator, message tests.

**Smallest safe change:** documentation/capture matrix first, one outcome per slice.

**Tests:** exact result family and no health mutation for a miss.

**Fixtures:** official full-duplex outcomes required.

**Risk:** high if inferred from formulas.

**Blockers:** deterministic capture scenarios.

**Acceptance:** outcome is capture-backed; normal hit policy remains unchanged.

### AO-DLL-021 - First-Class Corpse Full-Update Boundary

**Subsystem:** corpse packet construction.

**Evidence anchors:** distinct `CorpseFullUpdateIIR_t`; capture-backed current builder;
existing documentation warns generic AOtomation corpse model is a placeholder.

**Current behavior:** working packet construction is embedded in playfield/corpse logic.

**Suspected gap:** future callers may use the placeholder generic message incorrectly.

**Targets:** dedicated packet builder/service and existing corpse tests.

**Smallest safe change:** extract only the proven builder bytes/inputs without
changing order or model semantics.

**Tests:** byte-identical current corpse fixture and registration-before-send.

**Fixtures:** existing.

**Risk:** medium.

**Blockers:** avoid broad corpse refactor.

**Acceptance:** one named builder owns the proven body; no runtime byte/order change.

### AO-DLL-022 - Outgoing Resource/Template Referential Audit

**Subsystem:** items, nanos, visuals, playfields, city/mission objects.

**Evidence anchors:** DatabaseController identity lookup, ResourceManager cache/
fallback, typed RDB casts, AORebirth loaders/content.

**Current behavior:** individual lookups often throw or fallback; no consolidated
expected-client-resource-kind report exists.

**Suspected gap:** bad IDs can produce invisible/wrong objects or client resource failures.

**Targets:** offline validator/report under existing tools/docs conventions.

**Smallest safe change:** read-only scan of emitted/configured low/high/template/nano/
visual/playfield IDs with source owner and expected kind.

**Tests:** known valid, missing, and wrong-kind synthetic rows.

**Fixtures:** checked-in data files and SQL.

**Risk:** low.

**Blockers:** define type metadata for IDs that share numeric spaces.

**Acceptance:** deterministic report; no runtime/data mutation.

### AO-DLL-023 - Terminal Target/Type/Playfield Guard Matrix

**Subsystem:** service, mail, surgery, grid, city, door terminals.

**Evidence anchors:** client dynel type distinctions and existing target-specific
captures/routes.

**Current behavior:** `GenericCmd` delegates to multiple interaction handlers with
route-specific validation.

**Suspected gap:** wrong type, stale playfield, or identity collision can route a use
to the wrong service.

**Targets:** GenericCmd route classifier tests and interaction handlers.

**Smallest safe change:** tests/table first; add only missing type/playfield checks.

**Tests:** correct target, wrong type same instance, other playfield, stale identity.

**Fixtures:** existing terminal captures.

**Risk:** medium.

**Blockers:** distinguish intentional compatibility aliases.

**Acceptance:** every route documents and tests its exact target predicate.

### AO-DLL-024 - Inbound Handler Subscription/Direction Test

**Subsystem:** dispatch.

**Evidence anchors:** client top-level dispatch; AORebirth attribute reflection and
MemBus subscriptions.

**Current behavior:** startup checks warn for missing subscriptions; direction errors
can remain runtime-only.

**Suspected gap:** a new inbound handler can be silently omitted or duplicated.

**Targets:** ZoneServer reflection logic and offline test.

**Smallest safe change:** enumerate message body types and assert exactly one
inbound-capable subscription; outbound-only families must not subscribe.

**Tests:** duplicate type, missing handler, wrong direction, valid set.

**Fixtures:** compiled handler assembly.

**Risk:** low.

**Blockers:** test assembly reference seam.

**Acceptance:** test fails before startup for all three invalid configurations.

### AO-DLL-025 - Count/Slot/Index Serializer Fuzz Suite

**Subsystem:** packet validation.

**Evidence anchors:** `Message_t::Verify`; BinaryStream reads; count-bearing FullCharacter,
Stat, InventoryUpdate, Bank, shop/vendor, and dialogue packets.

**Current behavior:** serializer exceptions reject some malformed bodies, but systematic
count-to-remaining-length and slot-bound coverage is absent.

**Suspected gap:** oversized/negative/truncated counts can allocate excessively or
reach handlers with inconsistent arrays.

**Targets:** AOtomation serializer tests, then narrow serializer guards.

**Smallest safe change:** mutate captured bodies around every count/slot boundary and
record current behavior before adding bounds.

**Tests:** zero, max accepted, negative signed, overflow, truncated, extra entries.

**Fixtures:** existing recovered bodies.

**Risk:** low for tests, medium for shared limits.

**Blockers:** choose per-family limits rather than one arbitrary global cap.

**Acceptance:** malformed inputs fail deterministically without large allocation/dispatch.

### AO-DLL-026 - Vehicle And Movement-State Capture Matrix

**Subsystem:** movement/vehicles.

**Evidence anchors:** Vehicle position/velocity/run functions and RTTI casts among
Vehicle/CharVehicle/NPCVehicle.

**Current behavior:** normal/sit/follow movement is modeled; explicit vehicle class,
velocity, fly/swim/fall transitions are not contract-mapped.

**Suspected gap:** mounted or special movement can appear with wrong state to peers.

**Targets:** capture report and movement fixture tests first.

**Smallest safe change:** capture transitions and enumerate changed packets/stats.

**Tests:** future per-state byte fixtures and two-client visibility.

**Fixtures:** missing.

**Risk:** high if inferred from client physics.

**Blockers:** Mike-run state captures.

**Acceptance:** server-relevant fields separated from client physics.

### AO-DLL-027 - Vendor/Shop Template Integrity Report

**Subsystem:** vendors/shops/OFAB.

**Evidence anchors:** client identity/resource lookup and typed item resources;
AORebirth vendor/shop template DAOs and existing validators.

**Current behavior:** broad SQL validation exists, but outgoing item low/high IDs and
expected resource kind should be linked directly to vendor/template source rows.

**Suspected gap:** valid SQL references can still select wrong/missing client resources.

**Targets:** existing vendor data validator/report.

**Smallest safe change:** add read-only resource existence/kind columns.

**Tests:** valid stock, missing low/high, invalid QL interpolation.

**Fixtures:** checked-in SQL/data.

**Risk:** low.

**Blockers:** resource-kind metadata.

**Acceptance:** zero unexplained outgoing stock IDs in validated sets.

### AO-DLL-028 - Despawn/DropDynel/Resync Event Dictionary

**Subsystem:** object removal/resynchronization.

**Evidence anchors:** recovered distinct quit/drop structures, current capture-backed
`Despawn`, dynel remove-space evidence.

**Current behavior:** current corpse/NPC paths use playtested Despawn; DropDynel model exists.

**Suspected gap:** future code may substitute packets by name and regress cleanup.

**Targets:** documentation/capture report and packet-order tests.

**Smallest safe change:** map lifecycle event to observed family, direction, body,
authority, and current caller; no runtime replacement.

**Tests:** current events continue using proven family.

**Fixtures:** existing plus targeted resync capture.

**Risk:** medium.

**Blockers:** resync/relocate trigger captures.

**Acceptance:** no ambiguous generic "remove dynel" recommendation remains.

### AO-DLL-029 - Localized Feedback/Result ID Evidence Table

**Subsystem:** client-visible errors/text.

**Evidence anchors:** `ldb.dll+0x00001fc3` text lookup; Feedback/ChatText/result captures.

**Current behavior:** numeric IDs and literal text coexist.

**Suspected gap:** wrong category/message IDs can show unrelated client text.

**Targets:** read-only generated reference and validators.

**Smallest safe change:** catalogue emitted IDs with capture-visible text and source owner.

**Tests:** duplicate conflicting meanings and missing provenance.

**Fixtures:** existing captures/logs.

**Risk:** low.

**Blockers:** incomplete visible-text observations.

**Acceptance:** high-impact error paths have evidence-backed IDs or explicit unknowns.

### AO-DLL-030 - Room/Readiness Symptom Diagnostics

**Subsystem:** dungeon/playfield readiness.

**Evidence anchors:** `N3.dll+0x0000c8aa`, `+0x0000d9d8`, Vehicle room-space lookup,
playfield activation chain.

**Current behavior:** AORebirth has playfield parentage/collision but no client room graph.

**Suspected gap:** invisible/non-interactive dungeon symptoms may correlate with packet
order, not missing server room geometry.

**Targets:** lifecycle trace/report only.

**Smallest safe change:** log generation, playfield identity, transform, introduction,
CharInPlay, interaction, and despawn timestamps for a targeted capture.

**Tests:** trace schema and correlation, no gameplay change.

**Fixtures:** targeted dungeon two-client capture.

**Risk:** low.

**Blockers:** symptom reproduction.

**Acceptance:** report can distinguish stale generation/order from client-local room work.

## Top Five Code-Ready Slices

The immediately safe set is `AO-DLL-001`, `AO-DLL-003`, `AO-DLL-004`,
`AO-DLL-005`, and `AO-DLL-006`. None requires an unresolved packet layout, client
physics, a combat formula, or an AO client launch.

### Standalone Prompt 1 - AO-DLL-001

```text
Work in C:\Users\Mike\Documents\AORebirth. Read AGENTS.md, AI_START_HERE.md,
docs/project/PROJECT_STATE.md, docs/ai/CURRENT_TASK.md,
docs/project/KNOWN_DECISIONS.md, docs/project/ARCHITECTURE.md, and
docs/ai/WORKFLOW.md first.

Implement AO-DLL-001 from Docs/AOREBIRTH_DLL_EVIDENCE_BACKLOG.md: add strict,
bounded inbound envelope preflight before ZoneEngine dispatch. The evidence anchors
are Connection.dll+0x000019ba and MessageProtocol.dll+0x00001e81. Require at least
the 16-byte AOtomation header, require the declared header size to equal the actual
buffer length, and guard access to the four-byte N3 discriminator. Preserve valid
captured bytes and current unknown-family rejection. Do not impose global trailing-
body rejection unless every existing fixture proves full consumption; diagnostics
and tests are sufficient for that part.

Keep the change narrow to MessageSerializer/ZoneClient and focused tests. Add tests
for short header, short N3 key, size mismatch both ways, unknown subtype, truncated
known subtype, and valid recovered bodies. Do not launch AO. Run
Tools\build_aorebirth_debug.cmd, relevant messaging tests, git diff --check, and
git status --short. Do not commit unless explicitly asked.
```

### Standalone Prompt 2 - AO-DLL-003

```text
Work in C:\Users\Mike\Documents\AORebirth and follow the documented startup/read
sequence. Implement AO-DLL-003 from Docs/AOREBIRTH_DLL_EVIDENCE_BACKLOG.md.

In CharDCMoveMessageHandler, reject a movement message before Controller.Move or
playfield fanout when any coordinate or quaternion component is NaN or infinity.
The contract anchors are Vehicle.dll+0x0000d11d, Vehicle.dll+0x0000e21d, the N3
position/rotation setters, and the recovered 54-byte CharDCMove fixture. Preserve
the raw move byte, Unknown1 tick, AuxA, AuxB, and all valid serialized bytes. Do not
normalize movement physics, invent velocity, or rename opaque fields.

Add focused tests proving each invalid component causes no mutation/fanout and valid
captured movement remains unchanged. Do not launch AO. Run the approved debug build,
relevant messaging/movement tests, git diff --check, and git status --short. Do not
commit unless explicitly asked.
```

### Standalone Prompt 3 - AO-DLL-004

```text
Work in C:\Users\Mike\Documents\AORebirth and follow the repository startup docs.
Implement AO-DLL-004 from Docs/AOREBIRTH_DLL_EVIDENCE_BACKLOG.md.

Fix Pool.GetObject<T>(Identity parent, Identity identity) so a missing identity in
an existing parent/type bucket returns null instead of dereferencing temp.GetType()
and throwing NullReferenceException. Preserve TypeInstanceMismatchException when an
entity exists but has the wrong concrete type. Do not change missing-parent or
missing-type semantics in this slice. Confirm callers such as AttackMessageHandler
continue to take their existing null rejection path.

Add focused Pool tests for correct type, missing identity, wrong type, missing type
bucket, and missing parent, plus one invalid Attack target regression if the current
test boundary supports it. Do not launch AO. Run Tools\build_aorebirth_debug.cmd,
the focused tests, git diff --check, and git status --short. Do not commit unless asked.
```

### Standalone Prompt 4 - AO-DLL-005

```text
Work in C:\Users\Mike\Documents\AORebirth and follow the documented startup/read
sequence. Implement AO-DLL-005 from Docs/AOREBIRTH_DLL_EVIDENCE_BACKLOG.md.

Simplify Playfield.ResolveOrCreatePlayfieldTransferDestination so ZoneServer.PlayfieldById
is the single owner of destination lookup/creation. Remove the discarded parent-scoped
Pool.GetObject<Playfield> call and unreachable duplicate constructor fallback. If the
server contract unexpectedly returns null, fail explicitly with context. Do not change
Teleport serialization, callback order, sleeps, redirect behavior, local-grid behavior,
or any packet bytes.

Add focused tests or the smallest existing lifecycle assertions for existing and newly
created destinations and unchanged transfer order. Do not launch AO. Run the approved
debug build, relevant lifecycle tests, git diff --check, and git status --short. Do not
commit unless explicitly asked.
```

### Standalone Prompt 5 - AO-DLL-006

```text
Work in C:\Users\Mike\Documents\AORebirth and follow the repository startup docs.
Implement AO-DLL-006 from Docs/AOREBIRTH_DLL_EVIDENCE_BACKLOG.md.

Make Playfield corpse identity and inventory-handle allocation collision-aware while
preserving the current wire ranges. Check active and pending corpse state before
returning a wrapped value, use a bounded search, and fail deterministically with
useful context on exhaustion. Include loot-item identity allocation only if it can be
guarded in the same narrow Playfield-owned state without refactoring corpse behavior.
Do not change corpse packet bytes, register-before-CorpseFullUpdate order, loot timing,
or despawn timing.

Add tests for normal allocation, wrap to a free value, occupied wrapped values,
pending corpses, handle collision, and exhaustion. Run the approved debug build,
corpse/combat regression assertions that do not launch AO, git diff --check, and
git status --short. Do not commit unless explicitly asked.
```
