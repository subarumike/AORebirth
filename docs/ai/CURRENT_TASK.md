# Current Task

## Current Focus

Complete the Subway dungeon from the existing capture corpus before requesting
more gameplay evidence. The full location inventory and raw lifecycle recovery
are complete. All `74` Subway-bearing sessions with raw packet rows now
reprocess successfully, so incomplete legacy
projections no longer require repeat gameplay captures. The active work is to
promote the recovered exact evidence and then validate the quarantined PF127
population in bounded runtime batches.

## Subway Tailor and vendors (2026-07-19)

- Finalized official-live capture `20260719-021611` now supplies the complete
  Tailor KnuBot dialogue, including exact ordered append segments, root/about/
  measurement/wares transitions, eight QL1 measurement rewards `256415..256422`,
  and the separate shopping-basket GenericCmd shop route.
- The server now accepts the captured inbound `KnuBotOpenChatWindow`, binds the
  allocated PF127 Tailor identity through the existing vendor runtime registry,
  sends Tailor's captured `Unknown2=0` open, and preserves the shop as a distinct
  GenericCmd action. The existing five non-Tailor merchants remain direct-use
  shops because this capture contains no preceding dialogue for them.
- The same capture preserves a second atomic stock observation for all six
  merchants: 203 rows versus the current 202-row runtime baseline. Pharmacist
  and Container match exactly; Tailor, Weaponsdealer, Armorer, and part of Tools
  vary. The exact alternate CSV is checked in as evidence, but runtime stock is
  unchanged because complete pools, weights, refresh timing, and QL-roll rules
  remain unresolved.
- Focused Tailor/bootstrap tests and the Debug build pass. Chat, Login, and Zone
  were restarted on ports `6996`, `7012`, `7500`, and `7501`. Private-client
  dialogue, reward, close/reopen, and basket validation remains pending.

## AOSharp capture-safety repair (2026-07-19)

- Official-client attempts `20260719-001621` and `20260719-001715` terminated
  abruptly. The second session loaded and recorded about 3.8 seconds of traffic,
  then ended mid-write without a managed callback exception or finalized capture
  boundary. Windows recorded the repeating native cascade `0xc0000409`,
  `GUI.dll 0xc00001a5`, and `0xc0000005`; prior dump analysis ties this signature
  family to the injected `ProcessChatInput` GUI patch. This is capture-tool/client
  corruption, not evidence of a Subway server failure.
- Capture-tagged injection retains packet/game hooks, rejects duplicate Bootstrap
  injection with a named per-client guard, and releases the guard on constructor
  failure or Bootstrap unload. The launcher refuses stale binaries through a
  deployed Bootstrap contract self-test before it selects a client target.
- Comprehensive enemy capture no longer instantiates the native PF127 geometry
  probe. Geometry remains isolated to the explicit geometry-only workflow; the
  promoted server collision asset is unchanged.
- The original crash attribution to the long-standing 131-byte native GUI rewrite
  was not live-proven, but disabling all chat interception caused the `/aocap`
  regression. Capture-safe mode now installs one isolated `ProcessChatInput`
  EasyHook only after the duplicate-injection guard is held. It dispatches exact
  `/aocap` and `/aosmoke` prefixes, passes every other command through unchanged,
  and never runs the native NOP rewrite or `GetCommand` hook. The tracked native
  string layout now allocates the required `0x18` bytes instead of the upstream
  undersized `0x14`, and every hook invocation disposes it deterministically.
  AOSharpLiveCapture itself acknowledges readiness only after initialization and
  both command registrations; the injector times out fail-closed otherwise. Mike
  again owns in-game `/aocap start` and `/aocap stop`; atomic
  external control remains a fallback. Native PF127 geometry probing remains
  disabled in comprehensive mode.
- Capture-plugin build and capture-safe injector build/self-test are the offline
  validation boundary. Live typed-command validation remains pending; do not
  describe the restoration as live-proven until Mike confirms it.

## Quest-system foundation and TOTW gateway (2026-07-17)

