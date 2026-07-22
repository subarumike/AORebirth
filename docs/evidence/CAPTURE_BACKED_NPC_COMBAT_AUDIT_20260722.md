# Capture-backed NPC combat audit - 2026-07-22

## Scope and acceptance rule

This audit covers every configured capture-backed hostile or retaliating NPC in the implemented Subway, Temple of Three Winds, Nascence, Arete-family, Rome Blue, and Thrak Garden content, including Subway merchants, Cursed Silvertail, and dynamic mission mobs. A runtime packet-context contract passes only when one identified capture actor owns the complete packet definition and the content resolver binds it to the same proven enemy generation without a nearest-level or cross-enemy substitution. A generic hand, enum label, or another enemy profile's packet is not certification. Temple uses exact source-identity binding because same-name Cultists vary by source; the known-good level-5 Thief uses its independently live-proven level/monster-data generation binding because runtime respawns do not retain the official capture identity. Existing capture-backed damage and cadence policies are deliberately held constant under the task's no-gameplay-change rule.

Unsupported actors remain spawned and visible but are registered with a passive, quarantined combat contract. The shared combat tick refuses every registered quarantined contract.

## Authoritative good-versus-bad trace

### Known-good Subway reference

- Capture: `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260711-170337`
- Actor: Thief `0x795B5DB2`
- `packets.hex.log`: SCFU `#155`, owner-linked WIFU `#156`, `SpecialAttackWeapon` `#301`, `Attack` `#302`, normal `AttackInfo` `#480`, `#564`, and `#654`.
- WIFU: weapon `0x2573BACB`, owner `0x795B5DB2`, playfield `0x00153008`, slot `6`, state machine `1000015:0`, flags `67109889`, templates `121567/121567`, QL `1`, Energy `-1`, delays `235/235`.
- Attack start: N3 outer `0`, empty special list, values `32/32/32/32/0`, Attack outer/action `0/0`.
- AttackInfo: amount `9`, ammo `-1`, slot `6`, unknown `0`, target `0x7944C065`, numeric hit wire value `3`, weapon instance `0`, N3 outer `0`.

Exact recorded bytes:

- WIFU, including capture transport/session framing:
  `000B000A0001008700000DB47944C0653B1D22680000C74A2573BACB000000000B0000C350795B5DB200153008000F424F0000000001060000276A0000000004000401000000170001DADF000002BD00000001000002BE0001DADF000002BF0001DADF0000019C000000010000001AFFFFFFFF00000126000000EB000000D2000000EB00000000`
- `SpecialAttackWeapon` N3 body:
  `1D3C0F1C0000C350795B5DB200000003F10000002000000020000000200000002000000000`
- `Attack` N3 body:
  `284940700000C350795B5DB2000000C3507944C06500`
- `AttackInfo` N3 body:
  `46002F160000C350795B5DB20000000009FFFFFFFF000000060000C3507944C065000000000000000300000000`

### Failing Temple profile before this repair

- Capture: `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260721-031913`
- Actor: Cultist `0x7984B379`, monster data `26147`, level `20`.
- `packets.hex.log`: SCFU `#66`, owner-linked WIFU `#67`, `SpecialAttackWeapon` `#547`, `Attack` `#548`, normal `AttackInfo` `#559` and `#637`.
- WIFU: weapon `0x257EF84A`, owner `0x7984B379`, playfield `938000`, slot `6`, state machine `1000015:0`, flags `1027`, templates `144103/144104`, QL `24`, Energy `15`, delays `235/235`.
- Attack start: N3 outer `0`, empty special list, values `305/305/305/12/0`, Attack outer/action `0/0`.
- AttackInfo `#559`: amount `15`, ammo `14`, slot `6`, unknown `0`, target `0x70CBBEF3`, numeric hit wire value `3`, weapon instance `0`, N3 outer `0`.
- AttackInfo `#637`: amount `18`, ammo `13`; the remaining fields are unchanged.

