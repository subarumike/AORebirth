# PF4582 TemplateHash Resolution Audit

This deterministic audit covers all 38 authoritative PF4582 TemplateHash groups. It is identity-resolution evidence only and authorizes no runtime activation.

## Metrics

```text
PF4582_TEMPLATE_HASHES_TOTAL=38
PF4582_BASELINE_MAPPED=14
PF4582_BASELINE_UNRESOLVED=24
PF4582_BASELINE_PROVEN=14
PF4582_BASELINE_EVIDENCE_INCOMPLETE=0
PF4582_BASELINE_CONFLICT=0
PF4582_AUDIT_PROVEN=0
PF4582_AUDIT_CANDIDATE=17
PF4582_AUDIT_AMBIGUOUS=1
PF4582_AUDIT_NO_EVIDENCE=6
PF4582_NEWLY_PROVEN=0
PF4582_BLOCKED_PLACEMENTS_PROVEN=0
PF4582_BLOCKED_PLACEMENTS_CANDIDATE=164
PF4582_BLOCKED_PLACEMENTS_AMBIGUOUS=1
PF4582_BLOCKED_PLACEMENTS_NO_EVIDENCE=6
PF4582_DYNAMIC_NAMES=7
PF4582_UNRESOLVED_HASH_BLOCKED_PLACEMENTS=171
PF4582_BASELINE_MAPPED_BLOCKED_PLACEMENTS=10
PF4582_RUNTIME_ACTIVE_BEFORE=25
PF4582_RUNTIME_ACTIVE_AFTER=25
PF4582_RUNTIME_BLOCKED_BEFORE=181
PF4582_RUNTIME_BLOCKED_AFTER=181
PF4582_RUNTIME_ACTIVATION_CHANGED=NO
```

## Blocked-placement accounting

The 24 baseline-unresolved hashes contain 171 blocked placements. Ten additional blocked Island Reet placements use the baseline-mapped ISRE hash; 171 + 10 = the runtime total of 181.

This corrects an arithmetic conflict in the requested test list: blocked placements across the 24 baseline-unresolved hashes cannot sum to 181 because 10 blocked Island Reet records use the baseline-mapped ISRE hash.

## Baseline mapping verification

| TemplateHash | Tag | Placements | Active | Blocked | Profile | Verification |
|---:|:---:|---:|---:|---:|---|---|
| 1096042831 | OITA | 1 | 1 | 0 | IccShuttleportSpawn:Omni-Trans Equipment Vendor | BASELINE_PROVEN |
| 1146375747 | CNTD | 1 | 1 | 0 | IccShuttleportSpawn:Clan Equipment Vendor | BASELINE_PROVEN |
| 1163019598 | NERE | 1 | 1 | 0 | IccShuttleportSpawn:Neutral Observer | BASELINE_PROVEN |
| 1163021903 | ONRE | 1 | 1 | 0 | IccShuttleportSpawn:Omni-Tek Recruitment Officer | BASELINE_PROVEN |
| 1163023177 | ISRE | 11 | 1 | 10 | IccShuttleportSpawn:Island Reet | BASELINE_PROVEN |
| 1163284805 | EQVE | 1 | 1 | 0 | IccShuttleportSpawn:Vendor Antonio Stacklund | BASELINE_PROVEN |
| 1178682433 | ADAF | 1 | 1 | 0 | IccShuttleportSpawn:Adri Afeli | BASELINE_PROVEN |
| 1196247123 | SHMG | 1 | 1 | 0 | IccShuttleportSpawn:Manager Travis Molen | BASELINE_PROVEN |
| 1229079369 | ICBI | 1 | 1 | 0 | IccShuttleportSpawn:ICC Bio-Inspector | BASELINE_PROVEN |
| 1229343811 | CLFI | 1 | 1 | 0 | IccShuttleportSpawn:Clan Field Surgeon Elsa Oosta | BASELINE_PROVEN |
| 1230127939 | CCRI | 1 | 1 | 0 | IccShuttleportSpawn:Clan Recruiter | BASELINE_PROVEN |
| 1230327119 | OMUI | 1 | 1 | 0 | IccShuttleportSpawn:Omni Unicorn Squadleader Fixx | BASELINE_PROVEN |
| 1330925122 | BNTO | 1 | 1 | 0 | IccShuttleportSpawn:Brandon Thorn | BASELINE_PROVEN |
| 1414742857 | ICST | 12 | 12 | 0 | IccShuttleportSpawn:ICC Shuttle Guard | BASELINE_PROVEN |

All 14 baseline mappings are proven at the repository-governance level by an explicit source NpcId → numeric TemplateHash → governed mapping → current runtime profile chain validated by the placement importer. This is not a raw-packet TemplateHash observation and does not promote same-hash siblings.

## Unresolved impact ranking

