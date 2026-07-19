# Subway Remaining Quarantine Audit

## Decision

The 16 rows below belong to whole-enemy accepted profiles and are now active.
No additional official-live capture is required. Their remaining evidence gap
is bounded AORebirth private-client validation of materialization, client-send,
traversal, combat, death, corpse, and respawn behavior. The only rows still
quarantined are 11 Violent Vagabonds whose profiles lack local-player landed
damage and reset/leash proof.

## Exact accepted-profile rows

| Family | Source identity | Level |
| --- | --- | ---: |
| Deranged Shopper | `79574527` | 8 |
| Looter | `79557CB8` | 10 |
| Looter | `7957E5CD` | 9 |
| Mugger | `79557F14` | 10 |
| Mugger | `7957E5C6` | 9 |
| Mugger | `7957E5C7` | 8 |
| Mugger | `7957E5C8` | 8 |
| Mugger | `7957E5CA` | 10 |
| Stim Fiend | `79557F12` | 11 |
| Stim Fiend | `7957E128` | 12 |
| Stim Fiend | `7957E415` | 9 |
| Stim Fiend | `7957E5CF` | 10 |
| Stim Fiend | `7957E5D0` | 10 |
| Stim Fiend | `7957E5D1` | 10 |
| Disobedient Bot | `79557C66` | 7 |
| Disobedient Bot | `7957E40A` | 10 |

These are diagnostic-manifest ordinals `4`, `10..12`, `16`, `20..21`, `24`,
and `31..38` from capture `20260710-202132`.

## Evidence boundary

- Profile-level gates already bind population, appearance, combat, movement,
  loot, corpse, credits, respawn, and corpse lifetime for all five families.
- The supported-family and ordinary-population tests now require all 16 rows
  to be active while preserving an exact 11-row Violent Vagabond quarantine.
- The Deranged Shopper whole-enemy gate now requires its exact row to be active.
- Source `79557C66` is the only row with a private diagnostic artifact. It
  reached `ELIGIBLE` and `MATERIALIZED`, but remained outside client visibility
  and never entered the per-enemy send ledger. That does not prove its SCFU or
  lifecycle path.
- None of the other 15 rows has a completed materialization-plus-client-send
  artifact.

## Bounded validation order

1. Deranged Shopper `79574527` alone.
2. Both Looters.
3. All five Muggers.
4. Six Stim Fiends, tracking L9 `7957E415` separately because L9 credits are
   not exact.
5. Disobedient Bot L10 `7957E40A`, then L7 `79557C66`; the L7 SIW field and
   credits retain explicit policy boundaries.

The Disobedient Bot generated-report/runtime provenance mismatch is reconciled
from the existing capture corpus. Its two active rows still require the same
private-client validation described above.
