# Arete Large-Capture Reconciliation — 2026-08-05

## Root cause

The large-capture merge combined valid new capture-backed Arete content with
three independent reconciliation failures:

1. Generated active-coverage expectations still described the earlier actor
   population and binding totals.
2. Four quest runtimes replaced established atomic, legacy-aware reward and
   retry behavior with direct state mutation or premature item consumption.
3. Commit `3f5404b2` deleted the tracked PF1931 world-interaction decoder while
   its repository acceptance contract still required it.

The generated artifact is not independent gameplay authority. Current runtime
content definitions and capture provenance are authoritative; the affected
active-coverage artifact and manifest were regenerated from those inputs. The
generator's stale SANDSTORM evidence identifier was repaired in generator
source rather than patched into generated JSON.

## Repaired behavior

- Marcus Wounded Workers again applies its exact XP/credit reward atomically,
  recognizes legacy reward markers, and rewards before consuming the stim.
- Flint Bio-Com again applies the Alex reward atomically before consuming the
  quest item and recognizes its legacy ledger key.
- Stan Goodman again separates legacy and current completion markers, applies
  the exact XP/credit reward atomically, and retries item delivery before
  sealing current completion.
- The Rex/Marcus handoff again applies its exact reward atomically before
  consuming suppressant, recognizes legacy/current state, and recovers split
  historical reward state without double-granting.
- The exact previously tracked PF1931 world-interaction decoder is restored.

No supported capture-backed enemy combat, population, movement, interaction,
loot, timing, or dialogue behavior was replaced with inferred behavior.

## Generated cohort

- Initial actor count: 1,600.
- Configured maximum actor count: 1,602.
- Binding record count: 1,576.
- Arete family actor count: 96.
- Generation identity:
  `4217fcc90b5adc10847c647d8134dea271c9c2cc73cae94a0ca34b4bd37950cf`.

## Validation

- Generated combat cohort: PASS.
- Arete acceptance: 60/60 PASS.
- Complete AOtomation suite: 1,038/1,038 PASS.
- Debug build: PASS.
- Complete mandatory integration gate: PASS twice from the unchanged delivery
  commit.
- Database preflight and restarted Chat/Login/Zone exact-port ownership: PASS.
- Optional WebEngine: inactive by policy.
