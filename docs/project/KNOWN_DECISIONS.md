# Known Decisions

Generated: 2026-06-02

## Use Evidence-Backed Packet Repair

Decision: Packet and gameplay behavior changes must be backed by official live capture, private-server capture, AO stripdown source, AOSharp/AODB reference evidence, or local code facts.

Reason: Repeated visual/client issues were caused by guessing packet behavior.

Alternatives considered: Patch from visual symptoms or intuitive AI behavior.

Consequences: Repairs may take longer up front, but they are easier to defend and less likely to break unrelated systems.

## Live Client Behavior Bugs Start With Capture

Decision: For AORebirth bugs involving current AO client behavior, packet flow, UI actions, item movement, inventory, bank, backpacks, shops, trade, missions, NPC interactions, pets, combat actions, or other client/server behavior, Codex must prioritize live capture/log evidence before designing the repair.

Required behavior:

- Treat the live AO client as the authoritative protocol source.
- Treat legacy server code as a partially-correct reference, not proof.
- Do not rely on static audit alone when packet behavior is involved.
- Start with live capture or existing capture review whenever feasible.
- User should only perform in-game actions; Codex must inspect logs/captures itself.
- If capture is not possible, explicitly say so and explain the fallback evidence.
- Repairs must be based on confirmed live packet/message behavior when available.

Reason: Current-client behavior repeatedly differs from legacy AO Rebirth assumptions, especially in inventory, bank, trade, shop, mission, NPC, and UI-driven packet flows.

Alternatives considered: Start from static source audit, legacy server behavior, or inferred packet semantics before checking live evidence.

Consequences: Client-behavior bug work starts with existing capture review or a live capture plan whenever feasible, and fallback evidence must be explicit when capture cannot be obtained.

## Capture-Derived Content Uses Identity-First Evidence

Decision: NPC, mob, statel, static dynel, vendor, quest, item, and playfield reconstruction must use the captured AO identity as the primary key. Display names, item names, screenshots, nearby objects, spatial proximity, visual similarity, assumed meshes, or assumed relationships may guide a search, but they must never define or replace runtime data.

Reason: The Rex B18D Cargo Box investigation showed that using rendered text, nearby clutter, and plausible templates can corrupt runtime reconstruction even when the correct capture data exists elsewhere. The correct workflow is to search all relevant capture folders for the exact identity and locate identity-linked full-update/stat evidence before writing data.

Evidence hierarchy:

1. Exact identity-linked full-update packet.
2. Exact identity-linked stat/update packet.
3. Exact identity-linked interaction packet.
4. Decoded capture logs.
5. Extracted analysis output.
6. Screenshots and visual observations.
7. Names, proximity, and nearby objects.

Required pre-change behavior:

- Search the complete relevant capture set for the exact identity in `events.log`, `packets.hex.log`, `system-messages.log`, `npc-interactions.log`, `inventory-updates.csv`, `enemy-state.csv`, `enemy-state.json`, `vendor-full-updates.csv`, `shop-updates.csv`, and decoded full-update outputs before declaring evidence missing.
- Separate interaction evidence from definition evidence. For example, `GenericCmd Action=Use -> Terminal:56D9B4AF` proves use of that identity only; it does not prove template, mesh, name, position, rotation, stat blob, or event configuration.
- Produce an evidence table before SQL or game-data edits:

| Field | Proposed value | Exact identity | Capture folder | Packet/log source | Confidence |
| --- | --- | --- | --- | --- | --- |

- Use the labels `confirmed`, `captured`, `temporal candidate`, `visual candidate`, `inferred`, and `unresolved` consistently. Only exact identity-linked packet evidence can promote a field to `confirmed`.
- Stop without changing data when the exact identity cannot be found, required full-update evidence is missing, captures conflict, field semantics are unknown, a template is only supported by name/appearance, or the row would require guessed fields.
- Keep evidence extraction, spawn/static-dynel data creation, visual smoke, use/interact routing, objective progression, mission completion, and rewards as separate tasks.
- Local SQL patches must document exact rows affected, pre-apply verification query, apply command, post-apply verification query, rollback query, and confirmation that no unrelated rows changed.

Alternatives considered: Fill missing fields from nearby objects, rendered names, screenshots, template lookups, or smoke-test visual iteration.

Consequences: Capture-derived data work must fail closed. Smoke tests validate captured data reproduction; they do not create evidence or justify guessed data.

## Official Static Placements Are Evidence, Not Runtime Authorization

