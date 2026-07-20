# Project State

Primary Codex memory file for AO Rebirth. This top section is the current source of truth. Historical sections below are retained for evidence and provenance; older Rex/B18D/B18E/gate notes in those sections may be superseded by the current state here.

## Current Focus

- Finalized Subway captures `20260720-042205`, `20260720-043018`,
  `20260720-044358`, and `20260720-044610` are integrated into the generated
  PF127 ordinary provider. The corpus now has `351` exact identity-linked,
  positive-credit corpse observations across `26` profiles. Workman strict
  loot is `30` opens / `22` positive / `8` empty with `27` item/QL entries;
  Infector is `14/6 positive/8 empty`; Neural Burnout is `6/4/2`; Lost Thought
  is `3/3/0`; and Uncontrollable Anger is `4/4/0`. The default `240`-second
  ordinary respawn policy remains unchanged because no capture proved a more
  specific replacement timer.
- Combat evidence now records Workman `59` normal `9..23` plus seven
  report-only criticals `28..42`, Infector `54` normal `15..36` plus three
  report-only criticals `52..75`, Neural Burnout `15..22`, and Slum Runner
  cadence `4.210628`. Workman runtime weapon resolution no longer depends on
  a hard-coded capture-row count; it requires the exact selected source and
  generation tuple.
- Finalized Subway captures `20260720-031855`, `20260720-032106`,
  `20260720-033513`, and `20260720-033749` are integrated. PF127 now has `322`
  active ordinary rows and zero quarantined rows. Workman Striker has `22`
  sources, `31` exact source-local atomic level/stat/weapon generations, three
  new captured patrols, normal local-player evidence `9..23`, and strict loot
  `13/2 empty`. Architect Striker now has `18` normal `10..17` outcomes and
  strict loot `6/1 empty`; Uncontrollable Anger has four normal `9..18`
  outcomes and strict loot `3/0 empty`. Infected Attendant gains one exact idle
  patrol and strict loot `5/1 empty` but remains incomplete because the new
  retaliation produced no landed hit. Strike Foreman's two new fights and loot
  snapshots are retained as named, report-only evidence; unresolved weapon
  selection, respawn, leash/reset, aggro threshold, and corpse lifetime keep
  that encounter dormant.
- Quest-system status as of `2026-07-17` supersedes the older process-local Rex
  and generic-scaffolding notes retained below for history. ZoneEngine now has a
  MySQL-backed character-scoped mission lifecycle, deduplicated objective
  observations, persistent character/account flags, atomic current-to-next
  handoffs, and an idempotent reward ledger. Rex B18C/B18D/B18E/B18F/B194 use
  this runtime. Windcaller Karrec's bounded PF655 flow includes burger/card
  handout and exact trade, `+2` stat-75 side tokens, a durable `5000`
  personal-research allocation record, account flag `totw-wall-access`, mission
  cleanup, and the captured wall transfer to PF647. PF655 now also materializes
  Karrec, Annoying Dude, and Maddy Cardile from capture-exact appearances; the
  latter two replay their complete `16`- and `19`-segment walking cycles as
  passive social NPCs. See
  `docs/project/QUEST_SYSTEM_AUDIT_20260717.md`.
- PF655 live validation on `2026-07-18` confirms the shared initial visibility
  snapshot originates from `ClientConnected`. Karrec and Annoying Dude were
  visually confirmed, all three quest NPC SCFUs reached and were accepted by
  the final transport boundary, and Karrec's `254`-byte body matches official
  capture `20260717-223626` apart from the expected runtime dynel identity. The
  diagnostic is bounded to PF655 test client `CanbeAffected:22` and the three
  quest-NPC runtime identities.
- Quest limitations remain explicit: live mission-table creation/startup and
  private-client restart/duplicate/reward/gateway smoke are pending; no general
  research-progression subsystem applies the recorded allocation; total ordinary
  XP, the official account-flag identity, denial packet, NPC spawn template, and
  interaction-distance boundary are uncaptured. Unresolved fixed `PerkUpdate`
  values are not emitted.
- The completed 313-folder location inventory identifies 44 Subway-only
  captures, 34 mixed Subway/outside captures, 231 elsewhere captures, and four
  unresolved startup/crash remnants. Of the 78 Subway-bearing sessions, 74
  contain actual raw packet rows; the four without raw rows are
  `20260714-171439`, `20260714-185728`, `20260714-202820`, and
  `20260719-001621`. The lifecycle batch reports `74/74` PASS with zero offline
  repairs, recapture requirements, or tool errors.
  Finalized `20260717-214751` is complete, partial `214612`/`215250` contribute
  bounded combat evidence, and `220340` remains analyzer-INCOMPLETE/offline-
  decode-required but explicitly does not require recapture. The content ledger
  contains 25,321 rows while keeping 59 official-live sessions separate from 13
  AORebirth-private validations. See
  `docs/ai/CURRENT_TASK.md`,
  `docs/generated/aosharp_capture_inventory.md`, and
  `docs/generated/aosharp_subway_capture_content.md`.
- The generated ordinary provider now preserves 314 exact, identity-matched,
  death-linked positive-credit corpse observations across 26 profiles. The
  recovered batches include all accepted ordinary observations from the deep
  `20260709-220439`, `20260709-222339`, `20260709-225408`,
  `20260710-211430`, `20260712-153918`, `20260712-223719`,
  `20260712-232137`, `20260716-034104`, `20260716-221358`,
  `20260716-222007`, `20260716-222201`, `20260720-031855`,
  `20260720-032106`, and `20260720-033749` sessions. The latest recovery adds
  23 exact generations and 12 previously missing profile/level/credit tuples;
  the Discarded Pet audit adds exact L10 and L6 credit corpses from
  `20260708-004038` and `20260709-205921`. No cross-profile credit rule is
  presented as capture proof.
  Runtime profiles preserve each proven CATMesh and only observed level-credit
  rules; players, owned pets, named encounters, outside-playfield rows,
  ambiguous links, and zero/post-loot rows remain excluded.
- Legacy item snapshots are retained only as exact identity-linked evidence
  outcomes and cannot become runtime drop odds. Strict initial corpse snapshots
  alone supply runtime probability denominators; reopened loot windows count
  once per corpse generation. The false Stim Fiend item attribution is removed,
  while Disobedient Bot, Thief, and Filth Flea runtime loot policies remain
  unchanged.
- Legacy lifecycle recovery now supports exact start-only metadata, a single
  demonstrably truncated terminal packet, observed SCFU run-speed/alignment and
  opaque-tail families, and terminal special-attack slot omission without
  inventing field semantics. Snapshot-only corpses with no raw
  `CorpseFullUpdate` are positive local-presence evidence rather than false
  decoder debt; CFU-only fields remain unresolved unless a raw packet proves
  them.
- Raw offline recovery of capture `20260710-202132` adds an exact L10 Mugger
  death link from `(SimpleChar:7957E5CA)` to `(Corpse:00F6C001)`, CATMesh
  `17534`, and `88` credits. The same corpse's three item rows are indexed as a
  single observed outcome but do not become guaranteed or independently rolled
  runtime drops without probability evidence.
- Official-live Subway zoning now uses the exact PF127 entry landing
  `(65.80835,115.6148,318.9879)` and PF655 main-exit landing
  `(3304.028,35.11,837.9951)` with captured headings. The existing post-zone
  grace plus a contact-edge latch prevents the in-radius official exit landing
  from bouncing back into PF127; the unwanted second exit remains disabled.
- Legacy finalized captures whose packet log continued after capture shutdown
  are decoded within the recorded capture window. Existing capture
  `20260708-004038` now recovers 329 SCFUs, 15 corpse rows, and 9 respawn rows
  with zero decode errors while explicitly excluding 13,247 trailing rows, so
  no repeat gameplay capture is needed.
- Active development priority: make the Subway dungeon, resource/playfield `127`, fully playable using capture-backed implementation.
- Current implementation roadmap:
  1. Complete NPC population, appearances, patrol paths, and combat validation.
  2. Complete static world objects, doors, containers, interactive objects, and environmental details.
  3. Implement room-by-room parity, named NPCs, scripted events, boss encounters, and dungeon progression.
  4. Add vendors, quest interactions, remaining polish, and capture parity validation.
- Subway content binding uses resource/playfield `127`; live runtime instance ids such as `R=1187842` and capture/runtime output such as `Playfield2:122002` must not be used as the server content binding key.
- Subway content work should use completed AOSharpLiveCapture folders supplied by Mike. Codex does not launch the AO client or capture tooling unless Mike explicitly instructs it in the current task.
- Prefer visible Subway gameplay improvements over architectural refactoring, implement incrementally within playfield `127`, and do not resume Playfield decomposition unless Mike explicitly requests it.
- Live AO captures are authoritative. Avoid speculative fixes and require capture-backed evidence for Subway behavior.
- PF127 has a promoted server collision asset from geometry-only safe capture `20260714-185728`, with fail-closed loading and segment/triangle queries. Mike live-validated that Vergil no longer damages the player through Subway walls and resumes attacking when line of sight is clear. The later pictured open-doorway repro proved attack permission was incorrectly reusing the wider movement corridor: the raw ground ray hit a low threshold and an offset movement probe clipped the frame. PF127 now uses a distinct captured `+1.0 Y` center attack ray while retaining all six body-width probes for chase movement and route validation. A global `ZoneEngine.Core.Navigation` hostile-NPC chase capability owns provider-gated movement checks, deterministic bounded grid/A* planning, cached route following, stuck/deviation recovery, retry suppression, lifecycle cleanup, and collision-aware return-to-home routing. PF127/resource `127` is the first and only enabled geometry provider; every hostile NPC on the shared PF127 combat path inherits it. PF127 retains its accepted `100`-unit default home leash, while finalized official-live capture `20260716-222007` gives Vergil a narrower `40`-unit NPC travel boundary: two resets produced `40.52` and `40.30` unit return paths while the living target remained about `5..8` units away. Vergil's target safety boundary remains `100`, preventing a fleeing target from causing an early reset. Vergil healing and Abmouth combat-only summon state reset with their leash return. The capture does not prove a moving home anchor, so runtime retains each NPC's activation coordinate. Other playfields preserve legacy chase until authoritative navigation input exists. PF127 remains same-elevation only because the promoted geometry does not prove walkable-floor projection or cross-elevation connectivity. Focused tests and the Debug build pass. Mike's `2026-07-17` private-client smoke confirms Vergil's corrected fight, `40`-unit reset, return home, and re-engagement work as intended. See `docs/project/NPC_CHASE_NAVIGATION.md`.
- AOSharpLiveCapture and AOSharpCaptureAnalyzer now support a PF127 geometry-only safe mode, durable no-throw capture boundaries, geometry snapshot export, and fail-closed promotion validation. The safe mode intentionally disables comprehensive packet/dynel callbacks and exists only for geometry acquisition after the character is already stable inside PF127; normal comprehensive captures remain the default for gameplay evidence. After official-client attempts `20260719-001621` and `20260719-001715` reproduced native GUI/SEH corruption, comprehensive capture injection gained a versioned fail-closed Bootstrap contract, a per-client duplicate-injection guard, atomic external file control, and isolation from native PF127 geometry probes. Disabling all chat interception caused the `/aocap` regression. Capture-safe mode now restores only an isolated `ProcessChatInput` hook for exact `/aocap` and `/aosmoke` prefixes after the duplicate guard is held; it does not run AOSharp's 131-byte native GUI rewrite or `GetCommand` hook, and all other commands pass through unchanged. The tracked native string layout uses the required `0x18` allocation and deterministic disposal instead of the upstream undersized `0x14`; AOSharpLiveCapture signals readiness only after initialization and both command registrations, and injector success requires that bounded acknowledgement. Offline capture/injector validation passes without launching or attaching to the client; live command validation remains pending.
- The generalized `CapturedSubwayEncounterRuntimeService` now owns capture-backed named encounters without treating every boss as Abmouth. Abmouth-only proactive aggro and Infector lifecycle logic are profile-gated. Vergil Aeneid is a standalone PF127 boss with three observed variants: `L29/6796 HP/scale 131/RunSpeed 131`, `L30/7227 HP/scale 132/RunSpeed 135`, and `L31/7659 HP/scale 132/RunSpeed 140`. Its exact body/texture/mesh/waypoint appearance, QL23 Cast-Off E-Beamer `122123` weapon ownership, and 420-byte CATMesh `5921` corpse remain intact; the special corpse serializer now patches selected MonsterScale. Level 31 alone replays captured nano `43827` as a `187`-point nearby direct heal, level 30 alone replays nano `43880` as the observed `34`-point low-health self-heal, and level 29 fails closed with no invented heal. Captures `20260712-232711`, `20260712-234401`, and `20260716-034433` are preserved as three atomic Vergil item-plus-credit snapshots (`610`, `587`, and `563` credits respectively), so runtime replay cannot mix outcomes between corpses; wider pool membership and official selection probabilities remain unresolved. The generated combat report keeps five local-player hits at `22..23` separate from three player-owned Killer-pet hits at `23..28`, leaves cadence unresolved for the mixed-target fight, and retains weapon-owned runtime damage/recharge. Finalized capture `20260716-220400` adds a second atomic Abmouth item-plus-`587`-credit snapshot and keeps four local-player hits separate from ten Healer/Wrath pet-facing hits. Mike's live timing confirms both named bosses respawn exactly ten minutes after death while their loot-bearing corpses remain for 30 minutes; respawn and corpse cleanup are independent. No nano summon was observed, and later HoT ticks and cast interruption rules remain unresolved.
- Eumenides is a separate named PF127 encounter built from atomic capture `20260716-034559`: L20, 2792 HP, MonsterData `203726`, scale `130`, RunSpeed `76`, exact appearance/flags/heading, owner-linked `123267/123268` weapons at QL20 and QL17, capture-bounded `23.359` proactive acquisition, shared LOS/navigation, and a `100`-unit leash. Capture `20260717-214612` proves Eumenides attacks first at a `23.358918` horizontal-unit lower bound, while `20260717-215250` independently proves `21.203307`; the former replaces the contradicted `15.609` runtime radius. Captures `20260717-214612`, `20260717-214751`, and `20260717-215250` expand combat evidence to 21 normal local-player hits at `25..45`, two misses, initial `143/143/143/143/0` special-attack context, and a `4.311321`-second median interval; runtime damage and recharge remain owned by the equipped item. Runtime keeps QL20 because the capture corpus does not prove the QL17/QL20 respawn selection rule. Exact capture `20260716-222007` supplies the 416-byte CATMesh `17905` corpse and `186` credits. Complete capture `20260717-214751` and manually audited exact rows from metadata-unfinalized `20260717-215250` add two atomic three-item-plus-186-credit snapshots: the first contains QL22 Living Cyber Armor Sleeves `163430/163431`, QL1 item `301714`, and QL200 item `287146`; the second contains QL1 item `301715`, QL16 item `160051/160050`, and QL200 item `287146`. Follow-up `20260717-220340` proves the same loot-bearing corpse persists for more than `9m46s`, then cleans up within `0.660s..1.960s` after becoming empty. Its cross-session identity timing also permits a `310.001s` death-to-new-identity interpretation, which conflicts with Mike's repeated ten-minute runtime observation and is therefore retained as unresolved evidence rather than overriding the existing ten-minute policy. The loot-bearing lifetime remains 30 minutes and empty cleanup remains three seconds. Wider loot-pool probabilities and active-nano refresh behavior remain unresolved.
- AOSharpLiveCapture inventory and combat projections no longer depend on concrete decoded-message casts, focus selection, manual fight markers, or a shared annotation failure boundary. Every observed `InventoryUpdate` slot is exported through reflection-safe members, and combat type-name routing always exports Attack/AttackInfo/SpecialAttackWeapon/CharSecSpecAttack/MissedAttackInfo/CastNanoSpell/CharacterAction/HealthDamage/Buff/Reload/StopFight while focused annotation and state tracking run in independent guarded stages. Projection guards and the capture-tool build pass. The next ordinary capture should confirm populated derived CSVs, but the preserved Vergil raw packets already provide complete evidence and do not need recapturing.
- Finalized official-live capture `20260719-021022` adds source-specific complete
  patrols for active Filth Fleas `0x7953AFCC` (10 segments, 28 complete cycles)
  and `0x795317F5` (18 segments, 12 complete cycles), active Discarded Pet
  `0x79528FDA` (24 segments, five complete cycles), and active Violent Vagabond
  `0x7953AFA1` (10 segments, four complete cycles). Four existing Flea routes
  and the existing Vagabond route are independently corroborated. Ambiguous
  complete routes remain evidence-only and are not mapped. The Violent
  Vagabond patrol evidence adds no combat result, so the family remains
  capture-incomplete for landed damage, while source `0x7953AFA1` keeps its
  active disposition. The family is now runtime-active under the explicit
  same-level Subway damage policy described below.
  Incidental Mugger evidence adds one miss and SIW context without changing the
  captured Mugger landed-damage range. The same capture adds one L5 Mugger
  corpse with `44` credits, CATMesh `17534`, and one QL5
  `123495/123496` item, bringing strict Mugger loot to 18 first opens (15
  positive, three empty). No respawn or corpse-lifetime timing is proven. Corpus
  counts remain 313 folders, 78 Subway-bearing, 74 raw, and lifecycle `74/74`
  PASS because the folder was already indexed while running and is now final.
- Subway enemies are not accepted one subsystem at a time. The source-level accepted-enemy gate must cover spawn, movement/chase, combat contract, weapon context, corpse visual, loot, respawn, and loot/despawn behavior together before an enemy can be treated as finished.
- Ordinary Subway enemies are represented by validated type profiles plus exact spawn rows and are consumed by one `OrdinaryEnemyRuntimeService`. The former supported-family and generated-ordinary spawn orchestrators are retired. Thief, Filth Flea, Bloodcreeper, and the eight restored deep recurring families use the shared path; all 322 runtime population rows are represented and active, with zero quarantined rows. See `docs/project/ORDINARY_ENEMY_RUNTIME.md`.
- Finalized official-live capture `20260719-020104` adds an exact four-segment
  replay patrol for Disobedient Bot source `0x79557C66` and an exact 26-segment
  replay patrol for Violent Vagabond source `0x7957E5C4`. Bot combat evidence is
  now 15 landed local-player hits at `6..15` plus ten misses, and its strict loot
  sample is eight atomic outcomes: three positive, five empty, and three proven
  memberships including `113398/113399` at QL7. Vagabond has 40 misses with no
  landed local-player damage. Because the official Vagabonds repeatedly miss
  the test character, runtime now uses an explicit playability policy: the
  adjacent same-level Subway Mugger normal range `9..12`, the Vagabond's own
  captured `4.5802404`-second cadence and `0/6/0/0` AttackInfo shape, and its
  captured empty-SIW `32/35/29/31/0` context. Red Wine template `130590`
  remains excluded from combat. All 22 exact Vagabond rows are active. This
  capture proves neither respawn timing nor corpse lifetime and does not establish
  background patrol behavior for other sources.
