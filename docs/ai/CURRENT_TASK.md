# Current Task

## Current Focus

Complete the Subway dungeon from the existing capture corpus before requesting
more gameplay evidence. The full location inventory, raw lifecycle recovery,
and content-level Subway ledger are complete. All `65` Subway-bearing sessions
with raw packet rows now reprocess successfully, so incomplete legacy
projections no longer require repeat gameplay captures. The active work is to
promote the recovered exact evidence and then validate the quarantined PF127
population in bounded runtime batches.

## Quest-system foundation and TOTW gateway (2026-07-17)

- The MySQL-backed, character-scoped mission repository now covers lifecycle,
  objective progress, replay-safe observations, character/account flags, and
  idempotent reward stages. Rex B18C through the B18F/B194 handoff now use this
  runtime instead of process-local authoritative state.
- Windcaller Karrec's capture-backed PF655 flow now covers dialogue acceptance,
  burger/card handout, an exact two-item trade, `+2` stat-75 side tokens, a
  durable `5000` personal-research allocation record, mission cleanup, and
  account flag `totw-wall-access`. The known wall `Terminal:C004028F` transfers
  eligible accounts to PF647 at payload landing `(1814, 29, 2699)`.
- Remaining validation is live database/schema startup and private-client smoke
  for restart persistence, duplicate prevention, dialogue/trade/rewards, and
  denied/eligible wall use. The capture does not prove the official account-flag
  identity, denial packet, NPC spawn template, total ordinary XP, or research
  progression semantics; unresolved `PerkUpdate` fields are not replayed.
- Detailed status: `docs/project/QUEST_SYSTEM_AUDIT_20260717.md`.

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
- The content-level ledger covers all `72` Subway-bearing sessions with `25,320`
  aggregated evidence rows: `59` official-live and `13` AORebirth-private.
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
- The generated ordinary provider now preserves `282` exact, death-linked,
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
  Molecules item `301713` is `1/3`, and the twelve strict Slum Runner outcomes
  from `20260716-222201` are included without treating every observed item as
  guaranteed.
- Capture-local SCFU ownership now preclassifies player-owned pets before the
  weaker dossier/combat/stat/movement role heuristics, so Killer and other
  owner-linked pets can no longer be projected into the enemy ledger.
- Combat evidence indexing now includes `20260709-225408`,
  `20260710-211430`, `20260716-221358`, and `20260716-222201`, with normal and
  critical hit summaries separated in
  `docs/generated/subway_enemy_combat_contracts.json`.
- Reviewed legacy capture `20260709-213711` now contributes exact Workman
  Striker and Architect Striker combat rows. Declared overlap rules deduplicate
  only the simultaneous `20260709-212115 -> 212336/213711` projections within
  a `20`-millisecond audited logger-skew boundary. Workman therefore has `47`
  distinct normal local-player hits at `14..23`, six distinct criticals at
  `36..42`, and a `5.092328`-second median attack interval; two Killer-pet hits
  remain separate. Architect has `15` distinct normal hits at `13..17`, one
  `38` critical, and a `5.425420`-second median interval. Unrelated captures,
  events within one capture, and target roles are never collapsed.
- The reviewed raw combat burst from legacy capture `20260709-222339` proves
  Strike Foreman's empty `SpecialAttackWeapon`, `Attack` initiation, and three
  outgoing `AttackInfo` packets against the non-local player Wardog: two normal
  `18` hits and one `40` critical at `4.849144`- and `5.000854`-second
  intervals. These rows are other-player evidence only; they do not become a
  local-player damage range. The sequence/byte-exact fallback yields to derived
  rows and preserves that target-role boundary.
- The diagnostic quarantine selector now changes spawn eligibility in the
  world-population owner when explicitly selected. The selector is disabled in
  the normal runtime; all `38` rows remain quarantined. A bounded
  `population-activation-ledger.csv` now records `ELIGIBLE`, `MATERIALIZED`, or
  `FAILED` for selected rows so the next private-client batch can distinguish
  selection from actual runtime creation without changing eligibility.
- Slum Runner is the third enemy admitted by the whole-enemy acceptance gate,
  after Thief and Filth Flea. Its 24 exact spawns, captured `5..11` normal
  damage and `4.210098`-second cadence, shared chase, strict loot sample,
  CATMesh `31774`, 19 corpse/credit observations, ordinary corpse lifetimes,
  and observed `59.433`-second death-to-respawn interval are guarded together.
