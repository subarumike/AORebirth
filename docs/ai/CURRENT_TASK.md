# Current Task

## Current Focus

Complete the Subway dungeon from the existing capture corpus before requesting
more gameplay evidence. The full location inventory and the content-level
Subway ledger are complete. Previously unindexed official-live combat, loot,
corpse, credits, and teleport evidence is now integrated; the remaining
unreferenced folders do not currently prove another safe implementation slice.

## Corpus Inventory

- `294` timestamped AOSharp capture folders were classified using exact
  playfield evidence only.
- `37` are Subway-only, `31` are mixed Subway/outside zoning sessions, and
  `222` are elsewhere.
- `4` folders are unresolved because they contain no gameplay packets or
  location snapshots; they are empty startup remnants, not missing evidence.
- After the current integrations, `15` Subway or mixed folders have no
  generated/runtime reference in the location inventory. The content ledger
  narrows that to only three official-live Subway-only sessions without a
  runtime-source reference: `20260709-213711`, `20260712-232848`, and
  `20260716-220255`. They contain partial/ambient evidence, not a new complete
  runtime slice.
- Names, character names, capture dates, and repository references do not
  determine location.
- The complete per-folder result is generated at
  `docs/generated/aosharp_capture_inventory.csv` and
  `docs/generated/aosharp_capture_inventory.md` by
  `Tools/inventory_aosharp_captures.py`.
- The content-level ledger covers all `68` Subway-bearing sessions with `21,559`
  aggregated evidence rows: `55` official-live and `13` AORebirth-private.
  It records identities, related identities, evidence kinds, source artifacts,
  row scope, realm, and reference category in
  `docs/generated/aosharp_subway_capture_content.csv` and `.md`.

## Implemented In This Slice

- Legacy finalized capture folders whose packet log continued after capture
  shutdown now decode only rows within `captureStartUtc..captureEndUtc`.
  Capture `20260708-004038` is recovered without recapture: `329` SCFUs,
  `15` corpse rows, and `9` respawn rows decode with zero errors while `13,247`
  trailing packet-log rows are explicitly excluded.
- Filth Flea normal player-facing damage now uses the merged official-live
  slot ranges: melee slot `0` rolls `3..10`, poison slot `1` rolls `14..24`.
  Critical `13` and `47` outcomes remain separate evidence and cannot widen
  normal runtime rolls.
- Filth Flea loot now preserves `18` complete official-live corpse outcomes:
  `15` proven item memberships and `5` empty inventories. Exact L4=`23` and
  L5=`29` credit rules are active; other captured spawn levels retain the
  accepted private `23..79` fallback policy instead of becoming unresolved.
- Thirty-two identity-matched, death-linked official-live corpse observations
  now supply exact CATMesh and per-level credit rules for Filth Flea, Thief,
  Mugger, Discarded Pet, Shadow, Slum Runner, Infector, Neural Burnout,
  Fragmented Soul, Melded Patterns, Molested Molecules, and Premature Pattern.
  Pre-existing and zero-credit unlinked corpses remain excluded.
- Offline recovery of raw capture `20260710-202132` now links L10 Mugger
  `(SimpleChar:7957E5CA)` to `(Corpse:00F6C001)`, exact CATMesh `17534`, and
  `88` credits. Its three-item inventory is indexed as one observed corpse
  outcome, but remains outside runtime loot because one outcome proves item
  membership, not independent odds or a guaranteed bundle.
- Official-live Subway zoning is restored exactly: PF127 entry landing
  `(65.80835,115.6148,318.9879)`, PF655 main-exit landing
  `(3304.028,35.11,837.9951)`, and their captured headings. The main exit keeps
  the post-zone grace and contact-edge latch so its in-radius official landing
  cannot bounce the character back into PF127. The second exit remains disabled.

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
- Slum Runner now uses seven death-linked corpse observations across
  `20260716-034656` and `20260716-215947`, CATMesh `31774`, and exact captured
  level rules L21=`131`, L22=`137`, and L23=`144`. Item loot remains a separate
  observed pool and no credit rule is invented for unobserved levels.

## Validation

- Ordinary provider generator content-equivalence check: PASS.
- AOSharp analyzer build and SCFU self-test: PASS.
- NPC lifecycle decoder self-test, including finalized-window filtering: PASS.
- Real offline recovery of `20260708-004038`: PASS.
- Subway content inventory tests: `10/10` PASS; full `294`/`68` corpus
  regeneration: PASS.
- Subway loot evidence: `14/14` PASS.
- Flea combat and whole-enemy acceptance guardrails: PASS.
- Official entry/main-exit zoning guardrails: PASS.
- Capture inventory classifier and reviewed-corpus drift check: PASS.
- World population foundation: `25/25` PASS.
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

Diagnostic session `pf127-vis-one-bot-20260717` currently enables only captured
Disobedient Bot source identity `79557C66`; all other quarantined rows remain
disabled. Log into the private server, enter PF127, traverse into and out of the
bot's interest range near `(151.409,107.615,271.044)`, then fight and kill it.
Confirm the client remains stable, the bot behaves normally, and its corpse can
be opened. This is the first bounded population rollout gate; do not enable
`ALL_38`.

## Remaining Capture-Backed Work

1. The three official-live Subway-only sessions without runtime references are
   audited: `20260709-213711` is unfinalized partial SCFU evidence,
   `20260712-232848` contains no Abmouth/Vergil identity row, and
   `20260716-220255` is ambient bridge evidence. None proves a missing safe
   content slice.
2. Strike Foreman has a complete captured population profile but no indexed
   outgoing combat or loot. Keep those subsystems unresolved rather than
   guessing.
3. Bitaxel has exact appearance evidence but no player-facing combat and an
   unresolved MonsterData value. Do not activate it yet.
4. Container Supplier stock and dialogue remain unresolved. Keep the captured
   appearance visible without synthesizing an inventory or interaction.
5. The `38` diagnostic population rows are all exact, non-duplicate official
   PF127 rows. One Disobedient Bot row is staged behind the ignored diagnostic
   selector; the remaining `37` stay quarantined until bounded private
   login/traversal validation proves the first activation is stable.
6. Existing PF127 door evidence describes working interior doors, not exits.
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
