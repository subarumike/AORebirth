# Generic Ordinary-Enemy Runtime

Population ownership: profile-backed Subway rows are activated and respawned by `WorldPopulationController`. `OrdinaryEnemyRuntimeService` materializes requested rows only and does not enumerate population or own respawn timers. The catalog remains the capture-backed adapter with 321 rows, 310 active, and 11 quarantined.

## Decision

An ordinary enemy is an `OrdinaryEnemyProfile` plus one or more
`OrdinaryEnemySpawnDefinition` rows. Ordinary enemies do not receive their own
spawn class, AI class, combat loop, corpse handler, or respawn handler.

Custom C# encounter modules are reserved for named bosses, owned summons, and
scripted mechanics that cannot be represented by the validated profile model.
The ordinary catalog rejects those categories rather than silently treating
them as normal enemies.

## Normalized data model

`OrdinaryEnemyProfile` contains stable type data:

- stable profile and family keys;
- display name and `MonsterData`;
- template-backed or captured-direct construction;
- appearance, SCFU variant, texture, and mesh data;
- aggression, chase, and return policy;
- combat mode, damage source, visible-weapon policy, and captured combat contract;
- loot and credit evidence;
- corpse packet and lifetime policy;
- evidence references and explicit boss/summon exclusion flags.

`OrdinaryEnemySpawnDefinition` contains placed-instance data:

- stable spawn key and captured source identity;
- profile key and playfield;
- captured source level, health, health damage, scale, and run speed;
- one non-null generic level definition (`Fixed` or `InclusiveRange`), its evidence status, and its generation reroll policy;
- exact position and orientation;
- static, patrol, or captured-route movement data;
- exact captured SCFU overrides when present;
- explicit respawn assignment (`Inherit`, `Explicit`, or `NoRespawn`) plus evidence;
- active or quarantined runtime disposition;
- capture, timestamp, and owner provenance.

Unknown combat, loot, credit, movement, and exact official respawn timing remains explicit.
It is not converted into a zero, false, guaranteed drop, guessed level range, or
working combat contract. Eligible ordinary rows without an explicit respawn exception inherit the documented PF127 project policy. The validator rejects duplicate profile keys, spawn
keys, source identities, missing profile references, invalid controlled values,
owned spawns, and scripted/boss rows.

## Controlled behavior values

- Aggression: `Passive`, `Retaliate`, `Auto`, `Scripted`, or explicit `Unresolved`.
- Movement: `Static`, `Patrol`, `Roam`, `Scripted`, or explicit `Unresolved`.
- Combat: unarmed melee, natural melee, equipped melee, equipped ranged, nano,
  hybrid, scripted, or explicit unresolved.
- Damage: captured fixed, weapon roll, profile range, natural attack, scripted,
  or explicit unresolved.
- Loot evidence: guaranteed proven, observed available, profile inherited,
  none proven, or unresolved.
- Level: captured fixed value or an inclusive evidence-backed/policy range.
- Level reroll: never for fixed rows; once per new population generation for a
  range that explicitly selects `NewPopulationGeneration`.
- Respawn: inherit the ordinary default, use an explicit policy, explicitly do
  not respawn, or fail closed as unresolved.

Scripted modes are modeled so imports can classify them, but the ordinary
runtime validator rejects them and directs them to a custom encounter module.
Random roaming is not guessed. A `Roam` row requires captured waypoints and uses
the shared waypoint movement path until stronger behavior evidence exists.

## Runtime ownership

`WorldPopulationController` is the PF127 ordinary population owner. It selects
enabled rows, resolves their effective respawn policy, creates generation
numbers, delegates materialization, and schedules respawns through the single
`WorldRespawnScheduler`.

`OrdinaryEnemyRuntimeService` is the only PF127 ordinary materializer. It:

1. receives one population generation and selects its level exactly once through an injected selector;
2. reuses that immutable selection for retries of the same generation;
3. constructs a template-backed or captured-direct `Character`;
4. applies the selected level, health, health damage, scale, and run speed before combat preparation;
5. applies profile appearance and movement data and prepares the combat contract;
6. stores the generation and selected variant in the normalized runtime definition;
7. preserves direct-spawn packet order: SCFU, then visible weapon definitions;
8. delegates visibility replay to the existing visibility services;
9. prevents duplicate source registration and removes runtime state during final despawn.

Visibility loss/re-entry, combat reset, corpse transitions, route recalculation,
and ordinary runtime ticks do not call the selector. A ranged row rerolls only
when `WorldPopulationController` requests a new generation. Fixed rows remain
fixed after respawn.

Existing services retain their established responsibilities:

- `NPCRuntimeService`: lifecycle coordination, target acquisition, combat, death,
  cleanup, and ordinary-runtime delegation.
- `CapturedEnemyCombatRuntime` and `NpcCombatTickCoordinator`: weapon setup,
  attack timing, attack packets, and damage-source selection.
- `NPCController` and `PlayfieldNpcCombatMovementRuntimeService`: patrol, chase,
  follow, and return movement.
- `NpcCorpseLifecycleCoordinator`, playfield corpse access, and timed lifecycle
  services: death, corpse materialization, loot access, and despawn.
- visibility packet and fanout services: SCFU, weapon definitions, `CharInPlay`,
  and client delivery.

No packet layout or global visibility batching/fanout behavior is changed.

## Audited implementation map

| Classification | Current owner |
| --- | --- |
| Generic data | `OrdinaryEnemyProfile`, `OrdinaryEnemySpawnDefinition`, `OrdinaryEnemyCatalog` |
| Generic runtime | `OrdinaryEnemyRuntimeService`, `NPCRuntimeService`, `NpcCombatTickCoordinator`, `NPCController` |
| Family/enemy data | captured supported and generated ordinary providers, adapted into normalized profiles |
| Family/enemy runtime | retired for ordinary Subway enemies |
| Boss/scripted runtime | separate content/encounter modules only; no Subway boss is routed through the ordinary catalog |
| Visibility infrastructure | existing playfield visibility sequencing/fanout and PF127 diagnostics |
| Capture import | `generate_subway_ordinary_content.py` |
| Tests | AOTomation messaging lifecycle/profile tests |

The audit found duplicate spawn construction, stat/appearance application,
movement setup, runtime registration, packet announcement, and respawn ownership
in `CapturedSubwaySpawnOrchestrator` and
`CapturedSubwayOrdinarySpawnOrchestrator`. Both runtime files are removed. The
captured providers remain evidence/data inputs; packet serializers and lifecycle
services remain their established owners.

## Subway migration

The catalog normalizes all existing supported-family and generated ordinary
Subway evidence into 26 reusable type profiles and 321 exact spawn rows:

- supported profiles: Filth Flea, Discarded Pet, Disobedient Bot, Mugger, Thief,
  and Violent Vagabond;
- generated ordinary profiles: Slum Runner, Molested Molecules, Shadow,
  Infector, Architect Striker, Melded Patterns, Workman Striker, Looter,
  Deranged Shopper, Bloodcreeper, Stim Fiend, Neural Burnout, Incomplete
  Rebuild, Fragmented Soul, Redundant Scan, Uncontrollable Anger, Infected
  Attendant, Lost Thought, Empty Shell, and Premature Pattern.

The configured activation boundary is 310 active rows. The 16 accepted-profile
rows activated in this slice require bounded private-client validation before
this can be called the safe runtime boundary. The remaining 11 PF127 diagnostic
rows are Violent Vagabonds still missing capture-proven local-player damage and
reset boundaries. Profile or spawn existence alone does not enable a row.

Named bosses and owned summons are not in the catalog.

## Thief parity