- Molested Molecules is the fourth accepted ordinary enemy. Its nine exact
  spawns, `16..42` normal player-facing damage, `4.749995`-second cadence,
  shared chase, three strict loot outcomes, CATMesh `5921`, seven exact
  positive-credit corpses, four-minute private ordinary respawn policy, and
  ordinary corpse lifetimes are guarded together. The respawn value remains
  explicit private-server policy rather than an official-live timing claim.
- Disobedient Bot is the fifth accepted ordinary enemy. All 12 exact spawn rows
  now preserve captured NPC family `138` and an explicit `450`-second post-NPC-
  despawn schedule; official capture `20260708-143600` records `459.913`
  seconds death-to-replacement and a `0.190`-unit position delta. Seventeen
  normal local-player SIW1 hits prove the aggregate `8..15` damage envelope,
  while focused attempts retain the capture-exact `5.973723`-second recharge.
  SIW1 context is selected from the spawned level (`L5=30/30/30/30/22`,
  `L6=35`, `L8=45`, `L9=49`, `L10=54`); active L7 uses the explicit bounded
  midpoint policy `40`, and other levels fail closed. Thirteen valid exact
  corpse/credit chains, seven strict loot outcomes, two proven item
  memberships, CATMesh `15215`, shared chase, and ordinary corpse lifetimes are
  guarded with combat and respawn. The two existing Bot rows in the 38-row
  operational quarantine remain quarantined; profile acceptance did not enable
  them. Critical behavior, proactive aggro radius, and leash/reset distance
  remain unobserved rather than guessed.
- Workman Striker now has a strict, generation-deduplicated loot denominator
  from ten complete first corpse opens: eight positive and two explicitly
  empty. Ten item/QL entries retain exact `1/10` or `2/10` observed frequencies;
  ten other corpse generations were never opened and remain corpse/credit-only
  evidence. The generator fails closed against the two exact zero-item packet
  lines and matching corpse/dead-NPC generations. Wider pool completeness and
  official probabilities remain unresolved. All 21 active Workman spawns now
  resolve their exact owner-linked captured low/high/QL weapon tuple at runtime;
  missing, conflicting, unknown, or aggregate source selection fails closed.
  Weapon items own normal damage and recharge, with no fixed damage or synthetic
  attack context. The shared weapon-critical formula is still not evidence-
  backed, so Workman is not yet admitted by the whole-enemy gate.
- Melded Patterns now equips its capture-proven QL20 Irreparable Sleekblaster
  Minor `121817/121818`. Damage and recharge are item-owned through the shared
  equipped-weapon path; no special-attack context, fixed damage override,
  critical policy, loot probability, respawn exception, or acceptance claim is
  synthesized.
- One reusable reviewed legacy-open validator now promotes four more strict
  item tables from exact corpse-full-update/dead-NPC generations and their first
  raw `InventoryUpdate` before identity reuse. Shadow has 15 complete opens
  (eight positive/seven empty), ordinary Infector seven (three/four), Architect
  Striker four (three/one), and Melded Patterns four (three/one). Overlapping
  `20260709-212115/212336` Shadow generations count once; unopened and snapshot-
  only corpses never enter a denominator. Runtime uses independent observed-
  sample entries with `ItemPoolComplete=false`; the basis values are private
  existing-capture policy, not claims about official probabilities.
- The Subway combat-contract analyzer now supplements legacy identity mapping
  from `enemy-dossier.json` and exact corpse dead-NPC links before consuming
  combat rows. Its regenerated Bot projection recovers nine decoded
  local-player hits at `8..15`; the eight additional authoritative raw rows are
  retained in the 17-hit source audit instead of being silently discarded.
- Eumenides is now a dedicated named PF127 encounter from atomic capture
  `20260716-034559`: exact L20/2792 HP appearance, QL20 weapon context,
  capture-bounded proactive acquisition, shared LOS/chase/leash behavior, exact
  416-byte CATMesh `17905` corpse, and fixed observed `186` credits. Weapon
  damage and recharge remain item-owned; the expanded three-capture fight set
  proves 21 normal local-player hits at `25..45` with a `4.311321`-second median
  interval. Finalized `20260717-214751` adds one exact atomic corpse snapshot:
  186 credits plus QL22 Living Cyber Armor Sleeves `163430/163431`, QL1 item
  `301714`, and QL200 item `287146`. Wider loot-pool probabilities and active-
  nano refresh semantics remain unresolved. Follow-up `20260717-220340` starts
  after the corpses already exist, so its additional item rows remain membership
  evidence rather than a fabricated item-plus-credit outcome. Mike observed the
  live ten-minute respawn and 30-minute loot-bearing corpse timer during that
  session; the folder does not packet-encode those UI/timing boundaries. Runtime
  already uses those values, plus three-second empty cleanup.
