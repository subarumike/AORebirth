# Subway Discarded Pet Evidence Audit

## Runtime decision

Discarded Pet is ordinary PF127 content with 29 fixed capture rows. The existing
corpus is sufficient to activate all 29 rows without another live capture.
Runtime keeps retaliation plus shared chase, the ordinary four-minute respawn
policy, and `3/240/3` regular-corpse cleanup. No proactive radius, leash,
return-home rule, patrol route, or critical chance is synthesized. The eleven
newly enabled rows still require a bounded private-client login/traversal smoke
before the configured activation can be called runtime-safe.

## Population

The 29 fixed rows preserve six exact captured shapes:

| Level | Health | MonsterScale | RunSpeed |
| ---: | ---: | ---: | ---: |
| 5 | 115 | 93 | 24 |
| 6 | 138 | 93 | 28 |
| 7 | 160 | 94 | 32 |
| 8 | 183 | 94 | 36 |
| 9 | 205 | 95 | 40 |
| 10 | 227 | 95 | 44 |

The eleven rows promoted from diagnostic quarantine are `79557C09`,
`79557C26`, `79557C31`, `79557C8B`, `79557CA7`, `79557CAB`, `79557CAD`,
`7957E411`, `7957E4A5`, `7957E4B1`, and `7957E4BC`.

## Combat

Focused official-live evidence in `20260708-143600` plus
`20260709-210452` contains 37 normal local-player hits spanning `9..18`.
Four critical hits span `30..33` and remain report-only. All landed SIW1
AttackInfo rows use ammo `-1`, slot `0`, unknown `0`, and weapon instance
`0x53495731`.

Thirty same-source landed-hit intervals span `4.609299..5.950416` seconds.
Their conventional median is `5.089763` seconds. Player attack to the pet's
Attack start spans `0.440707..1.680932` seconds, proving retaliation. A raw
combat path for identity `794ADBEF` moves approximately `8.153` units and
proves chase. Every reviewed fight ends in the pet's death, so disengage,
reset, leash, and return-home boundaries remain unobserved.

Raw SpecialAttackWeapon packets use templates `0x23566/0x23567` and tag SIW1.
The first four numeric fields are stable by level: L5 `30`, L6 `35`, L7 `39`,
L8 `45`, L9 `49`, and L10 `54`. The fifth field varies across the capture and
its rule is unresolved. Runtime therefore preserves the captured AttackInfo
shape and damage/cadence without inventing a stable fifth-field value.

## Loot and corpse

Sixteen reviewed complete first opens contain 13 positive and three empty
inventories. The 13 exact item/QL memberships form an explicitly incomplete
pool. Corpse CATMesh is `15929`; exact credits are L5 `18`, L6 `21`, L7 `25`,
L8 `28`, L9 `32`, and L10 `35`.

Legacy capture `20260708-004038` contributes an exact L10/35-credit corpse by
joining its fully decoded SCFU identity `794A16EE` to the corpse full update.
Capture `20260709-205921` contributes an exact L6/21-credit corpse for identity
`7953178A`. Both joins are generator-validated so they cannot silently drift.

Five death-to-later-observation links provide only `330..460` second upper
bounds. They do not establish an exact official respawn timer, so the ordinary
private four-minute policy remains explicit policy rather than a capture claim.
