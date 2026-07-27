# Subway Violent Vagabond Combat Evidence Review

Date: 2026-07-26

## Result

All 22 active PF127 Violent Vagabonds remain fail-closed. The corpus contains
41 raw Vagabond-owned miss results, or 40 distinct observations after the
declared overlap between captures `20260709-212115` and `20260709-212336` is
counted once. It contains no Vagabond-owned `AttackInfo`, normal landed result,
critical result, or terminal result.

The original blocker had two parts:

1. A generic extractor defect attributed `MissedAttackInfo` to its N3 envelope
   source, which is the observing/defending player, instead of the embedded
   attacker identity. This discarded valid miss ownership during profile
   correlation.
2. A genuine combat-contract evidence gap remains after that defect is fixed.
   `MissedAttackInfo` has no damage type, hit type, weapon slot, weapon
   instance, damage amount, ammunition result, critical distinction, or lethal
   result. Miss-only evidence therefore cannot define the required landed
   `AttackInfo` contract.

No production combat behavior, packet factory, binder, timer, scheduler, or
population row was changed. No Vagabond-specific parser branch was added.

## Baseline

- Branch: `master`
- Starting local HEAD: `e011211dc3c47b241c9a983dc34f8859fe957493`
- Starting `origin/master`: `e011211dc3c47b241c9a983dc34f8859fe957493`
- Starting tracked worktree: clean
- Preserved untracked work:
  `Mission_Tables_Level_Restrictions_Teaming_Levels.ods`, `diagnostics/`, and
  `tools-temp/ProcDump/`
- Starting active coverage: PF127 `238/84`, PF1931 `87/80`, combined
  `325/164`, denominator `489`
- Starting Vagabond coverage: `0/22`

## Active actor reconciliation

All rows use runtime selector `subway.supported.203733`, playfield `127`,
MonsterData `203733`, and surface `subway-ordinary`. The population-row number
is the source line in `CapturedSubwayContentProvider.cs`. Each binding has
`actorCount=1`; the 22 binding keys and 22 source identities are unique.

`Generated candidate: none` means the generated runtime catalog contains zero
capture-certified profiles matching the Vagabond archetype key. The generated
inventory has unresolved evidence profiles for levels 6, 7, 8, 9, and 10, but
each has `normalCompleteChainCount=0` and
`captureCertifiedVariantCount=0`. Those evidence records are not combat
contracts.