Thief now uses the shared profile/runtime path while preserving the accepted
captured values: source identity `0x7953AEA5`, template `A051`, level 5, maximum
health 146 with captured current health 115, scale 93, run speed 20, exact
position, captured appearance/SCFU bytes, retaliate aggression, captured patrol
replay, QL1 Solar-Powered Pistol `121567` in the right hand, weapon-derived
damage, captured attack timing/context, captured corpse packet/CATMesh,
guaranteed QL1 Stolen Handbag `297055`, three-second fully-looted cleanup,
four-minute unlooted lifetime across close/reopen, and 60-second post-despawn
respawn.

Finalized capture `20260717-012651` proves the 146 maximum independently: the
Thief recovered from 115 to 146 at one health per second, then a 96-point hit
left exactly 50 health. The Thief profile therefore owns a one-point,
one-second passive recovery interval that remains active during combat. The
observed corpse disappearance after closing with loot is not promoted as an
identity-specific rule: all normal enemies retain loot-bearing corpses for four
minutes, and closing or reopening the loot window does not shorten that timer.

## Filth Flea parity

Filth Flea uses the same runtime with template `A096`, per-spawn level/health/
position/run-speed rows, retaliate aggression, captured patrol replay where
present, the captured opening poison and repeating natural-melee sequence, exact
SCFU material override, exact corpse packet, observed item loot, observed credit
range `29..79`, and 240-second post-despawn respawn.

The combat tick no longer identifies Filth Flea by name or `MonsterData`.
Opening and repeating special attacks are generic captured-contract data.

## Bloodcreeper level policy

Bloodcreeper is the only current ordinary row with an inclusive level
definition. Catalog data configures `L15..L25`, rerolled once per new population
generation through the same selector used by every ordinary row. Captured
`L24/691 HP/run 83` and `L25/724 HP/run 86` anchor the existing private derived
progression. The level 15-23 values remain documented policy, not capture claims.
All other 320 rows remain explicit fixed-level definitions until evidence or an
approved design decision establishes a range; the shared mechanism does not
guess one.

## Ordinary loot evidence boundary

The canonical evidence record for the current Bloodcreeper and Disobedient Bot
loot slice is
`docs/evidence/SUBWAY_BLOODCREEPER_DISOBEDIENT_BOT_LOOT_AUDIT.md`. Corpse joins
canonicalize padded and unpadded numeric identities, remain generation-scoped,
and exclude names, proximity, database identity alone, or duplicate observations
as substitutes for an exact identity chain.

Disobedient Bot has fifteen exact corpse generations in the audited corpus and
eight strict complete loot outcomes. Those strict outcomes contain one QL1 Small
Power Supply (`234877/234877`), one QL10 Eye Implant: Pharma Tech, Bright
(`104683/104684`), one QL7 `113398/113399` item, and five item-empty inventories.
The runtime uses a provisional weighted-one policy with relative weights
`1 + 1 + 1 + 5 empty`. The three memberships are capture-proven; the weighting
is private-server policy, not an
official probability claim, and the broader pool remains incomplete. Burnt Out
Memory Chip (`234876/234876`) cannot roll because its corpse linkage is
incomplete.

Bloodcreeper has four reviewed complete first opens: one positive and three
empty. The positive generation proves QL30 item `42640/42641`; runtime replays
that observed `1/4` entry independently while preserving
`ItemPoolComplete=false`. Its proven 150-credit behavior remains independent of
the incomplete item-pool boundary.

The restored deep-population slice uses strict initial corpse snapshots, including
empty snapshots, when calculating observed item frequencies. It does not infer
guaranteed loot from a successful roll. The catalog now contains 321 represented
rows, 310 active rows, and 11 quarantined rows.