- The MySQL-backed, character-scoped mission repository now covers lifecycle,
  objective progress, replay-safe observations, character/account flags, and
  idempotent reward stages. Rex B18C through the B18F/B194 handoff now use this
  runtime instead of process-local authoritative state.
- Windcaller Karrec's capture-backed PF655 flow now covers dialogue acceptance,
  burger/card handout, an exact two-item trade, `+2` stat-75 side tokens, a
  durable `5000` personal-research allocation record, mission cleanup, and
  account flag `totw-wall-access`. The known wall `Terminal:C004028F` transfers
  eligible accounts to PF647 at payload landing `(1814, 29, 2699)`. PF655 now
  also materializes Karrec, Annoying Dude, and Maddy Cardile from their exact
  captured appearance contract; Annoying Dude and Maddy replay their complete
  `16`- and `19`-segment walking cycles while all three remain passive social NPCs.
- PF655 initial NPC visibility was recovered on `2026-07-18` by restoring the
  proven `ClientConnected` existing-character snapshot. A live login visually
  confirmed Karrec and Annoying Dude; final-boundary diagnostics recorded
  successful SCFU and `CharInPlay` writes for all three NPCs. Karrec's runtime
  SCFU body is `254` bytes and matches official capture `20260717-223626` after
  normalizing only the runtime dynel identity. The retained final-boundary trace
  is restricted to PF655 test client `CanbeAffected:22` and quest-NPC runtime
  identities `1000000` through `1000002`.
- Remaining validation is live database/schema startup and private-client smoke
  for restart persistence, duplicate prevention, dialogue/trade/rewards, and
  denied/eligible wall use. The capture does not prove the official account-flag
  identity, denial packet, NPC spawn template, total ordinary XP, or research
  progression semantics; unresolved `PerkUpdate` fields are not replayed.
- Detailed status: `docs/project/QUEST_SYSTEM_AUDIT_20260717.md`.

## Corpus Inventory

- `307` timestamped AOSharp capture folders were classified using exact
  playfield evidence only.
- `41` are Subway-only, `31` are mixed Subway/outside zoning sessions, and
  `231` are elsewhere.
- `4` folders are unresolved because they contain no gameplay packets or
  location snapshots: `20260509-182711`, `20260528-210106`,
  `20260621-013227`, and `20260622-081426`. They are empty startup remnants,
  not missing Subway evidence.
- The capture inventory contains `313` sessions: `44` Subway, `34` mixed,
  `231` elsewhere, and `4` unresolved. Its `78` Subway-bearing sessions contain
  `74` sessions with actual raw packet rows. The four without raw rows are
  private validation/crash/geometry sessions `20260714-171439`,
  `20260714-185728`, `20260714-202820`, and `20260719-001621`; their exact
  non-packet artifacts remain indexed.
- The deterministic lifecycle batch reprocessor now reports `74/74` PASS,
  zero offline repairs, zero recapture requirements, and zero tool errors.
- Names, character names, capture dates, and repository references do not
  determine location.
- The complete per-folder result is generated at
  `docs/generated/aosharp_capture_inventory.csv` and
  `docs/generated/aosharp_capture_inventory.md` by
  `Tools/inventory_aosharp_captures.py`.
- The checked-in content-level ledger currently covers `72` Subway-bearing
  sessions with `25,321` aggregated evidence rows: `59` official-live and `13`
  AORebirth-private. The six newer Subway-bearing sessions are present in the
  capture inventory and lifecycle batch; the ledger must be regenerated before
  its aggregate row and realm totals are restated.
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
- The generated ordinary provider now preserves `301` exact, death-linked,
  positive-credit corpse observations across `26` capture-backed profiles.
  The recovered deep batches include all accepted observations from
  `20260709-220439`, `20260709-222339`, `20260709-225408`,
  `20260710-211430`, `20260712-153918`, `20260712-223719`,
  `20260712-232137`, `20260716-034104`, `20260716-221358`,
  `20260716-222007`, and `20260716-222201`. The latest recovery adds 16
  identity-linked generations and 11 previously missing profile/level/credit
  tuples; the Discarded Pet audit adds exact L10 and L6 credit corpses from
  `20260708-004038` and `20260709-205921` without inferring cross-profile rules.