| Row | Runtime identity | Level | Binding / coverage key | Capture sessions containing this exact identity | Observed outgoing target | Generated candidate and exact rejection |
|---:|---|---:|---|---|---|---|
| 128 | `0x7953AA4A` | 10 | `8f6d0297f22d78bc4160` / `5e4625f74e66ca33e8d2` | `20260709-205921`, `20260709-210452`, `20260709-212115`, `20260709-212336`, `20260709-220439`, `20260709-222339`, `20260709-225408` | none | none; no own-source landed `AttackInfo` |
| 129 | `0x7953AD40` | 6 | `1647eb43a7161be89c1b` / `5d94bac6cd23c68f2771` | `20260709-205921`, `20260709-210452`, `20260709-212115`, `20260709-212336`, `20260709-220439` | none | none; no own-source landed `AttackInfo` |
| 130 | `0x7953AD48` | 7 | `597da9c1437f06cb776b` / `0af3cccd61171f33e8b7` | `20260709-205921`, `20260709-210452`, `20260709-212115`, `20260709-212336`, `20260709-220439`, `20260709-222339`, `20260709-225408` | none | none; no own-source landed `AttackInfo` |
| 131 | `0x7953AD49` | 6 | `cc40d964ec37bb5cb7bd` / `a410946188dcf4738adc` | `20260709-205921`, `20260709-210452`, `20260709-212115`, `20260709-212336` | `0x7944C065`, miss only | none; exact miss has no landed result semantics |
| 132 | `0x7953AD4A` | 7 | `e534c5b90fb229c1a15c` / `2f0e9ff2f62e8658f701` | `20260709-205921`, `20260709-210452`, `20260709-212115`, `20260709-212336`, `20260709-220439` | none | none; no own-source landed `AttackInfo` |
| 133 | `0x7953AD4C` | 7 | `3281d44c29d97fcd08c8` / `e0364bccfbecc1fd731b` | `20260709-205921`, `20260709-210452`, `20260709-212115`, `20260709-212336`, `20260709-220439` | none | none; no own-source landed `AttackInfo` |
| 134 | `0x7953AD54` | 8 | `7efff72b570a3ea51936` / `7a00e93406598294faf6` | `20260709-205921`, `20260709-210452`, `20260709-212115`, `20260709-212336`, `20260709-220439`, `20260709-222339`, `20260709-225408` | none | none; level 8 has no own-source result |
| 135 | `0x7953AD58` | 10 | `d54d2b04df404762d8b1` / `a75418f8147ef19e39df` | `20260709-205921`, `20260709-210452`, `20260709-212115`, `20260709-212336`, `20260709-220439`, `20260709-222339`, `20260709-225408` | none | none; no own-source landed `AttackInfo` |
| 136 | `0x7953AD76` | 10 | `b3884ae50358fd557e29` / `a2289573becaa8424761` | `20260709-212115`, `20260709-212336`, `20260709-220439`, `20260709-222339`, `20260709-225408` | none | none; no own-source landed `AttackInfo` |
| 137 | `0x7953AF49` | 7 | `fe319becdb130896fe14` / `0ce6b605882221913558` | `20260709-210452`, `20260709-212115`, `20260709-212336`, `20260709-220439` | none | none; no own-source landed `AttackInfo` |
| 138 | `0x7953AFA1` | 7 | `2b6748e4dedbf4afa031` / `c13f33999c38482ba57c` | `20260709-212115`, `20260709-212336`, `20260709-220439` | none | none; no own-source landed `AttackInfo` |
| 139 | `0x79557CAC` | 10 | `ab2cfb47b28292f8bd88` / `7b85767ccbf4151317e2` | `20260710-202132` | none | none; no own-source landed `AttackInfo` |
| 140 | `0x7957405C` | 7 | `9beaebb2540e4e1959b3` / `bdee29137c3fe2244154` | `20260710-202132` | none | none; no own-source landed `AttackInfo` |
| 141 | `0x795743A7` | 10 | `a0fbf27e062169087e06` / `14b71b7817ab6989404b` | `20260710-202132`, `20260710-211430` | none | none; no own-source landed `AttackInfo` |
| 142 | `0x795743A8` | 10 | `a62b43b1b1539653ec50` / `f72471f91cc1dff80366` | `20260710-202132`, `20260710-211430` | none | none; no own-source landed `AttackInfo` |
| 143 | `0x7957E02C` | 7 | `1ae8655bf2e1bf683627` / `bc2641abd8cd7941386f` | `20260710-202132`, `20260710-205400`, `20260710-211430`, `20260710-212455` | none | none; no own-source landed `AttackInfo` |
| 144 | `0x7957E02E` | 7 | `7350e37df703a1b46a67` / `0e3c9b4cc910a8eb510d` | `20260710-202132` | none | none; no own-source landed `AttackInfo` |
| 145 | `0x7957E123` | 6 | `1e156fd32869f50433f1` / `c7107cf6f6eaf022b9a5` | `20260710-202132`, `20260710-211430` | none | none; no own-source landed `AttackInfo` |
| 146 | `0x7957E40E` | 6 | `5f7519d97e5b67bf44ac` / `b7ec3439de3ef6d6886c` | `20260710-202132` | none | none; no own-source landed `AttackInfo` |
| 147 | `0x7957E5BF` | 7 | `9427c4746c3b8d060ad4` / `90bb5283bd5804662c3f` | `20260710-202132`, `20260710-202553`, `20260710-205400` | none | none; no own-source landed `AttackInfo` |
| 148 | `0x7957E5C4` | 7 | `2a338324d2e6ab4b240b` / `4083f7d4644af5f75c1a` | `20260710-202132`, `20260710-211430` | none | none; no own-source landed `AttackInfo` |
| 149 | `0x7957E5C5` | 6 | `f14ec0f7715bdb6d7df7` / `b0c92d4418fab4e66177` | `20260710-202132`, `20260710-211430` | none | none; no own-source landed `AttackInfo` |

