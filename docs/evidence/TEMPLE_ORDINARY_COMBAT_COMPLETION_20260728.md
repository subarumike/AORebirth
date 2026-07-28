# Temple ordinary-combat completion — 2026-07-28

## Outcome

This pass processed every actor in the starting `80`-actor PF1931
ordinary-combat quarantine. `78` actors are restored and `2` remain
fail-closed:

- all `76` quarantined Cultists are restored;
- Eternal Sentinel L20 `0x7983FA26` is restored;
- Murial the Faithful `0x7987F12D` is restored;
- Eternal Sentinel L18 `0x7983FA22` and `0x7983FBC2` remain quarantined.

The fixed PF127/PF1931 checkpoint moves from `377` certified / `112`
quarantined to `455` / `34` across `489` actors. PF127 remains `290/32`.
PF1931 moves from `87/80` to `165/2`.

The active-coverage artifact represents the `153` Temple ordinary-provider
rows directly as `151` certified / `2` unresolved. The broader fixed PF1931
checkpoint also includes the `14` previously certified Temple actors outside
that provider surface, yielding `165/2`.

## Capture and formula proof

The Cultist formula dataset contains `35` complete profile observations,
`99` exact landed AttackInfo packet references, and seven cross-family
held-out validations. Its complete observations come from:

- `20260528-190456`
- `20260721-031913`
- `20260721-032547`
- `20260721-033006`
- `20260721-043204`
- `20260721-052115`
- `20260721-230426`
- `20260722-042930`

For actor level `L` in the exact bounded domain `20..35`, define `base`:

```text
L20..L25: floor((31L - 10) / 2)
L26..L33: 17L - 42 - (L bitwise-and 1)
L34..L35: 17L - 43
```

The exact SpecialAttackWeapon numeric fields are:

```text
Unknown1 = base + 20 for MonsterData 26135; base otherwise
Unknown2 = base
Unknown3 = base
Unknown4 = floor((L + 4) / 2) for L20..L25
           floor((L + 6) / 2) for L26..L35
Unknown5 = existing ordered per-actor mutable state
```

Every observed value is reproduced exactly. L22, which was absent from the
complete numeric observations, is structurally held out and evaluates to
`336/336/336/13`. The domain rejects levels below 20 and above 35.

Rejected alternatives were a single unbounded affine expression, weapon-QL
lookup, source-identity lookup, nearest-level substitution, and any domain
outside L20..L35.

## Exact semantics retained

Capture continues to own:

- MonsterData and the seven distinct Cultist weapon families;
- equipped attack mode;
- right-hand slot `6` and AttackInfo instance `0`;
- WIFU outer shape and ordered stat identifiers;
- empty SpecialAttackWeapon special list;
- Attack N3/action `0/0`;
- one normal AttackInfo stream with numeric hit/damage types `3/0`;
- `WIFU -> SpecialAttackWeapon -> Attack -> AttackInfo` order;
- real weapon-pair, finite-ammunition, and raised-primary differences.

Production continues to own actor level, active WIFU QL and template
selection, damage, range, cadence, Energy/ammunition, and ordered mutable SAW
state. No runtime identity selector, nearest-level choice, copied raw value,
generic fallback, or cross-enemy archetype was added.

The binder canonicalizes only generated contracts whose packet semantics are
already identical; production-selected QL and mutable observations no longer
split those otherwise exact contracts. Multiple genuinely different semantic
contracts still fail closed.

## Non-Cultist dispositions

- Eternal Sentinel `0x7983FA26`, MD `41690`, L20: exact active
  `123381/123382` QL16 WIFU plus generated semantic profiles
  `e037cf6f4165eff5-71ebcc342951c27c` and
  `e037cf6f4165eff5-c036f50d1289554a`.
- Murial `0x7987F12D`, MD `26090`, L34: exact `122180/122181` QL36 WIFU
  from `20260721-232051`, exact five-hit 26-point stream, slot `6`, ammo `-1`,
  instance `0`, and SAW `258/258/258/21/0`.
- Eternal Sentinel `0x7983FA22`, MD `41690`, L18:
  `123381/123382` QL15 WIFU/start/miss evidence exists, but no complete
  same-level landed normal AttackInfo contract exists.