- Legacy item snapshots remain identity-linked evidence-only outcomes unless a
  reviewed raw first-open denominator pins every included corpse generation.
  Reused loot-window opens count once per generation, explicit empty packets
  count, and unopened or snapshot-only corpses do not. The previously false
  Stim Fiend attribution remains excluded; Disobedient Bot, Thief, and Filth
  Flea policies remain unchanged.

- Legacy finalized capture folders whose packet log continued after capture
  shutdown now decode only rows within `captureStartUtc..captureEndUtc`.
  Capture `20260708-004038` is recovered without recapture: `329` SCFUs,
  `15` corpse rows, and `9` respawn rows decode with zero errors while `13,247`
  trailing packet-log rows are explicitly excluded.
- Filth Flea normal player-facing damage now uses the merged official-live
  slot ranges: melee slot `0` rolls `3..10`, poison slot `1` rolls `14..24`.
  Reviewed source `79531748` from `20260709-205921` adds one slot-1 normal `15`
  and one slot-0 critical `7`; critical `7`, `13`, and `47` outcomes remain
  separate evidence and cannot widen normal runtime rolls.
- Filth Flea loot now preserves `18` complete official-live corpse outcomes:
  `15` proven item memberships and `5` empty inventories. Exact L4=`23` and
  L5=`29` credit rules remain active; the recovered corpus adds exact rules for
  every further observed level while retaining private fallback only for
  levels with no official corpse-credit observation.
- Offline recovery of raw capture `20260710-202132` now links L10 Mugger
  `(SimpleChar:7957E5CA)` to `(Corpse:00F6C001)`, exact CATMesh `17534`, and
  `88` credits. Finalized capture `20260719-021022` adds one exact L5 Mugger
  corpse with CATMesh `17534`, `44` credits, and one QL5 `123495/123496` item.
  Mugger runtime loot now uses the complete reviewed corpus of 18 first opens
  (15 positive/three empty), rather than promoting either corpse as guaranteed.
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
- Capture `20260712-232848` links Premature Pattern identity `79607A3B` back to
  source `79545356` through exact shared movement. That source now selects one
  complete stat-only generation: L17/368 HP/scale 98/RunSpeed 65 or L18/394
  HP/scale 98/RunSpeed 68. Its reviewed patrol follows the complete captured
  out-and-back route; this does not add an eighth population row. Neither
  generation invents a weapon, and uniform selection is private runtime policy.
- The normalized PF127 catalog is now `321` rows: `310` active and `11`
  quarantined diagnostic rows. It contains `26` profiles.
- Deep ordinary combat now uses capture-scoped identity mapping and only normal
  hits against the local player for runtime ranges. Critical hits and
  player-owned-pet hits remain separate evidence.
- Reviewed normal local-player hit evidence includes Incomplete Rebuild
  `17..35`, Melded Patterns `21..34`, Molested Molecules `16..42`, Neural
  Burnout `16..22`, Redundant Scan `19`, and Uncontrollable Anger `11..18`.
  Weapon-backed profiles do not replay those post-mitigation outcomes as fixed
  runtime damage.
- Incomplete Rebuild is now a complete accepted ordinary profile. Its ten exact
  PF127 sources select from `23` source-local capture-reviewed atomic
  level/health/scale/RunSpeed/weapon generations; selection occurs once per new
  population generation and cannot mix fields between observations. Fourteen
  later identities are associated to their unique source by an exact position
  or waypoint endpoint; ambiguous identity `7957E5F9` remains excluded because
  it has neither a close position nor a decoded waypoint. Every selected weapon
  owns runtime damage and recharge with captured AttackInfo context. The profile
  also preserves shared chase, captured return-home behavior, a conservative
  7-unit proactive policy, four-minute respawn, standard corpse lifetimes,
  strict `2/0 empty` first-open loot evidence, exact observed L17/L18/L19/L21
  credits, and explicit private-policy L20/L22 credit interpolation.
