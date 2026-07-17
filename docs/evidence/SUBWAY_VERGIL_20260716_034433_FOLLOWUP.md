# Subway Vergil Aeneid Follow-up Capture

## Scope and conclusion

Official-live capture `20260716-034433` is a mixed PF127 fight. Only rows tied
to Vergil Aeneid (`SimpleChar:796CD762`, MonsterData `203748`) are promoted for
Vergil. Killer (`SimpleChar:796D400B`) is the local player's pet, so attacks
against Killer remain a separate player-owned-pet evidence category and never
contribute to player-facing damage.

The capture adds a third exact Vergil variant, a complete third corpse snapshot,
and additional target-specific combat evidence. It does not replace the
weapon-owned damage model or establish repeat cadence, exact aggro radius,
respawn timing, LOS/navigation behavior, or a new weapon template.

## Exact variant and corpse

- Vergil is observed at level 29 with `6796/6796` health and RunSpeed `131`.
- The exact 420-byte CorpseFullUpdate links dead NPC `796CD762` to
  `Corpse:00F69001`, CATMesh `5921`, MonsterData `203748`, MonsterScale `131`,
  and `563` credits.
- The later local identity is the normalized form `Corpse:F69001`. The raw
  death, CorpseFullUpdate, Use, InventoryUpdate, and item-transfer chronology
  makes the linkage complete. The derived `unlinked` label is only the former
  padded-versus-unpadded identity projection mismatch and does not require a
  repeat capture.

Runtime promotion:

- The captured variant set is now L29/6796/scale 131/RunSpeed 131,
  L30/7227/scale 132/RunSpeed 135, and L31/7659/scale 132/RunSpeed 140.
- The shared appearance packet retains SCFU RunSpeedBase `134`; this capture
  has no alive L29 SCFU from which to replace that field.
- Healing is exact-level and fail-closed: L31 keeps its captured direct heal,
  L30 keeps its captured self-heal, and L29 does not inherit either behavior.
- The Vergil-specific corpse builder now writes MonsterScale from the selected
  runtime variant instead of leaving the old template value unchanged.

## Complete third loot snapshot

The initial inventory for normalized corpse `F69001` is one indivisible
items-plus-credits observation:

| Slot | Low/high template | QL | Quantity |
| ---: | --- | ---: | ---: |
| 0 | `202734/202735` | 33 | 1 |
| 1 | `301715/301715` | 1 | 1 |
| 2 | `160051/160050` | 24 | 1 |
| 3 | `21605/21605` | 1 | 100 |
| 4 | `287146/287146` | 200 | 1 |

The three observed Vergil corpses are replayed only as complete atomic
snapshots, including their linked credit outcomes:

- `20260712-232711`: its exact three items plus `610` credits.
- `20260712-234401`: its exact three items plus `587` credits.
- `20260716-034433`: the exact five items above plus `563` credits.

The private runtime may select among those observed snapshots, but the official
selection probabilities and wider loot pool remain unresolved. Items or credits
from different captured corpses must not be combined.

## Target-specific combat evidence

Vergil starts four attacks across the indexed captures. Capture
`20260716-034433` contributes five normal weapon hits against the local player
at `22..23` damage and three normal weapon hits against Killer at `23..28`
damage. All eight rows use weapon slot `6`, unknown field `0`, and weapon
instance `0`.

The generated combat report keeps local-player rows in the existing top-level
runtime-facing fields and preserves Killer rows under a separate
`playerOwnedPet` sidecar. The QL23 Cast-Off E-Beamer template `122123` remains
the prior weapon evidence because this capture contains no weapon full update.
Damage and recharge overrides remain zero so the equipped weapon and target
mitigation continue to own runtime rolls.

## Deliberately unresolved

- No repeat cadence is derived from this retarget-heavy player-and-pet fight.
- SpecialAttackWeapon headers and ammo states vary and do not justify replacing
  the existing global protocol shape.
- Proactive attack is observed, but the maximum aggro boundary is not.
- One two-point movement path does not establish new chase, LOS, doorway, or
  leash behavior.
- The post-death window is too short to establish Vergil respawn timing.
- No Vergil nano cast is present; unrelated nearby buff traffic is excluded.