Decision: The normalized database-wide type-`1000014`
`PlayfieldDistrictInfo_t` corpus is the authoritative official static placement
evidence layer. Importing an `OfficialSpawnRecordId` does not establish a mob
identity, behavior profile, or runtime activation. Those joins remain separate,
explicit, and fail closed.

The source label `18.8.62_EP1` records extraction from the official old-graphics-
client database. EP1 and EP2 are client/graphics variants, not gameplay-content
or spawn-content partitions. `ACGHash_t` is retained as an official packed
four-byte scalar/tag with canonical text, official wire bytes, and native
uint32; it is not called a cryptographic hash, resource ID, dynel ID, or proven
mob-template identity.

Reason: Placement evidence is useful before identity and behavior are resolved,
but automatic promotion would turn structural data into guessed gameplay.

Consequences: All official records and duplicates remain independently
addressable; parser-limited resources remain explicit without synthetic rows;
unknown runtime state remains nullable; and no imported record may spawn until
a separate governed mapping and behavior-readiness gate authorizes it.

The checked-in corpus is retained as offline tooling and test evidence. It is
not compiled into or loaded by `ZoneEngine_New`, and the current derived Linux
inventory rejects the obsolete runtime corpus. Evidence availability is not
runtime activation; the gameplay spawn path remains under its separately
governed current content sources.

## Database Safety

Decision: Use only `cellao_codex_clean`; do not change schemas or wipe data without explicit approval.

Reason: Mike has multiple MySQL databases and active local data.

Alternatives considered: Broad DB reset or schema repair for convenience.

Consequences: Debugging may require more careful targeted queries, but it avoids data loss.

## Current-Client Parity Beats Legacy Assumptions

Decision: The current installed AO client is newer than the original AO Rebirth target, so current-client captures and AO stripdown docs can override old AO Rebirth structure assumptions.

Reason: Sit/stand, death/respawn, and packet shape issues were tied to client-version mismatches.

Alternatives considered: Preserve old packet models because AO Rebirth originally used them.

Consequences: Some legacy structures must be replaced or gated by evidence.

## FullCharacter Version 26

Decision: `FullCharacterMessageHandler.cs` must send `MsgVersion = 26`.

Reason: Current client expects version 26; this was part of the sit/stand repair.

Alternatives considered: Keep old version 25.

Consequences: Do not revert to 25.

## Live-Style Login State Initialization

Decision: `ClientConnected.cs` initializes live-style movement and social state:

- `State = 0`
- `CurrentMovementMode = Run`
- `PrevMovementMode = Run`
- `CurrentState = 0`
- `WaitState = 0`
- `SocialStatus = 4`
- `SpecialCondition = 3`
- `ActionCategory = 0`

Reason: This fixed sit/stand behavior with the current client.

Alternatives considered: Fake server-side action bootstrap packets.

Consequences: Do not reintroduce fake action bootstrap packets.

## Normal Auto-Attacks Use AttackInfo Only

Decision: Normal weapon and unarmed auto-attacks should use `AttackInfo` only, not `HealthDamage`.

Reason: Local tests produced duplicate combat text when `HealthDamage` was sent with normal weapon hits.

Alternatives considered: Emit `HealthDamage` for all damage because live uses it in some cases.

Consequences: Add `HealthDamage` only for targeted DoT, HoT, nano, environmental, or status cases after capture evidence.

## Equipment Visual Mesh Repair Is Locked In

Decision: Equip/unequip must call the verified weapon/equipment visual update path and preserve equipped-item persistence.

Reason: Weapon visuals, firing animation, dual wield, armor visuals, and equipment delays were validated in playtests.

Alternatives considered: Treat equipment as inventory-only state.

Consequences: Future inventory/trade repairs must not break equipped item visuals or persistence.

## Runtime Dynel Removal Uses Captured Despawn Path

Decision: Visible runtime NPC/corpse removal uses the captured identity-bearing `Despawn` frame, while `DropDynel` remains modeled/source-tested until runtime evidence proves a side effect.

Reason: Switching visible cleanup to `DropDynel` left corpses visible in playtest.

Alternatives considered: Use stripdown `DropDynel` for all removal.

Consequences: Static stripdown source is necessary but not always enough to choose runtime packet use.

## NPC Movement Requires Replay/Capture Before More Changes

Decision: NPC chase movement should not be patched from intuition.

Reason: Multiple attempted fixes caused jitter, teleporting, circling, snapping, and regressions.

Alternatives considered: Keep iterating in live playtest loops.

Consequences: Next movement work needs a replay contract and local packet comparison.

