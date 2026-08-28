# AO Spawn Population Reconstruction

## Result

The first deterministic population layer is implemented. It keeps official ACG topology, server-selected runtime MonsterData/archetypes, and transient runtime identities separate. Exact-row identity remains zero; useful local and playfield population scopes are recorded without reopening the nonexistent static ACG-to-MonsterData bridge.

## Model

```text
visual archetype -> contextual runtime variant -> spawn population -> ACG placements -> transient instances
```

Official topology contains 32805 placements. Shared ACG policy tags inside official districts are direct structural groups. A fixed 25m three-dimensional connected component is retained only as a heuristic secondary cluster and never becomes official semantics.

## Association scopes

- Exact placement: 0
- Local population: 766
- Playfield population: 1397
- Unassociated: 1029
- Conflict: 280

Exact placement requires the resolver's proven base playfield plus one unique exact coordinate candidate. Local population requires one topology population plus governed overlay evidence or repeated stable MonsterData with changing transient runtime IDs. Proximity alone remains a blocked candidate.

## Runtime population reuse

Captured evidence contains 3472 observations, 165 MonsterData IDs, and 159 exact visual archetypes.

Top reused archetypes:

| Archetype | Populations | Playfields | ACG placements | Observations | Levels | Names |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| `archetype-dbb46cbb8be5546a` | 29 | 2 | 37 | 111 | 2..41 | Gnarl the Roller, Grudgespine the Screaming Roller, Hatespine the Corrupted Roller, Rollerrat, Scourge Rollerrat |
| `archetype-ec5555cf58e2220d` | 17 | 1 | 1 | 51 | 10..250 | Adri Afeli, Adventurer of IPS, Basic Quality Weaponsdealer, Cody Monkie, Doctor of IPS |
| `archetype-0511d362f6791ebb` | 15 | 1 | 2 | 42 | 6..210 | Advanced Quality Weaponsdealer, Careless Citizen, Christopher Villalba, Clan Bartender, Dion Giscombe |
| `archetype-1ccaaac2abc51af3` | 13 | 1 | 1 | 24 | 3..195 | Agent of IPS, Bartender, Bureaucrat of IPS, Dockworker, Female Captain |
| `archetype-73143d6a69558639` | 11 | 3 | 15 | 88 | 1..5 | 32-V Docker, 34-I Helper, Cargo Droid, Engineer Automaton I, IIV-X Advanced Docker |
| `archetype-15284a6aaca17059` | 11 | 2 | 4 | 47 | 5..220 | Antonio Stacklund, Bruiser, Chang Meloche, Earl Dublin, Emery Annunziata |
| `archetype-0adbc252bc94694e` | 10 | 0 | 13 | 100 | 1..7 | Garbage Flea, Mutated Garbage Flea |
| `archetype-d310666d074bba2c` | 9 | 1 | 33 | 390 | 1..5 | Burning Cleaning Robot, Cleaning Robot, Cleanmeister Intelligence Robot, Malfunctioning Cleaning Robot |
| `archetype-b457cf088c8fd9a2` | 9 | 1 | 27 | 130 | 2..4 | Supreme Collector of Waste, Waste collector, Waste Collector |
| `archetype-85316bb23b707f3e` | 9 | 1 | 8 | 87 | 5..109 | Cross-Wired Junkbot, Engineer Guardbot, Sparepart the Corroded |
| `archetype-e959fe7bf87d9380` | 9 | 0 | 2 | 69 | 6..250 | Carol Schieffer, Clan Protester, Delois Guiney, Fia Lou, Food Provider |
| `archetype-79fce60dbc95f0ba` | 9 | 1 | 13 | 39 | 5..40 | Dockworker, Enforcer of IPS, Engineer of IPS, Rex Larsson, Shady Guy |
| `archetype-615c258585f61187` | 9 | 1 | 12 | 33 | 5..40 | Dockworker, Melvina Sandine, Trader of IPS |
| `archetype-36410b296d8f7bd4` | 9 | 2 | 6 | 20 | 30..40 | Craig-Or, Craig-Or of Flaming Barrels, Craig-Or of Gear & Ammo, Craig-Or of Preservation, Craig-Or of Protection |
| `archetype-4d9a0ec31c8218d6` | 8 | 1 | 12 | 58 | 2..5 | Dockworker, Protester, Violent Protester |
| `archetype-dbcdac694d276e41` | 8 | 0 | 0 | 19 | 13..220 | Advanced Predator M-30, Grammarr, Killer Mechdog, Lilengi160, Luna |
| `archetype-96421bebf1e93b95` | 7 | 2 | 1 | 156 | 1..2 | Beach Leet, Eleet, Flurryflutter the Phearsome Smasher, Flurryflutter the Troubled Smasher, Leet |
| `archetype-80873d61835db8d0` | 7 | 1 | 4 | 33 | 1..19 | Concrete Adder, Giant Snake, Shore Snake, Slipcoil Banetwister, Slipslidder Banetwister |
| `archetype-0198fd6b97381e87` | 7 | 1 | 0 | 20 | 1..125 | Agent Deth, KillzJoo, ALLOFF, Anger Manifestation, Frenzy Embodiment |
| `archetype-b866af61a08d0c44` | 7 | 2 | 1 | 9 | 100..250 | Bodyguard Logan Fixx, Omni Unicorn Squadleader Fixx, Unicorn Squadleader |