- Legacy combat overlap is now handled explicitly rather than counted as independent evidence. Only declared simultaneous pairs `20260709-212115 -> 212336/213711` deduplicate within the audited 20-millisecond logger-skew boundary, using source, target, semantic target role, message, damage, slot, unknown, hit type, and weapon-instance shape; matching is one-to-one per supporting capture and never collapses events within one capture or unrelated sessions. Workman Striker now has 56 distinct normal local-player hits at `9..23`, six criticals at `36..42`, `5.139163`-second median cadence, four misses, three report-only SpecialAttackWeapon rows across two shapes, and two separately classified Killer-pet hits. Architect Striker has 18 normal `10..17` hits, one `38` critical, and `5.425420`-second cadence; its two `87/87/87/87/0` attack-context rows remain report-only.
- Workman Striker's accepted whole-enemy profile proves 22 active exact sources across levels 13/14/15/16/17/25 and 31 source-local atomic level/stat/weapon generations. Exact CATMesh/credits and 13 deduplicated complete first corpse opens retain 11 positive and two explicit empty outcomes with 15 item/QL entries; unopened corpse generations remain excluded and wider pool completeness remains unresolved. Runtime fails closed for missing, conflicting, unknown, aggregate, or partial generation selection. Equipped items own normal damage and recharge while captured AttackInfo carries ammo `-1`, slot `6`, unknown `0`, and instance `0`; no SpecialAttackWeapon or critical formula is invented. Sources `0x7953A84F`, `0x7953AA0D`, and `0x79545224` use their exact captured patrols. Acceptance guards all sources/variants, shared chase, strict incomplete-pool loot, private respawn, and shared corpse lifetimes together.
- Melded Patterns now uses its capture-proven QL20 Irreparable Sleekblaster Minor `121817/121818` through the generic equipped-weapon path. Weapon stats own normal damage and recharge; no special-attack context, fixed override, critical policy, loot probability, or respawn exception is invented. It is accepted by the whole-enemy gate with its exact weapon-owned normal path and those exclusions preserved. Focused tests and the Debug build pass; private-client validation is pending.
- A reusable reviewed first-open validator now promotes 18 strict item denominators. In addition to Shadow, ordinary Infector, Architect Striker, and Melded Patterns, the recovered set is Mugger `18/3 empty`, Discarded Pet `16/3`, Stim Fiend `13/0`, Looter `11/5`, Violent Vagabond `14/1`, Bloodcreeper `4/3`, Infected Attendant `5/1`, Fragmented Soul `4/0`, Deranged Shopper `3/0`, Incomplete Rebuild `2/0`, Redundant Scan `2/1`, Uncontrollable Anger `3/0`, Lost Thought `1/0`, and Neural Burnout `4/2`. Exact capture/allocation allowlists and complete raw-generation fingerprints fail closed; declared overlap projections count once, while unopened, snapshot-only, and the known false Stim Fiend attribution remain excluded. Generated strict summary metadata drives `IndependentEntries`, observed empty counts, and `ItemPoolComplete=false` without a catalog MonsterData hardcode list. Empty Shell and Premature Pattern remain tableless; replay weights are private existing-capture policy, not official probability claims.
- Incomplete Rebuild now has ten exact source rows and `23` source-local capture-reviewed atomic level/health/scale/RunSpeed/weapon generations across L17..L22. Generation selection is uniform private policy, occurs once per population generation, and cannot form Cartesian combinations. Fourteen later identities are joined to unique sources by exact position or waypoint evidence; ambiguous `7957E5F9` is excluded. Selected items own runtime damage and recharge with captured AttackInfo context. The profile carries captured return-home behavior, shared chase, a conservative 7-unit proactive policy, four-minute respawn, exact CATMesh `5921`, ordinary corpse lifetimes, two strict positive first opens, observed L17/L18/L19/L21 credits, and policy-only L20/L22 interpolation. Nano `90405` restores `21` CurrentNano immediately plus `959` 15-second ticks over four hours, costs `47` nano and `6` NCU, refreshes without stacking, and keeps its targeting/timing/nano-pool assumptions explicitly policy-scoped.
- Redundant Scan now selects from ten source-local capture-reviewed atomic level/health/scale/RunSpeed/weapon generations across its four exact sources and L19..L22. The three stationary anchors require a unique position association; source `795451C4` is the sole captured patrol anchor and later rows retain that unique waypoint shape. Same-level weapon rerolls remain separate and incomplete SCFU/weapon observations remain report-only. Runtime fails closed for aggregate, missing, conflicting, unknown, or forged selection and lets item stats own damage/recharge rather than replaying the single observed `19` as a constant. Its reusable ordinary profile also carries the captured `121336 -> 121248` ally-or-self support pair. Playfield-owned transient NPC state broadcasts the exact Cast/Finish/Buff/SetNanoDuration order, applies nanos.dat-backed `+9/-13` deltas across the exact 23 affected skills, refreshes without stacking, projects active nanos for late observers, and reverses only its owned deltas on 180-second expiry, recipient death/despawn, or reset. It bypasses player NCU/DAO/timers. A conservative 7-unit proactive aggro policy, exact active population and patrol/static dispositions, strict `2/1 empty` loot, exact L19..L22 corpse credits/CATMesh, ordinary corpse lifetimes, and private respawn now pass the whole-enemy gate. Private runtime validation remains outstanding.
- Fragmented Soul now selects from `19` distinct source-local capture-reviewed atomic level/health/scale/RunSpeed/weapon generations across its ten exact active sources and L17..L21. The unmatched identity `7970245D` remains report-only. Runtime selection fails closed for aggregate, unknown, missing, duplicate, mismatched, or cross-source evidence; selected `123685..123688` items own damage and recharge while captured AttackInfo retains ammo `24`, slot `6`, unknown `0`, and instance `0`. Retaliatory shared chase, inherited private respawn, strict `4/0 empty` loot, six exact item memberships, CATMesh `5921`, standard `3/240/3` corpse lifetimes, observed L17/L18/L21 credits, and policy-only L19/L20 credit progression pass the whole-enemy gate. Nano `95447` dynamically resolves its sole nanos.dat target Skill effect (stat `381`, `+42`) instead of hard-coding the modifier in runtime; it refreshes without stacking and removes only its owned delta. Its exact four-hour duration, cost `44`, NCU `7`, range `20`, observed four-self/four-ally split, and ten-second repeat baseline are preserved. Spawn nano pools use only non-interpolated evidence floors L19=`665`, L20=`782`, and L21=`829`; L17/L18 remain unresolved. Private runtime validation remains outstanding.
- Discarded Pet is the twentieth accepted ordinary profile. All 29 exact L5..L10 population rows are configured active; the 11 newly enabled rows still require bounded private-client activation validation. Captures `20260708-143600` and `20260709-210452` prove 37 normal local-player SIW1 hits at `9..18`, with four `30..33` criticals retained as report-only evidence. Runtime keeps AttackInfo ammo `-1`, slot `0`, unknown `0`, instance `SIW1`, and a `5.089763`-second conventional median across 30 same-source landed-hit intervals; the varying raw SpecialAttackWeapon fifth field remains unresolved and is not synthesized. The profile is retaliatory with shared chase and no invented proactive radius, leash, reset, or return-home boundary. Its strict `16/3 empty` incomplete loot pool, CATMesh `15929`, standard `3/240/3` corpse rules, and 25 exact positive-credit corpses include recovered L6 and L10 records from `20260709-205921` and `20260708-004038`.
- Uncontrollable Anger is the twenty-first accepted ordinary profile. Six exact active rows preserve captured levels `13,13,19,20,23,23`, two patrol anchors, four static anchors, retaliatory shared chase, four local-player SIW1 normal outcomes at `9..18`, three misses, strict `3/0 empty` loot, CATMesh `96177`, inherited private respawn, and standard `3/240/3` corpse rules. Four Killer-pet hits at `25..42` and one other-player hit at `19` remain separate; the `19` local critical remains report-only. The reviewed `20260709-222339` Killer window retains the complete `5.1165513`, `5.1671525`, `10.1003489` CSV interval series and uses the six-decimal median `5.167153`; the doubled interval is evidence, not divided inference. Seven exact positive-credit corpses cover L11/L12/L13/L20/L21, while active L19 and L23 remain credit-unresolved rather than receiving an invented formula.
- The capture-complete whole-enemy accepted set remains 21 of 26 ordinary profiles. Infected Attendant, Lost Thought, Empty Shell, Premature Pattern, and Violent Vagabond remain capture-incomplete, but all 43 of their rows are active and the catalog has no quarantine. The reviewed corpus evidence is indexed without target-role leakage: Infected has one local `11` outcome and six reviewed non-local attack starts; Lost has 11 other-player hits at `15..20` with a `4.5320703`-second median; Empty has local `15`, two misses, other-player `19`, hostile nanos `26414/81998/82482`, exact local nano-duration targets, and six reviewed proactive local starts; Premature has local normal `22`, local critical `41`, other-player `16`, player-pet `38`, and two reviewed proactive local starts. Their one-hit or non-local samples remain report-only and cannot become fixed runtime damage. Vagabond has 40 distinct local misses after overlap deduplication, a `4.5802404`-second median attempt cadence, and two exact simultaneous other-player attack starts. Its landed damage cannot be sampled against the current test character, so the user-approved private policy applies the same-level Mugger `9..12` normal range while retaining captured Vagabond cadence and packet shape. Runtime stays retaliatory with shared chase and no automatic radius. The explicit `450`-second post-NPC-despawn policy comes from the exact `449.759588` interval and `1.088` position delta. Held template `130590` is Red Wine and is rejected as a combat weapon. Genuine capture gaps remain numeric reset/leash boundaries for all five; respawn cycles for Infected/Lost/Empty/Premature; usable owner-linked weapon evidence for those four; repeated local cadence/range for Infected/Empty/Premature; local landed damage for Lost; exact landed Vagabond parity beyond the approved policy; and strict attributable Empty/Premature loot.
- Shadow, ordinary Infector, Architect Striker, Melded Patterns, and Workman Striker join the confirmed whole-enemy accepted set, bringing it to ten. Looter, Bloodcreeper, Stim Fiend, and Neural Burnout bring the set to fourteen, Mugger is fifteenth, Deranged Shopper is sixteenth, Incomplete Rebuild is seventeenth, Redundant Scan is eighteenth, Fragmented Soul is nineteenth, Discarded Pet is twentieth, and Uncontrollable Anger is twenty-first. Looter resolves all eight exact owner-linked `123038/123039` weapon tuples by source identity and QL. Capture `20260720-031025` additionally proves repeated patrols for Looter sources `0x79545029` (10 segments) and `0x7954503C` (12 segments); five other observed sources remain stationary, while suspected duplicate `0x7957E5CD` remains unresolved and unchanged. Mugger resolves all nine current sources to QL1 `121567/121567`, carries only its captured AttackInfo fields, and lets the item own damage, damage bonus, and recharge; aggregate, missing, conflicting, or unknown selection fails closed. Its 38 normal `9..12` hits remain separate from three report-only `21` criticals. The same finalized capture maps Deranged Shopper live alias `79803651` to canonical source `0x79574527` through the sole matching profile and patrol anchors, adds its 83-row flag-24 idle patrol while excluding ten later flag-25 NpcPath rows and one additional non-NpcPath flag-25 movement row, and preserves its exact QL8 `125454/125455` weapon. Its ten normal hits now span `7..15`, one `27` critical stays report-only, six source-associated misses and seven generated aggregate misses remain distinct, and empty SIW plus attack-start, StopFight, and death context remain evidence-only. Its strict loot denominator is `3/0 empty`, including item `234876` QL1 on an L8 CATMesh `5927` corpse with `47` credits. The capture proves no respawn or corpse-lifetime result, so existing private lifecycle policies remain unchanged. All accepted Looter, Mugger, Deranged Shopper, Stim Fiend, and Disobedient Bot rows are active for bounded private validation. The gate binds exact population disposition, combat, strict incomplete loot, corpse visuals/credits, private respawn, and shared corpse lifetimes. Ordinary generation, the expanded gate, WorldPopulation `39/39`, named encounter `26/26`, and Subway loot `22/22` pass. Focused capture tests and the Debug build pass; Chat, Login, and Zone were restarted on their expected ports.
- Capture `20260709-212115` materializes six exact Subway merchant NPC owners. Tailor, Weaponsdealer, Armorer, Pharmacist, Tools, and Container Supplier expose six owner-linked vending machines containing all `202` captured baseline rows in exact slot order. Container Supplier uses the exact 62-row `Cont` inventory captured on vending-machine template `99634` in `20260613-221619`. Finalized capture `20260719-021611` adds Tailor's complete KnuBot flow, exact ordered prompt segments, eight QL1 Jobe measurement rewards `256415..256422`, and the captured shopping-basket instruction. The inbound chat-open packet is now routed through the allocated Tailor runtime identity; the later GenericCmd remains the sole shop-open action. The same capture preserves an alternate atomic 203-row stock observation: Pharmacist and Container match, while Tailor, Weaponsdealer, Armorer, and part of Tools vary. Runtime keeps the 202-row baseline because pools, selection weights, refresh timing, and QL rolling remain unresolved. Direct use is identity- and playfield-checked, vendor full updates retain the NPC owner identity, and vendor cleanup follows playfield teardown. Private-client Tailor validation remains pending.
- Every ordinary spawn has one generic level definition: 274 remain capture-fixed, Bloodcreeper uses its configured inclusive `L15..L25` policy range, 22 Workman Striker sources use `31` source-local atomic level/stat/weapon generations, ten Incomplete Rebuild sources use `23`, four Redundant Scan sources use ten, ten Fragmented Soul sources use `19`, and Premature Pattern source `79545356` uses two stat-only generations at L17/L18. An injected selector resolves once per population generation before stats and combat preparation; visibility, combat reset, corpse state, and navigation cannot reroll it, while a new respawn generation can. The selected variant and generation remain attached to the runtime definition.
- Playfield runtime decomposition has completed through the latest extracted runtime services and is now maintenance work, not the active development focus.
- Corpse open, item loot transfer, and corpse credit payout are capture-backed and live validated.
- Global loot production now resolves validated, versioned table assignments through a deterministic generator. Captured ordinary profiles (including Thief and Filth Flea), Cleaning Robot outcomes, legacy DB loot, credits, and explicit debug fixtures use adapters; pets/owned summons and unresolved evidence fail closed. `CorpseInventoryService` owns corpse loot state while existing packet sequencing, inventory handles, visibility, and lifetimes remain unchanged. Boss/dyna/encounter context is modeled and tested but no related gameplay was added.
- Global ordinary-world population adapts all 322 Subway profile-backed rows into normalized spawn/group/respawn definitions. `WorldPopulationController` owns all 322 active rows, lifecycle generations, and explicit death/despawn/corpse notifications; zero rows remain disabled or quarantined. `WorldRespawnScheduler` remains the single scheduler and starts the ordinary delay at final dead-NPC despawn. Eligible ordinary rows inherit a centralized four-minute PF127 project policy unless explicit spawn/archetype or group data overrides it. Thief retains its captured 60-second exception; Filth Flea and Bloodcreeper retain explicit four-minute policies; Violent Vagabond uses its capture-bounded 450-second exception. Named enemies, bosses, scripted encounters, summons, pets, temporary adds, vendors, static objects, containers, quest-owned content, explicit no-respawn rows, and unsupported classifications cannot inherit the ordinary default. This is private-project policy, not a claim of universal official AO timing parity. DB mobs and captured Arete robots remain documented legacy owners pending parity migration; no dyna content is active.
- Loot-bearing corpse close/reopen is implemented from official-live capture `20260712-195019` for the same `F6C002` corpse: open sends `InventoryUpdate`; close sends `Action 0x66`, `CharacterAction 110`, and the normal Use acknowledgement without an inventory refresh; reopen sends `InventoryUpdate` with a refreshed handle. The capture proves handle progression `113 -> 114 -> 115 -> 116` and the one-item remainder after looting. The rejected refresh-plus-`0x66` path is removed. Manual client validation is pending; the four-minute regular-corpse lifetime is unchanged by close/reopen.
- Normal corpses with an unlooted item or unclaimed credits receive a four-minute lifetime. Every corpse that is born empty or becomes empty after its final item and credits are removed uses the confirmed three-second cleanup delay. Loot-bearing Abmouth and Vergil corpses use their separate 30-minute encounter lifetime.
- The Def-Agg investigation is closed: live AO confirmed the repeated hint as normal level 1 client behavior.
- Generic unarmed NPC attacks without captured or equipped weapon context suppress `AttackInfo` so the client does not invent `nanobots / unknown damage` text. Capture-backed/equipped NPC attack packets remain enabled.
- First-room Subway Thief behavior is capture-backed through finalized capture `20260717-012651`: maximum health is 146, captured current health is 115, and passive recovery is one point per second including during combat. The observed disappearance after closing with the guaranteed QL1 Stolen Handbag still present is treated as the reported bug, not intended Thief behavior. The Thief now follows the same four-minute loot-bearing corpse rule as every normal enemy, with close/reopen preserving the body and three-second cleanup only after it becomes empty. Earlier combat behavior, projectile attack text, pistol-based damage rolls, captured movement/attack context, exact corpse visual, and 60-second respawn remain unchanged. Raw packet `CorpseFullUpdate #1580` from capture `20260710-205400` proves the exact 412-byte corpse shape, CATMesh `5907`, MonsterData `26092`, and material tail. The deferred mission chain is unchanged.
- AOSharpLiveCapture now has generic one-pass NPC lifecycle coverage: `packets.hex.log` preserves the original traffic and `raw-packets.csv` is its lossless, ordered, bidirectional packet index; capture modes and markers add validation requirements but never narrow raw collection. Broad visible non-player NPC/enemy/pet state, promoted full updates without a focus-only gate, movement, combat/death, exact corpse full updates, corpse inventory, loot movement, corpse presence/despawn, same-archetype same-position respawn timing from both death and corpse disappearance in `enemy-respawns.csv`, and capture-completeness validation remain covered. Respawn-marked captures validate incomplete unless a respawn is correlated. Loot-marked captures write `corpse-loot-observations.csv`, preserve the initial snapshot even when empty, retain corpse credits plus enemy/player levels and item rows, and validate a ten-corpse one-enemy sample. Live capture and `decode_npc_lifecycle_capture.py` use the same tracked direct raw SCFU decoder, while the offline path also merges dossier/full-update identity profiles and retro-decodes existing folders. Loot reconstruction canonicalizes padded and unpadded numeric corpse identities before identity joins and now tracks corpse identity generations: reused identities reset their open ordinal, rebind to the latest corpse full update, and cannot inherit stale enemy or credit metadata. Intact raw evidence with an incomplete or failed projection requires offline reconstruction, not repeated gameplay, whenever `recaptureRequired=false`, including `processingAllowed=false` or `offlineDecodeRequired=true`; gameplay recapture is reserved for missing or incomplete authoritative raw traffic. Raw callbacks now use a lossless start/restart boundary and an in-flight-aware quiet/maximum-stop gate; teardown drain failure is explicitly recapture-required. Offline tools validate raw lengths, reconcile both sinks by packet event, reject unresolved conflicts, preserve chronological order, and promote derived outputs atomically only after validation succeeds. Legacy folders with trailing packet-log data are bounded by their recorded capture start/end timestamps before completeness checks and decoding.
- Subway layered-loot rules define a dungeon-wide pool plus stable enemy-type pools for ordinary enemies and dedicated tables for named/boss enemies. Observed counts, sample size, runtime weight, empty weight, and explicit guaranteed exceptions remain separate fields; a positive-only sample is never inferred to be guaranteed. Disobedient Bot retains its provisional weighted-one `1 + 1 + 5 empty` policy. Reviewed strict ordinary pools use independent observed-sample entries and remain explicitly incomplete. Bloodcreeper now has four reviewed opens: QL30 item `42640/42641` appears once and three opens are empty, so runtime replays the observed `1/4` entry without claiming a complete pool.
- Slum Runner now has 21 identity-linked death/corpse observations with CATMesh
  `31774`: the seven focused records from `20260716-034656` and
  `20260716-215947`, plus fourteen recovered deep-corpus records. Exact observed
  credit rules cover L11=`66`, L12=`72`, L15=`92`, L16=`98`, L17=`105`,
  L18=`111`, L20=`124`, L21=`131`, L22=`137`, and L23=`144`; every active
  Slum Runner level now has an exact rule, while other levels remain unresolved.
  Its 24 exact spawns, `5..11` normal damage, `4.210098`-second cadence, shared
  chase, strict item sample, ordinary corpse lifetime, and observed
  `59.433`-second death-to-respawn interval now pass the whole-enemy gate.
  Item loot remains a separately sampled pool and no official distribution is
  claimed. Slum Runner is the third accepted ordinary enemy after Thief and
  Filth Flea.
- Molested Molecules is the fourth accepted ordinary enemy. Nine exact spawns,
  20 normal local-player hits at `16..42`, captured `4.749995`-second cadence,
  shared chase, three strict loot outcomes with four observed `1/3` item
  memberships, CATMesh `5921`, and seven exact positive-credit corpses pass the
  whole-enemy gate together. Its four-minute respawn remains the documented
  private ordinary policy, not an official-live timing claim.
