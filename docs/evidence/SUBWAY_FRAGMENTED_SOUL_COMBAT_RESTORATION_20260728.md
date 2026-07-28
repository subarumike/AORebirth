# Subway Fragmented Soul Combat Restoration

Date: 2026-07-28

## Result

Fragmented Soul MonsterData `203729` now resolves one bounded,
capture-backed equipped-weapon combat setup for actor levels `17..21`.
The restoration uses the actor level and exact owner-selected weapon loadout;
runtime source identity is not a reusable combat key.

All ten active PF127 Fragmented Soul actors and all nineteen owner-defined
atomic generation variants resolve through the shared captured combat path.
The six actors that were quarantined at the start of this slice are restored.
The four already-certified reference actors remain certified.

## Starting Runtime Scope

The accepted runtime baseline was:

- PF127: `270` certified and `52` quarantined.
- PF1931: `87` certified and `80` quarantined.
- Combined PF127/PF1931: `357/132` of `489`.
- Fixed 53-actor remaining-combat scope: `32/21`.
- Fragmented Soul: `4/6`.

The six starting quarantined actors were:

| Source | Active variants |
| --- | --- |
| `0x7954516F` | L17 QL17 `123685/123686`; L18 QL18 `123685/123686` |
| `0x7954517A` | L18 QL20 `123686/123686`; L19 QL19 `123685/123686` |
| `0x7954518B` | L18 QL14 `123685/123686`; L19 QL23 `123687/123688`; L20 QL18 `123685/123686` |
| `0x7954518E` | L18 QL17 `123685/123686`; L18 QL20 `123686/123686` |
| `0x795451AE` | L21 QL25 `123687/123688` |
| `0x79545367` | L18 QL18 `123685/123686`; L18 QL19 `123685/123686` |

The four reference actors are `0x7954516A`, `0x7954518A`,
`0x795451AA`, and `0x79545248`. Together the ten actors have nineteen
atomic generation variants. Those variants remain owned by
`CapturedSubwayOrdinaryContentProvider`; this restoration does not introduce
an actor-identity map or duplicate their level, health, stat, QL, or loadout
selection.

## Raw Capture Evidence

The decisive capture sessions are:

- `20260709-222339`
- `20260709-225408`
- `20260712-223719`
- `20260716-222007`
- `20260720-051714`

Across those sessions the corpus contains twenty-one unique raw
`SpecialAttackWeapon` packets associated with Fragmented Soul from L17
through L21. Ten are members of complete initialization/attack chains and
eleven are orphan combat prefixes. The complete profiles contain twenty-two
complete chain observations because several chains have repeated correlated
`AttackInfo` outcomes.

The complete generated catalog contains eight semantic profiles:

- `76e772d3e32e2f5c-71b81d8ddfcffc8a`
- `76e772d3e32e2f5c-ef9cfa622cae4d22`
- `41ec8ecff96a0c8c-4f4ed102043d7370`
- `41ec8ecff96a0c8c-fedb453533892b94`
- `4b6baa7ad81c6eb1-8aa88308817eb871`
- `d066cff0134deb5d-6cbbbf63bf40882d`
- `d066cff0134deb5d-88401ddd47b9cf67`
- `d066cff0134deb5d-8c9873fc6b350927`

Every complete profile proves the same reusable client-visible semantics:

- equipped attack mode;
- item interpolation family `123685..123703`;
- WIFU slot `6`, instance `0`, state machine `1000015/0`;
- WIFU unknowns `11/262/0`, flags `1027`, count `1`, and initial Energy `25`;
- empty specials and `SpecialAttackWeapon` N3 `0`;
- `Attack` N3 `0` and action `0`;
- one repeating stream, slot `6`, instance `0`;
- numeric hit type `3` and damage type `0`;
- finite mutable ammo beginning at `24`;
- `WeaponItemFullUpdate -> SpecialAttackWeapon -> Attack -> AttackInfo`.

Observed `SpecialAttackWeapon.Unknown5` values `0`, `46`, and `61` are
per-actor ordered mutable state. They are retained as evidence and excluded
from reusable archetype identity. Repeated normal `AttackInfo` rows are
outcomes of the one proven stream, not extra attack streams.