Level reconciliation is exactly level 6: 5 actors; level 7: 10 actors;
level 8: 1 actor; level 10: 6 actors. There are exactly 22 unresolved binding
rows for 22 unique runtime identities.

## Capture search scope

The deterministic extractor searched all 364 capture sessions. The following
46 sessions contained Vagabond SCFU/dossier metadata or positive raw Vagabond
combat evidence and were used in the focused correlation:

`20260708-004038`, `20260708-143600`, `20260708-175514`,
`20260708-180248`, `20260708-181729`, `20260708-182237`,
`20260708-185451`, `20260708-185543`, `20260708-223814`,
`20260708-225850`, `20260709-164219`, `20260709-164414`,
`20260709-165538`, `20260709-165805`, `20260709-174823`,
`20260709-184655`, `20260709-193914`, `20260709-205921`,
`20260709-210452`, `20260709-212115`, `20260709-212336`,
`20260709-220439`, `20260709-222339`, `20260709-225408`,
`20260710-202132`, `20260710-202553`, `20260710-205400`,
`20260710-211430`, `20260710-212455`, `20260711-170337`,
`20260711-172140`, `20260711-172309`, `20260712-153918`,
`20260712-155528`, `20260712-160257`, `20260712-161506`,
`20260712-195019`, `20260713-013906`, `20260713-033511`,
`20260714-182132`, `20260717-012522`, `20260717-012651`,
`20260719-010047`, `20260719-020104`, `20260719-021022`, and
`20260719-021611`.

## Exact miss chains

All rows are inbound. Every miss embeds defender `0x7944C065`; its N3 source is
also `0x7944C065`, while its embedded attacker is the Vagabond. The exact miss
body shape is `n3Unknown=1`, `unknown1=0`, `unknown2=6`, `unknown5=0`.