## Leet study

Leet evidence forms 9 runtime populations across 2 captured playfield contexts, levels 1..15. Visual sameness is explicitly not gameplay, level, or loot sameness.

## PF4582 study

PF4582 retains 207 official placements across 77 topology populations and 70 observed runtime populations. The historical 25/181 count is superseded; 199/7 is the specialized catalog and 199/8 is the 207-row official overlay including NCNN. No runtime definition was changed.

## Borealis study

Borealis-related evidence forms 6 runtime populations. Guide and Guard preserve their exact captured appearance, but neither has exact placement identity; their candidate base-playfield relationship remains ambiguous.

## Readiness

- Visual ready: 480
- Population identity ready: 22
- Level evidence: 482
- Combat evidence: 312
- Loot evidence: 43
- Respawn ready: 0
- Exact placement ready: 0

Readiness is population-specific. Finite loot observations remain contextual samples, and movement envelopes use only captured movement. A current moved position never becomes a spawn position.

## Acceptance

```text
SPAWN_POPULATION_RECONSTRUCTION_IMPLEMENTED=YES
ACG_PLACEMENTS=32805
MONSTER_DATA_RECORDS=1470
EXACT_VISUAL_ARCHETYPES=1360
STRUCTURAL_FAMILIES=750
RUNTIME_OBSERVATIONS=3472
RUNTIME_MONSTERDATA=165
RUNTIME_ARCHETYPES=159
EXACT_PLACEMENT_ASSOCIATIONS=0
LOCAL_POPULATION_ASSOCIATIONS=766
PLAYFIELD_POPULATION_ASSOCIATIONS=1397
UNASSOCIATED_RUNTIME_OBSERVATIONS=1029
CONFLICTING_RUNTIME_OBSERVATIONS=280
SPAWN_POPULATIONS=18423
SPAWN_POPULATIONS_WITH_RUNTIME_EVIDENCE=482
SPAWN_POPULATIONS_WITH_VISUAL_READY=480
SPAWN_POPULATIONS_WITH_POPULATION_IDENTITY_READY=22
SPAWN_POPULATIONS_WITH_LEVEL_EVIDENCE=482
SPAWN_POPULATIONS_WITH_COMBAT_EVIDENCE=312
SPAWN_POPULATIONS_WITH_LOOT_EVIDENCE=43
SPAWN_POPULATIONS_WITH_RESPAWN_EVIDENCE=0
SPAWN_POPULATIONS_WITH_EXACT_PLACEMENT_READY=0
ACG_PLACEMENTS_WITH_POPULATION_EVIDENCE=237
ACG_PLACEMENTS_WITHOUT_POPULATION_EVIDENCE=32568
LEET_POPULATIONS=9
PF4582_POPULATIONS=109
BOREALIS_POPULATIONS=6
STATIC_ACG_MONSTERDATA_BRIDGE_SEARCH_REOPENED=NO
ACGHASH_USED_AS_MONSTERDATA=NO
RUNTIME_ID_USED_AS_PERSISTENT_IDENTITY=NO
HEURISTIC_EXACT_MATCHES=0
RUNTIME_NPC_DEFINITIONS_MODIFIED=NO
TESTS=PASS
DETERMINISTIC_REPEAT_RUN=YES
DETERMINISTIC_DIGEST=c9310e2623818f677a96aaeb91580a03cc639115ba8e5c2c3b8be8ce337a2ac5
COMMIT=PENDING
```
