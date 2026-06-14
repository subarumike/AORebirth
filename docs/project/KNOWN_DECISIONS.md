# Known Decisions

Generated: 2026-06-02

## Use Evidence-Backed Packet Repair

Decision: Packet and gameplay behavior changes must be backed by official live capture, private-server capture, AO stripdown source, AOSharp/AODB reference evidence, or local code facts.

Reason: Repeated visual/client issues were caused by guessing packet behavior.

Alternatives considered: Patch from visual symptoms or intuitive AI behavior.

Consequences: Repairs may take longer up front, but they are easier to defend and less likely to break unrelated systems.

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