Exact recorded bytes:

- WIFU, including capture transport/session framing:
  `0017000A0001008700000DBD70CBBEF33B1D22680000C74A257EF84A000000000B0000C3507984B379000E5010000F424F0000000001060000276A000000000000040300000017000232E7000002BD00000018000002BE000232E7000002BF000232E80000019C000000010000001A0000000F00000126000000EB000000D2000000EB00000000`
- `SpecialAttackWeapon` N3 body:
  `1D3C0F1C0000C3507984B37900000003F10000013100000131000001310000000C00000000`
- `Attack` N3 body:
  `284940700000C3507984B379000000C35070CBBEF300`
- First `AttackInfo` N3 body, Energy/ammo `15 -> 14`:
  `46002F160000C3507984B379000000000F0000000E000000060000C35070CBBEF3000000000000000300000000`
- Second `AttackInfo` N3 body, Energy/ammo `14 -> 13`:
  `46002F160000C3507984B37900000000120000000D000000060000C35070CBBEF3000000000000000300000000`

### Proven fault

The capture has the same required physical-weapon presentation pattern as the working Thief. The old Temple path discarded that pattern: it did not equip or serialize the Cultist's owner-linked WIFU, hardcoded ammo `-1`, and constructed attack-start packets without preserving the captured N3 outer fields. The client therefore received a slot-6/instance-0 hit without its captured physical weapon context and rendered the incoming damage as nanobot/unknown. `NpcCombatTickCoordinator.SendIncomingHitChatIfPlayer` then independently injected a second `Cultist hit you...` chat line.

The serializer layouts themselves were not at fault. Exact factory serialization now matches the N3 body from each capture byte-for-byte. Transport framing fields that belong to the captured session are intentionally outside the message serializer.

## Shared runtime boundary

- `CapturedEnemyCombatPacketFactory` is the only capture-backed constructor for WIFU, `SpecialAttackWeapon`, `Attack`, and `AttackInfo`.
- Content catalogs supply raw numeric fields and provenance; they do not construct packets.
- Physical contracts require a valid nine-stat WIFU definition in exact captured order and an exact evidence source identity.
- Fixed/generic AttackInfo contracts cannot pass readiness.
- Specialized contracts without an exact evidence source identity cannot pass readiness.
- A missing or invalid contract is registered passive; combat ticks return without falling through to generic hands or nanobots.
- Captured weapon Energy is per spawned actor. Energy `15` emits ammo `14`, then `13`; Energy `-1` remains `-1`.
- Re-visibility reads that same per-actor Energy state, so a later WIFU cannot reset or contradict subsequent AttackInfo ammo.
- Numeric hit values are preserved as integers. Temple source `0x7983FB93` proves wire value `4`, but remains quarantined because its only landed observation is critical and cannot prove an ordinary-hit selection rule.
- The synthetic incoming-hit chat path was removed. Client combat text now has one source: the serialized combat packet.
- Capture-backed NPC `FunctionType.Hit` damage is also blocked unless its packet/chat semantics become part of the proven shared contract; it cannot fall into the legacy generic AttackInfo plus synthetic-chat path.
- If a required captured weapon disappears from the live inventory or visibility state, the actor is re-quarantined instead of falling through to generic WIFU or unarmed combat.
- The existing Cultist `15..32` damage envelope, melee range,
  `2.129326`-second first-hit policy, and `4.635295`-second cadence remain
  unchanged. Source-local measured first-hit intervals are retained below as
  evidence only and do not alter runtime timing.

The exact critical-only wire-value-4 fixture retained for serializer coverage is:
`46002F160000C3507983FB93000000003A00000012000000060000C35070CBBEF3000000000000000400000000`.

## Coverage totals