- Disobedient Bot is the fifth accepted ordinary enemy. Its 12 exact spawn rows
  now use captured NPC family `138`; 14 normal local-player SIW1 hits prove the
  aggregate `8..15` envelope, while three other-player hits and two player-owned
  Killer-pet hits remain separate. Focused attempt traffic retains a
  `5.973723`-second recharge. Runtime resolves captured SIW1 contexts per spawn
  level (`L5=30/30/30/30/22`, `L6=35`, `L8=45`, `L9=49`, `L10=54`) instead of
  reusing the first L10 row for every Bot. L7 is an explicit bounded midpoint
  policy at `40`; other levels fail closed. Thirteen valid exact corpse/credit
  chains, seven strict loot outcomes, CATMesh `15215`, shared chase, ordinary
  corpse lifetimes, and the capture-backed `450`-second post-NPC-despawn delay
  pass the whole-enemy gate together. Capture `20260708-143600` records
  `459.913` seconds death-to-replacement at a `0.190`-unit position delta. The
  two previously quarantined Bot rows are now active for bounded private
  validation, and criticals, proactive aggro radius, and leash/reset distance
  remain unresolved.
- The generated Subway combat-contract report now supplements legacy identities
  from enemy dossiers and exact corpse dead-NPC links. It records nine projected
  plus five reviewed-raw Disobedient Bot local-player hits at `8..15`, three
  other-player hits at `8`, and two player-owned Killer-pet hits at `8` and `19`
  without crossing target-role boundaries.
- The completed corpus audit is `docs/evidence/SUBWAY_BLOODCREEPER_DISOBEDIENT_BOT_LOOT_AUDIT.md`. Fourteen identity-correlated official-live Disobedient Bot corpses disprove the earlier global `8..11` credit range. Known-level values remain exact observed rules (`L5=6`, `L6=8`, `L8=10`, `L9=11`, `L10=12`); unobserved levels remain unresolved instead of using a guessed formula. Seven strict complete item outcomes prove the two active memberships and five empty outcomes. Burnt Out Memory Chip (`234876/234876`) remains inactive because its corpse linkage is incomplete.
- Captured enemy combat uses a shared atomic contract and runtime registry across supported and generated ordinary Subway populations. Each spawn declares fixed captured `AttackInfo`, captured equipped weapon, specialized captured behavior, or unresolved evidence; captured weapons are equipped during spawn, retaliation and attack-source selection use the registry, contracts are removed on despawn, and incomplete contracts are logged/refused. Ordinary combat profiles can resolve a contract from the selected spawn level, preventing level-varying captured context from collapsing to the first grouped row. For Thief, live capture `20260711-170337` proves QL1 Solar-Powered Pistol `121567`, attack-context header `Unknown=0`, `SpecialAttackWeapon` body `32/32/32/32/0`, attack start after `1.409765s`, Target -> `StopMovingCmd` -> `SetPos` -> `NpcPath` after another `0.219999s`, first landed hit `11.409643s` after Thief attack start, captured `9`-point normal `AttackInfo`, approximately six-second repeats, and `StopFight` immediately before Death. Private capture `20260711-172309` proves the pre-repair mismatch. Mike's post-repair live validation confirms the accepted Thief slice now matches live behavior. Disobedient Bot combat/chase is privately validated; its two proven transferred item memberships are active under the provisional weighted-one policy, while the broader pool remains incomplete.
- Bloodcreeper is activated as ordinary non-boss content from survey capture `20260709-222339` and focused captures of the same single spawn. Catalog data uses the generic spawn-level definition to roll the repository's community-documented inclusive `L15..L25` band once per new population generation; no Bloodcreeper-specific selection logic exists in the shared model or runtime. Captured adjacent points `L24/691 HP/run 83` and `L25/724 HP/run 86` anchor a private derived progression of `+33 HP` and `+3 run speed` per level; `L15..L23` values are explicitly policy rather than capture claims. The spawn preserves scale `70`, NPC family `63`, appearance `1483`, flags, heading, and optional SCFU fields. Proactive acquisition is enabled with a bounded `7`-unit private radius from the observed approximately `6.25`-unit trigger. Its specialized contract replays independent Skinspider Bite/SKW1 and Skinspider Spit/SKW2 streams with exact templates/tags/slots, captured initial timing, roughly `7.4`-second per-hand cadence, and non-constant ranges `21..35` and `21..41`. CATMesh `26978` and level-24 credits `150` are proven repeatedly; `150` is retained across the private level range as inferred policy so other level rolls do not lose credits. Four reviewed first opens prove one QL30 `42640/42641` entry and three empty inventories; the runtime pool remains incomplete. Bloodcreeper is in the whole-enemy accepted gate; private validation remains pending.
- Mike's `2026-07-12` Thief playtests confirmed the fixed `9` damage path was active but still produced bad incoming client text (`nanobots / unknown damage`) while the Thief did not visibly attack. Official live sends `WeaponItemFullUpdate` for the Thief's QL1 Solar-Powered Pistol immediately after the Thief SCFU; AORebirth now announces captured equipped Subway NPC weapon definitions to the playfield after SCFU, but Mike's follow-up playtest proved the prior generic weapon definition was still not sufficient for the client to classify the Thief's `AttackInfo` as weapon/projectile damage.
- The repaired Thief weapon context is now live-proven: Mike's `2026-07-12` diagnostic check rendered `Thief hit you for 9 points of projectile damage`, confirming the original client accepts the repaired armed-NPC weapon definition. Official captured Thief and working armed Subway NPC weapon updates include live item energy and item timing stats (`Energy`, `AttackDelay`, `RechargeDelay`), while the previous AORebirth builder emitted `Energy=0` and omitted the timing stats. Official player weapon updates use `Energy=-1` when no finite energy value exists. AORebirth now builds weapon definitions with those live-shaped stats and replays weapon definitions in existing-character visibility snapshots after SCFU and before `CharInPlay`, covering both newly-spawned and already-visible armed NPCs. The temporary Thief damage suppression and one-hit diagnostic gate are removed; Thief damage now rolls from the equipped QL1 Solar-Powered Pistol `121567` item stats using the same current legacy weapon-roll input model as player normal weapon attacks while preserving the captured attack packet envelope and timing.
- Exact byte-vector validation proves the current message serializers can emit the official `20260711-170337` Thief `SpecialAttackWeapon`, `Attack`, and `AttackInfo` packet bodies byte-for-byte. Further Thief attack repair must focus on runtime delivery/order/client state rather than changing those three packet field values again.
- The centralized damage-calculation system now exists as a side-effect-free `ZoneEngine.Core.DamageCalculator` boundary. Current migrated production behavior preserves the repository legacy `CombatDamageRules` normal-hit formula for players, NPCs, and attack pets, and fixed captured damage is represented explicitly where required. The ordinary weapon-input provenance audit chose Outcome B: repository/runtime resources identify weapon min/max, legacy `DamageBonus`, raw damage type, attack/recharge timing, attack-skill dictionaries, some AMS caps, and damage-type AC/add-damage mappings; `WeaponDamageRequestBuilder` now reports diagnostic-only formula-readiness, provenance, and malformed/missing input issues. It is not wired into production damage. Critical bonus source, resolved normal/critical state, Add All Off ordering, AMS-cap zero semantics, AC formula/order, universal add damage, and complete caller integration remain unproven. Formula-backed requests therefore remain evidence-blocked and production callers stay on legacy or explicit captured strategies. Special attacks, PvP, reflects, absorbs, damage shields, nanos, perks, procs, percentage damage, and returned damage also remain evidence-blocked unless stronger repository/capture/database evidence is added.
- Ordinary weapon-hit parity now has an evidence-only framework: observation schema version `1.0`, synthetic evaluator fixtures, a fixed captured Thief record, live observation template, operator observation matrix, initial underdetermined parity report, validator, candidate evaluator/reporter, and opt-in diagnostic snapshot builder. This framework is report-only, disabled from production combat, and does not consume production random values or change damage. No full AO damage formula is proven. The accepted Subway Thief slice now uses the equipped QL1 Solar-Powered Pistol roll path validated by Mike's live playtest, but that validates Thief only and does not promote the broader formula.
- First ordinary-hit post-fix session `starter-pistol-postfix-001` validates corrected AORebirth legacy starter-pistol behavior only: 13 valid QL1 Solar-Powered Pistol `121567` hits against Arete `Malfunctioning Cleaning Robot` targets, 0 incomplete, 0 rejected, emitted damage range `2-18`, active `legacyDamageBonus=0`, valid weapon range bypassing the player fallback floor, and no duplicate/overlapping damage. The evidence validator now accepts lethal overkill when health delta equals `min(observedDamage, targetHealthBefore)`. No original AO AR/AC/critical/add-damage formula is proven or activated.
- Heartbeat NPC regeneration now exposes missing or duplicate health-stat cardinality instead of silently treating malformed characters as dead. Runtime evidence identifies the observed failure as duplicate health cardinality; valid numeric policy is explicit and non-mutating: only positive current health below maximum regenerates, while dead/nonpositive health and invalid/non-greater maximum health skip regeneration. The attacker scan reads health only for characters targeting the NPC, so unrelated malformed characters do not block a later valid attacker. Player regeneration and combat/death/corpse ordering are unchanged. Focused tests pass, the full supported assembly remains at the same six baseline guardrail failures, and the approved build/restart passes.
- Previous capture-backed enemy, movement, combat, zoning, and appearance milestones remain completed project history.
- Filth Flea appearance has been corrected from capture-backed Subway SCFU texture evidence.
- The hallway Filth Flea is accepted as of `2026-07-12`: Mike live-validated its combat path, corpse loot, credits, corpse cleanup, and repeat respawn behavior. The expanded official-live corpus now proves 18 complete corpse outcomes across `20260708-004038`, `20260709-210452`, `20260709-220439`, `20260712-155528`, and `20260712-161506`: 15 item memberships and five empty inventories. Exact death-linked credits are L4=`23` and L5=`29`; other captured spawn levels retain the private `23..79` fallback policy. Normal player-facing attack streams roll melee slot 0=`3..10` and poison slot 1=`14..24`; reviewed source `79531748` from `20260709-205921` adds one normal `15` and one critical `7`, while critical `7`, `13`, and `47` observations remain separate from runtime normal rolls. Capture `20260712-161506` correlates killed `(SimpleChar:795F924E)` to same-archetype same-position replacement `(SimpleChar:795F9294)` and records the corpse-disappearance boundary. Mike's repeated live observation establishes the four-minute post-despawn schedule, enforced by `AcceptedSubwayEnemyGateRequiresWholeEnemyCoverage` together with spawn, movement/chase, combat, appearance, corpse visual, loot, and credits.
- Subway PF127 retains a 124-row supported-family evidence set plus 197 ordinary runtime rows. The active runtime contains 113 supported-family and all 197 ordinary rows; only 11 Violent Vagabond supported rows remain quarantined. The earlier `ALL_38` test showed the selector was loaded but not applied by the consolidated population owner; that diagnostic integration is corrected, while normal runtime remains `NONE`. Selected diagnostic rows write a bounded `population-activation-ledger.csv` with `ELIGIBLE`, `MATERIALIZED`, or `FAILED` phases, separating selector admission from runtime creation without changing eligibility. The 16 accepted rows promoted in this slice still require bounded private validation. This server-side PF127 disposition is unchanged by the separate official live-client regression described below.
- Premature Pattern source `79545356` now preserves the two exact same-source generations joined from captures `20260709-225408` and `20260712-232848`: L17/368 HP/scale 98/RunSpeed 65 and L18/394 HP/scale 98/RunSpeed 68. It also follows the complete reviewed out-and-back patrol. The variants intentionally have no weapon because neither capture contains a complete weapon update; uniform selection is private policy, no extra population row was added, and PF127 now remains 322 total/322 active/zero quarantined after the later Workman addition.
- Global dynamic-character visibility now uses a playfield-owned bounded X/Z uniform index plus per-client bidirectional interest state. Defaults are 80-unit entry, 100-unit leave, and 32-unit cells with validated finite bounds. Initial snapshots, ordinary/captured/pet spawns, player/NPC movement, known-character messages, death/corpse visibility, despawn, respawn, zoning, disconnect, and playfield reset use the shared interest lifecycle; static dynel and vendor delivery remain unchanged. Entry packet order remains SCFU -> weapon definitions -> CharInPlay, and leave uses the proven `DespawnMessage` with `Unknown=1`. There is no pacing, batching, throttling, or pagination. ZoneEngine/AORebirth Debug build passes; focused policy/index/catalog/performance 12/12, executable state 8/8, lifecycle integration 9/9, spatial metrics/JSON 4/4, Python diagnostics 9/9, and exact packet measurement 4/4 pass. The aggregate wrapper completed at 203 total, 194 passed, and the same nine established baseline failures; every visibility-task test passed. No live client success is claimed. See `docs/project/VISIBILITY_INTEREST.md`.
- The PF127 diagnostic manifest retains its historical selector names `NONE`, `ALL_38`, `SUPPORTED_29`, `ORDINARY_9`, ordinal, identity, and family slices, but selectors control spawn eligibility only and cannot bypass spatial selection. The configured production disposition is now 322 active rows with zero quarantine. The 22 Violent Vagabond rows use the explicit playability damage policy while exact landed-damage parity remains unavailable. The corpus audit also establishes that Bitaxel is a player (`PlayerInfo`, lifecycle `player=True`) rather than a missing enemy. Capture-local SCFU owner identities now preclassify player-owned pets before weaker enemy-role projections. Strike Foreman remains the genuine uncovered named enemy: appearance, QL19 weapon, raw special/attack initiation, chase start, corpse visual, and credits are exact. Raw capture `20260709-222339` also proves two normal `18` hits and one `40` critical against the non-local player Wardog at approximately five-second intervals; Wardog's SCFUs are `PlayerInfo`, `player=True`, and MonsterData `0`, so those post-mitigation other-player outcomes are neither local-player nor pet evidence. Killed source `7954512E` is bound to exact QL19 WeaponInstance `25713A73` and corpse `00F6E017`, while captured positions prove a `20.250672`-unit proactive-acquisition lower bound. Captures `20260720-032106` and `20260720-033513` add six local normal `13` hits, two misses, two `154/154/154/117/0` attack-context rows, two L19/CATMesh `17870`/`176`-credit corpses, and two positive atomic loot outcomes. Respawn, leash/reset, exact acquisition threshold/upper bound, loot-bearing lifetime, and QL17/QL19 weapon selection remain unresolved, so the encounter stays inactive. Container Supplier stock is resolved by exact template-`99634` evidence; dialogue remains unresolved.
- Subway content work is the primary development track.
- AO client Subway room-space mitigation retains its earlier A/B validation for the four original callsites. Two matching official-live dumps on `2026-07-14` expose a fifth path: `n3RoomSurface_t::VetoPosition` calls `n3Playfield_t::PosToRoom` with cached room `-1`, and MSVCR100 throws `std::bad_cast` with `Bad dynamic_cast!` while converting `Space_i` to `n3RoomSpace_t&`. Both captured objects are non-null and present as `n3RoomSpace_t`; the dumps do not preserve why the CRT rejected the transient cast. This remains a client-side room-resolution defect and does not change AORebirth server content.
- The RoomSpace normal-shortcut repair at `Tools/AOClientRoomSpaceGuard/ProxyDll` now routes five audited `PosToRoom` callers through the existing non-throwing wrapper. The new `VetoPosition` callsites are old-client RVA `0x1570A` and new-client RVA `0x16F98`; both callers already handle a null room result. Proxy package build, wrapper self-test, deployment-helper tests, both profile inspections, and idempotent install verification pass. The rebuilt package is installed in `C:\Funcom\Anarchy Online` and `D:\Funcom\Anarchy Online`; in-game regression validation of the fifth callsite remains pending.
- Official old-client dump analysis on `2026-07-14`/`2026-07-15` identified four additional exact renderer/GUI failures: `randy31 +0x6C476` dereferences low indirect color pointer `0x100`; NVIDIA 591.86 faults at RVAs `0x172776C` and `0x173A009` during `randy31 +0x219B4` `DrawIndexedPrimitiveVB`; NVIDIA RVA `0x170C490` faults while `IDirect3DVertexBuffer7::Lock` flushes a queued GUI batch; and GUI image RVA `0x4ED00` (crash-report logical `+0x4DD00`) dereferences an impossible tree-search key `0x8`. The proxy uses byte-verified, fail-closed guards at the existing missing-color branch, the indexed-draw call boundary, the complete void GUI-batch boundary, and GUI tree-find entry RVA `0x4F2EF`. Only low invalid tree keys are converted to the original tree's native sentinel not-found result; normal keys fast-path directly to the original function without a memory query. The earlier forced T&L-HAL-to-plain-HAL selector rewrite caused an unacceptable renderer downgrade and has been removed; AO's launcher selection is now preserved. Build/package/self-tests, engine restart, both client installations, package verification, and installed hash `6DD800A587900F4C9D41759A719E3D97AD1B7727BD43702D7C1DC84E61310FE8` pass. Mike confirmed that selector-preserved build restored the expected 100 FPS.
- The official old-client `randy31` crash reported on `2026-07-16` maps logical `+0x24118` to image RVA `+0x25118`, where `mov eax,[edi]` read the wild EDI value. The report stack proves native vector-loop state `[ESP]=0x0A`, byte offset `0x20`, and entry index `2`. The proxy now arms a dedicated exception-only VEH before dump/N3/profile work; it requires the exact PE32/i386 image bytes and loop provenance, then pops the pushed state class and resumes at `+0x25147`, skipping only the corrupt first 16-byte render-state vector. It is separate from the existing `+0x2511A -> +0x2512F` one-entry recovery and adds no normal-frame work. Build/package/self-tests and the required engine restart pass. The package and both `C:\Funcom\Anarchy Online` and `D:\Funcom\Anarchy Online` installations now match DLL SHA-256 `CB2115E9812832413FF646041067EA0CB9E12CAF48E99F59B976B1CD494FA6E0`; in-game crash-regression validation remains pending.

## Current Baseline

- Current active handoff: `docs/ai/CURRENT_TASK.md` tracks Subway playability work.
- Repository purpose: local C#/.NET Framework-era Anarchy Online server workspace for Mike's current AO client and local `cellao_codex_clean` MySQL database; this is a legacy database name retained for local compatibility.
- Current stable approach: evidence-backed packet/gameplay/data repair, current-client parity over legacy assumptions, and identity-first capture-derived reconstruction.
- Documentation split: `docs/ai/CURRENT_TASK.md` remains the active task handoff; this file is the stable project memory; `docs/generated/` contains historical result reports only.
- Older active-task notes for surgery clinic, Rex/Marcus, private-city, org, corpse validation, and Playfield decomposition are retained below as historical project state, not the current priority.
- Quest system work remains on the back burner unless Mike explicitly resumes quest work.

## Current Hard Rules

- Do not use raw packet replay for Rex/Arete mission packets. Use decoded DTO/body serializer paths only.
- Do not guess packet behavior; unresolved packet semantics must stay unresolved until evidence-backed.
- For current AO client behavior bugs, treat the live AO client as authoritative, start with live capture or existing capture review whenever feasible, and base repairs on confirmed live packet/message behavior when available.
- Capture-derived content must be identity-first. Display names, proximity, screenshots, or plausible templates cannot define runtime data.
- Known workflow commands are project contracts. Use documented wrappers before exploration, protect the context window from command spam or large output, and never launch the AO game/client unless Mike explicitly instructs it in the current task.
- AOSharp live capture startup for Codex is `cmd /d /c tools-temp\start-aosharp-live-capture.cmd --title "<AO window title>"`, or `--pid <ao-client-pid>` only when Mike provides the process id. Do not use deprecated PowerShell capture startup or rediscover the workflow before running the wrapper.
- Cargo Box identity is exactly `Terminal:56D9B4AF`; do not substitute nearby terminals, rendered labels, templates, meshes, or inferred anchors.
- `CharacterAction` action `59` remains unresolved. Do not treat it as offer, accept, complete, fail, abandon, reward, or persistence semantics.
- Rex chain state is process-local/in-memory only. There is no DB mission persistence.
- Do not change database schemas or perform destructive database operations without explicit approval.
- Marcus Stone full quest chain must not be treated as fully implemented. The Marcus dirty vertical slice was rolled back.

## Current Arete Environment Gate Semantics

- Missing or empty Arete/Rex environment variables default enabled for local/dev.
- Explicit falsey values disable: `0`, `false`, `no`, `off`.
- Explicit truthy values enable: `1`, `true`, `yes`, `on`.
- Other non-empty values remain disabled.
- Current Rex gates using this model: `AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING`, `AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW`, `AO_REBIRTH_ENABLE_ARETE_REX_B18C_PROGRESS`, `AO_REBIRTH_ENABLE_ARETE_REX_B18D_PREVIEW`, and `AO_REBIRTH_ENABLE_ARETE_REX_B18E_COMPLETION`.