Slum Runner now has 21 identity-linked corpse generations: seven focused
records from `20260716-034656` and `20260716-215947`, plus fourteen recovered
deep-corpus records. They use CATMesh `31774`; exact credit rules cover observed
levels 11, 12, 15, 16, 17, 18, 20, 21, 22, and 23. Every active Slum Runner
level now has an exact rule, while other levels remain unresolved.
Its 24 exact spawns, captured `5..11` normal damage and `4.210098`-second
cadence, shared chase, strict loot sample, ordinary corpse lifetimes, and
`59.433`-second observed death-to-respawn interval now pass the whole-enemy
acceptance gate. Loot replay remains `ObservedSamples`; no official item
probability distribution is claimed.

Molested Molecules is the fourth accepted ordinary profile. Its nine exact
spawns cover captured levels 17 through 24. Twenty normal local-player hits
prove `16..42` damage with slot `6` and `4.749995`-second cadence. Three strict
complete inventories prove four observed `1/3` item memberships and one empty
outcome; seven positive-credit corpses preserve CATMesh `5921` and exact
captured level-credit rules. Shared chase and ordinary three-second empty/four-
minute loot-bearing corpse behavior apply. Its four-minute respawn remains the
centralized private PF127 ordinary policy, not an official-live timing claim.

Disobedient Bot is the fifth accepted ordinary profile. Its 12 exact spawn rows
use captured NPC family `138`. Fifteen normal SIW1 hits against local players
prove the aggregate `6..15` envelope, with ten captured misses kept explicit;
three other-player hits and two
player-owned Killer-pet hits remain separate, no critical is observed, and
focused attack attempts retain the exact
`5.973723`-second recharge instead of a miss-biased landed-hit interval. The
ordinary combat profile resolves SIW1 context from the selected spawn level:
captured `L5=30/30/30/30/22`, `L6=35`, `L8=45`, `L9=49`, and `L10=54`; L7 uses
the explicit bounded midpoint policy `40`, while other levels fail closed. The
generated combat projection contains nine decoded plus five reviewed-raw local
hits at `8..15`, three other-player hits at `8`, and two player-owned Killer-pet
hits at `8` and `19`.

Thirteen valid identity-linked corpse rows preserve CATMesh `15215` and exact
level-credit rules. The unlinked `20260713-013906` item outcome remains excluded.
Seven strict inventories retain the provisional weighted-one `1 + 1 + 5 empty`
policy for the two proven memberships. All Bot rows use an observed
`450`-second post-NPC-despawn delay; capture `20260708-143600` records
`459.913` seconds death-to-replacement at a `0.190`-unit position delta. Shared
chase and ordinary three-second empty/four-minute loot-bearing corpse behavior
apply. Both previously quarantined Bot rows are active for bounded private
validation. Proactive aggro radius and leash/reset distance remain unresolved.

Workman Striker is the tenth accepted ordinary profile. Declared overlap rules
reduce simultaneous capture
projections to 47 distinct normal local-player hits at `14..23`, six criticals
at `36..42`, and a `5.092328`-second median attack interval; two Killer-pet hits
stay in a separate target-role bucket. Twenty-one active exact spawns cover
levels 13, 14, 15, 16, 17, and 25, and every one has an owner-linked captured
weapon tuple. Ten complete first corpse opens prove eight positive and two empty
loot outcomes; ten unopened corpse generations do not enter the denominator.
The ten item/QL entries replay only their observed `1/10` or `2/10` frequencies,
with wider pool completeness unresolved. Every active source now resolves its
exact owner-linked captured low/high/QL weapon tuple; unknown, missing,
conflicting, or aggregate resolution fails closed. Those equipped items own
normal damage and recharge without a fixed-damage or captured-AttackInfo
override. The six observed critical outcomes remain report-only, matching the
explicit critical-parity gap already allowed for other accepted ordinary
profiles; no shared weapon-critical formula is invented. Whole-enemy coverage
now guards all 21 exact source weapons and spawns, fail-closed aggregate/unknown
selection, shared chase, strict incomplete-pool loot, CATMesh/credits, private
four-minute respawn, and ordinary corpse lifetimes together.