L19 has two exact SAW/Attack prefixes:

- `20260709-222339`, source `0x7954517A`, SAW sequence `6542`, Attack `6543`;
- `20260720-051714`, source `0x7980F12F`, SAW sequence `4465`, Attack `4466`.

Neither prefix has a correlated terminal `AttackInfo`, so neither is promoted
as a standalone generated contract. Their raw SAW numeric fields agree
exactly with the bounded family formula proved by the complete surrounding
levels.

Unsupported `CharacterAction` action `99`, `CastNanoSpell`, miss, and
uncorrelated hit-type `4` observations remain separate report-only outcomes.
They are not converted into normal repeating streams.

## Mathematical Setup

Formula ID:

`fragmented-soul-saw-6L-minus-1-plus-2-floor-L-over-2-v1`

For positive actor level `L`:

```text
base = 6 * L - 1
Unknown1 = base
Unknown2 = base
Unknown3 = base
Unknown4 = base + 2 * floor(L / 2)
```

The production implementation uses checked integer arithmetic and C# positive
integer division. It is allowed only for Fragmented Soul MonsterData `203729`,
levels `17..21`, weapon slot `6`, and an exact item interpolation domain:

- QL `1..19`: `123685/123686`
- QL `20`: `123686/123686`
- QL `21`: `123687/123687`
- QL `22..40`: `123687/123688`

All twenty-one raw SAW packets match exactly. Leave-one-level-out validation
matches at every observed level. A single affine rational expression for
`Unknown4`, tested with floor, ceiling, nearest-away, and nearest-even
rounding through denominator `64`, has zero exact candidates.

Rejected alternatives remain rejected:

- `Unknown4 = 7L - 1`: three mismatches at captured odd levels.
- `Unknown4 = 7L - 2`: two mismatches at captured even levels.
- four identical SAW fields: twenty-one raw-packet mismatches.
- weapon QL alone: at least six active-variant mismatches because the same QL
  occurs at different actor levels with different exact SAW values.
- direct `items.dat` interpolation: it selects the real weapon, damage, range,
  and cadence but does not encode these SAW fields.
- unbounded extrapolation: rejected outside L17..L21.

## Runtime Ownership

Capture owns the family, MonsterData, equipped mode, weapon interpolation
family, WIFU/SAW/Attack/AttackInfo structures and order, slot, instance,
numeric hit type, numeric damage type, and normal-stream semantics.

Existing production owners continue to own:

- atomic actor level, health, damage bonus, attack rating, and defense;
- weapon QL and exact low/high template pair;
- item-derived damage, range, attack delay, and recharge delay;
- runtime Energy and ammunition;
- mutable SAW state;
- actor generation and source identity.

`CapturedSubwayCombatCatalog.ForFragmentedSoul` first validates the selected
owner-linked atomic variant, then invokes the bounded mathematical generator.
The generated contract uses production equipped-weapon values and the shared
capture-backed packet coordinator. The generated profile catalog requires all
eight complete profiles to agree on the exact packet semantics before it
canonicalizes them into the formula-backed archetype. Cross-family,
cross-weapon, out-of-domain, wrong-slot, missing-generation, and genuinely
ambiguous inputs continue to fail closed.

## Coverage Result

The runtime result is:

- PF127: `276` certified and `46` quarantined.
- PF1931: unchanged at `87/80`.
- Combined PF127/PF1931: `363/126` of `489`.
- Fixed 53-actor remaining-combat scope: `38/15`.
- Fragmented Soul: `10/0`.

Exactly six runtime actors are restored. No additional active actor is enabled
outside this family.

The broad generated active-coverage artifact moves from `116/1396` to
`123/1389` across its `1512` actors. Its delta is seven because its earlier
source-local generated classification had one stale false-unresolved
Fragmented Soul row that the runtime catalog already resolved. The accepted
runtime baseline and focused runtime-resolution test remain the authority for
the six-actor restoration count.

No live AO client was launched. Private-client combat observation remains a
separate manual validation boundary.