- Capture `20260709-212115` now supplies six exact Subway merchant appearances.
  Tailor, Weaponsdealer, Armorer, Pharmacist, Tools, and Container Supplier now
  expose six owner-linked shop endpoints with all `202` stock rows in exact
  slot order. Container Supplier reuses the exact 62-row `Cont` inventory
  captured on vending-machine template `99634` in `20260613-221619`; its
  appearance and owner/terminal link remain sourced from `20260709-212115`.
  Dialogue remains unresolved and is not synthesized.
- Bitaxel is a player, not a Subway enemy: complete SCFU `PlayerInfo` and
  lifecycle `player=True npc=False pet=False` evidence now override combat-role
  heuristics throughout the generated content ledger.
## Validation

- Ordinary provider generator content-equivalence check: PASS.
- AOSharp analyzer build and SCFU self-test: PASS.
- NPC lifecycle decoder self-test, including finalized-window, legacy metadata,
  terminal-tail salvage, and snapshot-only corpse cases: PASS.
- Content-ledger classification and population-diagnostic tests: `34/34` PASS.
- Previously reviewed lifecycle reprocess set: `65/65` PASS; zero offline
  repairs, recaptures, and tool errors. The four newly indexed Eumenides folders
  are handled by their actual completion boundary: `214751` complete,
  `214612`/`215250` partial combat evidence, and `220340` analyzer-INCOMPLETE
  with offline decode required but no recapture required.
- Full `298`-folder location inventory and `72`-session Subway ledger
  regeneration: PASS.
- Current inventory/content-ledger regression suites: `27/27` PASS.
- Subway loot/corpse evidence: `18/18` PASS.
- Five-entry whole-enemy acceptance gate (Thief, Filth Flea, Disobedient Bot,
  Slum Runner, Molested Molecules): PASS.
- Official entry/main-exit zoning guardrails: PASS.
- Capture inventory classifier and reviewed-corpus drift check: PASS.
- World population foundation: `26/26` PASS.
- Subway merchant content: `4/4` PASS.
- Visibility interest/catalog: `12/12` PASS.
- Quarantine/spatial-selection guardrail: PASS.
- Named encounter/capture contract suite: `25/25` PASS.
- Runtime-coordinator ownership guard: PASS.
- Approved AORebirth Debug build: PASS.
- Chat, Login, and Zone restart: PASS; ports `6996`, `7012`, `7500`, and `7501`
  listening.
- Repository-wide AOtomation suite: `356/369`; 13 broader failures remain
  outside these changed Subway surfaces. Every focused test for
  Eumenides, Disobedient Bot combat/corpse/respawn, global loot, merchants, the
  whole-enemy gate, and capture inventory passes.
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
3. Strike Foreman has usable exact L19/736 HP appearance, QL19 weapon, raw
   `SpecialAttackWeapon` plus `Attack` initiation against the non-local player
   Wardog, three other-player outgoing hits (`18`, `18`, and critical `40`),
   approximately five-second observed target cadence, chase initiation,
   CATMesh `17870`, and `176` corpse credits. Local-player outcomes remain
   unobserved, and item loot, respawn timing, leash/reset, and exact acquisition
   range remain unresolved. Do not activate the encounter by guessing those
   missing boundaries; weapon-owned rolls must remain distinct from the
   observed post-mitigation other-player outcomes.
4. Bitaxel is classified as a player artifact and is not an enemy gap.
5. Container Supplier stock is resolved by exact template-`99634` capture
   evidence. Dialogue remains unresolved and must not be synthesized.
6. Geometry-safe capture `20260714-202820` identifies 18 unlocked interior door
   identities, including five observed in both open and closed states. It does
   not contain safe room-link indices. The working client-owned doors must not
   be replaced with invented server statels; the corpus still lacks
   identity-complete world static/container placements.

## Constraints

- Audit the existing corpus before any new capture request. No new capture is
  currently required.
- Mixed captures may contribute only exact PF127 identity rows.
- Keep Abmouth, Vergil, Eumenides, and Strike Foreman out of the ordinary
  recurring-enemy generator. Keep Bitaxel excluded as player evidence.
- Keep normal, critical, local-player, and player-owned-pet damage evidence
  separate.
- Loot observations prove membership and observed outcomes, not a complete
  official probability distribution.
- Do not auto-attach, inject, launch the AO client, or start live capture.
