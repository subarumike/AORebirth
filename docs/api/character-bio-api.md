# Character Bio API (PORK-compatible, AOR Live)

Status: specification, approved in principle by the project owner. No code, schema, or migration has been applied yet.

## 1. Purpose

Provide read-only, text-first character and organization bio data to external chatbots ("bebot", PHP) for commands such as `!whois`. The output mirrors the retail Anarchy Online PORK endpoint so that existing consumers work unchanged.

Out of scope: profile photos, PvP title preference, tower/notum data, Global Market Interface data, dynel state export (separate APIs).

## 2. Scope and identifiers

| Item | Value |
|---|---|
| Dimension (AOR Live) | `11` |
| Dimension for localhost test | `10` (out of scope here) |
| Character `Type` in `stats` | `50000` (`IZoneWorld.cs`, `CharacterType`) |
| Character lookup key | `characters.Name` (nickname) |

## 3. Endpoints

Retail-compatible routes:

```
GET /character/bio/d/11/name/{nick}/bio.xml
GET /character/bio/d/11/name/{nick}/bio.xml?data_type=json
```

- `{nick}` is matched against `characters.Name`. Matching is case-insensitive, as determined by the database collation. The exporter must confirm that `Name` is unique.
- Unknown nick: HTTP `404`.
- Response `Content-Type`: `text/xml;charset=utf-8` for XML, `application/json` for JSON.
- Files are served as static artifacts (see section 9). The endpoint must not expose database internals.

## 4. Data sources

### 4.1 Database

| Table | Columns used | Notes |
|---|---|---|
| `characters` | `Id`, `Name`, `FirstName`, `LastName` | `Username` (account) must never be exported. |
| `stats` | `Type`, `Instance`, `StatId`, `StatValue` | Key `(Type, Instance, StatId)`. Filter `Type = 50000`, `Instance = characters.Id`. |
| `organizations` | `Id`, `Name`, `GovernmentForm` | Looked up by the org ID from stat 5. |
| `character_bio_meta` | `id_char`, `last_updated` | New table, see section 6. |

### 4.2 Character stats (verified in `CharacterStat.cs`)

| Stat ID | Hex | Meaning | Value domain |
|---|---|---|---|
| 4 | `0x04` | Breed | 1–4 |
| 5 | `0x05` | Clan (org ID) | org ID, 0 if none |
| 33 | `0x21` | Side | 0 Neutral, 1 Clan, 2 Omni |
| 37 | `0x25` | TitleLevel | 1–7 |
| 48 | `0x30` | ClanLevel (org rank index) | index into governing form ranks |
| 54 | `0x36` | Level | 1–220 |
| 59 | `0x3B` | Sex | 0 Neuter, 1 Male, 2 Female |
| 60 | `0x3C` | Profession | 1–14 |
| 169 | `0xA9` | AlienLevel (defender rank) | 0–30 |
| 333 | `0x14D` | PvP_Rating | integer |

`VisualSex` (stat 369, `0x171`) is not used. It matches `Sex` on the test characters.

### 4.3 GameData (`D:\Funcom\GameData`)

| File | Used for |
|---|---|
| `Breed.json` | Breed name (1 Solitus, 2 Opifex, 3 Nanomage, 4 Atrox) |
| `Sex.json` | Gender name |
| `Side.json` | Faction name (Neutral, Clan, Omni) |
| `Profession.json` | Profession name (1 Soldier … 14 Shade) |
| `GoverningForms.json` | Org form and rank names |
| `Text.json` category `1060` | Profession title, id = `profession × 100 + TitleLevel` |
| `Text.json` category `1169` | Defender rank name, id = `AlienLevel × 100` |

Verified examples:
- Kingpin = id 407 = Fixer (4) × 100 + 7.
- Godfather = id 907 = Enforcer (9) × 100 + 7.
- Challenger = id 2200 = AlienLevel 22.

## 5. Field mapping

### 5.1 Derived values

- **TitleLevel**: read stat 37. If the stat is absent, derive it from Level using this table (source: wiki.aodb.us Player_Level):

  | Level from | TitleLevel |
  |---|---|
  | 1 | 1 |
  | 15 | 2 |
  | 40 | 3 |
  | 100 | 4 |
  | 150 | 5 |
  | 190 | 6 |
  | 205 | 7 |