- Eternal Sentinel `0x7983FBC2`, MD `41690`, L18:
  `123383/123384` QL22 WIFU/start/miss evidence exists, but no complete
  same-level landed normal AttackInfo contract exists.

## Starting quarantine actor matrix

| Source | Family | MD | L | Exact active WIFU | Contract | Final | Exact blocker |
|---|---|---:|---:|---|---|---|---|
| 0x79822FDF | Cultist | 26149 | 20 | 124313/124314 QL23 slot 6 | base | restored | - |
| 0x79834AFB | Cultist | 26082 | 21 | 130163/130164 QL19 slot 6 | base | restored | - |
| 0x79834B66 | Cultist | 26149 | 24 | 124313/124314 QL21 slot 6 | base | restored | - |
| 0x79834B77 | Cultist | 26149 | 25 | 124313/124314 QL22 slot 6 | base | restored | - |
| 0x79834CB9 | Cultist | 26147 | 35 | 144103/144104 QL28 slot 6 | base | restored | - |
| 0x79834CC7 | Cultist | 26137 | 35 | 204747/204747 QL1 slot 6 | base | restored | - |
| 0x79834CC9 | Cultist | 26137 | 22 | 204747/204747 QL1 slot 6 | base | restored | - |
| 0x79834D07 | Cultist | 26082 | 21 | 130163/130164 QL22 slot 6 | base | restored | - |
| 0x79834D0D | Cultist | 26082 | 22 | 130163/130164 QL25 slot 6 | base | restored | - |
| 0x79834DA6 | Cultist | 26149 | 33 | 124314/124314 QL32 slot 6 | base | restored | - |
| 0x79834DA8 | Cultist | 26137 | 31 | 204747/204747 QL1 slot 6 | base | restored | - |
| 0x79834DCB | Cultist | 26149 | 22 | 124313/124314 QL19 slot 6 | base | restored | - |
| 0x79834DCE | Cultist | 26074 | 20 | 204747/204747 QL1 slot 6 | base | restored | - |
| 0x79834DCF | Cultist | 26103 | 28 | 129028/129029 QL29 slot 6 | base | restored | - |
| 0x79834DD0 | Cultist | 26149 | 31 | 124314/124314 QL32 slot 6 | base | restored | - |
| 0x79834DDF | Cultist | 26074 | 28 | 204747/204747 QL1 slot 6 | base | restored | - |
| 0x79834DE9 | Cultist | 26135 | 28 | 158298/158299 QL27 slot 6 | raised-primary | restored | - |
| 0x79834DF3 | Cultist | 26137 | 33 | 204747/204747 QL1 slot 6 | base | restored | - |
| 0x79834E50 | Cultist | 26103 | 23 | 129028/129029 QL28 slot 6 | base | restored | - |
| 0x79834EC9 | Cultist | 26074 | 22 | 204747/204747 QL1 slot 6 | base | restored | - |
| 0x7983F8FD | Cultist | 26135 | 20 | 158298/158299 QL15 slot 6 | raised-primary | restored | - |
| 0x7983F8FE | Cultist | 26147 | 24 | 144103/144104 QL29 slot 6 | base | restored | - |
| 0x7983F9F4 | Cultist | 26149 | 26 | 124313/124314 QL26 slot 6 | base | restored | - |
| 0x7983FA22 | Eternal Sentinel | 41690 | 18 | 123381/123382 QL15 slot 6 | exact | quarantined | no complete same-level landed normal AttackInfo contract |
| 0x7983FA26 | Eternal Sentinel | 41690 | 20 | 123381/123382 QL16 slot 6 | exact | restored | - |
| 0x7983FA5D | Cultist | 26147 | 35 | 144103/144104 QL28 slot 6 | base | restored | - |
| 0x7983FAC2 | Cultist | 26147 | 22 | 144103/144103 QL21 slot 6 | base | restored | - |
| 0x7983FAE0 | Cultist | 26149 | 23 | 124313/124314 QL28 slot 6 | base | restored | - |
| 0x7983FAE3 | Cultist | 26103 | 29 | 129028/129029 QL23 slot 6 | base | restored | - |
| 0x7983FAEF | Cultist | 26147 | 22 | 144103/144103 QL21 slot 6 | base | restored | - |
| 0x7983FB02 | Cultist | 26074 | 23 | 204747/204747 QL1 slot 6 | base | restored | - |
| 0x7983FB03 | Cultist | 26103 | 26 | 129028/129029 QL25 slot 6 | base | restored | - |
| 0x7983FB27 | Cultist | 26135 | 30 | 158298/158299 QL29 slot 6 | raised-primary | restored | - |
| 0x7983FB2A | Cultist | 26074 | 28 | 204747/204747 QL1 slot 6 | base | restored | - |
| 0x7983FB2E | Cultist | 26147 | 28 | 144104/144104 QL30 slot 6 | base | restored | - |
| 0x7983FB3C | Cultist | 26074 | 20 | 204747/204747 QL1 slot 6 | base | restored | - |
| 0x7983FB3D | Cultist | 26137 | 24 | 204747/204747 QL1 slot 6 | base | restored | - |
| 0x7983FB3E | Cultist | 26147 | 26 | 144103/144104 QL23 slot 6 | base | restored | - |
| 0x7983FB40 | Cultist | 26149 | 28 | 124313/124314 QL24 slot 6 | base | restored | - |
| 0x7983FB43 | Cultist | 26149 | 23 | 124313/124314 QL29 slot 6 | base | restored | - |
| 0x7983FB85 | Cultist | 26149 | 26 | 124313/124314 QL28 slot 6 | base | restored | - |
| 0x7983FB88 | Cultist | 26149 | 29 | 124314/124314 QL32 slot 6 | base | restored | - |
| 0x7983FB8F | Cultist | 26149 | 21 | 124313/124314 QL24 slot 6 | base | restored | - |
| 0x7983FB93 | Cultist | 26149 | 29 | 124314/124314 QL32 slot 6 | base | restored | - |
| 0x7983FB97 | Cultist | 26137 | 34 | 204747/204747 QL1 slot 6 | base | restored | - |
| 0x7983FB9A | Cultist | 26147 | 21 | 144103/144104 QL24 slot 6 | base | restored | - |
| 0x7983FB9F | Cultist | 26149 | 22 | 124313/124314 QL17 slot 6 | base | restored | - |
| 0x7983FBA1 | Cultist | 26149 | 22 | 124313/124314 QL24 slot 6 | base | restored | - |
| 0x7983FBA2 | Cultist | 26137 | 25 | 204747/204747 QL1 slot 6 | base | restored | - |
| 0x7983FBA4 | Cultist | 26082 | 34 | 130164/130164 QL34 slot 6 | base | restored | - |
| 0x7983FBA5 | Cultist | 26082 | 22 | 130163/130164 QL17 slot 6 | base | restored | - |
| 0x7983FBA6 | Cultist | 26082 | 22 | 130163/130164 QL26 slot 6 | base | restored | - |
| 0x7983FBAD | Cultist | 26147 | 35 | 144103/144104 QL29 slot 6 | base | restored | - |
| 0x7983FBB6 | Cultist | 26135 | 35 | 158298/158299 QL31 slot 6 | raised-primary | restored | - |
| 0x7983FBB8 | Cultist | 26135 | 35 | 158298/158299 QL34 slot 6 | raised-primary | restored | - |
| 0x7983FBC2 | Eternal Sentinel | 41690 | 18 | 123383/123384 QL22 slot 6 | exact | quarantined | no complete same-level landed normal AttackInfo contract |
| 0x7983FBD0 | Cultist | 26103 | 24 | 129028/129029 QL24 slot 6 | base | restored | - |
| 0x7983FBDA | Cultist | 26149 | 20 | 124313/124314 QL21 slot 6 | base | restored | - |
| 0x7983FBE1 | Cultist | 26149 | 30 | 124314/124314 QL32 slot 6 | base | restored | - |
| 0x7983FBE7 | Cultist | 26103 | 21 | 129028/129029 QL22 slot 6 | base | restored | - |
| 0x7983FC33 | Cultist | 26074 | 27 | 204747/204747 QL1 slot 6 | base | restored | - |
| 0x7983FC39 | Cultist | 26149 | 22 | 124313/124314 QL23 slot 6 | base | restored | - |
| 0x7983FC3A | Cultist | 26135 | 35 | 158298/158299 QL30 slot 6 | raised-primary | restored | - |
| 0x7983FC43 | Cultist | 26149 | 30 | 124313/124314 QL25 slot 6 | base | restored | - |
| 0x7983FC46 | Cultist | 26082 | 20 | 130163/130164 QL23 slot 6 | base | restored | - |
| 0x7984B36E | Cultist | 26074 | 20 | 204747/204747 QL1 slot 6 | base | restored | - |
| 0x7984B3A8 | Cultist | 26082 | 28 | 130164/130164 QL34 slot 6 | base | restored | - |
| 0x7984B3AB | Cultist | 26135 | 28 | 158298/158299 QL28 slot 6 | raised-primary | restored | - |
| 0x7984B3DF | Cultist | 26135 | 26 | 158298/158299 QL26 slot 6 | raised-primary | restored | - |
| 0x79853114 | Cultist | 26137 | 33 | 204747/204747 QL1 slot 6 | base | restored | - |
| 0x7985316C | Cultist | 26137 | 31 | 204747/204747 QL1 slot 6 | base | restored | - |
| 0x798537AE | Cultist | 26137 | 34 | 204747/204747 QL1 slot 6 | base | restored | - |
| 0x7985EC38 | Cultist | 26147 | 30 | 144103/144104 QL24 slot 6 | base | restored | - |
| 0x7985EE29 | Cultist | 26135 | 35 | 158298/158299 QL42 slot 6 | raised-primary | restored | - |
| 0x7985EE30 | Cultist | 26147 | 35 | 144104/144104 QL30 slot 6 | base | restored | - |
| 0x79872BFA | Cultist | 26074 | 35 | 204747/204747 QL1 slot 6 | base | restored | - |
| 0x79872FE9 | Cultist | 26074 | 33 | 204747/204747 QL1 slot 6 | base | restored | - |
| 0x7987F12D | Murial the Faithful | 26090 | 34 | 122180/122181 QL36 slot 6 | exact | restored | - |
| 0x7987F146 | Cultist | 26149 | 25 | 124313/124314 QL20 slot 6 | base | restored | - |
| 0x7987F149 | Cultist | 26103 | 33 | 129028/129029 QL28 slot 6 | base | restored | - |