- Incomplete Rebuild nano `90405` restores `21` CurrentNano immediately and for
  `959` later 15-second ticks, refreshes without stacking, costs `47` nano and
  `6` NCU, and uses the capture-backed four-hour duration and 20-unit range.
  Selection timing, 25-percent cast chance, 50-percent self targeting, initial
  phase, and L17..L22 nano pools remain explicit private policy; combat actions
  continue while it casts.
- Redundant Scan no longer uses that observed `19` as fixed runtime damage. Its
  four current sources now select from ten source-local capture-reviewed atomic
  level/health/scale/RunSpeed/weapon generations across L19..L22, including
  same-level weapon rerolls. Three stationary anchors require a unique
  1.5-unit position association; source `795451C4` is the sole captured patrol
  anchor and later rows must retain that unique waypoint shape. Incomplete SCFU
  or weapon observations remain report-only. Runtime fails closed without one
  exact selected tuple and lets item stats own damage and recharge while
  preserving captured AttackInfo `ammo=17`, slot `6`, unknown `0`, instance
  `0`. Its captured
  `121336 -> 121248` support-nano pair now selects the nearest ordinary ally in
  the observed 7.5-unit envelope with self fallback, pauses weapon/patrol ticks
  for the 1.400106-second cast, broadcasts the captured packet sequence, applies
  the nanos.dat-backed `+9/-13` deltas to the exact 23 weapon/nano skills, and
  refreshes/reverses owned transient state after the 180-second duration without
  NCU, DAO, or threaded-timer reuse. A conservative 7-unit private proactive
  aggro policy is enabled from the observed acquisition. Its four exact active
  spawns, captured patrol/static dispositions, strict `2/1 empty` loot, exact
  L19..L22 corpse credits/CATMesh, ordinary corpse lifetimes, and private
  four-minute respawn now pass the whole-enemy gate. Private runtime behavior
  remains to be checked.
- Fragmented Soul is now a complete accepted ordinary profile from the existing
  corpus. Its ten exact active sources select from `19` distinct source-local
  capture-reviewed level/health/scale/RunSpeed/weapon generations across
  L17..L21; the unmatched `7970245D` observation remains report-only. Selected
  items own runtime damage/recharge while captured AttackInfo preserves
  `ammo=24`, slot `6`, unknown `0`, and instance `0`. The profile remains
  retaliatory with shared chase, inherits the private four-minute ordinary
  respawn, retains strict `4/0 empty` loot, CATMesh `5921`, standard `3/240/3`
  corpse lifetimes, observed L17/L18/L21 credits, and policy-only L19/L20
  credit progression. Nano `95447` uses its exact nanos.dat target Skill effect
  (NanoRange `+42`), four-hour duration, cost `44`, NCU `7`, 20-unit range,
  observed 50/50 self-or-ordinary-ally split, and ten-second decision baseline.
  Nano pools are limited to the non-interpolated observed floors L19=`665`,
  L20=`782`, and L21=`829`; L17/L18 remain unresolved.
- Discarded Pet is now a capture-complete accepted ordinary profile from the
  existing corpus. All 29 exact L5..L10 rows are configured active; the 11
  newly enabled rows still need a bounded private-client activation smoke.
  Captures `20260708-143600` and
  `20260709-210452` prove 37 normal local-player SIW1 hits at `9..18`; four
  `30..33` criticals remain report-only. Runtime preserves AttackInfo ammo
  `-1`, slot `0`, unknown `0`, instance `SIW1`, and the conventional
  `5.089763`-second median across 30 same-source landed-hit intervals. The raw
  SpecialAttackWeapon fifth field varies without a proven rule and is not
  synthesized. Retaliatory acquisition and an explicit 8.153-unit chase are
  preserved without inventing proactive aggro, leash, reset, or return-home
  boundaries. Strict `16/3 empty` loot, CATMesh `15929`, standard `3/240/3`
  corpse lifetimes, and 25 exact positive-credit corpses now include recovered
  L6 and L10 records from `20260709-205921` and `20260708-004038`.
