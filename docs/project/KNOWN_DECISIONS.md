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

## Trade/Inventory Fixes Must Stay Narrow

Decision: Player trade, corpse loot, and inventory acks must be fixed by packet path and source evidence, not by global item/credit clamps.

Reason: The same inventory packet families are used by loot and trade; broad fixes can duplicate items or break credits.

Alternatives considered: Clamp suspicious credit values or forcibly refresh inventories.

Consequences: Trace root cause first, then patch the confirmed emitter.