## Current AO Arete / Rex State

- `6553 Arete Landing` is enabled and is the active Rex test playfield.
- Rex Larsson identity is `SimpleChar:782DE568`.
- Rex checked-in content lives under `AORebirth/Server/ZoneEngine/Content/Arete/rex-larsson`.
- Rex works through B18F on the current baseline.
- B18C, B18D, B18E, and B18F handoff paths are implemented through safe DTO/body packet construction.
- B18C: Rex dialogue can offer `Mission:5514B18C`; B18C counts five `Malfunctioning Cleaning Robot` kills; captured per-kill feedback is emitted; final handoff sends captured mission-window sequence to B18D.
- B18C runtime targets are five evidence-backed `Malfunctioning Cleaning Robot` rows in playfield `6553`; the local spawn repair uses heartbeat-safe actor-baseline stats and preserves captured HP/level/monster data.
- B18D: exact Cargo Box use target is `Terminal:56D9B4AF`; use records B18D progress, cleans up B18D with DTO-built `QuestMessage Action=Delete`, and emits B18E `QuestFullUpdate`.
- B18E: returning to Rex from B18E state starts the captured return branch, deletes B18E with DTO-built `QuestMessage Action=Delete`, grants actual `+290 XP`, grants `+1040` credits, sends reward feedback, and emits B18F `QuestFullUpdate`.
- B18F: handoff is implemented as `Mission:5514B18F` / `Talk to Marcus Stone`. Marcus Stone identity evidence is `SimpleChar:782DE567`.
- Reward feedback text is `Received reward: 1281 XP, 1040 credits.` The `1281 XP` value is display metadata only and must not be applied as actual XP.
- Marcus Stone static B18F dialogue visibility is implemented for `SimpleChar:782DE567` in playfield `6553`, using captured `20260614-195107` B18F prompt/options.
- Marcus B18F -> B194 transition is implemented only for node `marcus_195107_b18f_002`, answer index `0`, option text `So, let me guess... You need some help with the fire?`. It requires Rex chain state `B18FPreviewed` or later, uses a process-local duplicate guard, sends DTO-built B18F `QuestMessage Action=Delete`, and sends DTO-built B194 `QuestFullUpdate`.
- Uncommitted Phase 4B item `296780` handout work exists inside the Marcus B18F -> B194 transition. It grants item `296780/296780` at QL `1` through `BaseInventory`, persists inventory immediately, sends safe DTO-built `TemplateAction`/`ContainerAddItem` notifications, and blocks the mission transition if template lookup, inventory add, or inventory persistence fails. It has focused build/search validation but has not had live smoke and is not committed. No raw replay, DB mission persistence, full Marcus quest chain, gas-fire use, trade, rewards, or follow-up mission is implemented.
- Historical stale Marcus runtime hook cleanup remains preserved: `ZoneEngine.csproj` no longer includes missing `MarcusStoneQuestChainHandler.cs`, and runtime router code no longer references `MarcusStoneQuestChainHandler`. Current Marcus static dialogue is registered through content-driven dialogue and loads the checked-in `Content/Arete/marcus-stone/manifest.json`; Marcus quest chain remains future work, gate behavior is unchanged, focused ZoneEngine build passed, and `git diff --check` passed for the cleanup.

## Current Arete / Rex Source Documents

- Current Rex content pack: `AORebirth/Server/ZoneEngine/Content/Arete/rex-larsson/manifest.json`.
- Key Rex/Arete result history: `docs/generated/rex_b18c_robot_progress_smoke_result.md`, `docs/generated/rex_b18d_to_b18e_safe_handoff_result.md`, `docs/generated/rex_mission_window_cleanup_return_state_result.md`, `docs/generated/rex_b18e_completion_b18f_handoff_result.md`, `docs/generated/rex_b18e_credit_reward_message_result.md`, and `docs/generated/arete_malfunctioning_cleaning_robot_spawn_result.md`.
- Older generated reports may describe earlier disabled-by-default gates, missing B18D cleanup, missing B18E completion, missing credits, or missing B18F handoff. Treat those as historical phase notes superseded by this top section unless a newer file says otherwise.

## Current Working Systems Summary

- Login, chat, and zone engines build/run locally in documented prior validations.
- Grid terminal and grid movement repair: outside grid terminal entry routes are capture/user-evidence backed for the tested terminals, named grid-side anchors are mapped for Tir, Omni Trade, Newland, Old Athen, Unicorn Defence Hub, Coast of Peace, Newland Desert, Stret East Bank, Tir County, Coast of Tranquility, Broken Shores, Galway County, second Lush Fields, second Omni-1 Entertainment, 4 Holes, Borealis, Three Craters West, and Three Craters East, and PF `152` grid level pads resolve `LineTeleport` destination playfield `0` to the current playfield. A 2026-06-22 private-server crash investigation found PF152 floor-pad `Terminal:C0160098` was still being handled as a full PF152-to-PF152 zone transfer; the fix changes it to a local current-playfield teleport. Grid route batch build/restart passed, and user smoke check passed.
- Grid zone-in crash diagnostics are installed for PF `152`: terminal entry now records source/destination context, zone login logs outbound object-bearing N3 messages during the initial Grid zone-in window, warnings fire for emitted integer `0x12` values and Vehicle-classified objects, and raw-vs-expected Grid exit nearby statel comparison is logged. Static/build validation passed on 2026-06-22; no game launch or live smoke was performed.
- AORebirth build validation should use `tools\build_aorebirth_debug.cmd`; it clears stale compiler/build/NuGet processes, verifies required package folders, restores packages explicitly with MSBuild only when required package folders are missing, builds `AORebirth.Core` before `ZoneEngine`, and uses single-node MSBuild with node reuse disabled. After successful rebuilds, Codex should restart engines with `cmd /d /c restart-engines.cmd`, which calls the approved root stop/start wrappers and adds no extra polling or diagnostics. PowerShell and `.ps1` wrappers are deprecated for Codex build, launch, validation, and live capture workflows.
- Engine launcher cleanup is implemented and validated: `start-engines.ps1` defaults to detached hidden Chat/Login/Zone headless startup, records stdout/stderr logs and PID metadata under `logs/engines`, waits for ports `6996`, `7012`, `7500`, and `7501`, and `stop-engines.ps1` stops engines through shutdown metadata before fallback. `status-engines.ps1` reports process and port state. `-WithWeb` starts and stops WebEngine on port `8181`. `-Visible` starts the debug-mode path and validated process/port startup in Codex, but this host reported `MainWindowHandle=0`, so desktop window visibility should be manually observed if window chrome matters.
- Runtime startup branding and shared server version baseline cleanup is implemented and validated: hidden Chat/Login/Zone logs show revision/banner branding as `AO Rebirth`, startup text uses `AO Rebirth Dev Team`, displayed version is `1.0.0.0`, and the Funcom/Anarchy Online notice remains unchanged.
- MySqlConnector migration and DAO transaction handling are repaired for login select/zone redirect.
- Current-client `FullCharacter` version 26 and live-style login state are locked decisions.
- Sit/stand, equipment visuals, inventory move, equip/unequip, bank deposit/withdraw/persistence, backpack open/close/reopen/movement/worn-slot persistence, corpse item/credit loot, player trade item/credit/cancel, vendor buy/sell/close, and death/respawn have passing documented validation for their repaired scopes.
- Backpack container open, item movement, worn-slot open, zoning visibility, persistence, and bag-in-bag rejection are implemented in the current working baseline.
- Vendor coverage is complete for practical live-accessible vendors. The current dirty OFAB repair covers the 13 BS Signup profession-locked armor terminals using live MP terminal evidence plus capture-limited fallback data and adds a live-shaped profession denial path with captured client feedback text; after validation/import/audit, the deferred statel backlog should drop from 26 to 13.

## Current Surgery Clinic Terminal State

- Surgery-clinic terminal repair is scoped to captured `GenericCmd Action=Use` behavior for `Stationary Automated Surgery Clinic` terminals, including affected private identity `Terminal:C00204A2`.
- Evidence split: private AO Rebirth capture `20260620-213807` proves `Terminal:C00204A2` and `Terminal:C00004A2` spawn as `Stationary Automated Surgery Clinic`; official live capture `20260621-062224` proves the use response family for a surgery-clinic terminal target; live capture `20260621-063942` proves post-terminal implant install uses `ClientMoveItemToInventory`.
- Implemented response: debit 300 credits, send captured `FormatFeedbackMessage`, send `CastNanoSpell` and `SetNanoDuration` for `NanoProgram:26732` with duration `90000`, grant 300 seconds of existing server-side implant access, send `SpecialUsed` for stat `124` with `5` seconds, acknowledge the original `GenericCmd`, then send delayed `SpecialAvailable` for stat `124`.
- Captured implant install path: unequip sends `ClientMoveItemToInventory Source=ImplantPage:<slot> Slot=0x6F` and receives `TemplateAction Unknown2=7` plus `ContainerAddItem`; equip sends `ClientMoveItemToInventory Source=Inventory:<slot> Slot=<implant slot>` and receives `ContainerAddItem` plus `TemplateAction Unknown2=6`.
- Not implemented: generic Statel event interpretation, shop/dialog/teleport/mission behavior, unobserved insufficient-credit behavior, database schema changes, and raw packet replay.
- Validation so far: approved debug build passed after cleanly stopping locked engines; `restart-engines.cmd` restarted Chat/Login/Zone; private AO Rebirth smoke passed for implant install/removal, clinic nano NCU cleanup on zone, and clinic effect expiry. Current implant-access build/restart validation is tracked in `docs/generated/surgery_clinic_implant_install_capture_result.md`.

## Current Bank Repair State

- Bank repair closure completed on 2026-06-20 and was manually live-smoked by Mike on the private AO Rebirth server.
- Confirmed behavior: deposit works, withdraw works, bank slot positions persist, close/reopen bank passes, zone change passes, relog passes, and persistence passes.
- Root cause: inventory-to-bank drag uses current-client `ClientContainerAddItem` (`0x1F4D5F7E`), not the legacy inbound `ContainerAddItem` handler path. AO Rebirth also decoded `ClientContainerAddItem` with the wrong body shape, so the base character identity and one-byte `Unknown` field misaligned the target/source identities. Bank reopen persistence visibility was also affected because bank slot serialization emitted zero instead of each real bank slot placement.
- Packet discovery result: live AOSharp capture showed `ClientContainerAddItem` body `1F4D5F7E 0000C350:<char> 00 0000DEAD:<char> 00000068:<inventory slot>`. The live server answered with `ContainerAddItem` (`0x47537A24`) as `Source=Inventory:<source slot>`, `Target=Bank/0xDEAD:<char>`, and `Slot=<bank slot>`.
- Serializer repair: `ClientContainerAddItemMessageSerializer` now reads `N3MessageType`, base character `Identity`, one-byte `Unknown`, target `Identity`, then source `Identity`, and consumes the full body for compatibility with observed private-client trailing bytes.
- Bank movement repair: `ClientContainerAddItemMessageHandler` handles only `Source.Type == Inventory` and `Target.Type == 0xDEAD` deposits, moves the item from the inventory page to the first free bank slot, sends the live-shaped `ContainerAddItem` acknowledgement, and persists through `character.BaseInventory.Write()`.
- Bank persistence repair: `BankSlot` field 0 is modeled as `Placement`, and `BaseInventoryPage.ToInventoryArray()` emits each actual slot key so bank item positions survive reopen, zone change, and relog.
- Update 2026-06-21: bank page items are excluded from zone/login `FullCharacter` inventory serialization and `WeaponItemFullUpdate` fanout. Live capture `20260621-073837` showed deposited bank item instances being emitted into wear-style weapon slots during zoning, and Mike's live smoke confirmed deposited bank items no longer appear in the Wear Weapon tab after zone/relog while withdraw still works.
- Temporary diagnostics removed: temporary bank diagnostic source logging and raw body capture storage were removed after closure.
- Validation performed: `SmokeLounge.AOtomation.Messaging.Tests` focused `N3RecoveredContractTests` passed `11/11`; `AORebirth.Core` Debug focused build passed; `ZoneEngine` Debug focused build passed; `git diff --check` passed with only existing LF-to-CRLF warnings; Chat/Login/Zone were restarted after rebuild and listened on ports `6996`, `7012`, `7500`, and `7501`.
- Files changed for the bank repair: `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/ClientContainerAddItemMessage.cs`, `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Serialization/Serializers/Custom/RecoveredN3MessageSerializers.cs`, `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/GameData/BankSlot.cs`, `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging.Tests/N3RecoveredContractTests.cs`, `AORebirth/Libraries/Source/AORebirth.Core/Inventory/BaseInventoryPage.cs`, `AORebirth/Server/ZoneEngine/Core/MessageHandlers/ClientContainerAddItemMessageHandler.cs`, and `AORebirth/Server/ZoneEngine/ZoneEngine.csproj`.

## Current Backpack Open Repair State

- Backpack open closure completed on 2026-06-20 and was manually live-smoked by Mike on the private AO Rebirth server.
- Confirmed behavior: right-click opens a backpack, left-clicking the window X closes it, and right-clicking the same backpack reopens it visually.
- Packet discovery result: current live client open is `GenericCmd Use` against `Inventory:<placement>`, which resolves through main inventory to `Container:<id>`. Existing/non-empty open sends `ChestFullUpdate` for `Container:<id>`, then `InventoryUpdate` for the same container, then the `GenericCmd` success ack with `Temp1=1` and the original inventory target.
- Fresh empty backpack handling: because AO Rebirth vendor buy does not yet emit the live purchase-time container introduction, first open introduces the empty container with `InventoryUpdate Unknown3=0`, sends `ChestFullUpdate`, then sends the open `InventoryUpdate Unknown3=1` before the `GenericCmd` ack.
- Close/reopen handling: close via `GenericCmd Use` on `Container:<id>` sends the live-shaped `Action` packet with `Unknown=1`, `ActionCode=1`, and `ActionIdentity=0x66`; reopen of an already-known page sends `Action` with `Unknown=0`, `ActionCode=1`, and `ActionIdentity=0x64`, followed by the normal `GenericCmd` ack.
- Backpack pages are keyed by the item/container identity `Container:<id>`, not by template id and not by inventory slot. Legacy uninstanced backpack templates get a deterministic local `Container` identity.
- Item movement into or out of backpacks is not implemented by the backpack-open repair and remains a separate future task.
- Temporary diagnostics removed: no temporary backpack logging or capture probe remains in the checked source.
- Validation performed: focused `AORebirth.Core` MSBuild passed; focused `ZoneEngine` MSBuild passed; `N3RecoveredContractTests` passed `13/13`; `git diff --check` passed with only LF-to-CRLF warnings; private live smoke passed.
- Files changed for the backpack-open repair: `AORebirth/Libraries/Source/AORebirth.Core/Inventory/BackPackInventory.cs`, `AORebirth/Libraries/Source/AORebirth.Core/Inventory/BaseInventoryPage.cs`, `AORebirth/Libraries/Source/AORebirth.Core/Inventory/BaseInventoryPages.cs`, `AORebirth/Libraries/Source/AORebirth.Core/Inventory/IInventoryPages.cs`, `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging.Tests/N3RecoveredContractTests.cs`, `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/GameData/IdentityType.cs`, `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/ChestItemFullUpdateMessage.cs`, `AORebirth/Server/ZoneEngine/Core/Controllers/PlayerController.cs`, `AORebirth/Server/ZoneEngine/Core/MessageHandlers/BackpackContainerActionMessageHandler.cs`, `AORebirth/Server/ZoneEngine/Core/MessageHandlers/ChestItemFullUpdateMessageHandler.cs`, `AORebirth/Server/ZoneEngine/Core/MessageHandlers/GenericCmdMessageHandler.cs`, `AORebirth/Server/ZoneEngine/Core/MessageHandlers/InventoryUpdateMessageHandler.cs`, and `AORebirth/Server/ZoneEngine/ZoneEngine.csproj`.

## Current Backpack Container Repair State

- Backpack container repair is committed in `d243fbb1` (`Fix backpack container open, movement, and persistence`).
- Live evidence: inventory-to-backpack drag uses `ClientContainerAddItem` with `Target=Container:<id>` and `Source=Inventory:<slot>`; the server answers with `ContainerAddItem` using the original source, target `Container:<id>`, and a server-chosen backpack slot.
- Live evidence: backpack-to-inventory drag uses `ClientMoveItemToInventory` with `SourceContainer=Backpack:<handle/slot>` and a target inventory slot; the server answers with `ContainerAddItem` from that backpack handle/slot to the character identity.
- Live evidence: right-clicking a worn backpack in `ArmorPage:<slot>` or `SocialPage:<slot>` sends `GenericCmd Use` for that worn slot. Closing sends `GenericCmd Use` for `Container:<id>`.
- Live evidence after zoning: first reopen of worn and inventory backpacks sends `ChestFullUpdate`, `InventoryUpdate` with the persisted item count, and a `GenericCmd` success ack. Mike confirmed bags persist, items inside bags persist, and bags can be opened from both worn slots and inventory after the latest test.
- Implementation shape: backpack pages remain keyed by `Container:<id>`; `InventoryUpdate` handles are registered back to the container identity so `Backpack:<handle/slot>` packets can resolve to the correct page; page writes replace only the affected page rows inside a transaction.
- Final validation before commit: focused `AORebirth.Core` build passed; focused `ZoneEngine` build passed; focused `N3RecoveredContractTests` passed `14/14`; `git diff --check` passed with only LF-to-CRLF warnings.
- Final smoke result: private live smoke passed after another round of testing. Bags persist, items inside bags persist, and bags open from worn slots and inventory.

## Current Backpack Nesting Guard State

- Bag-in-bag rejection is implemented server-side in `ClientContainerAddItemMessageHandler.TryMoveInventoryItemToBackpack`, before any destination slot allocation or inventory mutation.
- A source item is treated as a container item when it already has `IdentityType.Container` or matches the existing legacy backpack template classifier in `InventoryItemRules`.
- Book of Knowledge (`99302/99302`) is also classified as a backpack-style container, so right-click opens it through the existing backpack container path and the shared bag-in-bag guard rejects attempts to move it into another backpack/container.
- Normal non-container item-to-backpack moves still use the existing add/remove/ack/persist path. Backpack-to-inventory moves still use the existing `ClientMoveItemToInventory` backpack-handle path.
- Latest Book of Knowledge validation: live AOSharp capture `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260621-052003` showed the pre-fix right-click/drag issue; after the classifier repair, Mike live-smoked that Book of Knowledge opens and cannot be moved into another bag.
- Build/start validation performed for the Book repair: `cmd /d /c tools\build_aorebirth_debug.cmd` passed after stopping DLL-locking Chat/Login/Zone processes; `cmd /d /c start-engines.cmd` completed in 9.2 seconds and confirmed ports `6996`, `7012`, `7500`, and `7501` were listening.
- Files changed for the bag-in-bag guard and Book repair: `AORebirth/Libraries/Source/AORebirth.Core/Inventory/InventoryItemRules.cs`, `AORebirth/Server/ZoneEngine/Core/MessageHandlers/ClientContainerAddItemMessageHandler.cs`, `docs/ai/CURRENT_TASK.md`, and `docs/project/PROJECT_STATE.md`.

## Current OFAB Profession Terminal Repair State

- Current dirty work adds static vendor coverage for 13 missing `6007 BS Signup (dng)` OFAB profession armor terminals: Adventurer, Agent, Bureaucrat, Doctor, Enforcer, Engineer, Fixer, Keeper, Martial Artist, Nano-Technician, Shade, Soldier, and Trader.
- Live evidence from capture `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260620-230138` confirms the accessible Meta-Physicist OFAB terminal request/response shape: outbound `GenericCmd Use` to `VendingMachine:C0091777`, inbound `ShopUpdate` with 88 slots, inbound `Trade Open`, and inbound `GenericCmd` success ack.
- Live evidence from capture `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260620-234156` confirms denied profession terminal attempts receive `GenericCmd Use` failure ack with `Temp1=2` and do not receive `ShopUpdate` or `Trade Open`.
- Live client feedback observed for denied profession terminal attempts is `This effect can only be utilitized by <Profession>.` followed by `Your GM capabilities is required to be at least 1!`.
- Capture limitation: the other profession armor terminals are profession-locked for the current live character, so full live `ShopUpdate` capture for those terminals is not feasible from this character.
- Repair source model: the non-Meta-Physicist profession terminal rows are marked `CaptureLimited`; stock shape mirrors the captured MP profession terminal and item IDs come from current `docs/Ofab` profession lists plus current item-name data for profession shoulder/ring accessories.
- Implementation shape: `GenericCmd Use` checks the target `Vendor.TemplateHash` before `shophash` can send `ShopUpdate`; mismatched professions receive the captured client feedback text and the live-shaped `GenericCmd` failure ack (`Temp1=2`), while matching professions and non-profession OFAB terminals stay on the existing shop-open path.
- Dirty files for the OFAB repair are `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/vendors.sql`, `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/vendortemplate.sql`, `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/shopinventorytemplates.sql`, `AORebirth/Server/ZoneEngine/Core/MessageHandlers/GenericCmdMessageHandler.cs`, `docs/ai/CURRENT_TASK.md`, and `docs/project/PROJECT_STATE.md`.

