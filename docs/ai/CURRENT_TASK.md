# Current Task

## Current Focus

Complete the Subway dungeon from the existing capture corpus before requesting
more gameplay evidence. The full location inventory, raw lifecycle recovery,
and content-level Subway ledger are complete. All `65` Subway-bearing sessions
with raw packet rows now reprocess successfully, so incomplete legacy
projections no longer require repeat gameplay captures. The active work is to
promote the recovered exact evidence and then validate the quarantined PF127
population in bounded runtime batches.

## Corpus Inventory

- `294` timestamped AOSharp capture folders were classified using exact
  playfield evidence only.
- `37` are Subway-only, `31` are mixed Subway/outside zoning sessions, and
  `222` are elsewhere.
- `4` folders are unresolved because they contain no gameplay packets or
  location snapshots: `20260509-182711`, `20260528-210106`,
  `20260621-013227`, and `20260622-081426`. They are empty startup remnants,
  not missing Subway evidence.
- The `68` Subway-bearing sessions contain `65` sessions with actual raw packet
  rows. The three without raw rows are private validation/geometry sessions
  `20260714-171439`, `20260714-185728`, and `20260714-202820`; their exact
  non-packet artifacts remain indexed.
- The deterministic lifecycle batch reprocessor now reports `65/65` PASS,
  zero offline repairs, zero recapture requirements, and zero tool errors.
- Names, character names, capture dates, and repository references do not
  determine location.
- The complete per-folder result is generated at
  `docs/generated/aosharp_capture_inventory.csv` and
  `docs/generated/aosharp_capture_inventory.md` by
  `Tools/inventory_aosharp_captures.py`.
- The content-level ledger covers all `68` Subway-bearing sessions with `23,991`
  aggregated evidence rows: `55` official-live and `13` AORebirth-private.
  It records identities, related identities, evidence kinds, source artifacts,
  row scope, realm, and reference category in
  `docs/generated/aosharp_subway_capture_content.csv` and `.md`.

## Implemented In This Slice

- Raw-evidence inventory now counts actual packet-log and CSV data rows rather
  than nonzero file size. BOM-only and header-only sinks can no longer be
  mistaken for captured traffic.
- Legacy start-only metadata, one demonstrably truncated terminal packet row,
  exact two-byte run-speed alignment, observed opaque player/pet extensions,
  terminal special-attack slot omission, and the legacy ActiveNanos alignment
  family are handled as narrowly versioned evidence patterns. Internal or
  arbitrary corruption remains fail-closed.
- Lifecycle recovery is row-granular: one incomplete packet variant cannot
  quarantine hundreds of completely decoded SCFU or corpse rows. Final
  artifacts remain authoritative; pending artifacts are indexed only when a
  final artifact is absent and can never imply absence or completeness.
- Snapshot-only corpse presence no longer becomes a false decoder debt when no
  raw `CorpseFullUpdate` packet exists. Local presence remains usable evidence,
  while dead-NPC links, CATMesh, MonsterData, and credits stay unresolved
  unless a raw corpse update proves them.
- Reused corpse identities now keep the exact name and dead-NPC relationship
  from their own generation instead of inheriting the union of every prior use
  of the same identity.
- The generated ordinary provider now preserves `278` exact, death-linked,
  positive-credit corpse observations across `26` capture-backed profiles.
  The recovered deep batches include all accepted observations from
  `20260709-220439`, `20260709-222339`, `20260709-225408`,
  `20260712-153918`, `20260712-223719`, and `20260716-222007`.
- Legacy item snapshots are indexed as identity-linked evidence-only outcomes;
  they cannot become runtime drop odds. Runtime probability denominators come
  only from strict initial corpse snapshots, and reused loot-window opens count
  once per corpse generation. The previously false Stim Fiend attribution is
  removed; Disobedient Bot, Thief, and Filth Flea policies remain unchanged.

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
  L5=`29` credit rules remain active; the recovered corpus adds exact rules for
  every further observed level while retaining private fallback only for
  levels with no official corpse-credit observation.
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
## Validation

- Ordinary provider generator content-equivalence check: PASS.
- AOSharp analyzer build and SCFU self-test: PASS.
- NPC lifecycle decoder self-test, including finalized-window, legacy metadata,
  terminal-tail salvage, and snapshot-only corpse cases: PASS.
- Capture inventory, lifecycle batch, and content-ledger tests: `27/27` PASS.
- Full lifecycle reprocess: `65/65` PASS; zero offline repairs, recaptures, and
  tool errors.
- Full `294`-folder location inventory and `68`-session Subway ledger
  regeneration: PASS.
- Subway loot/corpse evidence: `18/18` PASS.
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

No additional official-live capture is required. The next runtime gate is the
already-staged diagnostic Disobedient Bot identity `79557C66`, followed by
bounded family batches from the `38` quarantined PF127 rows. All `38` are exact,
unique, and profile-backed; quarantine is an operational client-stability hold,
not missing content evidence. Do not permanently enable all `38` in one step
until the staged identity has a private-client runtime result.

## Remaining Capture-Backed Work

1. The raw lifecycle backlog is closed: all `65` raw Subway-bearing sessions
   decode and promote. The four location-unresolved folders are empty startup
   remnants and contain no recoverable gameplay traffic.
2. The `38` capture-backed PF127 population rows remain behind the diagnostic
   quarantine until bounded private-client validation. They comprise 11
   Discarded Pets, 11 Violent Vagabonds, 6 Stim Fiends, 5 Muggers, 2
   Disobedient Bots, 2 Looters, and 1 Deranged Shopper.
3. Strike Foreman has a complete captured population profile but no indexed
   outgoing combat or loot. Keep those subsystems unresolved rather than
   guessing.
4. Bitaxel has exact appearance evidence but no player-facing combat and an
   unresolved MonsterData value. Do not activate it yet.
5. Container Supplier stock and dialogue remain unresolved. Keep the captured
   appearance visible without synthesizing an inventory or interaction.
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