Melded Patterns no longer uses a fixed post-mitigation attack range as its
runtime damage source. Focused capture `20260716-034559` proves the QL20
Irreparable Sleekblaster Minor `121817/121818`; the ordinary combat profile now
equips that item and lets its stats own damage and recharge. The exact-evidence
gate fails closed if the focused capture or seven normal `21..34` hit boundary
drifts. No special-attack context or critical behavior is inferred, and the
profile is accepted by the whole-enemy gate with those exclusions preserved.

Reviewed first-open evidence now supplies strict item denominators for 18
ordinary profiles through one reusable raw-generation validator. In addition
to Shadow, ordinary Infector, Architect Striker, and Melded Patterns, the
recovered set is Mugger `18/3 empty`, Discarded Pet `16/3`, Stim Fiend `13/0`,
Looter `11/5`, Violent Vagabond `14/1`, Bloodcreeper `4/3`, Infected Attendant
`4/1`, Fragmented Soul `4/0`, Deranged Shopper `2/0`, Incomplete Rebuild `2/0`,
Redundant Scan `2/1`, Uncontrollable Anger `2/0`, Lost Thought `1/0`, and Neural
Burnout `4/2`. Each reviewed source is capture-allowlisted and its complete set
of exact corpse/dead-NPC/first-raw-inventory generations is fingerprinted;
capture allocations and declared overlap projections also fail closed. Unopened
and snapshot-only corpses remain excluded, as does the known false Stim Fiend
attribution. Runtime consumes generated strict-loot summary metadata, uses
`IndependentEntries`, preserves observed empty counts, and keeps
`ItemPoolComplete=false`; Empty Shell and Premature Pattern receive no table.

Shadow, ordinary Infector, Architect Striker, and Melded Patterns are the sixth
through ninth accepted ordinary profiles. Their gate coverage binds exact
spawns, appearance, captured normal combat, shared chase, strict incomplete-pool
loot, corpse visuals/credits, the private 240-second post-despawn ordinary
respawn policy, and shared three-second empty/four-minute loot-bearing corpse
lifetimes. Shadow's two, Infector's three, and Architect Striker's one observed
critical outcomes remain report-only. Ordinary Infector's generated `16..36`
fixed contract stays isolated from the Abmouth-owned specialized Infector path.
Architect Striker retains its captured fixed contract without an invented
weapon. Melded Patterns retains its exact QL20 `121817/121818` weapon-owned
damage/recharge path without invented special-attack or critical context.

The gate also includes Looter, Bloodcreeper, Stim Fiend, and Neural
Burnout. Looter resolves all eight exact owner-linked `123038/123039` tuples by
source identity and QL; its visible equipped item owns normal damage and
recharge, while aggregate, missing, conflicting, or unknown source selection
fails closed. The other three retain their specialized or fixed capture-backed
combat, strict incomplete loot samples, exact observed credit rows, shared chase,
ordinary corpse lifetimes, and private respawn policy. The ordinary generator and
Debug build pass, as do the expanded fourteen-profile gate, WorldPopulation,
and Subway loot suites.

Mugger is the fifteenth accepted profile. Its nine exact current source
identities each resolve a QL1 `121567/121567` weapon; aggregate, missing,
conflicting, and unknown source selection fails closed. The item owns damage,
damage bonus, and recharge, while only the captured AttackInfo ammo `-1`, slot
`6`, unknown `0`, and weapon instance `0` are replayed. Thirty-eight normal
`9..12` outcomes and three `21` criticals remain separate evidence, and the gate
also binds the strict 17-open incomplete loot pool, exact CATMesh/level credits,
shared chase, ordinary respawn, and corpse lifetimes.

