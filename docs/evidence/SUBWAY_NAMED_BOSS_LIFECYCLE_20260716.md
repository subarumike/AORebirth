# Subway Named-Boss Lifecycle Evidence — 2026-07-16

## Scope

This note records the lifecycle facts used for Abmouth Supremus and Vergil
Aeneid in Subway resource/playfield `127`. Boss respawn, loot-bearing corpse
lifetime, and empty-corpse cleanup are independent timers.

## Mike-confirmed live behavior

- Abmouth Supremus respawns exactly `10 minutes` after death.
- Vergil Aeneid respawns exactly `10 minutes` after death.
- A loot-bearing Abmouth corpse has a `30-minute` temporary lifetime.
- A loot-bearing Vergil corpse reports `Temporary: 29m` when inspected shortly
  after death, establishing the same configured `30-minute` lifetime.
- Every corpse in the game uses a `3-second` cleanup after all items and credits
  are gone, including corpses that were empty when created.

These are direct live-client timing and info-panel observations supplied by
Mike. They are not inferred from an incomplete respawn projection.

## Finalized capture `20260716-220400`

The capture is complete and its authoritative raw traffic is intact. It proves:

- A new L30 Abmouth spawned with `10324` HP, scale `162`, RunSpeed `115`, and
  monsterData `155962` at `2026-07-17T03:09:42.113Z`.
- Abmouth died at `03:10:27.064Z`; the 415-byte corpse full update followed
  `0.500s` later with CATMesh `155548`, scale `162`, and `587` credits.
- The dead NPC actor despawned about `10.079s` after death.
- The new Abmouth spawned while an older loot-bearing Abmouth corpse still
  existed and could be reopened. Named-boss respawn therefore cannot wait for
  corpse cleanup.
- Four local-player-facing Abmouth hits were `125`, `95`, `123`, and `74`.
  Ten hits against the player-owned Healer and Wrath Incarnation pets were
  `77..138` and remain separate from player-facing damage.
- Two L24/968-HP Infectors spawned approximately `1.18s` and `2.21s` after
  Abmouth began attacking and disappeared immediately after the boss death.

The capture does not independently measure the ten-minute respawn interval:
the preceding Abmouth death occurred before capture start, so
`enemy-respawns.csv` correctly leaves the correlation incomplete. The exact
ten-minute value comes from Mike's live timing confirmation.

## Second linked Abmouth loot snapshot

The new Abmouth corpse contains one atomic `587`-credit snapshot:

- `202741/202742`, QL32, quantity 1
- `202734/202735`, QL32, quantity 1
- `202717/202718`, QL32, quantity 1
- `85723/85722`, QL32, quantity 1
- `123968/123970`, QL25, quantity 1
- `287146/287146`, QL200, quantity 1

The client reused corpse identity `F69001`: the previous Vergil corpse was
despawned before Abmouth's new corpse full update. The original live projection
retained the stale Vergil label. Generation-aware offline reconstruction now
rebinds that row to Abmouth `(SimpleChar:7970254F)`, resets it to open ordinal
`1`, and restores monsterData `155962`, level `30`, and `587` credits. Runtime
replay keeps this result atomic and does not mix its slots with the older
Abmouth snapshot. Selection probabilities and the wider boss loot pool remain
unresolved.

## Runtime boundary

- Schedule each named boss from the death timestamp, not dead-NPC despawn or
  corpse removal.
- Permit a respawn while an older loot-bearing corpse remains.
- Keep loot-bearing boss corpses for `1800` seconds.
- Start the universal `3`-second cleanup only when both item loot and credits
  are empty.
- Live and offline capture correlation must key corpse observations by identity
  generation so a reused identity cannot inherit the previous corpse's enemy,
  credits, or open ordinal.