| Content surface | Initial active actors | Runtime-certified | Quarantined | Audit result |
| --- | ---: | ---: | ---: | --- |
| Subway ordinary/content rows | 322 | 1 | 321 | The level-5 Thief remains certified through its live-proven complete packet generation; all other rows quarantine. |
| Subway encounter slots | 3 | 0 | 3 | Abmouth, Vergil, and Eumenides are initial; two Infector slots arm later. Current contracts are source-unbound or aggregate. |
| Temple ordinary rows | 153 | 14 | 139 | Fourteen exact Cultist identities pass; all other Cultists, three Sentinels, and Murial quarantine. |
| Temple named encounters | 9 | 0 | 9 | Current contracts are source-unbound/aggregate. |
| Temple Reanimated Corpse adds | 2 | 0 | 2 | Both spawn at Temple encounter activation and quarantine. |
| Nascence core Hecklers | 40 | 0 | 40 | Current profiles use generic or cross-profile mappings without a complete source-local packet contract. |
| Nascence life population | 830 | 0 | 830 | PF4310/4311/4312 capture-backed wildlife is registered passive; its current runtime rows do not map source-local WIFU/attack-start/AttackInfo contracts. |
| Arete-family, including Marcus ambient actors | 91 | 0 | 91 | Current profiles use generic/formula paths; Marcus and the Burning Cleaning Robot are explicitly passive. Attack-immune Greedy Desert Reet is excluded below. |
| Additional Arete captured actors | 8 | 0 | 8 | Seven Malfunctioning Cleaning Robots and Engineer Automaton I now register unresolved instead of retaliating through generic combat. |
| Subway merchants | 6 | 0 | 6 | Capture-backed vendor NPCs register unresolved; interaction, stock, and visibility behavior are unchanged. |
| Rome Blue captured city population | 22 | 0 | 22 | Capture-backed city NPCs register unresolved instead of retaliating through generic combat. |
| Thrak Omni Garden population | 10 | 0 | 10 | Capture-backed garden NPCs register unresolved instead of retaliating through generic combat. |
| **Initial total** | **1,496** | **15** | **1,481** | **PASS: no unsupported configured actor can attack.** |

Two Infector slots can arm after Abmouth combat. Both quarantine, making the
configured maximum `1,498`, with `15` certified and `1,483` quarantined. Cursed
Silvertail is a quarantined dynamic replacement for one of five Dreaming
Silvertails and does not increase the population count. Dynamic mission mobs
also quarantine because the mission path has only a generic fixed contract.

Explicit non-retaliatory capture-backed exclusions are outside that denominator:
53 ICC HQ Social actors, 82 Arete Landing Social actors, 8 HoloDeck Social
actors, 3 Windcaller Karrec Social actors, 1 Surveillance Droid Social actor,
the KnuBot Perk-Reset provider, attack-immune Greedy Desert Reet, and
attack-immune Lolly the Crazed. `ForceTauntAggro` now requires a retaliatory
AI profile and therefore cannot turn these Social actors into a generic combat
fallback.

There are 77 coherent raw attack-chain identities in the audited dungeon
corpus: 59 Subway ordinary sources, the off-anchor complete level-5 Thief,
direct-complete Abmouth `0x7970254F`, direct-complete Eumenides `0x79748626`,
and 15 Temple Cultists. The level-5 Thief and fourteen Temple identities have
complete runtime packet-context mappings. The remaining 62 quarantine: all 59
Subway ordinary source mappings, source-unbound Abmouth and Eumenides, and
Temple `0x7983FB93`, whose only landed observation is critical.

### Certified Subway generation

The active level-5 Thief anchor `0x7953AEA5` resolves only the complete level-5 Thief contract owned by official capture actor `0x795B5DB2` in `20260711-170337`. A missing level or any non-level-5 request fails closed. Its WIFU, attack-start fields, `9`-point AttackInfo fixture, and existing timing remain byte-unchanged.

### Certified Temple source identities