| Session | Captured attacker | Level | Context packet ordinals | Miss packet ordinals and UTC timestamps |
|---|---|---:|---|---|
| `20260708-143600` | `0x794DF068` | 6 | WIFU `1999`; SAW `4786`; Attack `4787` | `4852` `19:39:58.5972545Z` |
| `20260708-143600` | `0x794DF076` | 6 | WIFU `893`; SAW/Attack `9618/9619`, `9724/9725` | `9657` `19:44:04.5525184Z`; `9728` `19:44:09.0431838Z` |
| `20260708-143600` | `0x794CD74B` | 7 | WIFU `890`; SAW/Attack `10474/10475`, `10589/10590` | `10526` `19:44:58.2627156Z`; `10609` `19:45:02.8429560Z`; `10672` `19:45:06.9127564Z`; `10728` `19:45:10.9927058Z` |
| `20260708-143600` | `0x794CD4CC` | 6 | WIFU `888`; SAW `11154`; Attack `11155` | `11209` `19:45:41.6936367Z` |
| `20260708-143600` | `0x794CD765` | 10 | no in-boundary WIFU; SAW `16210`; Attack `16211` | `16227` `19:53:45.7594660Z`; `16250` `19:53:49.5389956Z`; `16275` `19:53:53.3397377Z`; `16298` `19:53:57.1392594Z` |
| `20260708-143600` | `0x794DF33F` | 10 | WIFU `19184`; SAW `19378`; Attack `19379` | `19396` `20:01:17.0456600Z`; `19421` `20:01:21.2952410Z`; `19445` `20:01:25.5551886Z`; `19476` `20:01:29.4650907Z`; `19500` `20:01:33.3351628Z` |
| `20260709-205921` | `0x7953ABC5` | 7 | WIFU `2557`; SAW `3677`; Attack `3678` | `3754` `02:02:50.2747511Z` |
| `20260709-210452` | `0x7953ABA3` | 6 | no local WIFU/SCFU generation; SAW `965`; Attack `966` | `1038` `02:05:39.7040037Z` |
| `20260709-210452` | `0x7953ABAB` | 6 | no local WIFU/SCFU generation; SAW `4484`; Attack `4485` | `4527` `02:08:13.8466512Z` |
| `20260709-210452` | `0x79531279` | 6 | no local WIFU/SCFU generation; SAW `4825`; Attack `4826` | `4830` `02:08:28.1118405Z` |
| `20260709-210452` | `0x79528A5F` | 7 | no local WIFU/SCFU generation; SAW `4973`; Attack `4974` | `5026` `02:08:37.4115060Z` |
| `20260709-210452` | `0x79528F80` | 10 | WIFU `5319`; SAW `9333`; Attack `9334` | `9355` `02:14:10.1970201Z`; `9378` `02:14:14.7140178Z`; `9402` `02:14:19.2303657Z` |
| `20260709-212115` | `0x7953AD49` | 6 | WIFU `2169`; SAW `5451`; Attack `5452` | `5537` `02:26:34.7641418Z` |
| `20260709-212336` | `0x7953AD49` | 6 | WIFU `1032`; SAW `4314`; Attack `4315` | `4400` `02:26:34.7641418Z`; declared one-to-one overlap with the preceding row |
| `20260719-020104` | `0x797B885C` | unresolved capture-start generation | no WIFU/SCFU; SAW `1515`; Attack `1516` | `1568` `07:02:20.9974122Z`; `1676` `07:02:25.8679781Z`; `1796` `07:02:30.7185551Z`; `1909` `07:02:35.5660198Z`; `2025` `07:02:40.5075765Z`; `2154` `07:02:45.3276367Z`; `2259` `07:02:50.1470202Z`; `2361` `07:02:55.0585681Z` |
| `20260719-020104` | `0x797B885D` | unresolved capture-start generation | no WIFU/SCFU; SAW `1738`; Attack `1739` | `1797` `07:02:30.9380588Z`; `1914` `07:02:35.6670213Z`; `2026` `07:02:40.5075765Z`; `2159` `07:02:45.5671450Z`; `2260` `07:02:50.2470203Z`; `2366` `07:02:55.1885691Z` |

The regenerated mapped profiles retain 27 raw miss observations. The identical
`20260709-212115`/`20260709-212336` row reduces those to 26 distinct mapped
observations. The compact packet ledger retains the other 14 observations from
`20260719-020104` with corrected attacker source identities, for 40 distinct
misses in total.

### What the misses prove

- One visible initiation/miss stream per observed actor.
- Empty SAW special list, SAW N3 `0`, Attack N3 `0`, and Attack action `0`.
- Level-6 SAW stable values `32/35/29/31`, level-7 values `36/39/32/36`,
  and level-10 values `49/54/44/48`.
- SAW field 5 is mutable: examples include `0`, `23`, and `40`.
- Where a same-generation WIFU exists, it is owner-linked slot `6`, QL `1`,
  template `130590/130590`, Energy `1`, AttackDelay `175`, and RechargeDelay
  `175`.
- The observed defender is always local player `0x7944C065`.
- Repeated-result spacing ranges from approximately 3.78 to 5.06 seconds.
- The `20260719-020104` streams show exact
  `SAW -> Attack -> repeated MissedAttackInfo` order but begin after actor
  visibility; they do not contain SCFU or WIFU initialization.

### What the misses do not prove

- A normal or critical `AttackInfo` packet.
- Numeric hit type or damage type.
- Landed-result weapon slot or weapon instance.
- Landed damage, ammunition, lethal override, or target-health semantics.
- Whether template `130590` is itself the landed combat weapon rather than an
  equipped inventory item observed alongside a natural attack.
- A complete WIFU-to-result chain for the capture-start actors.
- A complete reusable combat stream for any supported level.