- **Profession title**: `Text.json` category 1060 with id `Profession × 100 + TitleLevel`. Empty if Profession is 0 or the id is missing.
- **Defender rank name**: `Text.json` category 1169 with id `AlienLevel × 100`. Names match ao-universe "AI Levels & You" (Fledgling … Vindicator). The HTML retail page shows the rank +1; XML and JSON are authoritative.
- **Organization rank name**: `GoverningForms.json`, form indexed by `organizations.GovernmentForm`, rank indexed by stat 48.

### 5.2 Character fields

| Retail XML | Retail JSON key | Source | Rule |
|---|---|---|---|
| `name/firstname` | `FIRSTNAME` | `characters.FirstName` | as stored |
| `name/nick` | `NAME` | `characters.Name` | as stored |
| `name/lastname` | `LASTNAME` | `characters.LastName` | as stored |
| `basic_stats/level` | `LEVELX` | stat 54 | integer |
| `basic_stats/breed` | `BREED` | stat 4 | GameData name |
| `basic_stats/gender` | `SEX` | stat 59 | GameData name |
| `basic_stats/faction` | `SIDE` | stat 33 | GameData name |
| `basic_stats/profession` | `PROF` | stat 60 | GameData name |
| `basic_stats/profession_title` | `PROFNAME` | derived 5.1 | may be empty |
| `basic_stats/defender_rank_id` | `ALIENLEVEL` | stat 169 | integer 0–30 |
| `basic_stats/defender_rank` | `RANK_name` | derived 5.1 | GameData name |
| n/a | `PVPRATING` | stat 333 | integer, JSON only |
| n/a | `PVPTITLE` | none | always empty string |
| n/a | `HEADID` | none | `0` |
| n/a | `CHAR_DIMENSION` | constant | `11` |
| n/a | `CHAR_INSTANCE` | `characters.Id` | integer |
| `pictureurl` | n/a | none | empty element |
| `smallpictureurl` | n/a | none | empty element |

### 5.3 Organization fields

| Retail XML | Retail JSON key | Source | Rule |
|---|---|---|---|
| `organization_membership/organization_id` | `ORG_INSTANCE` | stat 5 | integer |
| `organization_membership/organization_name` | `NAME` | `organizations.Name` | as stored |
| `organization_membership/rank` | `RANK_TITLE` | derived 5.1 | GameData name |
| `organization_membership/rank_id` | `RANK` | stat 48 | integer |
| n/a | `ORG_DIMENSION` | constant | `11` |

Rule: if stat 5 is `0` or absent, or the organization row is missing, omit `<organization_membership>` in XML and emit `null` in the JSON organization slot.

### 5.4 Timestamp

- XML: `<last_updated>yyyy/MM/dd HH:mm:ss Universal</last_updated>`.
- JSON: third array element, `yyyy/MM/dd HH:mm:ss`, no suffix.
- Value: `character_bio_meta.last_updated` converted to UTC. If no row exists for the character, use the export generation time.

## 6. Database change (requires approval, approved in principle)

```sql
CREATE TABLE character_bio_meta (
  id_char      INT UNSIGNED PRIMARY KEY,
  last_updated DATETIME(6) NOT NULL
);
```

- Existing tables are not modified.
- Written as an UPSERT whenever a notable change happens:
  - level change, normal or alien;
  - joining, leaving, or rank change in an organization;
  - first or last name change.
- Hook points must be identified in the engines before implementation. A DB trigger is an alternative and needs separate approval.
- Characters without a row use the export generation time as a fallback.

## 7. Output format

### 7.1 XML (retail structure)

```xml
<character>
	<name>
	<firstname>Thomas</firstname>
	<nick>Glitch</nick>
	<lastname>Hitandrun</lastname>
	</name>
	<basic_stats>
		<level>220</level>
		<breed>Opifex</breed>
		<gender>Male</gender>
		<faction>Omni</faction>
		<profession>Fixer</profession>
		<profession_title>Kingpin</profession_title>
		<defender_rank_id>22</defender_rank_id>
		<defender_rank>Challenger</defender_rank>
	</basic_stats>
	<pictureurl>
	</pictureurl>
	<smallpictureurl>
	</smallpictureurl>
	<organization_membership>
		<organization_id>6814</organization_id>
		<organization_name>A Better Tomorrow</organization_name>
		<rank>President</rank>
		<rank_id>0</rank_id>
	</organization_membership>
	<last_updated>2026/11/13 05:58:14 Universal</last_updated>
</character>
```