`base` means `temple-cultist-saw-bounded-level-piecewise-v1`;
`raised-primary` means
`temple-cultist-26135-saw-bounded-level-piecewise-plus-20-v1`.

## Generated and runtime state

- Formula dataset: schema `4`, `151` active Temple bindings, `80` starting
  actor dispositions, `78` restored starting actors, and `2` exact blockers.
- Active coverage Temple ordinary surface: `151` certified / `2` unresolved.
- Existing exact captured contexts continue through the generated exact-byte
  fixtures and the shared packet factory.
- Derived uncaptured integer-level contexts are verified field-by-field against
  the exact categorical packet contract and bounded numeric formula.

## Validation

- Capture extractor self-test: PASS.
- Formula dataset deterministic check under Python 3.12: PASS.
- Active-coverage deterministic check: PASS.
- ZoneEngine production compilation and deployment: PASS through the available
  .NET MSBuild engine after the repository wrapper reported that Visual Studio
  `MSBuild.exe` is absent.
- `git diff --check`: PASS.
- Engine restart: PASS; listeners are active on `6996`, `7012`, `7500`, and
  `7501`.
- The repository-approved test/build wrappers currently cannot run because the
  latest Visual Studio installation does not provide `vstest.console.exe` or
  `MSBuild.exe`; this is an environment dependency, not a code failure.
- A full 374-session regeneration exhausted all three existing isolated
  aggregate-worker retries through native Python interpreter failures at about
  1.6 GB. Generated combat catalog/fixture artifacts were therefore not
  replaced; the unchanged checked-in inventory remains the source for the
  successful narrow formula and active-coverage generation.