- Uncontrollable Anger is now the twenty-first whole-enemy accepted ordinary
  profile. Its six exact rows remain active at captured levels
  `13,13,19,20,23,23`, with two captured patrols and four static anchors.
  Runtime keeps the two local-player SIW1 outcomes at `11..18` separate from
  four Killer-pet outcomes at `25..42` and one other-player outcome at `19`.
  The reviewed `20260709-222339` Killer cadence window preserves all three CSV
  intervals (`5.1165513`, `5.1671525`, and `10.1003489`) without discarding or
  dividing the doubled interval; runtime uses the six-decimal median
  `5.167153`. Retaliatory shared chase, strict `2/0 empty` loot, CATMesh
  `96177`, six exact positive-credit corpses, inherited private respawn, and
  standard `3/240/3` corpse rules pass together. Credits remain unresolved for
  active L19 and L23 rows because no exact level-credit observation exists.
- Reviewed raw first opens and strict `corpse-loot-observations.csv` snapshots
  contribute explicit empty corpses to denominators. Redundant Scan's observed
  item is `1/2`, Molested Molecules item `301713` is `1/3`, and the twelve
  strict Slum Runner outcomes from `20260716-222201` are included without
  treating every observed item as guaranteed.
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
  local-player damage range. Killed source `7954512E` is bound to exact QL19
  WeaponInstance `25713A73` and corpse `00F6E017`; exact captured positions
  prove a `20.250672`-unit proactive-acquisition lower bound. The sequence/
  byte-exact fallback yields to derived rows and preserves that target-role
  boundary.
- The diagnostic quarantine selector now changes spawn eligibility in the
  world-population owner when explicitly selected. The selector is disabled in
  the normal runtime, and no current population row remains quarantined. A bounded
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
  seconds death-to-replacement and a `0.190`-unit position delta. Fifteen
  normal local-player SIW1 hits prove the aggregate `6..15` damage envelope;
  ten captured misses remain explicit;
  three other-player hits and two player-owned Killer-pet hits remain separate,
  while focused attempts retain the capture-exact `5.973723`-second recharge.
  SIW1 context is selected from the spawned level (`L5=30/30/30/30/22`,
  `L6=35`, `L8=45`, `L9=49`, `L10=54`); active L7 uses the explicit bounded
  midpoint policy `40`, and other levels fail closed. Fourteen valid exact
  corpse/credit chains, eight strict loot outcomes, three proven item
  memberships, CATMesh `15215`, shared chase, and ordinary corpse lifetimes are
  guarded with combat and respawn. Both previously quarantined Bot rows are now
  active for bounded private validation. Critical behavior, proactive aggro
  radius, and leash/reset distance remain unobserved rather than guessed.
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
  attack context. The shared weapon-critical formula remains unproven, so the
  six observed critical outcomes stay report-only and no formula is invented.
  Workman is accepted by the whole-enemy gate with all 21 source weapons and
  spawns, fail-closed source resolution, shared chase, strict incomplete-pool
  loot, CATMesh/credits, private ordinary respawn, and corpse lifetimes guarded
  together.
- Melded Patterns now equips its capture-proven QL20 Irreparable Sleekblaster
  Minor `121817/121818`. Damage and recharge are item-owned through the shared
  equipped-weapon path; no special-attack context, fixed damage override,
  critical policy, loot probability, or respawn exception is synthesized. Its
  exact weapon path and those exclusions now pass the whole-enemy gate.
- Finalized capture `20260719-020104` adds capture-exact replay patrols for Bot
  source `0x79557C66` (four segments) and Vagabond source `0x7957E5C4`
  (26 segments). The capture also adds the Bot's third strict item membership,
  `113398/113399` at QL7, and brings its strict sample to three positive and five
  empty outcomes. Vagabond now has 14 strict outcomes, 13 positive and one empty.
  Its reviewed combat evidence contains only misses, so runtime uses the
  user-approved same-level Mugger `9..12` normal range while preserving the
  Vagabond's captured cadence and attack packet shape. The capture proves neither respawn
  timing nor corpse lifetime, and it does not establish background patrols for
  any other source.