| Rank | TemplateHash | Canonical | Tag | Placements | Classification | Candidate or resolved profile | Primary blocker |
|---:|---:|:---:|:---:|---:|---|---|---|
| 1 | 1230522714 | 0x4958495A | ZIXI | 26 | CANDIDATE | mobtemplate:A026 | No accepted record links this numeric TemplateHash or a source NpcId to the captured identity. |
| 2 | 1246118721 | 0x4A464341 | ACFJ | 23 | CANDIDATE | mobtemplate:A002 | No accepted record links this numeric TemplateHash or a source NpcId to the captured identity. |
| 3 | 1095979092 | 0x41535054 | TPSA | 16 | CANDIDATE | mobtemplate:A033 | No accepted record links this numeric TemplateHash or a source NpcId to the captured identity. |
| 4 | 1095584067 | 0x414D4943 | CIMA | 13 | CANDIDATE | mobtemplate:A035 | No accepted record links this numeric TemplateHash or a source NpcId to the captured identity. |
| 5 | 1263749447 | 0x4B534947 | GISK | 10 | CANDIDATE | mobtemplate:A030 | No accepted record links this numeric TemplateHash or a source NpcId to the captured identity. |
| 6 | 1329812567 | 0x4F435457 | WTCO | 10 | CANDIDATE | mobtemplate:A029 | No accepted record links this numeric TemplateHash or a source NpcId to the captured identity. |
| 7 | 1380204867 | 0x52444143 | CADR | 10 | CANDIDATE | mobtemplate:A027 | No accepted record links this numeric TemplateHash or a source NpcId to the captured identity. |
| 8 | 1330725958 | 0x4F514446 | FDQO | 9 | CANDIDATE | mobtemplate:A004 | No accepted record links this numeric TemplateHash or a source NpcId to the captured identity. |
| 9 | 1514951251 | 0x5A4C5253 | SRLZ | 9 | CANDIDATE | mobtemplate:A000 | No accepted record links this numeric TemplateHash or a source NpcId to the captured identity. |
| 10 | 1430934083 | 0x554A5243 | CRJU | 8 | CANDIDATE | mobtemplate:A009 | No accepted record links this numeric TemplateHash or a source NpcId to the captured identity. |
| 11 | 1280525906 | 0x4C534652 | RFSL | 7 | CANDIDATE | mobtemplate:A034 | No accepted record links this numeric TemplateHash or a source NpcId to the captured identity. |
| 12 | 1314079299 | 0x4E534243 | CBSN | 7 | CANDIDATE | mobtemplate:A013 | No accepted record links this numeric TemplateHash or a source NpcId to the captured identity. |
| 13 | 1280462675 | 0x4C524F53 | SORL | 5 | CANDIDATE | mobtemplate:A012 | No accepted record links this numeric TemplateHash or a source NpcId to the captured identity. |
| 14 | 1380273228 | 0x52454C4C | LLER | 5 | CANDIDATE | runtime:CombatTestMobArchetype.StowawayRollerrat | No accepted record links this numeric TemplateHash or a source NpcId to the captured identity. |
| 15 | 1263751763 | 0x4B535253 | SRSK | 3 | CANDIDATE | mobtemplate:A003 | No accepted record links this numeric TemplateHash or a source NpcId to the captured identity. |
| 16 | 1262571596 | 0x4B41504C | LPAK | 2 | CANDIDATE | mobtemplate:A014 | No accepted record links this numeric TemplateHash or a source NpcId to the captured identity. |
| 17 | 1196249922 | 0x474D5342 | BSMG | 1 | NO_EVIDENCE | NONE | A stable identifier must link this TemplateHash or source NpcId to an accepted AO identity. |
| 18 | 1280132162 | 0x4C4D4442 | BDML | 1 | NO_EVIDENCE | NONE | A stable identifier must link this TemplateHash or source NpcId to an accepted AO identity. |
| 19 | 1280132418 | 0x4C4D4542 | BEML | 1 | NO_EVIDENCE | NONE | A stable identifier must link this TemplateHash or source NpcId to an accepted AO identity. |
| 20 | 1296911426 | 0x4D4D4C42 | BLMM | 1 | AMBIGUOUS | mobtemplate:A035, mobtemplate:A103, mobtemplate:A123 | A stable identifier must bridge this TemplateHash or source NpcId to one unique profile independently of the dynamic name and coordinates. |
| 21 | 1330463810 | 0x4F4D4442 | BDMO | 1 | NO_EVIDENCE | NONE | A stable identifier must link this TemplateHash or source NpcId to an accepted AO identity. |
| 22 | 1330467906 | 0x4F4D5442 | BTMO | 1 | NO_EVIDENCE | NONE | A stable identifier must link this TemplateHash or source NpcId to an accepted AO identity. |
| 23 | 1380796994 | 0x524D4A42 | BJMR | 1 | NO_EVIDENCE | NONE | A stable identifier must link this TemplateHash or source NpcId to an accepted AO identity. |
| 24 | 1497649731 | 0x59445243 | CRDY | 1 | CANDIDATE | mobtemplate:A016 | No accepted record links this numeric TemplateHash or a source NpcId to the captured identity. |