## Server Locomotion Uses the Vendored Lost-Eden Vehicle Port

Decision: ZoneEngine_New movement and collision use Lost-Eden's reverse-engineered `Vehicle_t` port, vendored unchanged (except a thread-static `DeltaTimeNow`) into `AORebirth.World.Vehicle` at the commit recorded in its `VENDORED.md`. Players drive a `CharVehicleSim` through Lost-Eden's `N3CharVehicle` flag-to-axis glue; NPCs follow an `NpcVehicleSim` `Path_t` + `PathGuide_t`. Line of sight, floor snap and raycasts query the same `ISurface`. BepuPhysics is removed. DotRecast navmesh planning is kept for NPC routes until `GraphPathFinder_t` is recovered.

Reason: The client simulates characters with this vehicle; running the same code on the server keeps server positions, speeds and NPC paths in step with what clients predict.

Alternatives considered: Keep Bepu for queries beside the vehicle, or reference Lost-Eden's source directly.

Consequences: Server-only behaviour (client position gates, mission movement hooks, flight authority, void hold, movement-mode stat) stays in `CharacterMotor`. Refresh the vendored copy per `VENDORED.md` rather than editing it. Surfaces are one-sided, so line of sight tests both directions.

## Historical: Retired ZoneEngine Hostile NPC Chase Navigation

Status: retained as historical design evidence. This section does not describe
the active `ZoneEngine_New` runtime. The preceding `ZoneEngine_New` movement
decision is the current authority.

Historical decision: Geometry-aware hostile-NPC pursuit was owned by `ZoneEngine.Core.Navigation` and consumed through the shared NPC combat/movement boundary. Playfields opted in only by supplying authoritative, versioned navigation input through `IPlayfieldChaseNavigationProvider`. PF127/resource `127` was the first provider and Vergil was the first representative end-to-end case; unsupported playfields preserved legacy direct chase.

Reason: Damage LOS proved the PF127 obstruction but left Vergil stationary. A shared bounded planner and route follower fixes that ownership gap without copying path state into individual enemies or pretending that unproven playfields are navigation-ready.

Alternatives considered: Vergil-specific waypoints, PF127 coordinates in boss logic, unconditional game-wide grid assumptions, or weakening LOS so the attack could continue through the wall.

Consequences: Every hostile NPC using the shared PF127 combat path inherits collision-aware chase. Route failure holds safely and retries with bounded work. Other playfields require authoritative geometry/provider tests before activation. PF127 remains same-elevation only, private live route validation is required, and no official AO pathfinding parity is claimed. See `docs/project/NPC_CHASE_NAVIGATION.md`.

## Trade/Inventory Fixes Must Stay Narrow

Decision: Player trade, corpse loot, and inventory acks must be fixed by packet path and source evidence, not by global item/credit clamps.

Reason: The same inventory packet families are used by loot and trade; broad fixes can duplicate items or break credits.

Alternatives considered: Clamp suspicious credit values or forcibly refresh inventories.

Consequences: Trace root cause first, then patch the confirmed emitter.

## Gameplay Systems Own A Subsystem Folder

Decision: New and active gameplay systems live under
`AORebirth/Server/ZoneEngine_New/Core/<System>/`, not inside
`AORebirth/Server/ZoneEngine_New/Core/Playfield/Playfield.cs`. See
`docs/project/SUBSYSTEMS.md`.

Reason: Playfield monolith merges and git rebase pulls repeatedly wiped or collided with in-progress systems (Mail). Folder ownership makes commits, reviews, and merges survivable.

Required workflow:

- Commit the subsystem before every pull.
- Use `git pull --no-rebase` (merge), never rebase local subsystem work onto origin.
- Push subsystem commits so GitHub holds the backup.

Alternatives considered: Keep growing `Playfield.cs`; only use separate VS projects for every system.

Consequences: Agents extract systems into `Core/<System>/` when touching them. Wire models stay in AOtomation.Messaging.

## Walk-In Interiors Are Per-Door Proxy Instances

Decision (2026-10-22): A playfield reached through a walk-in door that records a way back (a TeleportProxy
into a shop, a building, a training backyard) opens as its own playfield instance per source door, as live's
proxy playfields did. The client data has 133 such destinations: 130 indoor interiors and the three training
backyards 950, 952 and 954, so the rule is the door kind, not the destination's indoor flag. The instance id is derived from the door (`ProxyPlayfieldIds`: 0x7F000000 + the door
instance's low 24 bits), so it needs no table and is the same on every zone instance and after a restart.
A `Playfield` now has a `ResourceId` (the interior whose GameData it loads) beside its `Identity`; data
lookups key on the resource, runtime identity on the identity. The client is only ever told the resource
(`Playfield.ClientPlayfieldId`): it zones as into the plain interior, and only the server knows the instance.
Retail instead named the interior as a 0xC79E identity with a generator (0xC77D) re-keying the interior's
dynels; telling the client the instance id without that generator fails ("Unable to find the playfield data").

