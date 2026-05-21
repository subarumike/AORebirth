# Live Loot Observations

Exports observed corpse loot from decoded AO packet captures.

Inputs expected in a capture folder:

- `s2c_frames.jsonl`
- `ao_frames.jsonl`

Run:

```powershell
python .\tools-temp\live-loot-observations\Export-LiveLootObservations.py `
  .\tools-temp\live-pcaps\private-server-loot\2026-05-10_21-36-46 `
  --initial-cash 289
```

The exporter also reads the local client install at `C:\Funcom\Anarchy Online`
by default to fill playfield names when the repo's generated zone hint CSV does
not include a playfield id.

Outputs:

- `loot_body_observations.csv`: one row per opened corpse, including playfield id, enemy identity/name, enemy level, enemy max/current health from live `SimpleCharFullUpdate`, corpse/death location, item count, cash after looting, and computed credits gained.
- `loot_drop_observations.csv`: one row per item drop plus one row per credit drop, tied back to the same body/enemy context.

If `--initial-cash` is omitted, the first body's credit delta can only be computed when an earlier Cash stat update exists in the capture.

## Review-only seed export

After one or more captures have been decoded, summarize observed mob drops into reviewable seed files:

```powershell
python .\tools-temp\live-loot-observations\Export-ObservedMobLootSeed.py
```

Outputs:

- `CellAO\Documentation\MobLootCoverage\ObservedLiveLootSeed.csv`
- `CellAO\Documentation\MobLootCoverage\ObservedLiveLootSeed.review.sql`

The SQL file is deliberately not applied by the tool. It is a review artifact
that maps exact observed enemy names to checked-in `mobtemplate` hashes where
possible, estimates observed drop rates, and leaves missing template matches
visible for follow-up.