## Per-hash unresolved findings

### 1095584067 (0x414D4943, CIMA)

Classification: `CANDIDATE`. Placements blocked: 13. Candidate profiles: mobtemplate:A035.

CANDIDATE: The exact captured Cliff Malle identity and the unique same-name SQL profile agree on MonsterData; source and SQL level ranges overlap. No direct evidence connects the numeric TemplateHash or any source NpcId to the captured AO identity, so placement name, level, coordinates, MonsterData, and respawn context remain corroborating only.

Remaining blockers: No accepted record links this numeric TemplateHash or a source NpcId to the captured identity.

Evidence paths: `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/mobtemplate.sql`, `Captures/ICC Shuttleport [PF 4582] - 20260818-214552/enemy-dossier.json`, `docs/generated/aosharp_capture_inventory.csv`, `docs/reference/pf4582/PlayfieldDistrictInfo.json`, `docs/reference/pf4582/template-hash-evidence.json`.

### 1095979092 (0x41535054, TPSA)

Classification: `CANDIDATE`. Placements blocked: 16. Candidate profiles: mobtemplate:A033.

CANDIDATE: The exact captured Tropical Stalker identity and the unique same-name SQL profile agree on MonsterData and level range. No direct evidence connects the numeric TemplateHash or any source NpcId to the captured AO identity, so placement name, level, coordinates, MonsterData, and respawn context remain corroborating only.

Remaining blockers: No accepted record links this numeric TemplateHash or a source NpcId to the captured identity.

Evidence paths: `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/mobtemplate.sql`, `Captures/ICC Shuttleport [PF 4582] - 20260818-214552/enemy-dossier.json`, `docs/generated/aosharp_capture_inventory.csv`, `docs/reference/pf4582/PlayfieldDistrictInfo.json`, `docs/reference/pf4582/template-hash-evidence.json`.

### 1196249922 (0x474D5342, BSMG)

Classification: `NO_EVIDENCE`. Placements blocked: 1. Candidate profiles: none.

NO_EVIDENCE: The dynamic source name Dreadknot the Toxictwister is absent from accepted PF4582 identity artifacts; nearby alternate dynamic names are not identity evidence. No direct evidence connects the numeric TemplateHash or any source NpcId to the captured AO identity, so placement name, level, coordinates, MonsterData, and respawn context remain corroborating only.

Remaining blockers: A stable identifier must link this TemplateHash or source NpcId to an accepted AO identity.

Evidence paths: `Captures/ICC Shuttleport [PF 4582] - 20260818-214552/enemy-dossier.json`, `docs/generated/aosharp_capture_inventory.csv`, `docs/reference/pf4582/PlayfieldDistrictInfo.json`, `docs/reference/pf4582/template-hash-evidence.json`.

### 1230522714 (0x4958495A, ZIXI)

Classification: `CANDIDATE`. Placements blocked: 26. Candidate profiles: mobtemplate:A026.

CANDIDATE: The exact captured Alien Spider - Zix identity and the unique same-name SQL profile agree on MonsterData; their level ranges overlap. No direct evidence connects the numeric TemplateHash or any source NpcId to the captured AO identity, so placement name, level, coordinates, MonsterData, and respawn context remain corroborating only.

Remaining blockers: No accepted record links this numeric TemplateHash or a source NpcId to the captured identity.

Evidence paths: `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/mobtemplate.sql`, `Captures/ICC Shuttleport [PF 4582] - 20260818-214552/enemy-dossier.json`, `docs/generated/aosharp_capture_inventory.csv`, `docs/reference/pf4582/PlayfieldDistrictInfo.json`, `docs/reference/pf4582/template-hash-evidence.json`.

### 1246118721 (0x4A464341, ACFJ)

Classification: `CANDIDATE`. Placements blocked: 23. Candidate profiles: mobtemplate:A002.

CANDIDATE: The exact captured Scout - Jaax'Sinuh identity and the unique same-name SQL profile agree on MonsterData and level range. No direct evidence connects the numeric TemplateHash or any source NpcId to the captured AO identity, so placement name, level, coordinates, MonsterData, and respawn context remain corroborating only.

Remaining blockers: No accepted record links this numeric TemplateHash or a source NpcId to the captured identity. Respawn correlations are ambiguous and cannot supply the missing identity bridge.

Evidence paths: `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/mobtemplate.sql`, `Captures/ICC Shuttleport [PF 4582] - 20260818-214552/enemy-dossier.json`, `docs/generated/aosharp_capture_inventory.csv`, `docs/reference/pf4582/PlayfieldDistrictInfo.json`, `docs/reference/pf4582/template-hash-evidence.json`.

### 1262571596 (0x4B41504C, LPAK)