## Current Open Risks

- `Quest Delete` gameplay cause remains unresolved; current use is packet-level mission-window cleanup only.
- `CharacterAction` action `59` remains unresolved.
- Rex/Mission state is not persisted to DB and will not survive process restart as mission state.
- Marcus Stone quest chain beyond the committed B194 mission-window preview is not implemented. Uncommitted item `296780` handout work is validated but unsmoked and paused.
- OFAB profession armor terminal SQL and profession lock are dirty and pending private smoke.
- NPC chase/movement remains high risk and should not be changed without replay/capture evidence.
- The producer that inserted the observed duplicate health stat before the heartbeat attacker scan remains unresolved. The stat lookup deliberately fails loudly for a relevant malformed attacker so this corruption is not hidden.
- `PlayfieldAnarchyF` remains a current-client structure mismatch.
- Full gameplay systems for missions, quests, perks, research, pets, PvP/towers, teams, and organizations remain incomplete outside the documented repaired slices.

# Historical State Log

Historical notes below are preserved for provenance. Any older Rex/B18D/B18E statements about disabled-by-default gates, missing B18D cleanup, missing B18E completion, missing credits, or missing B18F handoff are superseded by the current memory section above. Historical `cellao_codex_clean` database references and old `Cellao-Clean` backup paths in this log are retained as exact historical provenance, not current repo naming guidance.

## Historical Current Status Snapshot

AO Rebirth is a local C#/.NET Framework-era Anarchy Online server workspace. Current work is focused on making the server compatible with Mike's current AO client and local `cellao_codex_clean` MySQL database; this is a legacy database name retained for local compatibility while evidence-backed packet, gameplay, and data repairs continue.

Capture-derived content reconstruction now has mandatory identity-first rules in `docs/project/KNOWN_DECISIONS.md` and `docs/ai/WORKFLOW.md`: captured AO identity is the primary key, complete relevant capture sets must be searched before declaring evidence missing, identity-linked full-update/stat evidence outranks names/screenshots/proximity, evidence tables are required before SQL or game-data edits, and uncertain fields must fail closed instead of being guessed.

Repository licensing now uses a dual-license structure: inherited CellAO portions remain under the CellAO BSD-style license terms, while AO Rebirth additions are proprietary. Root `LICENSE` and `NOTICE` files document the split and attribution.

Final runtime third-party attribution is documented in the root `NOTICE`: CellAO, bundled runtime source components, and all detected runtime NuGet dependencies are attributed; `tools-temp`, AOSharp, EasyHook, test packages, and historical captures remain excluded from runtime distribution.

# Working Systems