Deranged Shopper is the sixteenth accepted profile, Incomplete Rebuild the
seventeenth, Redundant Scan the eighteenth, Fragmented Soul the nineteenth,
Discarded Pet the twentieth, and Uncontrollable Anger the twenty-first.
Discarded Pet activates all 29 exact L5..L10
rows and keeps 37 normal local-player SIW1 outcomes at `9..18` separate from
four report-only `30..33` criticals. Its fixed capture contract retains ammo
`-1`, slot `0`, unknown `0`, instance `SIW1`, and a `5.089763`-second
conventional median cadence. Retaliatory acquisition and captured chase are
enabled without inventing proactive aggro, leash, reset, or return-home
boundaries. Strict `16/3 empty` loot, CATMesh `15929`, standard corpse
lifetimes, and 25 exact credit corpses pass the same whole-enemy gate. The
Uncontrollable Anger slice keeps six exact active rows at captured levels
`13,13,19,20,23,23`, two patrols and four static anchors, retaliatory shared
chase, local-player SIW1 damage `11..18`, strict `2/0 empty` loot, CATMesh
`96177`, inherited private respawn, and `3/240/3` corpse rules. Four Killer-pet
hits at `25..42` and one other-player hit at `19` remain separate. Its complete
reviewed Killer cadence window is `5.1165513`, `5.1671525`, `10.1003489`
seconds; runtime uses the six-decimal median `5.167153` without dropping or
dividing the doubled interval. Exact positive-credit evidence covers
L11/L12/L13/L20/L21, while active L19 and L23 remain unresolved. The
twenty-one-profile gate, WorldPopulation `36/36`, and Subway loot `22/22` pass.
ZoneEngine compiles. Private activation validation remains pending for the 16
rows activated in this slice; the prior Discarded Pet validation boundary is
tracked separately.

## Ordinary respawn policy

PF127 eligible ordinary enemies inherit a 240-second post-NPC-despawn default.
This is a private-project regular-enemy policy, not proof that every official AO
Subway enemy uses one exact timer.

Resolution precedence is:

1. explicit per-spawn or per-archetype policy;
2. explicit group/encounter policy where a group supplies one;
3. the shared 240-second ordinary default for `OrdinaryEnemy` classifications;
4. explicit no-respawn or unresolved/fail-closed behavior.

Current explicit exceptions are Thief at 60 seconds, Filth Flea at 240 seconds,
all 12 Disobedient Bot rows at 450 seconds, Bloodcreeper at 240 seconds, and all
24 Slum Runner rows at 60 seconds. The catalog contains 89 explicit spawn rows;
the other 232 represented ordinary
rows inherit the default without changing whether they are active or
quarantined. Named enemies, bosses, scripted
encounters, summons, pets, temporary encounter adds, vendors, static objects,
containers, and quest-owned entities cannot inherit the ordinary default.
Their existing owners and encounter-specific policies remain separate.

Group policy references resolve through the same controller registration path;
optional group overrides are injected before ordinary definitions resolve, and
missing or unused group references fail closed. Policy registration rejects
conflicting bodies that reuse one key, disabled overrides, invalid
lifecycle-start values, non-finite/out-of-range delays, and zero-delay
schedules. Bounded-random delays require the controller's explicit random
source. Scripted policies remain with encounter owners, and scaffolded
group-shared timers are rejected until synchronized semantics exist. Explicit
no-respawn assignments carry stable policy keys so distinct provenance cannot
collide. The single keyed scheduler rejects duplicate death or corpse-cleanup
work and stale generation tokens. An early death/corpse-removal timer retains
its pending due state and resumes when the dead runtime is released; it is not
dropped merely because materialization was not yet safe.

## Capture-to-profile workflow

`tools-temp/AOSharpCaptureAnalyzer/generate_subway_ordinary_content.py` is the
review boundary for future captured ordinary rows.

```text
python tools-temp/AOSharpCaptureAnalyzer/generate_subway_ordinary_content.py --check
python tools-temp/AOSharpCaptureAnalyzer/generate_subway_ordinary_content.py --write
```