Classification: `CANDIDATE`. Placements blocked: 2. Candidate profiles: mobtemplate:A014.

CANDIDATE: The exact captured Shuttle Saboteur identity and the unique same-name SQL profile agree on MonsterData; source and SQL level ranges overlap. No direct evidence connects the numeric TemplateHash or any source NpcId to the captured AO identity, so placement name, level, coordinates, MonsterData, and respawn context remain corroborating only.

Remaining blockers: No accepted record links this numeric TemplateHash or a source NpcId to the captured identity.

Evidence paths: `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/mobtemplate.sql`, `Captures/ICC Shuttleport [PF 4582] - 20260818-214552/enemy-dossier.json`, `docs/generated/aosharp_capture_inventory.csv`, `docs/reference/pf4582/PlayfieldDistrictInfo.json`, `docs/reference/pf4582/template-hash-evidence.json`.

### 1263749447 (0x4B534947, GISK)

Classification: `CANDIDATE`. Placements blocked: 10. Candidate profiles: mobtemplate:A030.

CANDIDATE: The exact captured Giant Snake identity and the unique same-name SQL profile agree on MonsterData; source and SQL level ranges overlap. No direct evidence connects the numeric TemplateHash or any source NpcId to the captured AO identity, so placement name, level, coordinates, MonsterData, and respawn context remain corroborating only.

Remaining blockers: No accepted record links this numeric TemplateHash or a source NpcId to the captured identity. MonsterData 30252 is shared and cannot establish identity alone.

Evidence paths: `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/mobtemplate.sql`, `Captures/ICC Shuttleport [PF 4582] - 20260818-214552/enemy-dossier.json`, `docs/generated/aosharp_capture_inventory.csv`, `docs/reference/pf4582/PlayfieldDistrictInfo.json`, `docs/reference/pf4582/template-hash-evidence.json`.

### 1263751763 (0x4B535253, SRSK)

Classification: `CANDIDATE`. Placements blocked: 3. Candidate profiles: mobtemplate:A003.

CANDIDATE: The exact captured Shore Snake identity and the unique same-name SQL profile agree on MonsterData and level range. No direct evidence connects the numeric TemplateHash or any source NpcId to the captured AO identity, so placement name, level, coordinates, MonsterData, and respawn context remain corroborating only.

Remaining blockers: No accepted record links this numeric TemplateHash or a source NpcId to the captured identity. MonsterData 30252 is shared and cannot establish identity alone.

Evidence paths: `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/mobtemplate.sql`, `Captures/ICC Shuttleport [PF 4582] - 20260818-214552/enemy-dossier.json`, `docs/generated/aosharp_capture_inventory.csv`, `docs/reference/pf4582/PlayfieldDistrictInfo.json`, `docs/reference/pf4582/template-hash-evidence.json`.

### 1280132162 (0x4C4D4442, BDML)

Classification: `NO_EVIDENCE`. Placements blocked: 1. Candidate profiles: none.

NO_EVIDENCE: The dynamic source name Burntooth the Inferno Muddevil is absent from accepted PF4582 identity artifacts; observed alternate Mudpuppy names cannot replace it. No direct evidence connects the numeric TemplateHash or any source NpcId to the captured AO identity, so placement name, level, coordinates, MonsterData, and respawn context remain corroborating only.

Remaining blockers: A stable identifier must link this TemplateHash or source NpcId to an accepted AO identity.

Evidence paths: `Captures/ICC Shuttleport [PF 4582] - 20260818-214552/enemy-dossier.json`, `docs/generated/aosharp_capture_inventory.csv`, `docs/reference/pf4582/PlayfieldDistrictInfo.json`, `docs/reference/pf4582/template-hash-evidence.json`.

### 1280132418 (0x4C4D4542, BEML)

Classification: `NO_EVIDENCE`. Placements blocked: 1. Candidate profiles: none.

NO_EVIDENCE: The dynamic source name Sparkletail the Jolly Wrecker is absent from accepted PF4582 identity artifacts; observed alternate dynamic names cannot replace it. No direct evidence connects the numeric TemplateHash or any source NpcId to the captured AO identity, so placement name, level, coordinates, MonsterData, and respawn context remain corroborating only.

Remaining blockers: A stable identifier must link this TemplateHash or source NpcId to an accepted AO identity.

Evidence paths: `Captures/ICC Shuttleport [PF 4582] - 20260818-214552/enemy-dossier.json`, `docs/generated/aosharp_capture_inventory.csv`, `docs/reference/pf4582/PlayfieldDistrictInfo.json`, `docs/reference/pf4582/template-hash-evidence.json`.

### 1280462675 (0x4C524F53, SORL)

Classification: `CANDIDATE`. Placements blocked: 5. Candidate profiles: mobtemplate:A012.

CANDIDATE: The exact captured Stowaway Rollerrat identity and the unique same-name SQL profile agree on MonsterData; source and SQL level ranges overlap. No direct evidence connects the numeric TemplateHash or any source NpcId to the captured AO identity, so placement name, level, coordinates, MonsterData, and respawn context remain corroborating only.

