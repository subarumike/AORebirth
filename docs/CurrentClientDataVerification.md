# Current Client Data Verification

Generated: 2026-06-07 04:28:30

## Scope

- AO client root: C:\Funcom\Anarchy Online
- AO client version.id: 18.8.62_EP1
- CellAO repo: C:\Users\Mike\Documents\Cellao-Clean
- Database read: cellao_codex_clean only

## Summary

| Area | Result |
| --- | ---: |
| Built item templates | 120842 |
| Built nano formulas | 10965 |
| Built playfields | 618 |
| Data file source/runtime/version issues | 0 |
| Live vendor mesh evidence rows not satisfied by item cache | 2 |
| Vendor DB rows with issues | 4 |
| Shop inventory rows with item-cache issues | 1 |
| Vending statels without complete DB shop coverage | 832 |

## Data Files

| File | Source version | Source rows | Built version | Built rows | Matches |
| --- | --- | ---: | --- | ---: | --- |
| items.dat | 18.8.62_EP1 | 120842 | 18.8.62_EP1 | 120842 | True |
| nanos.dat | 18.8.62_EP1 | 10965 | 18.8.62_EP1 | 10965 | True |
| playfields.dat | 18.8.62_EP1 | 618 | 18.8.62_EP1 | 618 | True |

Direct implication: source and built data must match before committing data alignment work. A fresh dev pull uses source data, while Mike's current running folder can contain extractor-generated files.

## Live Vendor Mesh Evidence

| Label | Template | items.dat mesh | Live mesh | Runtime override | Status |
| --- | ---: | ---: | ---: | ---: | --- |
| Basic Bookstore | 155599 | 93106 | 85976 | 85976 | Runtime override matches live |
| Omni Basic Devices | 155603 | 93106 | 85976 | 85976 | Runtime override matches live |

This separates template-cache truth from live runtime instance truth. If items.dat still differs from live but the runtime override matches, that is not an item import failure.

## Highest Value Repair Targets

1. Source/runtime data files are aligned.
2. Fix vendor DB rows with missing template/shop coverage before chasing individual shop terminal behavior.
3. Review statel vendors with no DB row; these spawn as empty statel vendors and can produce missing shop entry behavior.
4. Repair shop inventory rows whose item IDs do not exist in the current item cache.

## Report Files

- Data file audit: C:\Users\Mike\Documents\Cellao-Clean\tools-temp\current-client-data-verification\data-file-version-audit.csv
- Live vendor mesh audit: C:\Users\Mike\Documents\Cellao-Clean\tools-temp\current-client-data-verification\live-vendor-mesh-audit.csv
- Vendor DB audit: C:\Users\Mike\Documents\Cellao-Clean\tools-temp\current-client-data-verification\vendor-db-audit.csv
- Shop inventory audit: C:\Users\Mike\Documents\Cellao-Clean\tools-temp\current-client-data-verification\shop-inventory-template-audit.csv
- Statel vendor coverage: C:\Users\Mike\Documents\Cellao-Clean\tools-temp\current-client-data-verification\statel-vendor-coverage.csv

## Top Vendor DB Issues

| Id | Playfield | Hash | DB template | Template item | Shop hash | Active items | Issues |
| ---: | ---: | --- | ---: | ---: | --- | ---: | --- |
| 77725745 | 1186 | ICCPhaS | 297395 | 297395 | PC1H | 0 | shop inventory empty or inactive |
| 77791233 | 1187 | ContG | 99634 | 99501 | Cont | 62 | vendors.TemplateId differs from vendortemplate.ItemTemplate |
| 77791237 | 1187 | MedA | 152008 | 99575 | Med | 11 | vendors.TemplateId differs from vendortemplate.ItemTemplate |
| 77791238 | 1187 | MedS | 152012 | 151975 | Med | 11 | vendors.TemplateId differs from vendortemplate.ItemTemplate |

## Top Statel Vendor Coverage Issues

| Playfield | Name | Vendor id | Statel | Template | Coords | Issues |
| ---: | --- | ---: | --- | ---: | --- | --- |
| 346 | ACD Omnilab (dng) | 22675456 | 0xC000015A | 155225 | 73,10.01,385 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768000 | 0xC00001F4 | 99571 | 215.737,16.4,162.412 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768001 | 0xC00101F4 | 99599 | 214.427,16.4,162.485 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768002 | 0xC00201F4 | 99635 | 211.938,16.4,162.399 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768003 | 0xC00301F4 | 99575 | 217.069,16.4,162.746 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768004 | 0xC00401F4 | 99573 | 213.139,16.4,162.472 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768005 | 0xC00501F4 | 99570 | 211.355,16.4,160.109 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768006 | 0xC00601F4 | 99602 | 212.2,16.4,159.505 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768007 | 0xC00701F4 | 99598 | 214.657,16.4,159.581 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768008 | 0xC00801F4 | 99574 | 215.858,16.4,159.648 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768009 | 0xC00901F4 | 118287 | 217.19,16.4,159.708 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768010 | 0xC00A01F4 | 99601 | 211.295,16.4,161.41 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768011 | 0xC00B01F4 | 99600 | 213.404,16.4,159.507 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768012 | 0xC00C01F4 | 99572 | 217.917,16.4,160.724 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768013 | 0xC00D01F4 | 117938 | 217.822,16.4,161.884 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768014 | 0xC00E01F4 | 99503 | 219.649,16.4,160.128 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768015 | 0xC00F01F4 | 99533 | 220.751,16.4,160.499 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768016 | 0xC01001F4 | 99517 | 221.824,16.4,160.883 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768017 | 0xC01101F4 | 99541 | 222.913,16.4,161.281 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768018 | 0xC01201F4 | 99509 | 224.031,16.4,161.614 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768019 | 0xC01301F4 | 118285 | 225.127,16.4,161.93 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768020 | 0xC01401F4 | 99528 | 226.065,16.4,161.447 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768021 | 0xC01501F4 | 99522 | 226.448,16.4,160.231 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768022 | 0xC01601F4 | 99506 | 225.719,16.4,159.308 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768023 | 0xC01701F4 | 117673 | 224.476,16.4,158.955 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768024 | 0xC01801F4 | 99537 | 223.332,16.4,158.564 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768025 | 0xC01901F4 | 99526 | 222.312,16.4,158.193 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768026 | 0xC01A01F4 | 99539 | 221.331,16.4,157.809 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768027 | 0xC01B01F4 | 99540 | 220.278,16.4,157.358 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768028 | 0xC01C01F4 | 121033 | 219.673,16.4,157.879 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768029 | 0xC01D01F4 | 121031 | 219.333,16.4,159.129 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768030 | 0xC01E01F4 | 120511 | 232.893,16.399,165.46 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768031 | 0xC01F01F4 | 117749 | 231.973,16.4,166.237 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768032 | 0xC02001F4 | 99567 | 223.045,16.4,174.209 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768033 | 0xC02101F4 | 99557 | 217.05,16.4,172.115 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768034 | 0xC02201F4 | 99559 | 219.08,16.4,173.977 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768035 | 0xC02301F4 | 99558 | 215.845,16.4,172.039 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768036 | 0xC02401F4 | 99556 | 219.088,16.4,172.871 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768037 | 0xC02501F4 | 99560 | 219,16.4,175.131 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768038 | 0xC02601F4 | 99561 | 218.148,16.4,172.2 | no vendors row; runtime spawns empty statel vendor |