## Landed and terminal candidate correlation

There are no Vagabond-sourced `AttackInfo` packets anywhere in the 364-session
corpus. Every `AttackInfo` involving a Vagabond has a different source and the
Vagabond as target.

The only apparent terminal candidates in the strongest miss capture are:

| Session | Packet order | Exact result | Correlation decision |
|---|---|---|---|
| `20260719-020104` | player `AttackInfo #2401` -> Vagabond `StopFight #2403` -> Vagabond `CharacterAction action=99 #2405` | source `0x7944C065`, target `0x797B885D`, amount `111`, ammo `3`, slot `8`, damage type `4`, hit type `3`, instance `0` | player-owned lethal hit against the Vagabond; not a Vagabond outgoing stream |
| `20260719-020104` | player `AttackInfo #2478` -> Vagabond `StopFight #2480` -> Vagabond `CharacterAction action=99 #2482` | source `0x7944C065`, target `0x797B885C`, amount `156`, ammo `11`, slot `6`, damage type `4`, hit type `3`, instance `0` | player-owned lethal hit against the Vagabond; not a Vagabond outgoing stream |

No target-health change was assigned to a Vagabond by proximity. Interleaved
player attacks remain player attacks because their raw source identity is
`0x7944C065`. The two terminal results are existing player attack outcomes, not
extra Vagabond streams.

## Contract and ownership decision

The proven Vagabond initiation/miss stream count is one. The proven complete
landed combat stream count is zero. Consequently there is no archetype ID and
no actor can pass the restoration gate.

Capture-bound fields remain unresolved where the miss packet cannot prove
them: landed hit type, landed damage type, landed weapon slot/instance,
normal/critical/terminal distinctions, and exact complete WIFU/SAW/Attack/
AttackInfo ordering. These were not moved into production.

Existing production owners remain unchanged:

- active spawn data owns level, health, scale, and movement statistics;
- `NpcCombatAttackRules` and the active combat definition own existing private
  damage/range/cadence policy;
- the shared combat coordinator and packet factory own scheduling, cancellation,
  lethal health calculation, and per-actor mutable Energy/ammunition state.

Those owners cannot supply missing capture-bound packet identity. In
particular, the pre-existing private Mugger-range damage policy is not evidence
for Vagabond hit type, damage type, weapon identity, or landed packet shape and
is not used to certify these actors.

## Extractor repair and deterministic result

The generic `MissedAttackInfo` decoder now names the two embedded identities
`attacker` and `defender`. `PacketRecord.source`/`target` and the generic source
projection use those identities for this message family. Correlation retains
the exact preceding SAW and Attack, optional WIFU, raw miss fields, packet
order, and explicit missing evidence. Aggregation retains attacker, defender,
N3 source, miss fields, packet-order proof, and the miss packet reference.

The deterministic full-corpus result remains:

- 364 sessions discovered
- 348 canonical sessions
- 2,647 complete `AttackInfo` chains
- 243 capture-certified profiles
- 92 runtime-ready profiles
- 290 semantic definitions
- 100 runtime-ready definitions
- 1,286 unresolved profiles
- zero decode/projection errors
- zero recoverable evidence blockers

No generated runtime catalog definition or exact-byte fixture was added because
no complete Vagabond contract exists. The active inventory input hash was
updated to the regenerated inventory; active classifications and counts are
unchanged.

## Final count reconciliation

| Scope | Before | After |
|---|---:|---:|
| PF127 | 238 certified / 84 quarantined | 238 certified / 84 quarantined |
| PF1931 | 87 certified / 80 quarantined | 87 certified / 80 quarantined |
| Combined | 325 certified / 164 quarantined | 325 certified / 164 quarantined |
| Violent Vagabond | 0 certified / 22 quarantined | 0 certified / 22 quarantined |
| Total denominator | 489 | 489 |

There are 22 actor-level rejection rows and 22 unique quarantined Vagabond
actors. No duplicate rejection, borrowed neighboring level, fallback contract,
or identity whitelist was introduced.