- Login, chat, and zone engines build and run locally.
- AOSharp Live Capture remains isolated under `tools-temp` and now builds as a fuller passive data logger: new sessions standardize raw packet, event, vendor, shop, system-message, chat/dialogue, NPC-interaction, inventory, enemy-state, metadata, and health-validation outputs without changing AO client behavior or runtime server behavior.
- Arete dialogue/quest framework scaffolding now exists under ZoneEngine `Core/Arete` as inactive models, JSON file/directory/manifest loaders, registries, validators, aggregate content validation, aggregate validation reporting, synthetic condition-reference validation, dialogue session services, no-op condition/action helpers, in-memory mission-state services, a synthetic dialogue-action to mission-state adapter, inactive dialogue action reference validation, file-loaded action reference validation coverage, inactive objective playback, and an unused zero-pack bootstrap helper. A PowerShell validation harness under `tools-temp/arete-framework-validation` covers 131 synthetic in-memory, file-loaded, directory-loaded, manifest-loaded, dialogue-session, mission-state, dialogue-action adapter, content action reference, file-loaded action reference, aggregate content validation, condition-reference, and aggregate report cases. The first inactive captured Rex Larsson content draft now exists under `AORebirth/Server/ZoneEngine/Content/Arete/rex-larsson` with a manifest, one dialogue pack, one quest pack, eight captured answer-list nodes, eight recovered `KnubotAppendText` prompt nodes, fifteen visible options, and three QuestFullUpdate-decoded mission definitions. `Mission:5514B18C`, `Mission:5514B18D`, and `Mission:5514B18E` now have decoded titles, objectives, QuestFullUpdate evidence metadata, cautious in-pack packet-sequence links, non-executable objective trigger evidence, and inactive objective playback for B18C kill-count robot/death observations, B18D GenericCmd Use against packet identity `Terminal:56D9B4AF`, and B18E Rex KnuBotOpenChatWindow return signal. Later Arete captures resolved the B18D static dynel identity/full-update data for `Terminal:56D9B4AF`; Rex packet semantics review confirmed mission-targeted action parameters and packet-level `QuestAction.Delete`, but action `59`, delete gameplay meaning, executable mission transition semantics, exact objective progress mapping beyond the current gated B18C/B18D smoke paths, and dialogue-to-mission routing remain unresolved. Rex aggregate validation and dry-run pass; outside the separate gated B18C quest-preview packet path, no quest packet emission, SQL, schema, rewards, inventory, XP, credits, character mutation, persistence, executable mission action, real condition semantics, or gameplay behavior is wired yet.
- Rex Larsson has a controlled live dialogue route behind disabled-by-default environment gate `AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING`. The route uses the shared `ContentDrivenNpcDialogueRouter` registration model, while Rex remains the only registered content-driven NPC. When enabled, only `SimpleChar:782DE568` in playfield `6553 Arete Landing` loads the captured Rex Arete manifest and uses the existing KnuBot open/append-text/answer-list/close packet path to show captured dialogue prompt text/options and advance an in-memory dialogue session. Legacy `KnuBotScriptName` NPCs still fall through to the compiled KnuBot path. A second disabled-by-default gate, `AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW`, controls the captured `Mission:5514B18C` `QuestFullUpdate` preview. Manual smoke on 2026-06-17 showed raw captured frame replay causes a hard client hang, so raw replay remains blocked. The safe B18C DTO/body serializer sends the decoded QuestFullUpdate through normal `ZoneClient.SendCompressed(MessageBody)` framing; live smoke confirmed B18C appears in the client mission window without a client hang. A third disabled-by-default gate, `AO_REBIRTH_ENABLE_ARETE_REX_B18C_PROGRESS`, now enables B18C kill progress only after the safe preview is emitted for that player. The progress observer hooks the existing `Playfield.KillNpcTarget` death point and counts only `Malfunctioning Cleaning Robot` deaths for `Mission:5514B18C`; it sends captured per-kill feedback from capture `20260614-194454`. Kills `1/5` through `4/5` send the captured encoded `FormatFeedbackMessage` remaining-count payload plus captured `FeedbackMessage CategoryId=110 MessageId=249817907`; kill `5/5` sends the captured generic feedback and then a one-time captured mission-window handoff sequence: `CharacterAction` action `59` targeting `Mission:5514B18C`, `Quest` action `Delete` for `Mission:5514B18C`, and next `QuestFullUpdate` for `Mission:5514B18D`. This B18C handoff is still packet-level only: no rewards, inventory, XP/credits, DB writes, persistence, or action/delete gameplay interpretation is enabled. Capture `20260614-194454` contains named robot spawn/full-update/death/corpse evidence, including level, HP, `monsterData=297023`, and coordinates. Five evidence-backed captured robot rows are staged in `tools-temp/sql-staging/arete_malfunctioning_cleaning_robot_mobspawns.sql` and applied locally to `mobspawns` for playfield `6553`; DB verification shows Rex plus five target robots and zero unrelated `6553` rows. Follow-up load-screen smoke showed captured SCFU-visible stats alone were not enough for the old heartbeat and SimpleCharFullUpdate paths, so the staged robot SQL includes separated runtime actor-baseline scaffold stats; each robot has 27 stat rows while retaining the same captured spawn positions, HP, level, and monster data. Manual smoke confirmed the B18C handoff advances the client into `Mission:5514B18D`. The B18D static dynel row for DB-backed `Terminal:56D9B4AF` in playfield `6553` has been reset to exact captured packet evidence from later Arete capture segments. Capture `20260614-194454` proves the B18D `GenericCmd Action=Use` target, while captures `20260614-205724`, `20260614-214819`, and `20260614-215831` contain repeated `SimpleItemFullUpdate` packets for the same identity. The corrected row uses captured position `(3621.576, 51.745, 780.4768)`, rotation `(0, -0.7101817, 0, 0.7040185)`, and captured stats `Flags=139265`, `StaticInstance=297277`, `ACGItemLevel=1`, `ACGItemTemplateID=297277`, `ACGItemTemplateID2=297277`, `MultipleCount=1`, `AnimPlay=0`, and `AnimPos=0`. Rejected local smoke attempts using nearby `Terminal:57369E8E` `Junk`, template `285300`, or explicit `Mesh=18794` are not represented in the corrected row. A fourth disabled-by-default gate, `AO_REBIRTH_ENABLE_ARETE_REX_B18D_PREVIEW`, now enables a narrow exact-target `GenericCmd Action=Use` route for `Terminal:56D9B4AF` in Arete after the player has received the B18D preview. When all Rex gates are enabled, the route acknowledges the click, records B18D objective observed/complete in memory as preview-only progress `1/1`, and sends a DTO-built B18E `QuestFullUpdate` from captured packet `20260614-194454/packets.hex.log:5767` so `Mission:5514B18E` can appear as `Return to Rex Larsson`. The B18E DTO body matches captured packet `#5339` byte-for-byte from the N3 body onward. No B18D `Quest Delete`, B18D action `59`, rewards, inventory, XP/credits, DB writes beyond the placement SQL, persistence, general StaticDynel event execution, B18E completion, or action/delete gameplay interpretation is enabled.
- NPC dialogue starts now request a visible face-toward-player update through the existing recovered `SetWantedDirection` support for legacy KnuBot conversations and the gated Rex Arete route. Manual in-client smoke passed. Normal NPC chase movement remains on the existing coordinate `FollowTarget` path and was not changed.
- Update 2026-06-18: the Rex live-route status above is superseded for B18D/B18E cleanup. B18D now sends DTO-built `QuestMessage Action=Delete` for only `Mission:5514B18D` after exact Cargo Box use, and B18E completion is now available behind `AO_REBIRTH_ENABLE_ARETE_REX_B18E_COMPLETION`. The B18E path sends DTO-built `QuestMessage Action=Delete` for only `Mission:5514B18E`, grants the proven actual `+290 XP`, grants captured `+1040` credits, sends reward feedback text `Received reward: 1281 XP, 1040 credits.`, and sends DTO-built `Mission:5514B18F` / `Talk to Marcus Stone` QuestFullUpdate. The `1281 XP` value is display metadata and is not applied as actual XP. No B18D/B18E action `59`, item rewards, inventory mutation, DB mission persistence, Marcus Stone dialogue, SQL/schema changes, or raw packet replay is enabled.
- `6553 Arete Landing` is enabled in `ZoneEngine/XML Data/Playfields.xml` so the debug `/tp` command can pass the `Playfields.ValidPlayfield` allow-list check and reach the current-client playfield data already present in `playfields.dat`.
- Dependency cleanup for proprietary-readiness is in progress/completed for the requested GPL/unlicensed targets: `MySql.Data` was replaced by `MySqlConnector`, WCell-derived `Cell.Core`/`Cell.Util` compiled sources were replaced with clean implementations, and AOSharp remains isolated to `tools-temp`/capture provenance rather than the main solution build.
- Post-MySqlConnector login select is repaired: the generic DAO helper now passes the active transaction to Dapper, allowing character select `SetOnline` to complete and LoginEngine to redirect the client to Zone.
- DAO transaction handling is hardened after the MySqlConnector migration: locally owned DAO transactions commit only after successful work, roll back on failure, and nested DAO write/read helpers in transaction scopes receive the active connection/transaction.
- Post-sweep live validation passed for login, zone entry, shop open, vendor buying, and timed logout, with no current-window MySqlConnector/Dapper/transaction errors in engine logs.
- Current-client `FullCharacter` version 26 and live-style login state are locked project decisions.
- Sit/stand behavior is repaired.
- Weapon and armor equipment visuals are repaired for the current test scope.
- Equipped items persist across relog in the documented test scope.
- Inventory Move Live Verification result: PASS. A junk item moved correctly between inventory slots before relog and remained in the correct slot after relog.
- Equip Item Live Verification result: PASS. Item equipped correctly before relog, no duplicate remained in inventory, and after relog the item remained equipped in the correct equipment slot.
- Unequip Item Live Verification result: PASS. Item moved from equipment slot to inventory correctly, the equipment slot became empty, no duplicate remained equipped, and after relog the item stayed in inventory while the equipment slot stayed empty.
- Corpse Item Loot Live Verification result: PASS. Non-credit corpse item appeared in inventory correctly, the corpse no longer offered the looted item, no duplicate item appeared, cash did not change from item loot, and the item remained in inventory after relog.
- Corpse Credit Loot Live Verification result: PASS. One correct corpse credit message displayed, cash increased by exactly the awarded amount, no inventory item was created from credit loot, increased cash persisted after relog, and no duplicate corpse credit feedback was observed.
- Player Trade Item Live Verification result: PASS. Item left player A inventory correctly, appeared in player B inventory correctly, no duplicate item existed, cash remained unchanged, and after relog the item remained only with player B.
- Player Trade Credits Live Verification result: PASS. Player A cash decreased by the expected amount, player B cash increased by the expected amount, no inventory items moved, appeared, or disappeared, cash values persisted after relog, and no duplicate cash behavior was observed.
- Player Trade Cancel/Decline Live Verification result: PASS. Trade panes closed correctly, the offered item remained with the original player, cash remained unchanged, no duplicate item or cash behavior occurred, and state persisted correctly after relog.
- Vendor Buy Live Verification result: PASS. Purchased item appeared in inventory correctly, cash decreased by the exact purchase price, no duplicate item appeared, and after relog the purchased item and reduced cash value both persisted.
- Vendor Sell Live Verification result: PASS. Sold item left inventory correctly, cash increased by the exact sale price, no duplicate item appeared, and after relog the sold item remained absent and increased cash value persisted.
- Vendor Close/Cancel Live Verification result: PASS. Pending vendor transaction state closed without accepting, cash stayed unchanged, items remained with their original owner/location, no duplicate item appeared, and the same item/cash state persisted after relog.
- Live Persistence Verification complete: inventory move, equip item, unequip item, corpse item loot, corpse credit loot, player trade item, player trade credits, player trade cancel/decline, vendor buy, vendor sell, and vendor close/cancel all matched expected client-visible behavior and survived relog.
- Death/respawn white-screen behavior is repaired.
- Corpse use, item loot, credit loot, XP text, and corpse despawn have working documented paths. The completed corpse credit investigation fixed the `CorpseFullUpdate` cash offset, removed duplicate manual corpse credit chat, retained focused assertions, and passed Cliff Malle playtest verification.
- Player trade item and credit transfer have been repaired and verified in the documented test scope. Credit-only, item-only, mixed item-plus-credit, and cancel/decline trades behaved as expected, and no player trade display or commit defect was reproduced. Temporary `TRADE_*` logging remains available for future trade investigation.
- Broad combat smoke `-SkipBuild`, focused corpse credit assertions, and inventory/container regression assertions pass after stale harness assertions were cleaned up. The cleanup changed harness expectations only, not gameplay behavior.
- Vendor shop buy, sell, close, and current-client ICC shop stock coverage have been repaired for the captured Fair Trade areas.
- Omni Basic General Shop live-capture import completed from AOSharp capture `20260612-012644`. The validated staged SQL added 23 `1183 ord_smarket_omni_basic` vendor rows, 16 vendor templates, and 16 shop inventory groups with 690 inventory rows. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 381`; total uncovered statel vendors dropped from `404` to `381`, and `1183 ord_smarket_omni_basic` dropped from `39` to `16`. No runtime vendor behavior changed.
- Non-shop statel template `155225` (`Refreshing Drink`) is excluded from vendor coverage metrics, missing-vendor reports, capture targeting, and import planning while remaining visible in raw statel coverage output. AOSharp captures `20260612-012644` and `20260612-044234` showed VendorFullUpdate evidence but no ShopUpdate inventory rows, and live operator verification found the Superior instances were not reachable/openable. Verification now shows `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 351`, and `StatelVendorExclusions = 30`. No SQL, vendor mappings, imports, or runtime vendor behavior changed.
- Inaccessible playfield `500 Parnassos` is excluded from active vendor coverage metrics, missing-vendor reports, capture targeting, and import planning while remaining visible in raw statel coverage output. Operator verification confirmed there is no practical live-client access path for capture. Current verification shows `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 29`, and `StatelVendorExclusions = 169`; the actionable capture backlog dropped from `89` to `29`. No SQL, vendor mappings, imports, or runtime vendor behavior changed.
- Neutral Training Startup Equipment import completed from AOSharp capture `20260614-002319`. The validated staged SQL added 2 `954 Neutral Training` vendor rows, 1 vendor template, and 1 new shop inventory group with 9 inventory rows. Both Basic Startup Equipment statels have direct VendorFull and ShopUpdate evidence and share exact inventory hash `WHBW`. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 27`, and `StatelVendorExclusions = 169`; actionable uncovered statel vendors dropped from `29` to `27`. Current coverage/actionability chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104 -> 99 -> 96 -> 93 -> 89 -> 29 -> 27`. No runtime vendor behavior changed.
- Freelancers Inc. HQ - Rome Agency Shop import completed from AOSharp capture `20260614-022639`. The validated staged SQL added 1 `7011 Freelancers Inc. HQ - Rome` vendor row, 1 vendor template, and 1 new shop inventory group with 26 inventory rows. The imported row covers Agency Shop template `285348` at X 93.972 Y 2.01 Z 73.734 with direct VendorFull and ShopUpdate evidence. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 26`, and `StatelVendorExclusions = 169`; actionable uncovered statel vendors dropped from `27` to `26`. Current coverage/actionability chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104 -> 99 -> 96 -> 93 -> 89 -> 29 -> 27 -> 26`. No runtime vendor behavior changed.
- Vendor coverage campaign freeze completed. Status: COMPLETE (LIVE COVERAGE). The campaign is complete for all practical live-accessible vendors. The remaining `26` uncovered statel vendors are deferred because they require setup-specific access: BS Signup profession-locked terminals, sided/org-dependent Tower Shop terminals, Clan-only shops, ICC Holodeck / Arete divergence, Unicorn Outpost, and special registration interiors. No SQL, capture, import, mapping change, or runtime vendor behavior change was made for the freeze.
- Omni Superior General Shop live-capture import completed from AOSharp capture `20260612-044234`. The validated v2 staged SQL added 27 `1185 ord_smarket_omni_sup` vendor rows, 20 vendor templates, and 19 new shop inventory groups while reusing existing map shop hash `LJI7`. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 324`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `351` to `324`. Current live-capture coverage chain is `404 -> 381 -> 351 -> 324`. No runtime vendor behavior changed.
- Clan Basic General Shop live-capture import completed from AOSharp capture `20260612-225855`. The validated staged SQL added 29 `1180 ord_smarket_clan_basic` vendor rows, 29 vendor templates, and 25 new shop inventory groups with 1575 inventory rows while reusing existing shop hashes `G4XZ`, `HYDQ`, `LJI7`, and `R5R7`. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 295`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `324` to `295`. Current live-capture coverage chain is `404 -> 381 -> 351 -> 324 -> 295`. No runtime vendor behavior changed.
- Clan Superior General Shop live-capture import completed from AOSharp capture `20260612-232439`. The validated staged SQL added 19 `1182 ord_smarket_clan_sup` vendor rows, 19 vendor templates, and 14 new shop inventory groups with 594 inventory rows while reusing existing shop hashes `LJI7`, `CHHQ`, `OHOO`, `JYPE`, and `Cont`. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 276`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `295` to `276`. Current live-capture coverage chain is `404 ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ 381 ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ 351 ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ 324 ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ 295 ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ 276`. No runtime vendor behavior changed.
- Omni Advanced General Shop live-capture import completed from AOSharp capture `20260613-002828`. The validated staged SQL added 23 `1184 ord_smarket_omni_advanced` vendor rows, 16 vendor templates, and 15 new shop inventory groups with 760 inventory rows while reusing existing shop hash `LJI7`. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 253`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `276` to `253`, and `1184 ord_smarket_omni_advanced` has no remaining vendor-scan targets. Current live-capture coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253`. No runtime vendor behavior changed.
- Omni Basic Implant Terminals live-capture import completed from AOSharp capture `20260613-005616`. The validated staged SQL added 13 `1183 ord_smarket_omni_basic` implant vendor rows and 13 vendor templates, with no new shop inventory groups because existing implant shop hashes `5BUX`, `5M5F`, `6MQN`, `6YPW`, `7LZ3`, `A32J`, `JWHR`, `KV75`, `KVVT`, `O3KI`, `RNWW`, `RO4Q`, and `SBQ6` were reused. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 240`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `253` to `240`, and `1183 ord_smarket_omni_basic` has no remaining vendor-scan targets. Current live-capture coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240`. No runtime vendor behavior changed.
- Neutral Basic General/Specialty Shop live-capture import completed from AOSharp captures `20260613-012810` and `20260613-014033`. The validated staged SQL added 6 `1193 spec_smarket_neut_basic` vendor rows, 6 vendor templates, and 6 new shop inventory groups with 64 inventory rows; Specialist Commerce required Trader access. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 234`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `240` to `234`, and `1193 spec_smarket_neut_basic` has no remaining vendor-scan targets. Current live-capture coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234`. No runtime vendor behavior changed.
- spec_smarket specialty import completed from operator-approved inferred reuse of Neutral Basic/Specialty captures `20260613-012810` and `20260613-014033`. The validated staged SQL added 16 vendor rows across `1189 spec_smarket_clan_advanced`, `1190 spec_smarket_clan_sup`, `1191 spec_smarket_omni_advanced`, and `1192 spec_smarket_omni_sup`, plus 12 vendor templates. No new shop inventory groups were added; existing shop hashes `I3E4`, `7ATH`, `7X7Q`, `PX4X`, `FBQ3`, and `FLEW` were reused. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 218`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `234` to `218`, and the four spec_smarket playfields have no remaining vendor-scan targets. Current live-capture coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218`. No runtime vendor behavior changed.
- Clan Advanced General Shop live-capture import completed from AOSharp capture `20260613-034740`. The validated staged SQL added 16 `1181 ord_smarket_clan_advanced` vendor rows, 16 vendor templates, and 11 new shop inventory groups with 505 inventory rows while reusing existing shop hashes `Cont`, `IVM2`, `IYD4`, `JTYS`, and `LJI7`. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 202`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `218` to `202`, and `1181 ord_smarket_clan_advanced` has no remaining vendor-scan targets. Current live-capture coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202`. No runtime vendor behavior changed.
- Overnight exact-template inferred vendor import completed from existing captured/inferred template evidence. The validated staged SQL added 31 vendor rows only, reusing existing `vendortemplate` hashes and existing `shopinventorytemplates` hashes; no new vendor templates or shop inventory groups were added. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 171`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `202` to `171`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171`. No runtime vendor behavior changed.
- Neutral ICC implant/cluster import completed from AOSharp capture `20260613-170220`. The validated staged SQL added 24 vendor rows across `2064 neut_basic_implants_shop` and `2073 neut_advanced_implants_shop`, 12 vendor templates, and 12 new shop inventory groups with 1876 inventory rows. The `2064` rows are captured mappings; the `2073` rows are high-confidence exact-template reuse from the captured `2064` template evidence, matching the existing ICC pharmacy reuse pattern across these interiors. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 147`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `171` to `147`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147`. No runtime vendor behavior changed.
- Arete ICC implant/cluster import completed from AOSharp capture `20260613-172753`. The validated staged SQL added 5 `6553 Arete Landing` vendor rows, 5 vendor templates, and 5 new shop inventory groups with 573 inventory rows. The imported core targets are ICC Basic Implants, ICC Faded Clusters, ICC Bright Clusters, ICC Shiny Clusters, and ICC Pharmacy; incidental nearby capture evidence was intentionally excluded. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 142`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `147` to `142`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142`. No runtime vendor behavior changed.
- Newland + Omni startup import completed from AOSharp capture `20260613-185338`. The validated staged SQL added 9 vendor rows across `565 Newland Desert` and `710 Omni-1 Trade`, 6 vendor templates, and 6 new shop inventory groups with 232 inventory rows. The imported rows cover Newland Basic Armor, Newland Basic Startup Equipment, Newland Basic Nano Clusters, Food, Drinks, and four OT Basic Startup Equipment statels; the four Omni startup vendors share one deduplicated inventory group. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 133`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `142` to `133`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133`. No runtime vendor behavior changed.
- Clan Basic Startup Equipment import completed from AOSharp capture `20260613-211234`. The validated staged SQL added 4 `540 Old Athen` vendor rows, 1 vendor template, and 1 new shop inventory group with 9 inventory rows. The four Old Athen Clan Basic Startup Equipment statels share one deduplicated inventory group; `0xC000021C` had captured inventory but no direct VendorFull and was correlated by template `99569` plus exact inventory match with the three VendorFull-confirmed startup terminals. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 129`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `133` to `129`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129`. No runtime vendor behavior changed.
- Broken Shores + Lush Fields live capture import completed from AOSharp capture `20260613-215211`. The validated staged SQL added 2 vendor rows across `665 Broken Shores` and `695 Lush Fields`, 2 vendor templates, and 2 new shop inventory groups with 190 inventory rows. The imported rows cover Broken Shores OT Advanced Trade Skills and Lush Fields Basic Startup Equipment; the Lush Fields startup inventory was captured as a new group because it differs from Newland startup by QL. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 127`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `129` to `127`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127`. No runtime vendor behavior changed.
- Holes in the Wall live capture import completed from AOSharp capture `20260613-221619`. The validated staged SQL added 3 vendor rows across `791 Holes in the Wall` and `4565 Hardware Dimenion - Superior`, 2 vendor templates, and 1 new shop inventory group with 87 inventory rows while reusing existing shop hash `Cont`. The two Holes in the Wall rows use captured ShopUpdate inventory on exact statel identities plus current-client statel target metadata because the client crash prevented VendorFull rows; the Hardware Dimension row is high-confidence exact-template inference for template `151974`. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 124`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `127` to `124`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124`. The incidental inventory-only identity `(VendingMachine:12E4CE58)` had no target correlation and was not imported. No runtime vendor behavior changed.
- Tower Shop + BS Signup live capture import completed from AOSharp capture `20260613-223554`. The validated staged SQL added 18 vendor rows across `4704 Tower Shop (dungeon)` and `6007 BS Signup (dng)`, 18 vendor templates, and 18 new shop inventory groups with 2047 inventory rows. The imported rows cover 14 Tower Shop terminals and 4 BS Signup OFAB terminals; Clan City Buildings, Neutral City Buildings, and Leets-R-Us were VendorFull-only / not openable and were not imported, and remaining BS Signup profession-locked terminals require matching professions. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 106`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `124` to `106`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106`. No runtime vendor behavior changed.
- Omni Training Startup Shop import completed from AOSharp capture `20260613-231115`. The validated staged SQL added 1 `950 Omni Training` vendor row, 1 vendor template, and 1 new shop inventory group with 7 inventory rows. The imported row covers Startup Shop! template `100035`; VendorFull evidence was captured on playfield entry/dynel spawn and ShopUpdate evidence was captured by opening the terminal. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 105`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `106` to `105`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105`. No runtime vendor behavior changed.
- Treepine Hut OT Clothes import completed from AOSharp capture `20260613-233535`. The validated staged SQL added 1 `1887 Treepine Hut` vendor row, 1 vendor template, and 1 new shop inventory group with 16 inventory rows. The imported row covers OT Clothes template `99490`; incidental already-covered Treepine captures were intentionally not imported. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 104`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `105` to `104`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104`. No runtime vendor behavior changed.
- Uncle Bazzit's Workshop live capture import completed from AOSharp capture `20260613-184615`. The validated staged SQL added 5 `4354 Uncle Bazzits Workshop (Dng)` vendor rows, 5 vendor templates, and 4 new shop inventory groups with 129 new inventory rows while reusing existing exact shop hash `Fash` for Maria's Fashion. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 99`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `104` to `99`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104 -> 99`. No runtime vendor behavior changed.
- Jobe Basic dimensions live capture import completed from AOSharp capture `20260614-000058`. The validated staged SQL added 3 vendor rows across `4563 Hardware Dimension - Basic` and `4567 Dimensional Shift - Basic`, 3 vendor templates, and 3 new shop inventory groups with 98 inventory rows. The imported rows cover Basic Armor, Costly Regenerative Supplies --- 1-90, and Basic Implants; same-template Advanced dimensional targets were intentionally not imported without direct capture or explicit inference approval. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 96`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `99` to `96`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104 -> 99 -> 96`. No runtime vendor behavior changed.
- Jobe Advanced dimensions live capture import completed from AOSharp capture `20260614-002319`. The validated staged SQL added 3 vendor rows across `4564 Hardware Dimension - Advanced` and `4568 Dimensional Shift - Advanced`, 3 vendor templates, and 2 new shop inventory groups with 68 new inventory rows while reusing exact shop hash `HMIZ` for regenerative supplies. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 93`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `96` to `93`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104 -> 99 -> 96 -> 93`. No runtime vendor behavior changed.
- Jobe Superior dimensions live capture import completed from AOSharp capture `20260614-002319`. The validated staged SQL added 4 vendor rows across `4565 Hardware Dimension - Superior` and `4569 Dimensional Shift - Superior`, 4 vendor templates, and 4 new shop inventory groups with 116 inventory rows. Imported targets were Superior Armor, Superior Equipment for Nano Specialists, Costly Regenerative Supplies --- 100-175, and Superior Implants. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 89`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `93` to `89`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104 -> 99 -> 96 -> 93 -> 89`. Incidental Heavenly Business capture evidence was already covered and not imported. No runtime vendor behavior changed.
- `1183 ord_smarket_omni_basic` static vendor coverage was expanded with 20 approved mappings. The targeted import backed up `vendors`, inserted only those rows into `cellao_codex_clean.vendors`, and verified `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `730` to `710`, and `1183 ord_smarket_omni_basic` dropped from `77` to `57`. No runtime vendor behavior changed.
- `1184 ord_smarket_omni_advanced` static vendor coverage was expanded with 21 approved mappings. The targeted import backed up `vendors`, inserted only those rows into `cellao_codex_clean.vendors`, and verified `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `710` to `689`, and `1184 ord_smarket_omni_advanced` dropped from `68` to `47`. No runtime vendor behavior changed.
- `1185 ord_smarket_omni_sup` static vendor coverage was expanded with 21 approved mappings. The targeted import backed up `vendors`, inserted only those rows into `cellao_codex_clean.vendors`, and verified `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `689` to `668`, and `1185 ord_smarket_omni_sup` dropped from `68` to `47`. No runtime vendor behavior changed.
- `500 Parnassos` static vendor coverage was expanded with 25 approved mappings. The targeted import backed up `vendors`, inserted only those rows into `cellao_codex_clean.vendors`, and verified `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `668` to `643`, and `500 Parnassos` dropped from `140` to `115`. No runtime vendor behavior changed.
- `1182 ord_smarket_clan_sup` static vendor coverage was expanded with 17 approved mappings. The approved rows are present in `cellao_codex_clean.vendors`; the latest import run did not insert duplicates because all 17 IDs already existed. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `643` to `626`, and `1182 ord_smarket_clan_sup` dropped from `44` to `27`. No runtime vendor behavior changed.
- `655 Andromeda` static vendor coverage was expanded with 16 approved mappings. The targeted import backed up `vendors`, inserted only those rows into `cellao_codex_clean.vendors`, and verified `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `626` to `610`, and `655 Andromeda` dropped from `17` to `1`. Template `151987` remains unknown. No runtime vendor behavior changed.
- `1180 ord_smarket_clan_basic` static vendor coverage was expanded with 4 approved mappings. The targeted import backed up `vendors`, inserted only those rows into `cellao_codex_clean.vendors`, and verified `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `610` to `606`, and `1180 ord_smarket_clan_basic` dropped from `43` to `39`. No runtime vendor behavior changed.
- `1181 ord_smarket_clan_advanced` static vendor coverage was expanded with 4 approved mappings. Commit `fbcc1a4` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 602`. Total uncovered statel vendors dropped from `606` to `602`, and `1181 ord_smarket_clan_advanced` dropped from `30` to `26`. No runtime vendor behavior changed.
- `2064 neut_basic_implants_shop` static vendor coverage was expanded with 3 approved mappings. Commit `ed869d5` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 599`. Total uncovered statel vendors dropped from `602` to `599`, and `2064 neut_basic_implants_shop` dropped from `15` to `12`. No runtime vendor behavior changed.
- `2073 neut_advanced_implants_shop` static vendor coverage was expanded with 3 approved mappings. Commit `a79b5ec` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 596`. Total uncovered statel vendors dropped from `599` to `596`, and `2073 neut_advanced_implants_shop` dropped from `15` to `12`. No runtime vendor behavior changed.
- `565 Newland Desert` static vendor coverage was expanded with 3 approved mappings. Commit `2bb7ad5` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 593`. Total uncovered statel vendors dropped from `596` to `593`, and `565 Newland Desert` dropped from `9` to `6`. No runtime vendor behavior changed.
- `2096 4holes Fashion` static vendor coverage was expanded with 3 approved mappings. Commit `0522ffb` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 590`. Total uncovered statel vendors dropped from `593` to `590`, and `2096 4holes Fashion` dropped from `7` to `4`. No runtime vendor behavior changed.
- `4567 Dimensional Shift - Basic` static vendor coverage was expanded with 3 approved mappings. Commit `7c10b5a` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 587`. Total uncovered statel vendors dropped from `590` to `587`, and `4567 Dimensional Shift - Basic` dropped from `5` to `2`. No runtime vendor behavior changed.
- `4568 Dimensional Shift - Advanced` static vendor coverage was expanded with 3 approved mappings. Commit `5e5303b` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 584`. Total uncovered statel vendors dropped from `587` to `584`, and `4568 Dimensional Shift - Advanced` dropped from `5` to `2`. No runtime vendor behavior changed.
- `4569 Dimensional Shift - Superior` static vendor coverage was expanded with 3 approved mappings. Commit `abee0ce` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 581`. Total uncovered statel vendors dropped from `584` to `581`, and `4569 Dimensional Shift - Superior` dropped from `5` to `2`. No runtime vendor behavior changed.
- `4563 Hardware Dimension - Basic` static vendor coverage was expanded with 2 approved mappings. Commit `0ded4a9` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 579`. Total uncovered statel vendors dropped from `581` to `579`, and `4563 Hardware Dimension - Basic` dropped from `4` to `2`. No runtime vendor behavior changed.
- `6553 Arete Landing` static vendor coverage was expanded with 2 approved mappings. Commit `389e8b3` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 577`. Total uncovered statel vendors dropped from `579` to `577`, and `6553 Arete Landing` dropped from `8` to `6`. No runtime vendor behavior changed.
- `4564 Hardware Dimension - Advanced` static vendor coverage was expanded with 2 approved mappings. Commit `aa62dcd` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 575`. Total uncovered statel vendors dropped from `577` to `575`, and `4564 Hardware Dimension - Advanced` dropped from `4` to `2`. No runtime vendor behavior changed.
- `4565 Hardware Dimension - Superior` static vendor coverage was expanded with 2 approved mappings. Commit `1810408` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 573`. Total uncovered statel vendors dropped from `575` to `573`, and `4565 Hardware Dimension - Superior` dropped from `5` to `3`. No runtime vendor behavior changed.
- `2060 neut_basic_weapon_shop` static vendor coverage was expanded with 1 approved mapping. Commit `83fc74f` added the source SQL row, the targeted import inserted only that row into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 572`. Total uncovered statel vendors dropped from `573` to `572`, and `2060 neut_basic_weapon_shop` dropped from `5` to `4`. No runtime vendor behavior changed.
- `2070 neut_advanced_weapons_shop` static vendor coverage was expanded with 1 approved mapping. Commit `9c41ed9` added the source SQL row, the targeted import inserted only that row into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 571`. Total uncovered statel vendors dropped from `572` to `571`, and `2070 neut_advanced_weapons_shop` dropped from `5` to `4`. Backup: `C:\Users\Mike\Documents\Cellao-Clean\tools-temp\db-backups\vendors_before_2070_neut_advanced_weapons_shop_20260610_040826.sql`. Rejected candidates `135659521`/`297466`, `135659522`/`297470`, `135659523`/`99572`, and `135659524`/`99573` remain uncovered until matching `vendortemplate` evidence is found. No runtime vendor behavior changed.
- `600 Varmint Woods` static vendor coverage was expanded with 1 approved mapping. Commit `e197b9f` added the source SQL row, the targeted import inserted only that row into `cellao_codex_clean.vendors`, query-back confirmed `39321612 | 600 | 93063 | AdvOA`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 570`. Total uncovered statel vendors dropped from `571` to `570`, and `600 Varmint Woods` dropped from `3` to `2`. Backup: `C:\Users\Mike\Documents\Cellao-Clean\tools-temp\db-backups\vendors_before_600_varmint_woods_20260610_052107.sql`. Rejected candidates `39321600`/`99479` and `39321601`/`99482` remain uncovered until matching `vendortemplate.ItemTemplate` evidence is found. No runtime vendor behavior changed.
- Surgery clinic and implant flows have documented repaired behavior.

# Partially Working Systems

- Inventory, corpse item loot, corpse credit loot, player trade item/credit/cancel, and vendor buy/sell/close persistence flows have passing source assertion coverage where available and completed live-client relog verification for the documented repaired paths.
- Combat works for basic weapon/NPC test scenarios, but packet semantics are not complete.
- Corpse visuals and `CorpseFullUpdate` remain areas for broader cleanup, but the corpse cash value offset is repaired and guarded by focused assertions.
- Shop/vendor database coverage is complete for practical live-accessible vendors. The remaining 26 statel coverage gaps are deferred access/setup backlog, not active capture work.
- Playfield/interior mapping has repaired fixtures and remaining audit candidates.
- Enemy spawn testing has supported low-level families, but final spawn tables are not complete.
- DB-backed mob loot is modeled and partially wired, with limited reviewed data.
- Nanos, tradeskills, teams, organizations, pets, missions, quests, perks, research, bank, bags, stacks, and containers need separate focused work.

# Known Broken Systems

- NPC chase/movement is high risk and not gameplay-ready.
- `PlayfieldAnarchyF` is documented as a current-client structure mismatch.
- Some packet classes are missing, under-modeled, or awaiting capture-backed runtime use.
- Broad static vendor coverage remains incomplete.
- Full gameplay systems for missions, quests, perks, research, pets, PvP/towers, teams, and organizations are not complete.

# Historical Development Focus

Current work is focused on gated Rex Larsson B18E completion and B18F handoff smoke. Rex dialogue, safe B18C preview, B18C kill feedback/progress, B18D preview display, exact Cargo Box identity/use routing, B18D cleanup, and safe B18E `QuestFullUpdate` emission are wired behind disabled-by-default gates. The B18E completion gate `AO_REBIRTH_ENABLE_ARETE_REX_B18E_COMPLETION` now triggers only from the captured Rex return branch after `B18EPreviewed` state, sends DTO-built `QuestMessage Action=Delete` for only `Mission:5514B18E`, grants actual `+290 XP`, grants `+1040` credits, sends captured-equivalent reward feedback text, and sends DTO-built `QuestFullUpdate` for `Mission:5514B18F` / `Talk to Marcus Stone`. Action `59`, item rewards, inventory mutation, DB mission persistence, Marcus Stone dialogue, SQL/schema changes, raw packet replay, and broader live NPC integration remain unresolved.

Update 2026-06-18: the Rex B18D cleanup pass now adds a DTO-built `QuestMessage Action=Delete` for only `Mission:5514B18D`, sourced from `20260614-194454/packets.hex.log:5765`, after exact `Terminal:56D9B4AF` use. This is treated only as captured B18D mission-window cleanup; `Quest Delete` gameplay meaning remains unresolved. Rex chain state is in-memory only and routes `B18EPreviewed` players to captured return dialogue node `rex_194454_006` so Rex does not offer B18C again in that process-local state. No rewards, inventory, XP/credits, DB persistence, B18E completion, action `59`, SQL, schema changes, or raw packet replay are implemented.

Update 2026-06-18: B18E completion is now implemented as a gated preview/handoff path only. The B18E delete DTO body matches captured packet `#5495`, and the B18F QuestFullUpdate DTO body matches captured packet `#5497`, both byte-for-byte from the N3 body onward. The handler uses in-memory per-character completion flags to prevent duplicate B18E delete, XP grant, or B18F send. Manual in-client smoke is still required to verify visible B18E removal, XP increase, B18F appearance, and client stability.