`--check` is the safe default. It builds and validates in memory, compares the
canonical generated content with the checked-in provider, and writes nothing.
`--write` validates first and atomically replaces changed output. Generation is
deterministic and fails closed for identity/profile collisions, missing profile
references, named bosses, owned summons, malformed or unsupported rows, and
unresolved combat values rendered as concrete capture evidence.

Generated rows remain reviewable and do not become enabled merely because they
were emitted.

## Adding an ordinary enemy

1. Finish a comprehensive live capture and run the analyzer in `--check` mode.
2. Review identity, placement, appearance, movement, combat, loot, corpse, and
   respawn-exception evidence. Leave unknowns explicit.
3. Add or reuse one mechanically accurate type profile.
4. Add exact spawn rows referencing that profile.
5. Keep new rows quarantined until the accepted-enemy coverage and runtime gate
   pass.
6. Run profile, population, combat, movement, corpse/loot, respawn, visibility,
   generator, and ZoneEngine validation.

No enemy-specific runtime class is added.

Future respawn captures should identify exceptions, named/encounter behavior,
or disputed timing. They are not required to prove the shared project default
individually for every ordinary row.

## Adding a scripted boss

Create a dedicated content/encounter module only when capture evidence proves a
mechanic cannot be represented by the ordinary profile. Keep its spawn and
script ownership outside `OrdinaryEnemyCatalog`; reuse shared combat, movement,
corpse, loot, and visibility services where applicable.

## Visibility boundary and unresolved evidence

The PF127 existing-character visibility-volume problem remains a separate global
visibility-layer task. This runtime does not add distance limits, packet budgets,
batching, throttling, delayed SCFU, pagination, acknowledgements, or per-enemy
visibility suppression. Enabling many quarantined rows can still exercise that
known boundary.

Finalized capture `20260719-020104` supplies an exact four-segment replay patrol
for Disobedient Bot source `0x79557C66` and an exact 26-segment replay patrol for
Violent Vagabond source `0x7957E5C4`. These are source-specific captured routes,
not evidence that other rows should receive background patrols. The same capture
brings Vagabond strict loot to 14 atomic outcomes (`13` positive, `1` empty), but
its combat sample still contains 40 misses and no landed local-player damage.
It proves neither respawn timing nor corpse lifetime.

Finalized capture `20260719-021022` supplies source-specific complete patrols for
active Filth Fleas `0x7953AFCC` (10 segments, 28 complete cycles) and
`0x795317F5` (18 segments, 12 complete cycles), active Discarded Pet
`0x79528FDA` (24 segments, five complete cycles), and active Violent Vagabond
`0x7953AFA1` (10 segments, four complete cycles). It independently corroborates
four existing Flea routes and the existing Vagabond route. Ambiguous complete
routes remain evidence-only and are not mapped. The Violent Vagabond patrol
evidence adds no combat result, so the family remains partially quarantined and
`0x7953AFA1` keeps its active disposition. Incidental Mugger evidence adds one
miss and SIW context without changing the captured Mugger landed-damage range.
The capture also adds an exact L5 Mugger corpse with `44` credits, CATMesh
`17534`, and one QL5 `123495/123496` item, raising strict Mugger loot from 17
outcomes (14 positive, three empty) to 18 (15 positive, three empty). It proves
neither respawn timing nor corpse lifetime.

Current unresolved data remains fail-closed, including combat sources without a
landed captured hit, unsupported/nonordinary respawn classifications without an
explicit owner policy, automatic-aggro radii not yet captured, level ranges not
established by evidence or decision, and random roam behavior not proven by
movement evidence. The remaining 11 Violent Vagabond rows stay quarantined;
one exact patrol does not resolve their missing landed-damage contract.
The whole-enemy gate still accepts 21 of 26 profiles. Infected Attendant, Lost
Thought, Empty Shell, Premature Pattern, and Violent Vagabond account for 32
active rows plus the 11 quarantined Vagabonds and require the next
corpus-backed completion pass; active disposition is not a claim that those
five profiles are complete.