Remaining blockers: No accepted record links this numeric TemplateHash or a source NpcId to the captured identity.

Evidence paths: `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/mobtemplate.sql`, `Captures/ICC Shuttleport [PF 4582] - 20260818-214552/enemy-dossier.json`, `docs/generated/aosharp_capture_inventory.csv`, `docs/reference/pf4582/PlayfieldDistrictInfo.json`, `docs/reference/pf4582/template-hash-evidence.json`.

### 1280525906 (0x4C534652, RFSL)

Classification: `CANDIDATE`. Placements blocked: 7. Candidate profiles: mobtemplate:A034.

CANDIDATE: The exact captured Reef Salamander identity and the unique same-name SQL profile agree on MonsterData; source and SQL level ranges overlap. No direct evidence connects the numeric TemplateHash or any source NpcId to the captured AO identity, so placement name, level, coordinates, MonsterData, and respawn context remain corroborating only.

Remaining blockers: No accepted record links this numeric TemplateHash or a source NpcId to the captured identity. MonsterData 30354 is shared and cannot establish identity alone.

Evidence paths: `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/mobtemplate.sql`, `Captures/ICC Shuttleport [PF 4582] - 20260818-214552/enemy-dossier.json`, `docs/generated/aosharp_capture_inventory.csv`, `docs/reference/pf4582/PlayfieldDistrictInfo.json`, `docs/reference/pf4582/template-hash-evidence.json`.

### 1296911426 (0x4D4D4C42, BLMM)

Classification: `AMBIGUOUS`. Placements blocked: 1. Candidate profiles: mobtemplate:A035, mobtemplate:A103, mobtemplate:A123.

AMBIGUOUS: An exact Oozefoot display name was captured, but the source marks the slot dynamic and multiple Malle-family profiles remain plausible. No direct evidence connects the numeric TemplateHash or any source NpcId to the captured AO identity, so placement name, level, coordinates, MonsterData, and respawn context remain corroborating only.

Remaining blockers: A stable identifier must bridge this TemplateHash or source NpcId to one unique profile independently of the dynamic name and coordinates.

Evidence paths: `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/mobtemplate.sql`, `Captures/ICC Shuttleport [PF 4582] - 20260818-214552/enemy-dossier.json`, `docs/generated/aosharp_capture_inventory.csv`, `docs/reference/pf4582/PlayfieldDistrictInfo.json`, `docs/reference/pf4582/template-hash-evidence.json`.

### 1314079299 (0x4E534243, CBSN)

Classification: `CANDIDATE`. Placements blocked: 7. Candidate profiles: mobtemplate:A013.

CANDIDATE: The exact captured Climbing Salamander identity and the unique same-name SQL profile agree on MonsterData; source and SQL level ranges overlap. No direct evidence connects the numeric TemplateHash or any source NpcId to the captured AO identity, so placement name, level, coordinates, MonsterData, and respawn context remain corroborating only.

Remaining blockers: No accepted record links this numeric TemplateHash or a source NpcId to the captured identity. MonsterData 30354 is shared and cannot establish identity alone.

Evidence paths: `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/mobtemplate.sql`, `Captures/ICC Shuttleport [PF 4582] - 20260818-214552/enemy-dossier.json`, `docs/generated/aosharp_capture_inventory.csv`, `docs/reference/pf4582/PlayfieldDistrictInfo.json`, `docs/reference/pf4582/template-hash-evidence.json`.

### 1329812567 (0x4F435457, WTCO)

Classification: `CANDIDATE`. Placements blocked: 10. Candidate profiles: mobtemplate:A029.

CANDIDATE: The exact captured Waste collector identity and the unique same-name SQL profile agree on MonsterData; source and SQL level ranges overlap. No direct evidence connects the numeric TemplateHash or any source NpcId to the captured AO identity, so placement name, level, coordinates, MonsterData, and respawn context remain corroborating only.

Remaining blockers: No accepted record links this numeric TemplateHash or a source NpcId to the captured identity.

Evidence paths: `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/mobtemplate.sql`, `Captures/ICC Shuttleport [PF 4582] - 20260818-214552/enemy-dossier.json`, `docs/generated/aosharp_capture_inventory.csv`, `docs/reference/pf4582/PlayfieldDistrictInfo.json`, `docs/reference/pf4582/template-hash-evidence.json`.

### 1330463810 (0x4F4D4442, BDMO)

Classification: `NO_EVIDENCE`. Placements blocked: 1. Candidate profiles: none.

NO_EVIDENCE: The dynamic source name Chipmind the Overclocked182-T1 is absent from accepted PF4582 identity artifacts; nearby generated robot names are not stable identity evidence. No direct evidence connects the numeric TemplateHash or any source NpcId to the captured AO identity, so placement name, level, coordinates, MonsterData, and respawn context remain corroborating only.