- Finalized capture `20260719-021022` adds source-specific complete patrols for
  active Filth Fleas `0x7953AFCC` (10 segments, 28 complete cycles) and
  `0x795317F5` (18 segments, 12 complete cycles), active Discarded Pet
  `0x79528FDA` (24 segments, five complete cycles), and active Violent Vagabond
  `0x7953AFA1` (10 segments, four complete cycles). Four existing Flea routes
  and the existing Vagabond route are independently corroborated. Ambiguous
  complete routes remain evidence-only and are not mapped. The Violent
  Vagabond patrol evidence adds no landed combat result; all 22 family rows are
  nevertheless active under the explicit playability damage policy, and
  `0x7953AFA1` keeps its active disposition.
  Incidental Mugger evidence adds one miss and SIW context without changing
  the captured Mugger landed-damage range. This capture proves neither respawn
  timing nor corpse lifetime.
- One reusable reviewed first-open validator now owns 18 strict item tables.
  In addition to Shadow, ordinary Infector, Architect Striker, and Melded
  Patterns, it recovers Mugger `18/3 empty`, Discarded Pet `16/3`, Stim Fiend
  `13/0`, Looter `11/5`, Violent Vagabond `14/1`, Bloodcreeper `4/3`, Infected
  Attendant `4/1`, Fragmented Soul `4/0`, Deranged Shopper `2/0`, Incomplete
  Rebuild `2/0`, Redundant Scan `2/1`, Uncontrollable Anger `2/0`, Lost Thought
  `1/0`, and Neural Burnout `4/2`. Exact source/allocation allowlists and
  generation fingerprints fail closed; declared overlaps deduplicate, while
  unopened and snapshot-only corpses remain excluded. Generated summary
  metadata drives `IndependentEntries`, observed empty counts, and
  `ItemPoolComplete=false` without a catalog MonsterData hardcode list. Empty
  Shell and Premature Pattern still have no item table.
- Shadow, ordinary Infector, Architect Striker, and Melded Patterns are now the
  sixth through ninth accepted ordinary profiles. Coverage binds their exact
  spawns, appearance, capture-backed normal combat, shared chase, strict
  incomplete-pool loot, corpse visuals/credits, private 240-second ordinary
  respawn policy, and shared corpse lifetimes. Shadow's two, Infector's three,
  and Architect Striker's one observed critical outcomes remain report-only.
  Ordinary Infector remains isolated from Abmouth-owned specialization;
  Architect Striker keeps fixed captured combat without an invented weapon;
  Melded Patterns keeps exact weapon-owned damage/recharge without invented
  special-attack or critical context.
- Looter now resolves all eight owner-linked `123038/123039` weapon tuples by
  exact source identity and QL. Its visible equipped item owns normal damage and
  recharge; aggregate, missing, conflicting, and unknown source selection fails
  closed, and no special-attack body is invented. The whole-enemy gate definition
  now also covers Looter, Bloodcreeper, Stim Fiend, and Neural Burnout, bringing
  the accepted set to fourteen.
- Mugger is the fifteenth accepted ordinary profile. All nine current sources
  resolve exact QL1 `121567/121567` weapons and fail closed for aggregate,
  missing, conflicting, or unknown selection. The item owns damage, damage
  bonus, and recharge while the captured AttackInfo keeps only ammo `-1`, slot
  `6`, unknown `0`, and weapon instance `0`. The 38 normal `9..12` outcomes stay
  separate from three report-only `21` criticals; strict 17-open loot, exact
  CATMesh/level credits, chase, respawn, and corpse lifetimes pass together.
- Deranged Shopper is the sixteenth accepted ordinary profile and its one exact
  runtime row is active for bounded private validation. Source
  `0x79574527` resolves only its owner-linked QL8 `125454/125455` weapon; the
  aggregate, unknown, missing, or conflicting paths fail closed. Eight normal
  local-player hits span `9..15`, one `27` critical remains report-only, and the
  corpus retains two misses at ammo `-1`, slot `6`, unknown `0` (one from the
  current source). Strict `2/0 empty` loot, L8/L9 credits, CATMesh `5927`,
  chase, inherited four-minute respawn, and `3/240/3` corpse rules pass
  together.
