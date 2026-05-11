# Enemy Spawn Coverage

Generated reports:

- `MonsterDataCorpseVisualHints.csv`: CellAO mob templates matched to AO Model Viewer `MonsterData -> CatMesh` data.
- `ClientHintedEnemyCoverage.csv`: client-hinted playfield enemy families compared against the current combat test catalog.

Regenerate:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools-temp\enemy-spawn-coverage\Export-EnemySpawnCoverage.ps1
```

Current supported combat-test families:

| Family | Test mob key |
| --- | --- |
| leet | `beachleet` |
| reet | `islandreet` |
| snake | `shoresnake` |
| rollerrat | `rollerrat` |
| flea | `duneflea` |
| lizard | `surflizard` |
| malle | `cliffmalle` |
| salamander | `reefsalamander` |
| spider | `alienspider` |

Useful early-zone coverage:

| Playfield | Supported | Still missing |
| ---: | --- | --- |
| `565` Newland Desert | lizard, leet, snake, salamander, flea | rhinoman, mutant, bronto, scorpiod, buzzsaw, minibull |
| `551` Wailing Wastes | rollerrat, spider | biofreak, scorpiod, hound, mutant |
| `585` Aegean | snake, leet, flea, rollerrat | rhinoman, hound, mechdog, anun |
| `600` Varmint Woods | lizard, spider, leet | rhinoman, bronto |
| `605` Belial Forest | snake, lizard | none from concrete hints |
| `716` Omni Forest | leet, malle, flea | mutant, biofreak, rhinoman, bronto |

This is a coverage map for controlled testing, not a final spawn table. It deliberately separates supported test families from enemy names that still need DB templates, visual/corpse proof, or live packet evidence. Statel-only text such as descriptive item/object copy is tracked as weak evidence instead of spawn coverage.