# Last Completed Milestone

Rex Larsson B18C gated live objective progress completed:

- Added disabled-by-default progress gate `AO_REBIRTH_ENABLE_ARETE_REX_B18C_PROGRESS`.
- All three Rex gates must be enabled for live objective progress: dialogue routing, B18C quest preview, and B18C progress.
- Successful safe B18C `QuestFullUpdate` preview now activates an in-memory `Mission:5514B18C` progress record for the source player only.
- The live death observer hooks `Playfield.KillNpcTarget` after the existing death animation send, where attacker, target identity, target name, and playfield are all available.
- Matching behavior is intentionally narrow: player attacker in Arete Landing, active B18C preview state for that player, target name exactly `Malfunctioning Cleaning Robot`, cap at `5/5`.
- Progress is logged server-side under `ARETE_REX_B18C_PROGRESS`; no mission-window progress packet is emitted because refresh fields are not proven.
- At `5/5`, only the in-memory objective complete flag is set. No Quest Delete, mission completion, rewards, inventory, XP/credit implementation, DB writes, B18D offer, chain progression, action `59`, or persistent mission state was added.
- Capture `20260614-194454` contains named `Malfunctioning Cleaning Robot` evidence with level `1`, HP `12/12`, `monsterData=297023`, and coordinates; representative raw references include `events.log:63`, `64`, `2719-2722`, and `3390-3408`.
- Five captured robot observations were promoted into isolated local runtime spawn data for playfield `6553`, using the minimum selected set with SimpleCharFullUpdate plus death evidence and 11 captured stat rows per robot. Local DB verification now shows Rex plus five target robots and zero unrelated `6553` rows.
- Focused ZoneEngine build passed, Rex inactive content dry-run passed, Arete validation harness passed 131 cases, and `git diff --check` passed with line-ending warnings only.

Prior Rex Larsson objective playback service completed:

- Target NPC: Rex Larsson, `SimpleChar:782DE568`.
- Target missions: `Mission:5514B18C`, `Mission:5514B18D`, and `Mission:5514B18E`.
- Temporary capture decoder `tools-temp/arete-analysis/scripts/decode_rex_questfullupdate.ps1` decodes Rex `QuestFullUpdate` packets from `packets.hex.log` using the existing x86 AOSharp capture assemblies.
- `Mission:5514B18C` title decoded as `Terminate 5 Malfunctioning Cleaning Robots`; objective decoded as `Kill 5 Malfunctining Cleaning Robots.` with captured spelling preserved.
- `Mission:5514B18D` title decoded as `Open the Cargo Box`; objective decoded as `Use (Right Click) the Cargo Box to open it.`
- `Mission:5514B18E` title decoded as `Return to Rex Larsson`; objective decoded as `Talk to Rex Larsson.`
- Rex content was updated with non-executable QuestFullUpdate evidence metadata, decoded titles/objectives, source identity linkage to `SimpleChar:782DE568`, and cautious packet-sequence links for `B18C -> B18D` and `B18D -> B18E`. `Mission:5514B18F` was observed as the next QuestFullUpdate after `B18E` delete but was not added because it is outside the target scope.
- Objective trigger metadata is now represented as non-executable evidence only: `B18C` records target name `Malfunctioning Cleaning Robot`, required count `5`, and `CharacterAction Action=Death` evidence; `B18D` records `GenericCmd Action=Use` against packet identity `Terminal:56D9B4AF`, with exact `SimpleItemFullUpdate` identity/position/rotation/stat evidence later recovered from Arete captures; `B18E` records `KnuBotOpenChatWindow` against Rex and adjacent return dialogue evidence.
- Inactive objective playback now replays stored Rex objective evidence into in-memory progress only: `B18C` reaches `5/5` with 9 matching robot death observations, `B18D` records `1/1` use interaction against packet identity `Terminal:56D9B4AF`, and `B18E` records `1/1` Rex talk observation. Later capture review found exact `SimpleItemFullUpdate` evidence for `Terminal:56D9B4AF` in Arete capture segments after the first Rex quest capture.
- `CharacterAction` action `59` remains unresolved because neither local AOtomation nor tool-side AOSharp names decimal `59` (`0x3B`), and no ZoneEngine handler maps it to offer, accept, complete, abandon, fail, or reward behavior.
- Tool-side AOSharp defines `QuestAction.Delete = 0x01`, so `Quest Delete` is packet-level delete/removal evidence. The gameplay cause remains unresolved. B18C per-kill progress mapping is partially implemented from capture, B18D uses packet identity `Terminal:56D9B4AF` with later exact full-update evidence, and B18D inventory effect plus B18E completion semantics remain unresolved.
- Focused ZoneEngine build, Rex aggregate validation/dry-run, Arete validation harness, and `git diff --check` passed. The dry-run left all three Rex missions `NotStarted`, executed 0 mission transitions, and kept objective playback separate from live character state.
- No runtime behavior, SQL, schema, live NPC wiring, packet emission, KnuBot behavior, persistence, inventory, XP, credits, rewards, or character mutation changed.

Neutral Training Startup Equipment import completed:

- Source capture: AOSharp capture `20260614-002319`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 2 vendor rows in `954 Neutral Training`, 1 vendor template, and 1 new shop inventory group with 9 inventory rows.
- Imported targets: two Basic Startup Equipment statels, vendor IDs `62521344` and `62521345`.
- Evidence note: both rows have direct VendorFull and ShopUpdate evidence from capture `20260614-002319`; both exact inventories deduplicate to shop hash `WHBW`.
- A test DB backup was created before import under `tools-temp/db-backups/neutral_training_startup_before_import_*.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 27`, and `StatelVendorExclusions = 169`.
- Total actionable uncovered statel vendors dropped from `29` to `27`.
- Current coverage/actionability chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104 -> 99 -> 96 -> 93 -> 89 -> 29 -> 27`.
- No runtime vendor behavior changed.

Prior Jobe Advanced dimensions live capture import completed:`r`n`r`n- Source capture: AOSharp capture `20260614-002319`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 3 vendor rows across `4564 Hardware Dimension - Advanced` and `4568 Dimensional Shift - Advanced`, 3 vendor templates, and 2 new shop inventory groups with 68 new inventory rows.
- Imported targets: Jobe Hardware Advanced Armor, Jobe Dimensional Advanced Regenerative Supplies, and Jobe Dimensional Advanced Implants.
- Evidence note: all three rows have direct VendorFull and ShopUpdate evidence from capture `20260614-002319`; regenerative supplies reused existing exact shop hash `HMIZ`.
- A test DB backup was created before import under `tools-temp/db-backups/jobe_advanced_dimensions_before_import_*.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 93`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `96` to `93`.
- Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104 -> 99 -> 96 -> 93`.
- No runtime vendor behavior changed.

Prior Jobe Basic dimensions live capture import completed:

- Source capture: AOSharp capture `20260614-000058`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 3 vendor rows across `4563 Hardware Dimension - Basic` and `4567 Dimensional Shift - Basic`, 3 vendor templates, and 3 new shop inventory groups with 98 inventory rows.
- Imported targets: Jobe Hardware Basic Armor, Jobe Dimensional Basic Regenerative Supplies, and Jobe Dimensional Basic Implants.
- Evidence note: all three rows have direct VendorFull and ShopUpdate evidence from capture `20260614-000058`; same-template Advanced dimensional targets were not imported without direct capture or explicit inference approval.
- A test DB backup was created before import under `tools-temp/db-backups/jobe_basic_dimensions_before_import_*.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 96`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `99` to `96`.
- Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104 -> 99 -> 96`.
- No runtime vendor behavior changed.

Prior Uncle Bazzit's Workshop live capture import completed:

- Source capture: AOSharp capture `20260613-184615`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 5 `4354 Uncle Bazzits Workshop (Dng)` vendor rows, 5 vendor templates, and 4 new shop inventory groups with 129 new inventory rows.
- Imported targets: Maria's Fashion, Uncle Bazzit's Miscellany, Uncle Bazzit's Floorplans, Uncle Bazzit's Landscaping, and Uncle Bazzit's Furnishings.
- Evidence note: all five rows have direct VendorFull and ShopUpdate evidence from capture `20260613-184615`; Maria's Fashion reused existing exact shop hash `Fash`.
- A test DB backup was created before import under `tools-temp/db-backups/bazzits_workshop_before_import_*.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 99`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `104` to `99`.
- Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104 -> 99`.
- No runtime vendor behavior changed.

Prior Treepine Hut OT Clothes live capture import completed:

- Source capture: AOSharp capture `20260613-233535`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 1 `1887 Treepine Hut` vendor row, 1 vendor template, and 1 new shop inventory group with 16 inventory rows.
- Imported target: OT Clothes template `99490`, vendor id `123666433`, statel `0xC001075F`, coordinates X `199.189` Z `286.698`.
- Evidence note: VendorFull and ShopUpdate were both captured directly. Incidental already-covered Treepine captures were intentionally not imported.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\treepine_ot_clothes_before_import_20260613-233955.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 104`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `105` to `104`.
- Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104`.
- No runtime vendor behavior changed.

Prior Omni Training Startup Shop live capture import completed:

- Source capture: AOSharp capture `20260613-231115`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 1 `950 Omni Training` vendor row, 1 vendor template, and 1 new shop inventory group with 7 inventory rows.
- Imported target: Startup Shop! template `100035`, vendor id `62259200`, statel `0xC00003B6`, coordinates X `60` Z `50`.
- Evidence note: VendorFull rows are emitted on playfield entry/dynel spawn for this target; ShopUpdate rows are emitted when the terminal is opened.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\omni_training_startup_before_import_20260613-232146.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 105`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `106` to `105`.
- Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105`.
- No runtime vendor behavior changed.

Prior Tower Shop + BS Signup live capture import completed:

- Source capture: AOSharp capture `20260613-223554`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 18 vendor rows across `4704 Tower Shop (dungeon)` and `6007 BS Signup (dng)`, 18 vendor templates, and 18 new shop inventory groups with 2047 inventory rows.
- Imported targets: 14 Tower Shop terminals plus 4 BS Signup OFAB terminals.
- Excluded from this import: Clan City Buildings, Neutral City Buildings, and Leets-R-Us were VendorFull-only / not openable; remaining BS Signup profession-locked terminals require matching professions.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\tower_bs_signup_before_import_20260613-224634.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 106`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `124` to `106`.
- Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106`.
- No runtime vendor behavior changed.

Prior Holes in the Wall live capture import completed:

- Source capture: AOSharp capture `20260613-221619`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 3 vendor rows across `791 Holes in the Wall` and `4565 Hardware Dimenion - Superior`, 2 vendor templates, and 1 new shop inventory group with 87 inventory rows.
- Imported targets: Holes in the Wall Containers, Holes in the Wall Superior Weapons, and one high-confidence exact-template inferred Hardware Dimension - Superior Superior Weapons statel.
- Reuse note: Holes in the Wall Containers exactly reused existing shop hash `Cont`; Holes in the Wall Superior Weapons created new shop hash `FZT5`.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\holes_in_wall_before_import_20260613-222653.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 124`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `127` to `124`.
- Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124`.
- No runtime vendor behavior changed.

Prior Broken Shores + Lush Fields live capture import completed:

- Source capture: AOSharp capture `20260613-215211`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 2 vendor rows across `665 Broken Shores` and `695 Lush Fields`, 2 vendor templates, and 2 new shop inventory groups with 190 inventory rows.
- Imported targets: Broken Shores OT Advanced Trade Skills and Lush Fields Basic Startup Equipment.
- Reuse note: Lush Fields Basic Startup Equipment did not reuse Newland Basic Startup Equipment because the captured treatment kit row is QL 8 instead of QL 4.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\broken_shores_lush_fields_before_import_20260613-221147.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 127`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `129` to `127`.
- Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127`.
- No runtime vendor behavior changed.

Prior Clan Basic Startup Equipment import completed:

- Source capture: AOSharp capture `20260613-211234`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 4 `540 Old Athen` vendor rows, 1 vendor template, and 1 new shop inventory group with 9 inventory rows.
- Imported targets: four Old Athen Clan Basic Startup Equipment statels for template `99569`.
- Deduplication: all four startup terminals share one deduplicated shop inventory group, `VZMO`.
- Correlation note: `0xC000021C` had captured inventory but no direct VendorFull; it was accepted as Captured by template `99569` and exact inventory match with the three VendorFull-confirmed startup terminals.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\clan_basic_startup_before_import_20260613-212759.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 129`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `133` to `129`.
- Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129`.
- No runtime vendor behavior changed.

Prior Newland + Omni startup import completed:

- Source capture: AOSharp capture `20260613-185338`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 9 vendor rows across `565 Newland Desert` and `710 Omni-1 Trade`, 6 vendor templates, and 6 new shop inventory groups with 232 inventory rows.
- Imported targets: Newland Basic Armor, Newland Basic Startup Equipment, Newland Basic Nano Clusters, Food, Drinks, and four OT Basic Startup Equipment statels.
- Deduplication: the four OT Basic Startup Equipment vendors share one deduplicated shop inventory group.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\newland_startup_before_import_20260613-204052.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 133`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `142` to `133`.
- Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133`.
- No runtime vendor behavior changed.

Prior Arete ICC implant/cluster import completed:

- Source capture: AOSharp capture `20260613-172753`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 5 `6553 Arete Landing` vendor rows, 5 vendor templates, and 5 new shop inventory groups with 573 inventory rows.
- Imported core targets: ICC Basic Implants, ICC Faded Clusters, ICC Bright Clusters, ICC Shiny Clusters, and ICC Pharmacy.
- Incidental nearby capture evidence was intentionally excluded from this import.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\arete_icc_before_import_20260613-174753.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 142`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `147` to `142`.
- Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142`.
- No runtime vendor behavior changed.

Prior Neutral ICC implant/cluster import completed:

- Source capture: AOSharp capture `20260613-170220`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 24 vendor rows across `2064 neut_basic_implants_shop` and `2073 neut_advanced_implants_shop`, 12 vendor templates, and 12 new shop inventory groups with 1876 inventory rows.
- Mapping basis: `2064` rows were captured directly; `2073` rows used high-confidence exact-template reuse from the captured `2064` template evidence, matching the existing ICC pharmacy reuse pattern across these interiors.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\neutral_icc_implants_before_import_20260613-171134.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 147`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `171` to `147`.
- Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147`.
- No runtime vendor behavior changed.

Prior overnight exact-template inferred vendor import completed:

- Source evidence: existing captured and operator-approved inferred `vendortemplate` rows with exact `TemplateId` matches and existing `shopinventorytemplates` hashes.
- Source SQL promotion added the validated staged vendor inserts to `vendors.sql` only.
- Coverage added: 31 vendor rows across Parnassos, Varmint Woods, Andromeda, Broken Shores, and Treepine Hut.
- Reused existing vendortemplate hashes: `NBBBPWA`, `CAWFVZL`, `CAXKPAK`, `CAKVRD3`, `CA4ANR3`, `CAIYRLU`, `SPPJAN4`, `CSFKCVG`, `CSSD5SY`, `CSXKWKP`, `CSZKPVY`, `CSAUZMP`, `CS5JCOM`, `OSLC3UI`, `OSRA2ZZ`, `OSGQXEO`, `OSCP3HJ`, `OSXOL6H`, `OSQC5XR`, `CBGXGWQ`, `CASMUGY`, `CS3Q3IF`, `OBIUAFT`, `OAX2G2O`, `OST6OJS`, `OAE5BNV`, `OAW76SU`, `NBCQ762`, `CBIGA24`, and `OAL6IVC`.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\overnight_exact_template_before_import_20260613-051359.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 171`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `202` to `171`.
- Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171`.
- Template `155225` remains excluded as a non-shop statel template.
- No runtime vendor behavior changed.

Prior Clan Advanced General Shop import completed:

- Source capture: AOSharp capture `20260613-034740`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 16 `1181 ord_smarket_clan_advanced` vendor rows, 16 vendor templates, and 11 new shop inventory groups with 505 inventory rows.
- Reused shop hashes: `Cont`, `IVM2`, `IYD4`, `JTYS`, and `LJI7`.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\cellao_codex_clean_clan_advanced_20260613-035810.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 202`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `218` to `202`.
- Current live-capture coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202`.
- Spot checks passed for `ClanAdvancedWeapons`, `ClanAdvancedDevices`, and `AdvancedRangedWeaponComponents`.
- Template `155225` remains excluded as a non-shop statel template.
- No runtime vendor behavior changed.

Prior spec_smarket specialty import (inferred) completed:

- Source inference: operator-approved reuse of Neutral Basic/Specialty captures `20260613-012810` and `20260613-014033`.
- Source SQL promotion added the validated inferred staged inserts to `vendortemplate.sql` and `vendors.sql`; `shopinventorytemplates.sql` was unchanged because all inventories reused existing shop hashes.
- Coverage added: 16 vendor rows across `1189 spec_smarket_clan_advanced`, `1190 spec_smarket_clan_sup`, `1191 spec_smarket_omni_advanced`, and `1192 spec_smarket_omni_sup`, plus 12 vendor templates.
- Reused shop hashes: `I3E4`, `7ATH`, `7X7Q`, `PX4X`, `FBQ3`, and `FLEW`.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\spec_smarket_before_import_20260613-033215.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 218`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `234` to `218`.
- Current live-capture coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218`.
- Spot checks passed for `ClanComputers`, `OTComputers`, and `ClanSpecialistCommerce`.
- Template `155225` remains excluded as a non-shop statel template.
- No runtime vendor behavior changed.

Prior Neutral Basic General/Specialty Shop import completed:

- Source captures: AOSharp captures `20260613-012810` and `20260613-014033`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 6 `1193 spec_smarket_neut_basic` vendor rows, 6 vendor templates, and 6 new shop inventory groups with 64 inventory rows.
- Specialist Commerce required Trader access and was captured in the second AOSharp session.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\neutral_basic_before_import_20260613-014923.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 234`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `240` to `234`.
- Current live-capture coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234`.
- Spot checks passed for `NeutralBasicComputers`, `NeutralBasicSpecialistCommerce`, and `NeutralBasicSuperiorCars`.
- Template `155225` remains excluded as a non-shop statel template.
- No runtime vendor behavior changed.

Prior Omni Basic Implant Terminals import completed:

- Source capture: AOSharp capture `20260613-005616`.
- Source SQL promotion added the validated staged inserts to `vendortemplate.sql` and `vendors.sql`; `shopinventorytemplates.sql` was unchanged because all implant inventories reused existing shop hashes.
- Coverage added: 13 `1183 ord_smarket_omni_basic` implant vendor rows and 13 vendor templates, with existing implant shop hashes `5BUX`, `5M5F`, `6MQN`, `6YPW`, `7LZ3`, `A32J`, `JWHR`, `KV75`, `KVVT`, `O3KI`, `RNWW`, `RO4Q`, and `SBQ6` reused.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\omni_basic_implants_before_import_20260613-011140.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 240`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `253` to `240`.
- Current live-capture coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240`.
- Spot checks passed for `BasicOmniTekAdventurerImplants`, `BasicOmniTekMetaPhysicistImplants`, and `BasicOmniTekKeeperImplants`.
- Template `155225` remains excluded as a non-shop statel template.
- No runtime vendor behavior changed.

Prior Omni Advanced General Shop import completed:

- Source capture: AOSharp capture `20260613-002828`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 23 `1184 ord_smarket_omni_advanced` vendor rows, 16 vendor templates, and 15 new shop inventory groups with 760 inventory rows, while reusing existing shop hash `LJI7`.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\omni_advanced_before_import_20260613-004623.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 253`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `276` to `253`.
- Current live-capture coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253`.
- Spot checks passed for `OTAdvancedArmor`, `OTAdvancedWeapons`, and `AdvancedImplants`.
- Template `155225` remains excluded as a non-shop statel template.
- No runtime vendor behavior changed.

Prior Clan Superior General Shop import completed:

- Source capture: AOSharp capture `20260612-232439`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 19 `1182 ord_smarket_clan_sup` vendor rows, 19 vendor templates, and 14 new shop inventory groups with 594 inventory rows, while reusing existing shop hashes `LJI7`, `CHHQ`, `OHOO`, `JYPE`, and `Cont`.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\clan_superior_before_import_20260613-000803.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 276`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `295` to `276`.
- Current live-capture coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276`.
- Spot checks passed for `ClanSuperiorArmor`, `ClanSuperiorWeapons`, and `ClanSuperiorContainers`.
- Template `155225` remains excluded as a non-shop statel template.
- No runtime vendor behavior changed.

Prior Clan Basic General Shop import completed:

- Source capture: AOSharp capture `20260612-225855`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 29 `1180 ord_smarket_clan_basic` vendor rows, 29 vendor templates, and 25 new shop inventory groups with 1575 inventory rows, while reusing existing shop hashes `G4XZ`, `HYDQ`, `LJI7`, and `R5R7`.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\clan_basic_before_import_20260612-231024.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 295`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `324` to `295`.
- Current live-capture coverage chain: `404 -> 381 -> 351 -> 324 -> 295`.
- Spot checks passed for `ClanBasicArmor`, `ClanBasicWeapons`, `BasicClanAdventurerImplants`, and `BasicImplants`.
- Template `155225` remains excluded as a non-shop statel template.
- No runtime vendor behavior changed.

Prior Omni Superior General Shop import completed:

- Source capture: AOSharp capture `20260612-044234`.
- Source SQL promotion added the validated v2 staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 27 `1185 ord_smarket_omni_sup` vendor rows, 20 vendor templates, and 19 new shop inventory groups, with existing map shop hash `LJI7` reused.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\omni_superior_v2_before_import_20260612-220448.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 324`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `351` to `324`.
- Current live-capture coverage chain: `404 -> 381 -> 351 -> 324`.
- Spot checks passed for `OTSuperiorArmor`, `OTSuperiorWeapons`, and `SuperiorImplants`.
- Template `155225` remains excluded as a non-shop statel template.
- No runtime vendor behavior changed.

Prior coverage exclusion for non-shop `Refreshing Drink` statels completed:

- Excluded template: `155225`.
- Exclusion reason: `NonShopStatelTemplate`.
- Evidence: AOSharp captures `20260612-012644` and `20260612-044234` emitted VendorFullUpdate rows but no ShopUpdate inventory rows for these identities, and live operator verification found the Superior instances were not reachable/openable.
- Implementation: current-client verification keeps excluded rows in `statel-vendor-coverage.csv` with `CoverageExcluded` and `ExclusionReason`, but excludes them from coverage metrics, missing-vendor reports, `vendor-scan-targets.csv`, capture targeting, and import planning.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 351`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `381` to `351`.
- No SQL, vendor mappings, imports, or runtime vendor behavior changed.

Prior Omni Basic General Shop import completed:

- Source capture: AOSharp capture `20260612-012644`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 23 `1183 ord_smarket_omni_basic` vendor rows, 16 vendor templates, and 16 shop inventory groups with 690 inventory rows.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\omni_basic_before_staged_import_20260612-032350.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 381`.
- Total uncovered statel vendors dropped from `404` to `381`.
- `1183 ord_smarket_omni_basic` uncovered count dropped from `39` to `16`.
- Spot checks passed for `OTBasicArmor`, `OTBasicWeapons`, and `BasicImplants`.
- No runtime vendor behavior changed.

Prior `600 Varmint Woods` vendor coverage expansion completed:

- Commit `e197b9f` added the 1 approved source SQL mapping.
- A targeted import inserted only that row into `cellao_codex_clean.vendors`.
- Query-back confirmed `39321612 | 600 | 93063 | AdvOA`.
- A `vendors` table backup was created before import: `C:\Users\Mike\Documents\Cellao-Clean\tools-temp\db-backups\vendors_before_600_varmint_woods_20260610_052107.sql`.
- Total uncovered statel vendors dropped from `571` to `570`.
- `600 Varmint Woods` uncovered count dropped from `3` to `2`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 570`.
- Rejected candidates `39321600`/`99479` and `39321601`/`99482` remain uncovered because no matching `vendortemplate.ItemTemplate` evidence exists.
- No runtime vendor behavior changed.

Prior `2070 neut_advanced_weapons_shop` vendor coverage expansion completed:

- Commit `9c41ed9` added the 1 approved source SQL mapping.
- A targeted import inserted only that row into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import: `C:\Users\Mike\Documents\Cellao-Clean\tools-temp\db-backups\vendors_before_2070_neut_advanced_weapons_shop_20260610_040826.sql`.
- Total uncovered statel vendors dropped from `572` to `571`.
- `2070 neut_advanced_weapons_shop` uncovered count dropped from `5` to `4`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 571`.
- Rejected candidates `135659521`/`297466`, `135659522`/`297470`, `135659523`/`99572`, and `135659524`/`99573` remain uncovered because no matching `vendortemplate` evidence exists.
- No runtime vendor behavior changed.

Prior `2060 neut_basic_weapon_shop` vendor coverage expansion completed:

- Commit `83fc74f` added the 1 approved source SQL mapping.
- A targeted import inserted only that row into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `573` to `572`.
- `2060 neut_basic_weapon_shop` uncovered count dropped from `5` to `4`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 572`.
- No runtime vendor behavior changed.

Prior `4565 Hardware Dimension - Superior` vendor coverage expansion completed:

- Commit `1810408` added the 2 approved source SQL mappings.
- A targeted import inserted only those 2 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `575` to `573`.
- `4565 Hardware Dimension - Superior` uncovered count dropped from `5` to `3`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 573`.
- No runtime vendor behavior changed.

Prior `4564 Hardware Dimension - Advanced` vendor coverage expansion completed:

- Commit `aa62dcd` added the 2 approved source SQL mappings.
- A targeted import inserted only those 2 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `577` to `575`.
- `4564 Hardware Dimension - Advanced` uncovered count dropped from `4` to `2`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 575`.
- No runtime vendor behavior changed.

Prior `6553 Arete Landing` vendor coverage expansion completed:

- Commit `389e8b3` added the 2 approved source SQL mappings.
- A targeted import inserted only those 2 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `579` to `577`.
- `6553 Arete Landing` uncovered count dropped from `8` to `6`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 577`.
- No runtime vendor behavior changed.

Prior `4563 Hardware Dimension - Basic` vendor coverage expansion completed:

- Commit `0ded4a9` added the 2 approved source SQL mappings.
- A targeted import inserted only those 2 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `581` to `579`.
- `4563 Hardware Dimension - Basic` uncovered count dropped from `4` to `2`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 579`.
- No runtime vendor behavior changed.

Prior `4569 Dimensional Shift - Superior` vendor coverage expansion completed:

- Commit `abee0ce` added the 3 approved source SQL mappings.
- A targeted import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `584` to `581`.
- `4569 Dimensional Shift - Superior` uncovered count dropped from `5` to `2`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 581`.
- No runtime vendor behavior changed.

Prior `4568 Dimensional Shift - Advanced` vendor coverage expansion completed:

- Commit `5e5303b` added the 3 approved source SQL mappings.
- A targeted import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `587` to `584`.
- `4568 Dimensional Shift - Advanced` uncovered count dropped from `5` to `2`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 584`.
- No runtime vendor behavior changed.

Prior `4567 Dimensional Shift - Basic` vendor coverage expansion completed:

- Commit `7c10b5a` added the 3 approved source SQL mappings.
- A targeted import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `590` to `587`.
- `4567 Dimensional Shift - Basic` uncovered count dropped from `5` to `2`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 587`.
- No runtime vendor behavior changed.

Prior `2096 4holes Fashion` vendor coverage expansion completed:

- Commit `0522ffb` added the 3 approved source SQL mappings.
- A targeted import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `593` to `590`.
- `2096 4holes Fashion` uncovered count dropped from `7` to `4`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 590`.
- No runtime vendor behavior changed.

Prior `565 Newland Desert` vendor coverage expansion completed:

- Commit `2bb7ad5` added the 3 approved source SQL mappings.
- A targeted import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `596` to `593`.
- `565 Newland Desert` uncovered count dropped from `9` to `6`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 593`.
- No runtime vendor behavior changed.

Prior `2073 neut_advanced_implants_shop` vendor coverage expansion completed:

- Commit `a79b5ec` added the 3 approved source SQL mappings.
- A targeted import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `599` to `596`.
- `2073 neut_advanced_implants_shop` uncovered count dropped from `15` to `12`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 596`.
- No runtime vendor behavior changed.

Prior `2064 neut_basic_implants_shop` vendor coverage expansion completed:

- Commit `ed869d5` added the 3 approved source SQL mappings.
- A targeted import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `602` to `599`.
- `2064 neut_basic_implants_shop` uncovered count dropped from `15` to `12`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 599`.
- No runtime vendor behavior changed.

Prior `1181 ord_smarket_clan_advanced` vendor coverage expansion completed:

- Commit `fbcc1a4` added the 4 approved source SQL mappings.
- A targeted import inserted only those 4 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `606` to `602`.
- `1181 ord_smarket_clan_advanced` uncovered count dropped from `30` to `26`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 602`.
- No runtime vendor behavior changed.

Prior `1180 ord_smarket_clan_basic` vendor coverage expansion completed:

- Commit `b6a6410` added the 4 approved source SQL mappings.
- A targeted import inserted only those 4 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `610` to `606`.
- `1180 ord_smarket_clan_basic` uncovered count dropped from `43` to `39`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Prior `655 Andromeda` vendor coverage expansion completed:

- Commit `9217459` added the 16 approved source SQL mappings.
- A targeted import inserted only those 16 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `626` to `610`.
- `655 Andromeda` uncovered count dropped from `17` to `1`.
- Template `151987` remains unknown and was not mapped.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Prior `1182 ord_smarket_clan_sup` vendor coverage expansion completed:

- Commit `d7556bb` added the 17 approved source SQL mappings.
- The 17 approved rows are present in `cellao_codex_clean.vendors`.
- The latest import run did not insert duplicates because all 17 approved IDs already existed.
- A `vendors` table backup was created before the verification/import attempt.
- Total uncovered statel vendors dropped from `643` to `626`.
- `1182 ord_smarket_clan_sup` uncovered count dropped from `44` to `27`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Prior `500 Parnassos` vendor coverage expansion completed:

- Commit `d47f12e` added the 25 approved source SQL mappings.
- A targeted import inserted only those 25 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `668` to `643`.
- `500 Parnassos` uncovered count dropped from `140` to `115`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Prior `1185 ord_smarket_omni_sup` vendor coverage expansion completed:

- Commit `e755c25` added the 21 approved source SQL mappings.
- A targeted import inserted only those 21 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `689` to `668`.
- `1185 ord_smarket_omni_sup` uncovered count dropped from `68` to `47`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Prior `1184 ord_smarket_omni_advanced` vendor coverage expansion completed:

- Commit `aa8da43` added the 21 approved source SQL mappings.
- A targeted import inserted only those 21 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `710` to `689`.
- `1184 ord_smarket_omni_advanced` uncovered count dropped from `68` to `47`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Prior `1183 ord_smarket_omni_basic` vendor coverage expansion completed:

- Commit `6dfb390` added the 20 approved source SQL mappings.
- A targeted import inserted only those 20 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `730` to `710`.
- `1183 ord_smarket_omni_basic` uncovered count dropped from `77` to `57`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Smoke harness cleanup passed after stale assertions were aligned with current repaired behavior:

- `Run-CombatSmokeTests.ps1 -SkipBuild` passes.
- `Run-CorpseCreditTraceAssertions.ps1` passes.
- `Run-InventoryContainerRegressionAssertions.ps1` passes.
- Stale assertions for cash stat serialization, NPC/shop cash mutation, login-time debug enemy spawning, and corpse credit feedback were cleaned up.
- No gameplay behavior was changed by the harness cleanup.

Inventory Move Live Verification result: PASS. The item moved correctly before relog and remained in the correct slot after relog.

Equip Item Live Verification result: PASS. The item equipped correctly before relog, no duplicate remained in inventory, and the item remained equipped in the correct equipment slot after relog.

Unequip Item Live Verification result: PASS. The item moved from equipment slot to inventory correctly, the equipment slot became empty, no duplicate remained equipped, and after relog the item remained in inventory while the equipment slot stayed empty.

Corpse Item Loot Live Verification result: PASS. Non-credit corpse item appeared in inventory correctly, the corpse no longer offered the looted item, no duplicate item appeared, cash did not change from item loot, and the item remained in inventory after relog.

Corpse Credit Loot Live Verification result: PASS. One correct corpse credit message displayed, cash increased by exactly the awarded amount, no inventory item was created from credit loot, increased cash value persisted after relog, and no duplicate corpse credit feedback was observed.

Player Trade Item Live Verification result: PASS. Item left player A inventory correctly, appeared in player B inventory correctly, no duplicate item existed, cash remained unchanged, and after relog the item remained only with player B.

Player Trade Credits Live Verification result: PASS. Player A cash decreased by the expected amount, player B cash increased by the expected amount, no inventory items moved, appeared, or disappeared, cash values persisted after relog, and no duplicate cash behavior was observed.

Player Trade Cancel/Decline Live Verification result: PASS. Trade panes closed correctly, the offered item remained with the original player, cash remained unchanged, no duplicate item or cash behavior occurred, and state persisted correctly after relog.

Vendor Buy Live Verification result: PASS. Purchased item appeared in inventory correctly, cash decreased by the exact purchase price, no duplicate item appeared, and after relog the purchased item and reduced cash value both persisted.

Vendor Sell Live Verification result: PASS. Sold item left inventory correctly, cash increased by the exact sale price, no duplicate item appeared, and after relog the sold item remained absent and increased cash value persisted.

Vendor Close/Cancel Live Verification result: PASS. Pending vendor transaction state closed without accepting, cash stayed unchanged, items remained with their original owner/location, no duplicate item appeared, and the same item/cash state persisted after relog.

Live Persistence Verification complete. Inventory move, equip item, unequip item, corpse item loot, corpse credit loot, player trade item, player trade credits, player trade cancel/decline, vendor buy, vendor sell, and vendor close/cancel all matched expected client-visible behavior and survived relog.

Player-to-player trade verification passed after temporary `TRADE_*` trace logging was added in commit `4b68d4e`. Verification showed:

- Credit-only trade behaved as expected.
- Item-only trade behaved as expected.
- Mixed item-plus-credit trade behaved as expected.
- Cancel/decline trade behaved as expected.
- No player trade display or commit defect was reproduced.
- Temporary `TRADE_*` logging remains available for future trade investigation.

Prior corpse credit repairs were pushed to `origin/master` in commits `343a31d` and `e953c76` after verification showed:

- `CorpseFullUpdate` cash stat id remains at offset `203`.
- Corpse cash value is patched at offset `207`.
- The old hardcoded `111` cash value is not preserved.
- Delayed corpse credit award mutates cash once and sends the normal changed-stat packet.
- Manual server `ChatText` corpse credit feedback is suppressed so the client displays one corrected message.
- Cliff Malle playtest displayed one `You received 3 credits from the corpse.` message.

Prior ICC/Fair Trade vendor stock repairs were pushed to `origin/master` in commit `cffc5da` after verification showed:

- vendor DB issues: 0
- shop inventory item-cache issues: 0
- tradeskill room captured rows: 3,101
- tradeskill vendor rows: 38

# Vendor Coverage Deferred Backlog

Status: COMPLETE (LIVE COVERAGE).

Vendor coverage campaign complete for all practical live-accessible vendors. Remaining vendors require setup-specific access and are deferred.

Final state:

- Current uncovered count: 26.
- Covered: all practical live-accessible vendors reached during the campaign.
- Deferred: access-restricted, setup-specific, profession-locked, sided, special-location, or current-client divergence vendors.
- No SQL was generated for this freeze.
- No capture was run for this freeze.
- Existing mappings were not modified.

| Category | Playfield | Name | VendorId | TemplateId | Reason blocked | Required setup |
| --- | ---: | --- | ---: | ---: | --- | --- |
| Clan-only vendors | 665 | Broken Shores | 43581441 | 99522 | Clan-side shop access friction; not practical from current Omni-focused sweep. | Leveled/access-capable Clan character. |
| Clan-only vendors | 952 | Clan Training | 62390272 | 100034 | Clan starter/training access requires a Clan character in the correct area. | Clan character with access to Clan Training. |
| Clan-only vendors | 1426 | Clan Registration dng | 93454336 | 25885 | Clan registration interior; outside current Omni/non-swap scope. | Clan character and registration interior access. |
| Clan-only vendors | 1426 | Clan Registration dng | 93454337 | 81799 | Clan registration interior; outside current Omni/non-swap scope. | Clan character and registration interior access. |
| Clan-only vendors | 7012 | Freelancers Inc. HQ - Old Athen | 459538432 | 284692 | Old Athen/Clan-side Freelancers access requires separate Clan setup. | Clan character able to reach Old Athen Freelancers HQ. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674752 | 266562 | OFAB terminal is profession-locked. | Adventurer character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674753 | 266563 | OFAB terminal is profession-locked. | Agent character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674754 | 266569 | OFAB terminal is profession-locked. | Bureaucrat character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674755 | 266564 | OFAB terminal is profession-locked. | Doctor character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674756 | 266565 | OFAB terminal is profession-locked. | Enforcer character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674757 | 266566 | OFAB terminal is profession-locked. | Engineer character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674758 | 266567 | OFAB terminal is profession-locked. | Fixer character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674759 | 266568 | OFAB terminal is profession-locked. | Keeper character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674760 | 266570 | OFAB terminal is profession-locked. | Martial Artist character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674762 | 266572 | OFAB terminal is profession-locked. | Nano-Technician character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674763 | 266574 | OFAB terminal is profession-locked. | Shade character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674764 | 266573 | OFAB terminal is profession-locked. | Soldier character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674765 | 266575 | OFAB terminal is profession-locked. | Trader character with BS Signup access. |
| Tower Shop sided/org-dependent | 4704 | Tower Shop (dungeon) | 308281349 | 249724 | Terminal did not open during live attempts. | Unknown; revisit only with explicit tower-shop access investigation. |
| Tower Shop sided/org-dependent | 4704 | Tower Shop (dungeon) | 308281366 | 295890 | Sided city-building terminal; not openable from current character side. | Clan-side or appropriate city-building access setup. |
| Tower Shop sided/org-dependent | 4704 | Tower Shop (dungeon) | 308281368 | 295892 | Sided city-building terminal; not openable from current character side. | Neutral-side or appropriate city-building access setup. |
| ICC Holodeck / Arete divergence | 6131 | ICC Holodeck Alien Training | 401801216 | 287476 | Current-client Arete/Holodeck access diverges from legacy AO Rebirth playfield assumptions. | Separate current-client source-data investigation, not normal vendor capture. |
| Unicorn / registration / special terminals | 1427 | Omni registration dng | 93519872 | 81799 | Special registration interior; not part of practical shop sweep. | Registration-interior access investigation. |
| Unicorn / registration / special terminals | 1428 | Neutral organisation dng | 93585408 | 81799 | Special registration interior; access unknown. | Neutral organisation registration access investigation. |
| Unicorn / registration / special terminals | 4364 | Unicorn Outpost | 285999104 | 256457 | Special outpost terminal; access/route not confirmed during campaign. | Unicorn Outpost access and terminal capture plan. |
| Unicorn / registration / special terminals | 4364 | Unicorn Outpost | 285999105 | 287037 | Special outpost terminal; access/route not confirmed during campaign. | Unicorn Outpost access and terminal capture plan. |

# Next Milestone

Move to the next AO Rebirth system. Do not continue vendor capture/import work unless Mike intentionally reopens the deferred access backlog with the required character, profession, side, or special-location setup. Keep NPC movement out of scope unless explicitly selected later.