Reason: Mike: multiple Clan Ordinary/Advanced Supermarket doors in 540 (and the two supermarkets in 800) led
to one shared shop; each door should be its own instance of the shop.

Alternatives considered: A lookup table of instances (needs persistence and cross-instance agreement);
instancing per player or team (not how live shops behaved); leaving interiors shared.

Consequences: One-way portals (TeleportProxy2, LineTeleport) and zone borders are never instanced. Dungeon ids are now bounded
below the proxy block. A character saved inside an instance whose door no longer leads to an interior logs
in on the door's playfield. The legacy `teleports` table still rewrites 85 doors to a different interior
than the client data names (for example 800's Advanced Supermarket door to 1186); those rows decide which
interior an instance copies and need their own review.

## Passive Regeneration Follows The Sitting Rules

Decision (2026-10-21): `PassiveRegenCalculator` computes each natural delta as breed base + skill bonus
(one per 100 points: Body Development for health, Nano Pool for nano) + the HealDelta / NanoDelta stat.
Sitting multiplies only the skill bonus by 1.5 (rounded down) and shortens the health tick by one second
(floor 1 s). The nano tick keeps its standing interval when sitting.

Reason: Mike supplied the live rules: "1.5 times the Health regeneration from the Body Dev bonus sitting
down", "1.5 times the Nano regeneration from the Nano Pool bonus sitting down", "the tick rate for
Healdelta is increased by 1 second (a 22 second tick becomes 21)", "Nanodelta only gets an increased tick
amount based on your Nano Pool skill but ticks just as fast".

Alternatives considered: The earlier AO-Universe reading (nano bonus from Body Development, sitting halves
both intervals), which this replaces.

Consequences: Nano regeneration now scales with Nano Pool; low-Stamina characters heal slower while
sitting than before (one second off the tick rather than half the tick).

## Social Aggro Reach Comes From The Attacked NPC's Spawn Radius

Decision (2026-11-05): a call for help reaches same-side, same-family NPCs within the attacked NPC's own spawn
point AssistanceRadius (capped by `socialAggro.maxRadius`), not within each helper's radius.

Reason: on 1931 Uklesh the Frozen (AssistanceRadius 30) answered every nearby Exarch's call (radius 5-7,
about 9-11 m away), where retail does not link pulling an Exarch to the boss. Pulling Uklesh still brings
the Exarchs.

Alternatives considered: requiring both radii to reach (no linking either way); lowering UKTH's radius in
the spawn data (hides the rule for every other wide-radius boss).

Consequences: a wide-radius NPC pulls in its neighbours; a short-radius NPC no longer draws a wide-radius
neighbour. Chained calls (`chain`) use each helper's own radius as it calls in turn.

## NPC Hits Draw Threat And Social Aggro Like Player Hits

Decision (2026-10-22): `NpcCharacter.OnDamaged` registers threat from any NPC attacker, not only players
and players' pets, so an NPC hit by another NPC fights back and calls its kind for help. A call for help
against an NPC attacker (not a player's pet) uses `socialAggro.npcVsNpcDefaultRadius` (Gameplay.json,
20 m) when the spawn point carries no AssistanceRadius; `defaultRadius` still governs player attackers.

Reason: Mike asked that NPCs social aggro in NPC-versus-NPC combat (the ICC shuttleport alien swarm on
4582). Only 57 of 630 playfields carry any AssistanceRadius in their spawn data, 4582 none, so the
existing per-spawn radius alone never produced helpers there; widening `defaultRadius` would also change
every NPC's response to players.

Alternatives considered: raising `defaultRadius` for everyone (changes player pulls across 573 playfields
without spawn radii); scripting the assist per NPC (content in scripts for a general mechanic).

Consequences: NPC fights now spread to same-side, same-family neighbours within 20 m; a guard struck by an
alien draws its fellow guards, and a struck alien draws its swarm. Players' pets are unchanged.

## Scripted Marches Leash From Their Target, Not Their Spawn