- The Subway combat-contract analyzer now supplements legacy identity mapping
  from `enemy-dossier.json` and exact corpse dead-NPC links before consuming
  combat rows. Its regenerated Bot projection retains 14 local-player hits at
  `8..15`, three other-player hits, and two player-owned Killer-pet hits as
  separate target-role evidence instead of silently mixing them.
- The combat analyzer now also recovers split detail-only weapon updates, raw
  enemy misses, and captured SpecialAttackWeapon shapes. Repeated identical
  weapon updates deduplicate, multiple owner-linked weapon shapes leave the
  aggregate summary unresolved, and source-specific evidence remains intact.
- Eumenides is now a dedicated named PF127 encounter from atomic capture
  `20260716-034559`: exact L20/2792 HP appearance, owner-linked QL20 and QL17
  `123267/123268` weapon evidence,
  capture-bounded `23.359` proactive acquisition, shared LOS/chase/leash
  behavior, exact
  416-byte CATMesh `17905` corpse, and fixed observed `186` credits. Weapon
  damage and recharge remain item-owned; the expanded three-capture fight set
  proves 21 normal local-player hits at `25..45`, two captured misses, initial
  `143/143/143/143/0` special-attack context, and a `4.311321`-second median
  interval. Runtime retains the existing QL20 variant because no capture proves
  the QL17/QL20 respawn selection rule. Capture `20260717-214612` proves
  Eumenides attacks first at `23.358918` horizontal units; `20260717-215250`
  independently proves the same behavior at `21.203307`. Complete capture
  `20260717-214751` and the manually audited exact rows from metadata-
  unfinalized `20260717-215250` add two atomic 186-credit corpse snapshots.
  The first contains QL22 Living Cyber
  Armor Sleeves `163430/163431`, QL1 item `301714`, and QL200 item `287146`;
  the second contains QL1 item `301715`, QL16 item `160051/160050`, and QL200
  item `287146`. Wider loot-pool probabilities and active-nano refresh semantics
  remain unresolved. Follow-up `20260717-220340` starts
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
- Subway loot/corpse evidence: `22/22` PASS.
- Twenty-one-profile whole-enemy gate now includes Discarded Pet, Fragmented Soul,
  Redundant Scan, and Incomplete Rebuild after Deranged Shopper, Mugger, Looter,
  Bloodcreeper, Stim Fiend, and Neural Burnout
  joined the previously confirmed ten. Ordinary generation check, expanded
  gate, WorldPopulation `36/36`, and Subway loot `22/22` pass.
- Playfield lifecycle class: `56/63`; every Subway and ordinary-enemy test
  passes. The seven remaining failures are the existing session lifecycle,
  teleport sequencing, and visibility ownership guardrails outside this slice.
- Official entry/main-exit zoning guardrails: PASS.
- Capture inventory classifier and reviewed-corpus drift check: PASS.
- World population foundation: `36/36` PASS.
- Subway merchant/Tailor content: `6/6` PASS; dialogue bootstrap: `5/5` PASS.
- Visibility interest/catalog: `12/12` PASS.
- Quarantine/spatial-selection guardrail: PASS.
- Named encounter/capture contract suite: `26/26` PASS.
- Runtime-coordinator ownership guard: PASS.
- ZoneEngine compile: PASS after the approved engine stop cleared the running
  executable lock.
- Chat, Login, and Zone restart: PASS; ports `6996`, `7012`, `7500`, and `7501`
  listening.
- Repository-wide AOtomation suite: 20 broader failures remain
  outside these changed Subway surfaces. Every focused test for
  Eumenides, Disobedient Bot combat/corpse/respawn, global loot, merchants, the
  whole-enemy gate, and capture inventory passes.
- The broader visibility lifecycle class still exposes its pre-existing pet
  observer source-guardrail failure because `PetRuntimeService` contains both
  the shared visibility hook and the older `AnnounceOthers` call. It is outside
  this Subway population slice and was not changed.

## Next Runtime Check