Remaining blockers: A stable identifier must link this TemplateHash or source NpcId to an accepted AO identity.

Evidence paths: `Captures/ICC Shuttleport [PF 4582] - 20260818-214552/enemy-dossier.json`, `docs/generated/aosharp_capture_inventory.csv`, `docs/reference/pf4582/PlayfieldDistrictInfo.json`, `docs/reference/pf4582/template-hash-evidence.json`.

### 1330467906 (0x4F4D5442, BTMO)

Classification: `NO_EVIDENCE`. Placements blocked: 1. Candidate profiles: none.

NO_EVIDENCE: The dynamic source name Malicespine the Wasteland Roller is absent from accepted PF4582 identity artifacts; nearby generated roller names are not stable identity evidence. No direct evidence connects the numeric TemplateHash or any source NpcId to the captured AO identity, so placement name, level, coordinates, MonsterData, and respawn context remain corroborating only.

Remaining blockers: A stable identifier must link this TemplateHash or source NpcId to an accepted AO identity.

Evidence paths: `Captures/ICC Shuttleport [PF 4582] - 20260818-214552/enemy-dossier.json`, `docs/generated/aosharp_capture_inventory.csv`, `docs/reference/pf4582/PlayfieldDistrictInfo.json`, `docs/reference/pf4582/template-hash-evidence.json`.

### 1330725958 (0x4F514446, FDQO)

Classification: `CANDIDATE`. Placements blocked: 9. Candidate profiles: mobtemplate:A004.

CANDIDATE: The exact captured Beach Leet identity and the unique same-name SQL profile agree on MonsterData and level range. No direct evidence connects the numeric TemplateHash or any source NpcId to the captured AO identity, so placement name, level, coordinates, MonsterData, and respawn context remain corroborating only.

Remaining blockers: No accepted record links this numeric TemplateHash or a source NpcId to the captured identity. Observed respawn timing cannot supply the missing identity bridge.

Evidence paths: `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/mobtemplate.sql`, `Captures/ICC Shuttleport [PF 4582] - 20260818-214552/enemy-dossier.json`, `docs/generated/aosharp_capture_inventory.csv`, `docs/reference/pf4582/PlayfieldDistrictInfo.json`, `docs/reference/pf4582/template-hash-evidence.json`.

### 1380204867 (0x52444143, CADR)

Classification: `CANDIDATE`. Placements blocked: 10. Candidate profiles: mobtemplate:A027.

CANDIDATE: The exact captured Cargo Droid identity and the unique same-name SQL profile agree on MonsterData; source and SQL level ranges overlap. No direct evidence connects the numeric TemplateHash or any source NpcId to the captured AO identity, so placement name, level, coordinates, MonsterData, and respawn context remain corroborating only.

Remaining blockers: No accepted record links this numeric TemplateHash or a source NpcId to the captured identity.

Evidence paths: `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/mobtemplate.sql`, `Captures/ICC Shuttleport [PF 4582] - 20260818-214552/enemy-dossier.json`, `docs/generated/aosharp_capture_inventory.csv`, `docs/reference/pf4582/PlayfieldDistrictInfo.json`, `docs/reference/pf4582/template-hash-evidence.json`.

### 1380273228 (0x52454C4C, LLER)

Classification: `CANDIDATE`. Placements blocked: 5. Candidate profiles: runtime:CombatTestMobArchetype.StowawayRollerrat.

CANDIDATE: The exact captured Rollerrat identity agrees with the existing AORebirth Rollerrat-family archetype on MonsterData and NpcFamily, making it the strongest current profile candidate. No direct evidence connects the numeric TemplateHash or any source NpcId to the captured AO identity, so placement name, level, coordinates, MonsterData, and respawn context remain corroborating only.

Remaining blockers: No accepted record links this numeric TemplateHash or a source NpcId to the captured identity. The generic level-2 Rollerrat stats differ from the Stowaway template and require independent profile proof before implementation.

Evidence paths: `AORebirth/Server/ZoneEngine/Core/CombatTestMobArchetype.cs`, `Captures/ICC Shuttleport [PF 4582] - 20260818-214552/enemy-dossier.json`, `docs/generated/aosharp_capture_inventory.csv`, `docs/reference/pf4582/PlayfieldDistrictInfo.json`, `docs/reference/pf4582/template-hash-evidence.json`.

### 1380796994 (0x524D4A42, BJMR)

Classification: `NO_EVIDENCE`. Placements blocked: 1. Candidate profiles: none.

NO_EVIDENCE: The dynamic source name Sparky the Stabber is absent from accepted PF4582 identity artifacts; unrelated Sparky tokens are not stable identity evidence. No direct evidence connects the numeric TemplateHash or any source NpcId to the captured AO identity, so placement name, level, coordinates, MonsterData, and respawn context remain corroborating only.