Decision (2026-10-22): while an NPC script holds or last issued a `WalkTo`/`RunTo`, or a brain patrol is
walking to a waypoint, the brain's distance leash measures from that walk's target (`NpcBrain.LeashAnchor`)
instead of `Home`; the scripted anchor clears when the program releases movement or re-posts the NPC, the
patrol anchor when the patrol stops. A new `SetHome` verb (`Here` or a point) moves `Home`
itself, so a march that ends far from the spawn can make the destination its post.

Reason: the ICC shuttleport alien swarm marches about 82 m from its spawn, past the 70 m leash range, and the
return route is on another navmesh island, so the leash snapped every alien back to its spawn mid-march or
on arrival.

Alternatives considered: raising the leash range (changes every mob); auto-homing on arrival (implicit; a
patrol leg would silently re-home each waypoint).

Consequences: scripted marches and patrol routes of any length work; combat still leashes by "nobody engageable nearby" as
before, and a fight that drags more than 70 m from the walk's target leashes to the current `Home`.

## NPCs Spread By Identity, Not By Steering

Decision (2026-10-22): `NpcSpread` gives every NPC a stable offset derived from its identity (multiplicative
hashing). A chase walks to an approach point inside the shortest weapon's reach, fanned across a 120-degree arc
around the target from the bearing the NPC first took it at; a scripted `WalkTo`/`RunTo` destination and a
patrol waypoint are shifted by up to 4 m (scaled from the body size). Direct script legs, pets (own formation),
wander (already random) and the walk home are not spread.

Reason: packs chasing one target, and the shuttleport swarm arriving at one point, stacked on a single spot.
Mike asked for a low-cost, best-effort spread: NPCs try to separate but are not required to.

Alternatives considered: neighbour-aware separation steering (a locality query per replan and a feedback loop
with the path planner); exact slot assignment (bookkeeping per target). Both cost more than the problem is
worth for a cosmetic outcome.

Consequences: no per-tick cost and no queries; two NPCs can still overlap when the arc or disc is crowded.
Scripts that need an exact arrival point use a direct leg, or accept the shift.

## NPCs Open At Long Reach, Then Close In

Decision (2026-10-22): an NPC's chase no longer ends the moment any weapon can reach. It settles within its
shortest weapon's reach and never farther than 10 m edge to edge (`NpcBrain.HoldsFightDistance`). In reach of
some weapon it alternates: hold and fire for 4 s (plus or minus 1.5 s from its identity), step closer for at most
3 s, repeat. A target out of every weapon's reach gets a plain chase. Pets, immobile or rooted NPCs, and NPCs
with no route closer hold where they are.

Reason: multi-weapon NPCs stopped at their longest reach and sniped (the alien specialist at 20 m with five 2 m
weapons idle; rifle-carrying mobs at 30 m). Mike chose "close to the shortest weapon's range" plus "close in over
time" from the options offered.

Consequences: melee-capable NPCs end up in melee and rifle mobs within 10 m. Kiting a ranged NPC by backing
off inside its reach does not keep it running: every step closer is followed by a hold in which it fires
(ranged weapons cannot fire on the move). A data-driven per-NPC preferred range was offered and not taken yet.

## NPC Kills Leave No Spoils

Decision (2026-10-22): an NPC that dies with NPC damage on it and no player (a pet counts as its owner) at 20% or
more of all damage leaves a corpse with no item table roll and no credits (`KillRewardResolver.PlayersEarnedSpoils`,
`Corpse.ForfeitSpoils`). Items the NPC was given to carry (SpawnItem) still drop.

Reason: Mike asked that an NPC killed by NPCs, where players did not do enough damage, award no credits or loot.
Scripted NPC wars (the ICC shuttleport alien swarm) otherwise fill the area with free corpses.

Consequences: uses the same 20% bar as experience; loot rights were already withheld when an NPC topped the damage.

## A Target Is Only Reachable If A Weapon Connects From The Route's End

Decision (2026-10-22): `NpcBrain.CanPathTo` also requires that, standing at the closest point the navmesh route
reaches, the NPC's longest weapon reaches the target (3D edge distance). Equal-threat ties prefer a target the NPC
can hit now, then its current target, then the nearest (`BreakThreatTie`).

Reason: scouts with 2 m weapons chased an immobile guard posted on a deck 3 m above the plaza, ran back and forth
beneath it, and ignored guards standing inside them. Reachability only asked whether the NPC could walk near the
target, not whether it could then hit it.

Consequences: such a target has no chance; after the 2 s per-target grace the NPC switches to another enemy it
can hit, or leashes home when there is none.