`0x79834EC1`, `0x79834EC3`, `0x79834ECC`, `0x79834ECD`, `0x79834ECF`, `0x7983FB96`, `0x7983FB98`, `0x7983FB9B`, `0x7983FBDF`, `0x7983FC37`, `0x7984B374`, `0x7984B375`, `0x7984B379`, `0x7984B37C`.

Selected `packets.hex.log` records, in WIFU / `SpecialAttackWeapon` / `Attack` / first-landed-`AttackInfo` order:

| Source | Capture | Records |
| --- | --- | --- |
| `0x7984B374` | `20260721-031913` | `#59 / #334 / #335 / #348` |
| `0x7984B375` | `20260721-031913` | `#61 / #397 / #398 / #418` |
| `0x7984B379` | `20260721-031913` | `#67 / #547 / #548 / #559` |
| `0x7984B37C` | `20260721-031913` | `#71 / #1063 / #1064 / #1075` |
| `0x7983FB96` | `20260721-032547` | `#29 / #2019 / #2020 / #2059` |
| `0x7983FB98` | `20260721-032547` | `#31 / #2285 / #2286 / #2437` |
| `0x7983FB9B` | `20260721-032547` | `#41 / #2562 / #2563 / #2609` |
| `0x79834ECD` | `20260721-052115` | `#597 / #5098 / #5099 / #5423` |
| `0x79834ECF` | `20260721-052115` | `#599 / #5293 / #5294 / #5432` |
| `0x7983FBDF` | `20260721-052115` | `#603 / #5419 / #5420 / #5565` |
| `0x79834EC1` | `20260721-052115` | `#605 / #8311 / #8312 / #8358` |
| `0x79834EC3` | `20260721-052115` | `#607 / #8764 / #8765 / #8834` |
| `0x79834ECC` | `20260721-052115` | `#609 / #4956 / #4957 / #5660` |
| `0x7983FC37` | `20260721-052115` | `#611 / #7987 / #7988 / #8044` |

Quarantined critical-only source `0x7983FB93` is `20260721-032547` records `#33 / #1838 / #1839 / #1881`.

Each certified Temple contract keeps the selected source chain's exact first-hit delay and first landed AttackInfo fields:

| Source | Weapon | Flags | Low/high template | QL | Energy | SAW values | First hit / ammo / wire | First-hit delay (s) |
| --- | --- | ---: | --- | ---: | ---: | --- | --- | ---: |
| `0x79834EC1` | `0x257ED88C` | 67109921 | 204747/204747 | 1 | -1 | 434/434/434/17/0 | 24/-1/3 | 1.6365208 |
| `0x79834EC3` | `0x257ED88F` | 1027 | 130164/130164 | 34 | -1 | 518/518/518/19/0 | 29/-1/3 | 1.6545747 |
| `0x79834ECC` | `0x257ED89A` | 1027 | 129028/129029 | 29 | -1 | 468/468/468/18/0 | 26/-1/3 | 13.4882616 |
| `0x79834ECD` | `0x257ED89C` | 67109921 | 204747/204747 | 1 | -1 | 400/400/400/16/0 | 23/-1/3 | 6.7575341 |
| `0x79834ECF` | `0x257ED89F` | 1027 | 144103/144104 | 26 | 15 | 351/351/351/13/5 | 20/14/3 | 3.0200282 |
| `0x7983FB96` | `0x257EDE4F` | 67109921 | 204747/204747 | 1 | -1 | 484/484/484/18/0 | 27/-1/3 | 2.0012955 |
| `0x7983FB98` | `0x257EDE51` | 1027 | 144103/144104 | 25 | 15 | 416/416/416/16/0 | 24/14/3 | 8.7926267 |
| `0x7983FB9B` | `0x257EDE57` | 67109921 | 204747/204747 | 1 | -1 | 351/351/351/13/0 | 21/-1/3 | 3.1704871 |
| `0x7983FBDF` | `0x257EDEB3` | 1027 | 158298/158299 | 34 | -1 | 470/450/450/17/0 | 26/-1/3 | 2.4264815 |
| `0x7983FC37` | `0x257EDF1A` | 67109921 | 204747/204747 | 1 | -1 | 434/434/434/17/0 | 24/-1/3 | 1.7271208 |
| `0x7984B374` | `0x257EF844` | 67109921 | 204747/204747 | 1 | -1 | 434/434/434/17/0 | 24/-1/3 | 2.0595459 |
| `0x7984B375` | `0x257EF846` | 1027 | 129028/129029 | 37 | -1 | 535/535/535/20/0 | 21/-1/3 | 2.3195167 |
| `0x7984B379` | `0x257EF84A` | 1027 | 144103/144104 | 24 | 15 | 305/305/305/12/0 | 15/14/3 | 2.1303261 |
| `0x7984B37C` | `0x257EF84F` | 1027 | 124314/124314 | 32 | 20 | 468/468/468/18/0 | 19/19/3 | 1.8387221 |