The existing-corpus implementation pass for the five incomplete ordinary
profiles is complete. Do not request duplicate captures for any evidence now
indexed below. The whole-enemy gate remains 21 of 26 because the remaining
gaps are genuinely absent from the 72-session corpus: reset/leash boundaries
for all five; respawn cycles for Infected Attendant, Lost Thought, Empty Shell,
and Premature Pattern; usable weapon and repeated local cadence/range evidence;
local landed damage for Lost Thought; landed damage semantics for Violent
Vagabond; and strict Empty Shell/Premature Pattern loot.

## Remaining Capture-Backed Work

1. The current inventory contains `78` Subway/mixed sessions: `74` have raw
   packet data and `4` do not. The four location-unresolved folders are empty
   startup remnants and contain no recoverable gameplay traffic.
2. Sixteen accepted-profile rows are active and await bounded private-client
   validation: 6 Stim Fiends, 5 Muggers, 2 Disobedient Bots, 2 Looters, and 1
   Deranged Shopper. All 321 ordinary population rows are active; none remain
   quarantined. The 22 Vagabonds require a private-client playability smoke.
3. Five of the 26 ordinary profiles remain outside the whole-enemy accepted
   set. Their 43 rows are active: Infected Attendant
   `5/0`, Lost Thought `4/0`, Empty Shell `5/0`, Premature Pattern `7/0`, and
   Violent Vagabond `22/0`. The combat report now separates every reviewed
   target role: Infected retains one local `11` outcome plus local, other-player,
   and pet attack starts; Lost retains 11 other-player hits at `15..20` with a
   `4.5320703`-second median; Empty retains local `15`, two misses, other-player
   `19`, and nanos `26414`, `81998`, and `82482`; Premature retains local normal
   `22`, critical `41`, other-player `16`, and pet `38`. These incomplete
   outcomes remain report-only instead of becoming constant fixed attacks.
   Premature source `79545356` now has its two exact stat-only generations and
   complete captured out-and-back patrol, but combat, strict loot, respawn
   timing, and leash/reset remain incomplete. Self-cast nano `81829` remains
   report-only because current runtime support cannot safely represent its
   captured multi-effect/ChangeVariable behavior.
   Vagabond now has 26 distinct local misses after overlap deduplication, a
   `4.5802404`-second versioned median attempt interval, and two exact simultaneous
   other-player attack starts. Automatic acquisition/chase is capture-proven at
   a guaranteed `16.606338`-unit lower bound but remains report-only while
   landed damage is unresolved; runtime stays retaliatory with shared chase and
   no automatic radius. Runtime damage is the explicit same-level Mugger
   `9..12` playability policy, not a capture-parity claim. Its `450`-second post-NPC-despawn policy is derived from
   the exact `449.759588` interval. QL1 template `130590` is Red Wine, is
   rejected as combat input, and all 22 rows are active.
4. Strike Foreman has usable exact L19/736 HP appearance, QL19 weapon, raw
   `SpecialAttackWeapon` plus `Attack` initiation against the non-local player
   Wardog, three other-player outgoing hits (`18`, `18`, and critical `40`),
   approximately five-second observed target cadence, chase initiation,
   CATMesh `17870`, and `176` corpse credits. Killed source `7954512E` is bound
   to QL19 WeaponInstance `25713A73` and corpse `00F6E017`; exact source/target
   positions prove proactive acquisition at `20.250672` units. Local-player
   outcomes remain unobserved, and item loot, respawn timing, leash/reset, and
   the exact acquisition threshold/upper bound remain unresolved. Do not
   activate the encounter by guessing those missing boundaries; weapon-owned
   rolls must remain distinct from the observed post-mitigation other-player
   outcomes.
5. Bitaxel is classified as a player artifact and is not an enemy gap.
6. Tailor dialogue is resolved by capture `20260719-021611`; the other five
   merchants expose direct-use shops without preceding KnuBot dialogue in that
   capture. Container Supplier stock remains resolved by exact template-`99634`
   evidence. Variable stock pools, weights, refresh timing, and QL rolling are
   unresolved and must not be synthesized.
7. Geometry-safe capture `20260714-202820` identifies 18 unlocked interior door
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