Remaining blockers: A stable identifier must link this TemplateHash or source NpcId to an accepted AO identity.

Evidence paths: `Captures/ICC Shuttleport [PF 4582] - 20260818-214552/enemy-dossier.json`, `docs/generated/aosharp_capture_inventory.csv`, `docs/reference/pf4582/PlayfieldDistrictInfo.json`, `docs/reference/pf4582/template-hash-evidence.json`.

### 1430934083 (0x554A5243, CRJU)

Classification: `CANDIDATE`. Placements blocked: 8. Candidate profiles: mobtemplate:A009.

CANDIDATE: The exact captured Cross-Wired Junkbot identity and the unique same-name SQL profile agree on MonsterData and overlapping level range. No direct evidence connects the numeric TemplateHash or any source NpcId to the captured AO identity, so placement name, level, coordinates, MonsterData, and respawn context remain corroborating only.

Remaining blockers: No accepted record links this numeric TemplateHash or a source NpcId to the captured identity.

Evidence paths: `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/mobtemplate.sql`, `Captures/ICC Shuttleport [PF 4582] - 20260818-214552/enemy-dossier.json`, `docs/generated/aosharp_capture_inventory.csv`, `docs/reference/pf4582/PlayfieldDistrictInfo.json`, `docs/reference/pf4582/template-hash-evidence.json`.

### 1497649731 (0x59445243, CRDY)

Classification: `CANDIDATE`. Placements blocked: 1. Candidate profiles: mobtemplate:A016.

CANDIDATE: The exact captured Specialist - Cha'Heru identity and the unique same-name SQL profile agree on MonsterData and level range. No direct evidence connects the numeric TemplateHash or any source NpcId to the captured AO identity, so placement name, level, coordinates, MonsterData, and respawn context remain corroborating only.

Remaining blockers: No accepted record links this numeric TemplateHash or a source NpcId to the captured identity.

Evidence paths: `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/mobtemplate.sql`, `Captures/ICC Shuttleport [PF 4582] - 20260818-214552/enemy-dossier.json`, `docs/generated/aosharp_capture_inventory.csv`, `docs/reference/pf4582/PlayfieldDistrictInfo.json`, `docs/reference/pf4582/template-hash-evidence.json`.

### 1514951251 (0x5A4C5253, SRLZ)

Classification: `CANDIDATE`. Placements blocked: 9. Candidate profiles: mobtemplate:A000.

CANDIDATE: The exact captured Surf Lizard identity and the unique same-name SQL profile agree on MonsterData and level range. No direct evidence connects the numeric TemplateHash or any source NpcId to the captured AO identity, so placement name, level, coordinates, MonsterData, and respawn context remain corroborating only.

Remaining blockers: No accepted record links this numeric TemplateHash or a source NpcId to the captured identity. Observed respawn timing cannot supply the missing identity bridge.

Evidence paths: `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/mobtemplate.sql`, `Captures/ICC Shuttleport [PF 4582] - 20260818-214552/enemy-dossier.json`, `docs/generated/aosharp_capture_inventory.csv`, `docs/reference/pf4582/PlayfieldDistrictInfo.json`, `docs/reference/pf4582/template-hash-evidence.json`.

## Evidence boundary

The complete repository and accepted PF4582 capture corpus was searched. None of the 24 unresolved decimal hashes or their source NpcIds occurs in a capture identity record. Exact names, captured MonsterData, NpcFamily, level, scale, coordinates, and respawn correlations therefore remain corroborating rather than direct hash evidence.

Evidence source categories inspected:

- Existing PF4582 runtime definitions and generated catalogs
- Governed runtime-evidence-map.json and placement importer tests
- mobtemplate.sql and related repository template/profile catalogs
- Accepted AOSharp capture inventory and retention records
- All accepted PF4582 raw and normalized capture artifacts
- PF4582 enemy dossier, movement, lifecycle, respawn, interaction, and vendor artifacts
- Captured appearance, combat, movement, social, vendor, guard, and identity contracts
- AORebirth mob archetypes and playfield runtime catalogs
- Generated capture-backed enemy inventories and unresolved audits
- Historical PF4582 documentation and source inventories

Accepted PF4582 capture inventory scope:

| Capture ID | Evidence digest | Validation | Raw packet evidence |
|---|---|---|---|
| 20260625-184019 |  | running | packets.hex.log |
| 20260625-184459 |  | running | packets.hex.log |
| 20260814-014647 | 9cc0a67d1b9e9df625bb70da6e6a067fc79f1a30adfdb5d2298e64ff98593637 | complete | both |
| 20260814-015856 | d8fc070e5c5d2db1d00616494e57ae6e89cdca7f725774ef5f6a103ecaf20930 | complete | both |
| 20260818-214552 | 1efdae64b0339bf658b42225cd51f8e058b73e5ad2ca309958caace07c1288d3 | incomplete | both |
| 20260819-014109 | 2d1f2c9bad33650cb37938be823ac41cd9ee20af18a397ebe3c247174c026d82 | complete | both |
| 20260819-015104 | 3e573babb52f67da52f93897eb8e2a1208df6ec2914e8ee6f2d9c9d72811115c | complete | both |
| 20260820-234653 | fff9d66eaa4fb7017626e2ff3c4c7895ebe9af28967507007d4b676b4c7c88ea | complete | both |
| 20260820-235024 | 79a44c621dd3f9c931da3722de449a12d7ef50dd386f952b8954451ca0f21807 | complete | both |
| 20260820-235749 | a6cd8bb1b61ea8f90d2d10f28788d7887ca713ea7cf27f41b7f67a82d0dfa67e | complete | both |
| 20260821-011636 | 71e23f2964231df971ab4bc166e2818f3bfae5fa1157ea0eb71a26807be3161b | complete | both |
| 20260821-012157 | cc1e1d7cc1b0d4697a3778da15b62c6e718e78eca534db23f217f8e3b239499e | complete | both |
| 20260821-012720 | 0652f321b6220a8e27e00b6dc91457329976ccfa9af321b2fb9c304fb0a9ec0c | running | both |
| 20260821-013914 | 462b8683bbeb465ea3b23ee2c7b99710fd5727c85cf885af79f54f89a26135aa | complete | both |
| 20260821-034134 | 933a36ccbd213df62b56848f6615812952aa0f4426adbc8c60785a369c343ea2 | complete | both |
| 20260821-212809 | e67a2dd3a7630498f8daaa553099fd113133f960796e407ca4d107fb267af604 | incomplete | none |
| 20260821-212904 | f8ead4d6f2b38f235233c3498621d78b46c47428c0cf4b4f056849c93a3a0e5c | incomplete | none |
| 20260821-214401 | 472aaf5735f6864dbb67da733c38457738a59a31c426bee265cdc78da483f71f | complete | both |
| 20260821-221848 | 392401d7fa299288800ca598d14efa3d31f1f7a974f178a96f3b3242e21fcaa6 | complete | both |
| 20260821-222657 | b8ca07e39bf8cde42b79cac03e610094c9f6467f5109116507f48872a83d2089 | complete | both |
| 20260821-224042 | 940f343463d33ed364aaec5309a70d959ff8afb1de568a08c294ccbcbc4656c7 | complete | both |
| 20260821-224352 | eee72491e985483d70ad65694bfad732c1b58a0ec4f6c4f727fcfd6f1fd73c56 | complete | both |
| 20260821-225212 | 392f2992da399d81b42fa3d643f85241d538ee7e9e9077f9cc43104729ca0e3c | incomplete | both |
| 20260821-225854 | e6e6b35457044d399e03ddc87f15d98aa82a5bed60195144d61df43c79d98686 | incomplete | both |
| 20260821-230553 | 879285dcdbb076d0ededc5baf54398cf4a2722b91248e10001d9c33662254eef | complete | both |

Pinned evidence digests:

- `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/mobtemplate.sql`: `75bd10fed1bd92d8ce1de16bb7179c97cf0e1dab8461eb4719d6f1c567584dfb`
- `AORebirth/Server/ZoneEngine/Core/CombatTestMobArchetype.cs`: `4e1be24bbf9afe7b035afd125b705b60df5b6e94e36acb7f117ae733aced5774`
- `AORebirth/Server/ZoneEngine/Core/Playfields/IccShuttleportSpawn.cs`: `7f0381d89cdaf3670425877d95f9014977c190506a878a262e082ebeb3ec0df7`
- `Captures/ICC Shuttleport [PF 4582] - 20260818-214552/enemy-dossier.json`: `36ef949c3e7fa40781ee52f78dfa164954957bb94404a1dcd4a2f13ccd924d24`
- `Tools/tests/test_generate_pf4582_placements.py`: `0df11e4f739407887024996b342bf0f743f3f3e5cfcf4574f426f8f26fececcb`
- `docs/generated/aosharp_capture_inventory.csv`: `8a217cc45121bf850cdadb4219a4457f30a339f832777f4f70a192feeb68e082`
- `docs/reference/pf4582/PlayfieldDistrictInfo.json`: `b747aea145cb36e3f9be5b2cacc7aaebca3d24017a14540ac1f29f4bd1296b32`
- `docs/reference/pf4582/runtime-evidence-map.json`: `02a1b167b97d1caa223aeaa60eaebbaf0a1e99ce6ddce753f7d54eae1f716869`

## Governance conclusion

No unresolved hash is newly proven. Candidate and ambiguous results remain non-runtime. A future promotion requires a stable accepted record that directly joins a source TemplateHash or source NpcId to one AO identity/profile; another name-, level-, position-, or timing-only capture will not close that gap.

Runtime remained 25 active and 181 blocked. No production, client, capture, or database operation was performed, and no commit or push is part of this audit.