The selected landed chain is the reproduced evidence unit. Earlier alternate attack starts exist for `0x79834EC1` and `0x79834EC3`, and `0x7983FC37` has another coherent first-hit timing; they are not blended into the selected contract. Source `0x7983FB93` remains quarantined because its only landed observation is the retained critical-only fixture.

### Subway raw candidates still quarantined

- Generic fixed mappings: 35.
- Source-local physical mappings missing exact WIFU/start fields: 17; the separately proven level-5 Thief is certified.
- Cross-source Melded Patterns mapping: 5.
- Filth Flea allowlist entry not wired through a source-complete resolver: 1.
- Bloodcreeper `0x795451C5`: packet fields are source-observed, but damage/timing are aggregate, so the literal own-source rule quarantines it.
- Direct-complete Abmouth `0x7970254F` and Eumenides `0x79748626` remain quarantined because their active encounter contracts are source-unbound.

The existing specialized and parallel factory signatures do not accept an
evidence source identity. Readiness therefore keeps those contracts
structurally quarantined until source-bound factories and full per-source
runtime mappings are implemented; it never treats their catalog values as an
implicit default.

## Automated acceptance

The shared-boundary tests assert:

- exact Thief and Cultist WIFU, `SpecialAttackWeapon`, `Attack`, and `AttackInfo` bytes;
- captured message order;
- Cultist ammo `15 -> 14 -> 13`;
- distinct Subway and Temple weapon evidence;
- raw hit wire values `3` and `4` without enum normalization;
- rejection of level-only, nearest-level, cross-monster, cross-level, and unknown-source Cultist requests;
- fourteen ready and 139 quarantined Temple ordinary rows;
- one ready level-5 Thief row and 321 quarantined Subway catalog rows;
- quarantine of current Subway and Temple named encounter contracts;
- use of the same fail-closed preparation boundary by mission, Nascence,
  Arete-family, Lorelei, cleaning-robot, and Marcus hostile entry points;
- use of the shared factory by visibility and combat runtime;
- absence of the synthetic incoming-hit chat path and Temple packet constructors.

Validation on 2026-07-22:

- `CapturedEnemyCombatPacketFactoryTests`: `6/6` PASS.
- `TempleOfThreeWindsOrdinaryContentTests`: `4/4` PASS.
- `WorldPopulationFoundationTests`: `39/39` PASS.
- `PlayfieldLifecycleTraceTests`: `54/66` PASS; the 12 failures are existing
  unrelated Arete robot, playfield/session sequencing, and direct-pool guardrails.
- Full messaging suite: `434/464` PASS; 30 unrelated repository failures remain.
- `cmd /d /c tools\build_aorebirth_debug.cmd`: PASS.
- `cmd /d /c restart-engines.cmd`: PASS; Chat/Login/Zone restarted and ports
  `6996`, `7012`, `7500`, and `7501` were confirmed listening by the wrapper.

Official-client rendering remains a separate acceptance step and is not proven by this audit.