The example above is the retail Glitch record (dimension 5). The AOR output must have the same tags and nesting; only values differ.

### 7.2 JSON (retail positional array)

```json
[
  {"SEX":"Male","BREED":"Opifex","PVPRATING":1701,"NAME":"Glitch","FIRSTNAME":"Thomas","PVPTITLE":"","LASTNAME":"Hitandrun","CHAR_DIMENSION":11,"ALIENLEVEL":22,"RANK_name":"Challenger","HEADID":0,"PROFNAME":"Kingpin","LEVELX":220,"PROF":"Fixer","CHAR_INSTANCE":649213,"SIDE":"Omni"},
  {"ORG_DIMENSION":11,"RANK_TITLE":"President","ORG_INSTANCE":6814,"NAME":"A Better Tomorrow","RANK":0},
  "2026/11/13 05:58:14"
]
```

- Element 0: character object, keys in the order shown.
- Element 1: organization object, or `null` when there is no organization.
- Element 2: timestamp string.

The retail Glitch values (`PVPRATING`, `HEADID`, `CHAR_INSTANCE`) are examples only and are not AOR data.

Compatibility choices (confirmed by owner):
- Keep retail key names and casing exactly, including `RANK_name` (JSON) and `defender_rank` (XML).
- Keep the positional JSON array.
- Keep the empty picture elements.

## 8. Error and edge handling

| Case | Behavior |
|---|---|
| Nick not found | HTTP 404 |
| Stat missing for a character | Treat as `0`; derived names become empty |
| Profession `0` | `profession_title` empty |
| AlienLevel `0` | `defender_rank` = name of id 0 from `Text.json` (`None`) |
| Org missing or stat 5 = 0 | Organization block omitted (XML) or `null` (JSON) |
| GameData id not found | Log a warning; emit an empty string, do not fail the whole export |
| Nick ambiguous (more than one row) | Must be prevented by a uniqueness check; export fails for that nick |

## 9. Generation and serving

- The exporter writes one file per character, using the retail-compatible names above, to the publish path configured in `.env`.
- Incremental run: select characters where `character_bio_meta.last_updated` is newer than the last successful export.
- Full sweep: run at a low frequency (at most every 24 hours) to repair drift.
- Writes must be atomic: write to a temporary file, then rename.
- Served by nginx as static files, with ETag or `Last-Modified` for polling bots.
- Export failures must not affect the game engines. The exporter is an optional module.

## 10. Security and privacy

- Never expose `characters.Username`, IP data, or any account field.
- Never expose private GameData in public repositories. Only derived names appear in output.
- Read-only database access for the exporter.
- No user input reaches SQL; the nick is parameterized.

## 11. Open items

- Confirm the hook points for `character_bio_meta` updates in the engines.
- Confirm the uniqueness of `characters.Name`.
- Confirm the backfill policy for characters without a `character_bio_meta` row.
- Confirm the `.env` variable name and publish path for the exporter.

## 12. Sources

- Repository: `AORebirth\Libraries\Source\AOtomation\...\GameData\CharacterStat.cs`, `IdentityType.cs`, `IZoneWorld.cs`.
- Repository: `AORebirth\Libraries\Source\AORebirth.Database\SqlTables\characters.sql`, `organizations.sql`, `stats.sql`.
- Game data: `D:\Funcom\GameData\Breed.json`, `Sex.json`, `Side.json`, `Profession.json`, `GoverningForms.json`, `Text.json`.
- Retail reference: `https://people.anarchy-online.com/character/bio/d/5/name/Glitch/bio.xml` and `?data_type=json`.
- Level and title reference: `https://wiki.aodb.us/wiki/Player_Level`.
- Alien level names: `https://www.ao-universe.com/guides/alien-invasion/gameplay-guides-3/ai-levels-you`.
