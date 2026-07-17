# Current Task

## Current Focus

Complete the Subway dungeon from the existing capture corpus before requesting
more gameplay evidence. The full capture inventory is complete, and the first
high-confidence implementation slice restores the deep ordinary enemy families
that were incorrectly excluded as named bosses.

## Corpus Inventory

- `294` timestamped AOSharp capture folders were classified using exact
  playfield evidence only.
- `37` are Subway-only, `31` are mixed Subway/outside zoning sessions, and
  `222` are elsewhere.
- `4` folders are unresolved because they contain no gameplay packets or
  location snapshots; they are empty startup remnants, not missing evidence.
- After the Eumenides, merchant, and Slum Runner integrations, `31` Subway or
  mixed folders still have no generated/runtime reference. That count is an
  audit queue, not proof that each folder contains independently usable content.
- Names, character names, capture dates, and repository references do not
  determine location.
- The complete per-folder result is generated at
  `docs/generated/aosharp_capture_inventory.csv` and
  `docs/generated/aosharp_capture_inventory.md` by
  `Tools/inventory_aosharp_captures.py`.

## Implemented In This Slice

- Restored `61` capture-backed spawn rows for eight recurring deep enemy
  families: Empty Shell, Fragmented Soul, Incomplete Rebuild, Melded Patterns,
  Molested Molecules, Premature Pattern, Redundant Scan, and Uncontrollable
  Anger.
- Two of those rows come from fully decoded SCFUs in the flushed
  `20260709-225408` packet log: Fragmented Soul `79545367` and Premature Pattern
  `79545356`. The stale start-time metadata and unrelated incomplete SCFUs do
  not invalidate those two `decoded_complete` rows.
- The normalized PF127 catalog is now `321` rows: `283` active and the same
  `38` quarantined diagnostic rows. It contains `26` profiles.
- Deep ordinary combat now uses capture-scoped identity mapping and only normal
  hits against the local player for runtime ranges. Critical hits and
  player-owned-pet hits remain separate evidence.
- Current runtime normal-hit ranges include Incomplete Rebuild `17..35`, Melded
  Patterns `21..34`, Molested Molecules `16..42`, Neural Burnout `16..22`,
  Redundant Scan `19`, and Uncontrollable Anger `11..18`.
- Strict `corpse-loot-observations.csv` snapshots now contribute empty corpses
  to loot denominators. Redundant Scan's observed item is `1/2`, Molested
  Molecules item `301713` is `1/8`, and the twelve strict Slum Runner outcomes
  from `20260716-222201` are included without treating every observed item as
  guaranteed.
- Combat evidence indexing now includes `20260709-225408`,
  `20260710-211430`, `20260716-221358`, and `20260716-222201`, with normal and
  critical hit summaries separated in
  `docs/generated/subway_enemy_combat_contracts.json`.
- The diagnostic quarantine selector now changes spawn eligibility in the
  world-population owner when explicitly selected. The selector is disabled in
  the normal runtime; all `38` rows remain quarantined.
- Eumenides is now a dedicated named PF127 encounter from atomic capture
  `20260716-034559`: exact L20/2792 HP appearance, QL20 weapon context,
  capture-bounded proactive acquisition, shared LOS/chase/leash behavior, exact
  416-byte CATMesh `17905` corpse, and fixed observed `186` credits. Weapon
  damage and recharge remain item-owned; item loot and active-nano refresh
  semantics remain unresolved. The private named-enemy policy is ten-minute
  respawn, 30-minute loot-bearing corpse, and three-second empty cleanup.
- Capture `20260709-212115` now supplies six exact Subway merchant appearances.
  Tailor, Weaponsdealer, Armorer, Pharmacist, and Tools expose their five atomic
  captured shop snapshots with all `140` stock rows in captured slot order.
  Container Supplier is visible but has no invented shop endpoint because its
  stock was not captured.
- Finalized Slum Runner capture `20260716-034656` now supplies six exact corpse
  observations with CATMesh `31774` and credits
  `144/144/144/131/137/131`. The six samples remain intact as explicit observed
  samples; no item loot or level correlation was invented.

## Validation

- Ordinary provider generator content-equivalence check: PASS.
- Capture inventory classifier and reviewed-corpus drift check: PASS.
- World population foundation: `25/25` PASS.
- Subway loot evidence: `12/12` PASS.
- Subway merchant content: `4/4` PASS.
- Visibility interest/catalog: `12/12` PASS.
- Quarantine/spatial-selection guardrail: PASS.
- Named encounter/capture contract suite: `25/25` PASS.
- Runtime-coordinator ownership guard: PASS.
- Approved AORebirth Debug build: PASS.
- Chat, Login, and Zone restart: PASS; ports `6996`, `7012`, `7500`, and `7501`
  listening.
- The broader visibility lifecycle class still exposes its pre-existing pet
  observer source-guardrail failure because `PetRuntimeService` contains both
  the shared visibility hook and the older `AnnounceOthers` call. It is outside
  this Subway population slice and was not changed.

## Next Runtime Check

Log into the private server and traverse PF127 with the normal quarantine
selector. Confirm the restored deep families remain stable; Eumenides appears,
aggros, shoots only with LOS, chases, leashes, dies, exposes `186` credits, and
respawns; all six merchants appear, the first five shops open, and Container
Supplier does not; and Slum Runner corpses use the captured visual/credit pool.
Do not enable `ALL_38` for this check.

## Remaining Capture-Backed Work

1. Strike Foreman has a complete captured population profile but no indexed
   outgoing combat or loot. Keep those subsystems unresolved rather than
   guessing.
2. Bitaxel has exact appearance evidence but no player-facing combat and an
   unresolved MonsterData value. Do not activate it yet.
3. Container Supplier stock and dialogue remain unresolved. Keep the captured
   appearance visible without synthesizing an inventory or interaction.
4. The `38` diagnostic population rows remain quarantined until bounded private
   login/traversal validation proves that their activation is stable.
5. Existing PF127 door evidence describes working interior doors, not exits.
   Do not remove them. The corpus does not yet provide identity-complete world
   static/container placements.

## Constraints

- Audit the existing corpus before any new capture request. No new capture is
  currently required.
- Mixed captures may contribute only exact PF127 identity rows.
- Keep Abmouth, Vergil, Eumenides, Strike Foreman, and Bitaxel out of the
  ordinary recurring-enemy generator.
- Keep normal, critical, local-player, and player-owned-pet damage evidence
  separate.
- Loot observations prove membership and observed outcomes, not a complete
  official probability distribution.
- Do not auto-attach, inject, launch the AO client, or start live capture.
