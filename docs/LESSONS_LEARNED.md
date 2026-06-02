# Lessons Learned

Generated: 2026-06-02

Future AI agents must read this file before implementing major systems.

## Guessing Packet Behavior Causes Regressions

Mistake: Patching movement, inventory, trade, or death behavior from visual symptoms alone.

Consequence: The client can accept packets but interpret fields differently, producing stale windows, wrong visuals, white screens, duplicated text, or movement artifacts.

Rule: Use official live capture, private-server capture, AO stripdown, AOSharp/AODB reference, or local code trace before changing behavior.

## Source Authority Must Stay Labeled

Mistake: Mixing official-live C2S-only evidence with private-server S2C evidence.

Consequence: Missing or stale labels lead to false conclusions about what the live server does.

Rule: Always label source authority and direction coverage.

## Current Client Differs From Original CellAO Target

Mistake: Assuming old CellAO packet structures still match Mike's current AO client.

Consequence: Sit/stand and death/respawn behavior broke until current-client structures were respected.

Rule: Current-client captures and hash-matched AO stripdown files can override old CellAO assumptions.

## HealthDamage Is Not Normal Auto-Attack Damage

Mistake: Sending `HealthDamage` for normal weapon hits because live uses it in some damage cases.

Consequence: Duplicate combat text.

Rule: Normal auto-attacks stay `AttackInfo` only. Add `HealthDamage` later only for captured DoT/HoT/nano/environment/status cases.

## Equipment Needed Visual Mesh Updates, Not Just Inventory State

Mistake: Treating equipped weapons as only inventory/stat changes.

Consequence: Weapon equipped in inventory but not visible in hand; attacks fell back to martial arts.

Rule: Preserve equipment visual mesh update path and stat recalculation.

## Player Trade And Corpse Loot Share Dangerous Packet Families

Mistake: Fixing trade or loot as isolated UI behavior without tracing shared inventory/container messages.

Consequence: Stale trade items, visual duplicates, invalid item copies, and credit desync.

Rule: Trace request and response packets end to end before changing either system.

## NPC Movement Is A High-Risk System

Mistake: Repeatedly adding stop/repath/leash/correction logic without a replay-backed contract.

Consequence: Jitter, teleporting, circling, delayed chase, and worse playtests.

Rule: Build movement changes from captured packet windows and replay tests, then patch runtime.

## `Playfield.cs` Is Too Broad

Architectural pitfall: `Playfield.cs` owns combat, death, corpse state, loot, despawn, movement, and telemetry.

Consequence: Small changes can accidentally affect multiple systems.

Rule: Patch narrowly and consider later service extraction after active behavior stabilizes.

## Smoke Tests Are Valuable But Not Full Simulation

Discovery: Source/assertion smoke tests caught and preserved many packet contracts.

Limit: Regex/source assertions cannot prove full client behavior.

Rule: Pair smoke assertions with Mike's focused playtests or replay/capture comparison when client visuals matter.

## Dirty Worktrees Need Discipline

Mistake: Letting unrelated gameplay, docs, packet model, and tool changes accumulate together.

Consequence: Harder commits, harder rollback, and higher chance of mixing unstable work with verified fixes.

Rule: Check status first and keep commits split by system.

