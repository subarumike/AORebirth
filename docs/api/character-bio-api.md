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
GET /character/bio/d/11/name/{nick}
GET /character/bio/d/11/name/{nick}/bio.xml
GET /character/bio/d/11/name/{nick}/bio.xml?data_type=json
```

The HTML page (section 5.6) is served at the first route.

Organization stats routes (section 5.5):

```
GET /org/stats/d/11/name/{orgId}
GET /org/stats/d/11/name/{orgId}/basicstats.xml
GET /org/stats/d/11/name/{orgId}/basicstats.xml?data_type=json
```

- `{nick}` is matched against `characters.Name`. Matching is case-insensitive, as determined by the database collation. The exporter must confirm that `Name` is unique.
- Unknown nick: HTTP `404`.
- Response `Content-Type`: `text/xml;charset=utf-8` for XML, `application/json` for JSON.
- Files are served as static artifacts (see section 9). The endpoint must not expose database internals.

### 3.1 Published files

The file names are the retail names. The exporter writes one folder per entity under the export path (`PORK_EXPORT_PATH`), and the folder is the retail route:

```
character/bio/d/11/name/{nick}/index.html      page (section 5.6)
character/bio/d/11/name/{nick}/bio.xml         XML (section 5.2)
character/bio/d/11/name/{nick}/bio.json        JSON (section 5.3), served for bio.xml?data_type=json
org/stats/d/11/name/{orgId}/index.html         page (section 5.5)
org/stats/d/11/name/{orgId}/basicstats.xml     XML
org/stats/d/11/name/{orgId}/basicstats.json    JSON, served for basicstats.xml?data_type=json
```

- `{nick}` in the folder name is the nick in lower case, so that the lookup is case-insensitive on every file system (the retail lookup is case-insensitive).
- The `.json` file is a static file that stands for the retail query string `?data_type=json`. Retail has no such file name, so the web server maps `data_type=json` on `bio.xml` to `bio.json`, and on `basicstats.xml` to `basicstats.json`, and sends the content types above. This mapping, the folder index and the `404` for an unknown nick are the web server configuration, which Mike owns (section 14).
- The dimension in the paths is `PORK_DIMENSION` (default `11`).
- The online lists (section 12) are AOR-specific. Their path and format are defined in section 12.6.

## 4. Data sources

### 4.1 Database

| Table | Columns used | Notes |
|---|---|---|
| `characters` | `Id`, `Name`, `FirstName`, `LastName` | `Username` (account) must never be exported. |
| `stats` | `Type`, `Instance`, `StatId`, `StatValue` | Key `(Type, Instance, StatId)`. Filter `Type = 50000`, `Instance = characters.Id`. |
| `organizations` | `Id`, `Name`, `GovernmentForm`, `LeaderId`, `Description`, `Objective`, `History` | Looked up by the org ID from stat 5. No side column: the faction is derived (section 5.5). |
| `character_bio_meta` | `id_char`, `last_updated` | New table, see section 6. |
| `org_online_meta` | `id_org`, `last_updated` | New table, see section 12.2. |
| `org_stats_meta` | `id_org`, `last_updated` | Approved table, see section 5.5. |

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
| n/a | `PVPRATING` | stat 333 (`PvP_Rating`) | integer, JSON only; see 5.2a |
| n/a | `PVPTITLE` | derived from stat 333 | Legacy title band; empty below 1400; see 5.2a |
| n/a | `HEADID` | none | `0` |
| n/a | `CHAR_DIMENSION` | constant | `11` |
| n/a | `CHAR_INSTANCE` | `characters.Id` | integer |
| `pictureurl` | n/a | none | empty element |
| `smallpictureurl` | n/a | none | empty element |

#### 5.2a PvP fields

- `PVPRATING` is the stat 333 value from the database. `NewCharacter.json` sets it to 1300 at creation.
- `PVPTITLE` is the Legacy title for the rating band. Band names come from `Text.json` category 2010, and their IDs overlap the Duel IDs (2002–2010). Band thresholds are not verified yet (wiki.aodb.us, marked outdated). Provisional: 1400 Freshman, 1500 Rookie, 1600 Apprentice, 1700 Novice, 1800 Neophyte, 1900 Experienced, 2000 Expert, 2100 Master, 2300 Champion, 2500+ Grand Master. Grand Master has ID 158737026; Master has no confirmed ID.
- Sample check: retail Glitch (rating 1701) is `Novice`, confirmed by the retail HTML title. The retail org JSON `PVPTITLE` reads `Neophyte`, one band too high, so it is a retail error. AOR publishes the correct band (`Novice`) and does not copy that error. The 1700 threshold is therefore consistent with the sample; the other thresholds stay provisional.
- AOR status: the in-game Legacy title is implemented (see 5.2b). `PVPTITLE` for the Bio API must use the same bands as the game (`PvpLegacyTitles.json`).

#### 5.2b PvP titles in game (validated)

Retail is the reference. Retail captures show the following behaviour, and AOR reproduces it.

- **Display mode**: stat 673 (`VisualFlags`) bits `0x180` select the mode: 0 Legacy, 1 Duel, 2 Solo, 3 Team. Toggling it (`CharacterAction 0xA6`) makes the server answer with the stat 673 update only. There is no appearance update. AOR does the same (`Player.TryApplyVisualFlags`).
- **Solo, Duel, Team titles**: the server sends only the scores in the character info packet (fields 35-42). The client builds the title text and the overhead prefix itself, so no title text is on the wire.

  | Info packet field | Stat |
  |---|---|
  | `PvpDuelWins` | 674 |
  | `PvpDuelLoses` | 675 |
  | `PvpProfessionDuelLoses` | 676 (hypothesis) |
  | `PvpSoloKills` | 678 |
  | `PvpTeamKills` | 680 |
  | `PvpSoloScore` | 682 |
  | `PvpTeamScore` | 683 |
  | `PvpDuelScore` | 684 |

- **Legacy title**: no retail capture contains a non-empty Legacy title (the retail character used has rating 1350, below the first band). The server computes it from stat 333 with `GameData/PvpLegacyTitles.json`, and sends it in the info packet `LegacyTitle` (playercard). The retail client does not receive stat 333.
- **Legacy overhead (editorial decision, not captured)**: when the mode is Legacy, the name sent in the entity update is prefixed with the title (for example `Grand Master Technykk`). It was validated in game for self and others, with no side effects (friend list included). Other modes keep the plain name.
- **Refresh**: overhead and playercard update on toggle without relog, for self and others. The overhead refreshes when the character is clicked or targeted (F1 self, Shift-Tab other).
- **`PvpLegacyTitles.json`**: `TextCategory` (2010) and `Bands` with `MinRating`, `TextId`, `Fallback`, `Note`. The name comes from `Text.json`. If `TextId` is null or has no text, `Fallback` is used, so `Text.json` stays untouched. Master (2100) uses `Fallback` "Master". Only the 1400 threshold is confirmed. The others are provisional.
- **Known gap**: the cross-instance `PlayerCard` (`world.proto`) has no PvP fields. Inspecting a character on another ZoneEngine instance shows zeros and an empty `LegacyTitle`. Fixing it needs additive proto fields, which are not approved yet.

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

### 5.5 Organization stats page (`/org/stats/d/11/name/{orgId}`)

Retail reference: `https://people.anarchy-online.com/org/stats/d/5/name/6814`, with `basicstats.xml` and `basicstats.xml?data_type=json` linked at the bottom. The page has a header, statistics, a member table and the links. Retail XML carries the identity, the faction and the roster only. The statistics and the header text (governance, description, objective, history) appear only in the JSON and in the HTML.

Header:

| Page field | XML | JSON key | Source | Rule |
|---|---|---|---|---|
| Organization id | `id` | `ORG_INSTANCE` | route key, `organizations.Id` | integer |
| Name | `name` | `NAME` | `organizations.Name` | as stored |
| Faction | `side` | `SIDE`, `SIDE_NAME` | stat 33 of the leader (`organizations.LeaderId`), `Side.json` | The leader is always on the organization's side; otherwise the organization is disbanded. |
| Governance | n/a | `GOVERNINGNAME` | `GoverningForms.json`, indexed by `organizations.GovernmentForm` | GameData name |
| Description | n/a | `DESCRIPTION` | `organizations.Description` | trimmed; the database default `'" "'` counts as empty |
| Objective | n/a | `OBJECTIVE` | `organizations.Objective` | same rule |
| History | n/a | `HISTORY` | `organizations.History` | same rule |
| Dimension | n/a | `ORG_DIMENSION` | constant | `11` |
| Last updated | `last_updated` | last element of the array, no key | render time of the export | UTC, format of section 5.4. As on retail, this is the time the file was rendered, not the content change time. |

Empty Description, Objective or History: the HTML shows "No ... added." as retail does. The JSON value is an empty string.

Statistics (HTML and JSON element 0):

- `NUMMEMBERS`: count of characters whose stat 5 equals the org id. Applicants (rank 6 in Department) are included, as the retail example shows (63 members).
- `MINLVL` and `MAXLVL`: minimum and maximum of stat 54.
- `AVGLVL`: average of stat 54. The JSON keeps full precision (82.9524 in the example). The HTML rounds up to an integer (82.9524 shows 83). Rounding up is accepted.
- Breed counts, from stat 4 (`Breed.json`): `SOLITUSCOUNT`, `OPIFEXCOUNT`, `ATROXCOUNT`, `NANORACECOUNT`.
- Gender counts, from stat 59 (`Sex.json`): `MALECOUNT`, `FEMALECOUNT`, `NEUTERCOUNT`.
- Profession counts, from stat 60 (`Profession.json`): `ADVENTURERCOUNT`, `AGENTCOUNT`, `BTCOUNT`, `DOCTORCOUNT`, `ENFCOUNT`, `ENGINEEERCOUNT`, `FIXERCOUNT`, `KEEPERCOUNT`, `MACOUNT`, `METACOUNT`, `NANOCOUNT`, `SHADECOUNT`, `SOLIDERCOUNT`, `TRADERCOUNT`. Retail key spellings are kept, typos included, because existing consumers read the keys.
- `MONSTERCOUNT`: published by retail as the dynel tag (mobs and bosses, which cannot join an organization). Always `0` for organizations.
- Percentages (HTML only, one decimal): `count × 100 / NUMMEMBERS`, rounded up to one decimal. The percentages are not forced to total 100.
- Zero counts are published as `0` in JSON and shown as `0` in HTML, as retail does.
- No members: min, average and max are empty (`null` in JSON) and percentages are omitted. There is no division by zero.

Members (XML `members/member`, JSON element 1, HTML table):

| XML | JSON key | Source |
|---|---|---|
| `firstname`, `nickname`, `lastname` | `FIRSTNAME`, `NAME`, `LASTNAME` | `characters` |
| `rank`, `rank_name` | `RANK`, `RANK_TITLE` | stat 48, `GoverningForms.json` |
| `level` | `LEVELX` | stat 54 |
| `profession`, `profession_title` | `PROF`, `PROF_TITLE` | stat 60, `Profession.json`, `Text.json` category `1060` |
| `gender` | `SEX` | stat 59, `Sex.json` |
| `breed` | `BREED` | stat 4, `Breed.json` |
| `defender_rank_id`, `defender_rank` | `ALIENLEVEL`, `DEFENDER_RANK_TITLE` | stat 169, `Text.json` category `1169` |
| `photo_url`, `smallphoto_url` | not published | retail CDN, section 10 |

- The JSON member object also has `CHAR_INSTANCE` (`characters.Id`, the in-game character id), `CHAR_DIMENSION` (`11`), `PVPRATING` (stat 333), `PVPTITLE` (section 5.2a) and `HEADID` (`0`).
- Order: by rank (stat 48), then `characters.Id`. This order is cosmetic and not a contract. Retail's order within one rank looks unsorted, possibly SELECT order, so consumers must not depend on it.
- Route key: `{orgId}` is `organizations.Id`, as in the retail example. An unknown id returns HTTP `200` with a null stub, as on retail: every organization field is `null`, `NUMMEMBERS` is `0` and the member list is empty. It is not a `404`.

Change tracking: `org_stats_meta`, approved, with the same pattern as `org_online_meta` (section 12.2) and `character_bio_meta` (section 6):

```sql
CREATE TABLE org_stats_meta (
  id_org       INT UNSIGNED PRIMARY KEY,
  last_updated DATETIME(6) NOT NULL,
  KEY ix_org_stats_meta_last_updated (last_updated)
);
```

- The engines upsert it with the current UTC time, in the same way as `org_online_meta` (section 12.2), when a field of this page changes:
  - join, leave or rank change (stats 5 and 48), for the old and the new organization;
  - level, profession, breed, gender or alien level change (stats 54, 60, 4, 59, 169) of a member;
  - first, nick or last name change of a member;
  - writes to `organizations` fields `Name`, `GovernmentForm`, `LeaderId`, `Description`, `Objective`, `History` (`MySqlCharacterPersistenceDao`, section 4.1);
  - side change (stat 33) of the current leader;
  - dissolution is handled by the sweep (section 9.1).
- Change detection uses the same pass as section 12.3, with the same watermark and overlap. A change re-renders the page once, and that render sets `last_updated` to the render time.
- The daily sweep re-renders every page in memory and compares it with the file, ignoring the `last_updated` line. It rewrites only the pages whose content differs. A difference with no `org_stats_meta` change means a missing hook. Log it as a warning.
- The heartbeat (section 12.4) touches only the online files. Stats pages are not touched, because they describe content, not presence, and carry no freshness promise. The daily sweep refreshes them: unchanged pages are not rewritten, and a page whose content changed without a hook is corrected at most 24 hours later. Their `last_updated` is the render time of the last real change or sweep rewrite.

### 5.6 Character page (`/character/bio/d/11/name/{nick}`)

Retail reference: `https://people.anarchy-online.com/character/bio/d/5/name/Glitch`, with the XML and JSON links at the bottom. The page is built from the same data as section 5.2 and 5.3. It uses the same `{nick}` lookup and the same `404` rule as the XML route (section 3). Only the presentation differs.

| Page element | Source | Rule |
|---|---|---|
| Title | PvP title (section 5.2a) | Shown before the name, in italics, as `_Title_`. Omitted when empty. |
| Name | `characters.FirstName`, `characters.Name`, `characters.LastName` | Empty parts are omitted. The nick is shown in double quotes. |
| Level | stat 54 | integer |
| Breed | stat 4, `Breed.json` | GameData name |
| Gender | stat 59, `Sex.json` | GameData name |
| Faction | stat 33, `Side.json` | GameData name |
| Profession | stat 60, `Profession.json`, with the profession title in parentheses | Title from section 5.1 (`Text.json` category 1060). Omitted when empty. |
| Organization | stat 5 (organization rank: stat 48, section 5.1), `organizations.Name` | Shown as `{rank name} of {organization name}`, for example `President of A Better Tomorrow`. The organization name links to `/org/stats/d/11/name/{orgId}` (section 5.5). Omitted when stat 5 is `0` or absent. |
| Defender rank | stat 169, `Text.json` category 1169 | Shown as `Defender Rank : (n)`, with `n` the real stat 169 value. Retail shows the value +1; AOR does not copy that offset. |
| Links | n/a | `bio.xml` and `bio.xml?data_type=json` at the bottom, as in section 3. |
| Face image | n/a | Not copied. Retail loads it from the Funcom CDN (section 10). |

- The organization line uses the same rank name as the member table of section 5.5 (`GoverningForms.json`, indexed by the governing form and stat 48).
- The page is rewritten only when its content changes, under the same rules as the bio XML and JSON (section 9).

## 6. Database change (approved)

Migration `20261116_pork_meta.sql` creates the three change-tracking tables of this specification (`character_bio_meta`, `org_stats_meta` in section 5.5, and `org_online_meta` in section 12.2). The engines require it (`SchemaContract`).

```sql
CREATE TABLE character_bio_meta (
  id_char      INT UNSIGNED PRIMARY KEY,
  last_updated DATETIME(6) NOT NULL,
  KEY ix_character_bio_meta_last_updated (last_updated)
);
```

The index serves the change-detection query of section 9, as for the two organization tables. It was added to the first approved definition, which had none.

- Existing tables are not modified.
- Written by the engine as an UPSERT with the current UTC time whenever a published field of the character changes (list supplied by the owner):
  - nick, first name or last name change;
  - Legacy PvP title change, that is a stat 333 change that crosses a title band (section 5.2a). Normally impossible, since the Duel mode supersedes it, so it only concerns old retail characters;
  - level change (stat 54, normal or shadow) or alien level change (stat 169, defender rank);
  - gender or breed change (stats 59 and 4, rare);
  - organization rank change (stat 48), leaving an organization, or joining another one (stat 5; a lockout of 6 hours separates leaving from joining);
  - character creation. This covers the rare case of a character deleted and recreated at once with the same nick: the new character has a new id and needs its own row. The published file is keyed by nick, so it is overwritten.
- The exact write sites are mapped in section 6.1. They are all in the shared database layer, not in the engines themselves. A DB trigger is an alternative and needs separate approval.
- Characters without a row use the export generation time as a fallback. The first sweep exports every existing character this way, so no backfill is needed.
- Character deletion is a hard delete (section 9.1). The row is removed, so no hook is needed on `character_bio_meta`; an orphan meta row is ignored by the exporter.
- Organization-wide changes alter the published bio of every member: the organization name, the governing form that names the ranks (it also resets the member ranks), and a dissolution that clears stat 5. Decision (owner): the same transaction upserts `character_bio_meta` for every member, with one statement of this shape:

```sql
INSERT INTO character_bio_meta (id_char, last_updated)
SELECT Instance, UTC_TIMESTAMP(6) FROM stats
WHERE Type = 50000 AND StatId = 5 AND StatValue = @Org
ON DUPLICATE KEY UPDATE last_updated = VALUES(last_updated);
```

  For a dissolution, it runs before the stat 5 rows are deleted. This is the technique already planned for `RecoverStaleOnline` (section 12.2).
- The owner's list misses two published fields that the code can change: faction (stat 33) and profession (stat 60), with the title level (stat 37) that names the profession title. They are added to the compared set of section 6.1. A faction change also ends the organization membership (`ClearOrganizationMembership`), so it is rare.

### 6.1 Hook map (engine write sites)

Mapped from the code. All hooks are implemented as inline SQL in the existing classes (no new file), and `ZoneEngine_New` compiles with them. `MySqlCharacterPersistenceDao` is in `AORebirth.Database\Domain\Characters`; its private helpers `TouchCharacterBio`, `TouchOrgStats`, `TouchOrgOnline`, `TouchOrgMembersBio` and `TouchChangedBioStats` hold the upserts. Every row writes in the same transaction as the change it follows, so a hook failure rolls back that change. Each upsert uses `UTC_TIMESTAMP(6)`.

Implementation notes:
- **Stat 333**: the DB layer has no access to the title bands (`PvpLegacyTitles.json`, read by the zone engine), so it touches the bio on any change of stat 333, not only on a band crossing. This only costs one extra re-export, and the stat is practically constant (Duel mode supersedes the Legacy rating).
- `WriteStats` adds one `SELECT` on the compared stats per save that carries at least one of them. Its cost is part of the localhost measurement.
- Organization text, rank, leader and governing form write unconditionally, because the number of affected rows is not reliable on `ON DUPLICATE KEY UPDATE`.
- `LoginEngine` creation, deletion and online hooks are in `CharacterName.cs`, `CharacterDao.DeleteOwnedData`, `MySqlCharacterDao.WriteOnline` and `RecoverStaleOnline`. A deleted character's own `character_bio_meta` row is kept; the sweep (section 9.1) reconciles it.

| Change | Write site | Tables touched |
|---|---|---|
| Level, alien level, breed, gender, faction, profession, title level, PvP rating band (stats 54, 169, 4, 59, 33, 60, 37, 333) | `MySqlCharacterPersistenceDao.WriteStats`, reached through `SaveStats`, `SaveSnapshot` and `SaveOnlineCheckpoint` (the zone engine passes the changed stats for a checkpoint and all stats for a snapshot) | Read the current values of the compared stats in the same transaction, then touch only when a value differs: `character_bio_meta` for the character, and `org_stats_meta` for its organization (stat 5) when a statistic of section 5.5 differs, and `org_online_meta` when level, alien level or profession (stats 54, 169, 60) differs (section 12.6). For stat 333, see the implementation note below (any change touches). For stat 33 of a leader, touch `org_stats_meta` of the led organization. |
| Character creation | `LoginEngine\Packets\CharacterName.cs`, after `StatDao.BulkReplace` of the initial stats, before the commit | `character_bio_meta` for the new id |
| Character deletion | `CharacterDao.DeleteOwnedData`, reached by `Delete` and `DeleteForUser` | Read stat 5 before the stats are deleted. Then `org_stats_meta` and `org_online_meta` for that organization (a leave). If the character leads an organization, treat it as a dissolution (see below). |
| Organization join | `SaveOrganizationMembership` | Read the previous stat 5, then `character_bio_meta` for the character, and `org_stats_meta` and `org_online_meta` for the old and the new organization |
| Organization rank change | `SaveOrganizationRank` | `character_bio_meta` for the character, `org_stats_meta` and `org_online_meta` for its organization (stat 5) |
| Organization leave or kick | `ClearOrganizationMembership` (already reads stat 5) | `character_bio_meta` for the character, `org_stats_meta` and `org_online_meta` for the organization |
| Organization dissolution | `DisbandOrganization`, and the leader branch of `DeleteOwnedData` | Bulk upsert of `character_bio_meta` for the members, then `org_stats_meta` and `org_online_meta` for the organization, before the stat rows are deleted |
| Organization name change | `SaveOrganizationText`, field `Name` | Bulk upsert of `character_bio_meta` for the members, and `org_stats_meta` |
| Governing form change | `SaveOrganizationGoverningForm` | Bulk upsert of `character_bio_meta` for the members (the form names the ranks and resets them), and `org_stats_meta` and `org_online_meta` |
| Organization description, objective or history | `SaveOrganizationText`, other fields | `org_stats_meta` |
| Organization leader | `SaveOrganizationLeader` | `org_stats_meta` |
| Logon and logoff | `MySqlCharacterDao.WriteOnline` (`MarkOnline`, `MarkOffline`), `RecoverStaleOnline` | `org_online_meta`, as in section 12.2 |

Not hooked, because no published field changes:
- Organization tax and bank (`SaveOrganizationTax`, `TransferOrganizationBank`), location writes, inventory, missions (the mission DAO writes the cash stat only), perks, nanos, locks.
- Name changes: no rename path exists in the active engines. Names are set at creation. The legacy `AORebirth.Core\Entities\Character.cs` rewrites them on save, and the legacy ZoneEngine is retired.
- `Misc.LogOffAll`, `Misc.LogOffCharacter` and `StatDao.DisbandOrganization`: no caller found in the repository. Checked by search only; if one is found reachable, it takes the matching hook above.

Cost: `WriteStats` gains one primary-key read of the compared stats per save. Measure it during the localhost test of the hooks.

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

The example above is the retail Glitch record (dimension 5). The AOR output must have the same tags and nesting; only values differ. The file starts with the declaration `<?xml version="1.0" encoding="utf-8"?>`, which the example omits. The organization XML (`basicstats.xml`) has its own structure, confirmed on the retail files: root `<organization>` with `id`, `name`, `side`, `last_updated`, then `<members>`; its JSON is `[header with statistics, members, timestamp]` (section 5.5).

### 7.2 JSON (retail positional array)

```json
[
  {"SEX":"Male","BREED":"Opifex","PVPRATING":1701,"NAME":"Glitch","FIRSTNAME":"Thomas","PVPTITLE":"NOVICE","LASTNAME":"Hitandrun","CHAR_DIMENSION":11,"ALIENLEVEL":22,"RANK_name":"Challenger","HEADID":0,"PROFNAME":"Kingpin","LEVELX":220,"PROF":"Fixer","CHAR_INSTANCE":649213,"SIDE":"Omni"},
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
| Organization id not found (`/org/stats`) | HTTP 200 with a null stub, as on retail (section 5.5) |
| Stat missing for a character | Treat as `0`; derived names become empty |
| Profession `0` | `profession_title` empty |
| AlienLevel `0` | `defender_rank` = name of id 0 from `Text.json` (`None`) |
| Org missing or stat 5 = 0 | Organization block omitted (XML) or `null` (JSON) |
| GameData id not found | Log a warning; emit an empty string, do not fail the whole export |
| Nick ambiguous (more than one row) | Must be prevented by a uniqueness check; export fails for that nick |
| Character deleted or renamed | File removed by sweep reconciliation (section 9.1), not by an engine hook |

## 9. Generation and serving

- The exporter writes one file per character, using the retail-compatible names above, to the export path configured in `.env` (section 14).
- One loop runs every tick, 30 seconds by default (`PORK_TICK_SECONDS`). A tick is the interval between two checks of the exporter. Each pass reads, in `character_bio_meta`, `org_stats_meta` and `org_online_meta`, the rows whose `last_updated` is newer than the watermark of the previous pass, and renders only the matching files. The online heartbeat (section 12.3) runs in the same pass. The tick and the heartbeat are the same loop.
- The watermark of a table is the highest `last_updated` seen. Each query starts one overlap earlier (default 60 seconds, `PORK_OVERLAP_SECONDS`), so that a late commit is not missed. Re-rendering an unchanged file is harmless. The watermarks are kept in a state file outside the served folder (section 14), so a restart resumes where the exporter stopped.
- Full sweep: once every 24 hours (`PORK_SWEEP_HOURS`) to repair drift and remove orphans (section 9.1).
- Writes must be atomic: write to a temporary file, then rename.
- Served by nginx as static files, with ETag or `Last-Modified` for polling bots.
- Export failures must not affect the game engines. The exporter is an optional module.
- Unchanged or inactive characters keep their published files as they are; an old `last_updated` is normal.

### 9.1 Sweep reconciliation (deleted and renamed characters)

The PORK API is independent of the engines. Engines do not signal deletions or renames, and no deletion hook or `deleted_at` column is added. Verified in code (`CharacterDao`): deleting a character is a hard `DELETE` of its `characters` row, and the same transaction deletes its stats. Deleting an organization leader also deletes the `organizations` row and clears stat 5 of every member. A deleted character or organization is therefore simply absent from the database, which is what the sweep tests.

Once every 24 hours (`PORK_SWEEP_HOURS`), the sweep covers all published files: characters, organization stats pages and online files. It does not select files by modification time, because the heartbeat touches online files and an orphan can have been written recently.

1. List the published files, the characters in the database (`Id`, `Name`) and the organizations (`Id`).
2. Orphans: a published file with no matching character (deleted, or old nick after a rename) or no matching organization (dissolved) is moved to a quarantine folder, then deleted after a retention period (default 7 days, `PORK_QUARANTINE_DAYS`; `0` deletes at once). A file that matches again is restored.
3. Existing: render the file in memory and compare it with the published one. Rewrite it only when the content differs. A difference with no meta change means a missing hook: log a warning. The organization `last_updated` line is ignored in the comparison (section 5.5).
4. Safety guard: if one pass would quarantine more than a configured share of files (default 5%, `PORK_SWEEP_ABORT_PERCENT`), or the database returns no or implausibly few characters or organizations, the sweep aborts the removal step and logs an error.

Dissolution does not wait for the sweep for the online file. The dissolution touches `org_online_meta` (section 12.2). When change detection reads an organization id that has no `organizations` row, the exporter removes the online file at once, with no retention, since it carries no history. The stats page of that organization is left to the sweep. This also stops the heartbeat from keeping a dissolved organization's file alive.

Consequence: a deleted character stays served until the next sweep (at most 24 hours), then returns 404 once quarantined. A rename creates the new file on the incremental run (its `last_updated` is touched by the name-change hook) and the old file is removed by the next sweep.

The same reconciliation applies to organization files (section 5.5 and section 12). An organization with no `organizations` row (dissolved) has its `/org/stats/` files and its online file quarantined under the same rules.

## 10. Security and privacy

- Never expose `characters.Username`, IP data, or any account field.
- Never expose private GameData in public repositories. Only derived names appear in output.
- Read-only database access for the exporter, through a dedicated database account.
- `.env` holds credentials. It is never committed (section 14).
- No user input reaches SQL; the nick is parameterized.
- Organization stats pages must not load retail CDN images or photos (`photo_url`, `smallphoto_url`) and must not embed any external resource. Bars are drawn with CSS widths.

## 11. Open items

Resolved by the owner and verified in code: the tick and heartbeat interval (30 s, `.env`), the sweep scope (section 9.1), the hook list and hook map (sections 6 and 6.1), bulk upsert for organization-wide changes (section 6), uniqueness of `characters.Name` (section 8), deleted characters (hard delete, section 9.1), online backfill (section 12.4), `.env` content and quarantine defaults of 7 days and 5% (section 14), PHP 8.2 in `Tools\PorkForAor`, Linux servicing and the web server stub (Mike), meaning of a missing online file (section 12.4).

- `characters.Name` has no unique index in `characters.sql` (primary key `Id` only). Uniqueness is enforced by the engine alone, as the owner states. The exporter handles an ambiguous nick as in section 8.
- Hook implementation: done (section 6.1), compiled with `ZoneEngine_New` and `LoginEngine`. It is tested on localhost by the owner.
- Closed: the legacy `UPDATE characters set Online=0` statements of `Misc.cs` (`LogOffAll`, `LogOffCharacter`) have no caller in the solution, so they need no `org_online_meta` hook (section 12.2).
- Open: the sweep (section 9.1) has not run on real data yet.
- Open: `memberIds` and the online query read `stats` by `StatId` and `StatValue`, which has no matching index. An index needs a schema approval and is not requested.
- Heartbeat cost: measured by the owner on localhost with the debug setting of section 12.5.

## 12. Online characters per organization (AOR extension)

Retail has no equivalent. Retail chatbots add the whole org roster to their friend list to learn who is online, which is limited to 1,000 entries. AOR instead publishes the list of online members per organization, so bots no longer need to friend-list the roster. The format is defined in section 12.6. Consumers do not have the reader module yet, so it is a new AOR-specific contract.

### 12.1 Source

- `characters.Online` joined to the organization of the character (stat 5, section 5.3). Read-only database access, like the bio export. The exporter does not need the WorldEngine or gRPC.
- Characters with stat 5 equal to `0` or absent are not listed.

### 12.2 Change tracking: `org_online_meta` (approved)

```sql
CREATE TABLE org_online_meta (
  id_org       INT UNSIGNED PRIMARY KEY,
  last_updated DATETIME(6) NOT NULL,
  KEY ix_org_online_meta_last_updated (last_updated)
);
```

The index serves the exporter's change-detection query (section 12.3). It is approved with the table.

Same pattern as `character_bio_meta` (section 6): the engine writes it, and the exporter only reads it. No dirty flag and no database trigger.

- Verified in code: `characters` has no organization column. The organization is stat 5 in `stats` (`Type`, `Instance`, `StatId`, `StatValue`), where `Type` is the character identity type `50000` (`CharacterType` in `IZoneWorld.cs`).
- All `Online` writes from Login, Chat and Zone converge on `MySqlCharacterDao.WriteOnline(characterId, online)` (`MarkOnline` and `MarkOffline`). That method knows only the character id.
- In the same transaction as `UPDATE characters SET Online=...`, read the stat 5 of the character. If it is not `0`, upsert `org_online_meta` for that organization with the current UTC time.
- `RecoverStaleOnline` resets all non-zero `Online` rows in one transaction. It must also upsert `org_online_meta` for every organization of the captured characters, with one `INSERT ... SELECT`.
- `Misc.cs` contains legacy `UPDATE characters set Online=0` statements (`LogOffAll`, `LogOffCharacter`). They have no caller in the solution, so they need no hook. If one is called in the future, it must apply the same `org_online_meta` upsert.
- Organization join, leave and dissolution must also touch `org_online_meta` for the old and the new organization, since stat 5 can change while a character is online. This extends the hook list of section 6.

### 12.3 Generation

- The exporter runs the loop of section 9, with a period of 30 seconds by default (`PORK_TICK_SECONDS`). For online files, each pass does two things:
  1. Change detection: read the `org_online_meta` rows whose `last_updated` is newer than the watermark of the previous pass, then render the online file of each of those organizations. A change becomes visible within one pass. The query starts about one minute before the watermark, so a row committed late is not missed; re-rendering an unchanged organization is harmless.
  2. Heartbeat: for every other published organization, set the file's modification time to now. There is no database query and no body rewrite. If the file is missing, render it instead.
- The published set is the organizations present in `org_online_meta` that have an `organizations` row. An organization that has been dissolved loses its online file (section 9.1). Online files outside the set are reconciled by the full sweep, like section 9.1.
- Writes are atomic (section 9). A rendered file replaces the previous one by rename, so its modification time is the render time.
- The online files are independent from the bio files and from `character_bio_meta`.

### 12.4 Freshness and server crash

A crashed engine can leave `Online=1` rows until the next start, and engines do not send any order to the API. Freshness is therefore carried by the file's modification time, which the exporter keeps current, and consumers read it from the HTTP headers:

- The body contains only the member list. It has no timestamp, so a body that is unchanged between two reads means no change. The body format (section 12.6) keeps this property: no timestamp, and a stable member order.
- Heartbeat period: one tick, 30 seconds by default (section 12.3). The exporter touches or renders each published file at least once per period. The consumer threshold is 4 periods, which is `4 × PORK_TICK_SECONDS`, so it must be changed together with the tick. It is a consumer rule, not an exporter setting.
- nginx sends `Last-Modified` from the file's modification time. A consumer compares `Date` with `Last-Modified` in the same response, so no client clock is needed. An age up to 2 minutes (4 periods) means the file is live. An older file means the online state is unknown, not "nobody online". A stopped exporter is detected the same way, because nothing touches the file.
- nginx derives `ETag` from the modification time and the size, so each touch changes it. Serve the online location with `etag off;` so that `Last-Modified` is the only validator. Consumers must not use `ETag` to detect a change.
- `Last-Modified` means "last validated by the exporter", not "last changed". A change is a difference in the body.
- Service start (backfill): after a reboot, nobody is online. `RecoverStaleOnline` resets `Online` for every character and touches `org_online_meta` (section 12.2), so every organization is rendered with no online member. The lists are then fed by the logons that follow, one organization at a time. The owner's wish is a wipe of all online elements at that point; this reaches the same result without a wipe order.
- Exporter start: the exporter renders every published online file from the database, which replaces any list left by a previous run. After an engine reboot the database says nobody is online, so the result is the same as a wipe. If only the exporter restarted, the database still holds the real online members, so a blind wipe would hide them until their next relog. The start order does not matter. No engine-to-API signal is needed.
- An organization that has had no online member since the last start may have no file. Consumers read a missing online file, or a file older than 4 periods, as "unknown or offline". They never read it as an error, and the exporter does not publish a placeholder.

### 12.5 Volume

- Change: one `org_online_meta` upsert per logon or logoff of a member, in the transaction that already updates `characters.Online`.
- Change detection: one indexed query per pass (section 12.2), plus one render for each changed organization only.
- Heartbeat: one file touch per published organization every 30 seconds. There is no SQL and no body write, so the heartbeat costs metadata only, not content writes.
- If the touch count proves heavy, the period and the staleness threshold must change together (the threshold stays at 4 periods).
- Measurement: `PORK_DEBUG_HEARTBEAT=1` in `.env` (default `0`) makes each pass write one line to standard output: the duration of the pass, the number of files touched and the time they took, the rows read from each meta table with the query time, the number of renders and the time they took, and a warning when a pass lasts longer than the tick. The owner runs it on localhost and reports the result. It changes no behavior and no file.

### 12.6 Online list format (approved)

A simple structured list, available as XML and JSON like the organization roster and the character export. No HTML page.

Path, in the retail layout of section 3.1: `org/online/d/11/name/{orgId}/online.xml` and `online.json`. `{orgId}` is `organizations.Id`. The web server maps `?data_type=json` to the `.json` file, as for the other exports. This path is approved by the owner.

Five fields per online member, which is what a chatbot needs to answer "who is online". The tag names reuse the roster member tags of section 5.5, so a consumer can share its member parser:

| Field | XML | JSON key | Source |
|---|---|---|---|
| Nickname | `nickname` | `NAME` | `characters.Name` |
| Level (1-220) | `level` | `LEVELX` | stat 54 |
| AI level (0-30) | `defender_rank_id` | `ALIENLEVEL` | stat 169 |
| Profession | `profession` | `PROF` | stat 60, as in section 5.5 |
| Organization rank name | `rank_name` | `RANK_TITLE` | stat 48 and `GoverningForms.json`, as in section 5.5 |

```xml
<online_members>
	<member>
		<nickname>Glitch</nickname>
		<level>220</level>
		<defender_rank_id>22</defender_rank_id>
		<profession>Fixer</profession>
		<rank_name>President</rank_name>
	</member>
</online_members>
```

```json
[{"NAME":"Glitch","LEVELX":220,"ALIENLEVEL":22,"PROF":"Fixer","RANK_TITLE":"President"}]
```

- The JSON is a plain array of member objects. It has no timestamp element, unlike the retail positional array, because the body must stay identical between two reads when nothing changed (section 12.4).
- The organization is not repeated in the body: the path names it.
- Members are ordered by `NAME`, case-insensitively, so that an unchanged set always gives an identical body. The order is otherwise cosmetic.
- No member online: `<online_members/>` and `[]`. This file is rendered, not omitted, so that a consumer can tell "nobody online" from "unknown" (section 12.4).
- The rank name is the same text as in the roster. The level is a number, not a title.

Freshness of the values: the list is mainly about presence, but the shown values follow their changes. In addition to logon, logoff, join, leave and dissolution (section 12.2), `org_online_meta` is touched when a member's level, AI level or profession (stats 54, 169, 60) differs in `WriteStats`, and in `SaveOrganizationRank` and `SaveOrganizationGoverningForm` (ranks are reset and renamed). Implemented (section 6.1). It is the owner's choice to keep it and to disable it later if the cost proves too high; removing these three touches changes nothing else, and the daily sweep (about 24 hours) then refreshes the values.

## 13. Sources

- Repository: `AORebirth\Libraries\Source\AOtomation\...\GameData\CharacterStat.cs`, `IdentityType.cs`, `IZoneWorld.cs`.
- Repository: `AORebirth\Libraries\Source\AORebirth.Database\SqlTables\characters.sql`, `organizations.sql`, `stats.sql`.
- Game data: `D:\Funcom\GameData\Breed.json`, `Sex.json`, `Side.json`, `Profession.json`, `GoverningForms.json`, `Text.json`.
- Retail reference: `https://people.anarchy-online.com/character/bio/d/5/name/Glitch/bio.xml` and `?data_type=json`.
- Retail reference (character page): `https://people.anarchy-online.com/character/bio/d/5/name/Glitch`.
- Retail reference (organization stats): `https://people.anarchy-online.com/org/stats/d/5/name/6814`, `.../6814/basicstats.xml` and `.../6814/basicstats.xml?data_type=json`. Additional samples: `300` (Clan, 6 members), `8500` (Clan, 2 members, zero counts), `2000` (unknown id, null stub).
- Hook sites (read only): `AORebirth\Libraries\Source\AORebirth.Database\Domain\Characters\MySqlCharacterPersistenceDao.cs` (organization writes), `MySqlCharacterDao.cs` (online writes), `AORebirth\Libraries\Source\AORebirth.Database\Dao\OrganizationDao.cs`.
- Level and title reference: `https://wiki.aodb.us/wiki/Player_Level`.
- Alien level names: `https://www.ao-universe.com/guides/alien-invasion/gameplay-guides-3/ai-levels-you`.
- Deletion and uniqueness (read only): `AORebirth\Libraries\Source\AORebirth.Database\Dao\CharacterDao.cs` (`DeleteOwnedData`, hard delete of the character, its stats and, for a leader, the organization), `SqlTables\characters.sql` (primary key `Id`, no unique index on `Name`).
- Hook map (read only): `MySqlCharacterPersistenceDao.cs` (`WriteStats`, `SaveOrganization*`, `ClearOrganizationMembership`, `DisbandOrganization`), `AORebirth\Server\LoginEngine\Packets\CharacterName.cs` (character creation and deletion), `AORebirth\Server\ZoneEngine_New\Core\Characters\CharacterSnapshotService.cs` and `Core\Data\MySql*Repository.cs` (callers).

## 14. Exporter application (PHP)

- Runtime: PHP 8.2 or newer (8.2 is the development version), command line only, with the `pdo_mysql` extension. No framework, and no Composer dependency unless a later need is shown. PHP does not answer HTTP requests: nginx, or any HTTP server, serves the exported files as static files.
- Location: `Tools\PorkForAor` in the repository. The tool is independent of the engines and shares no code with them. It reads the database read-only (section 10) and the GameData JSON (section 4.3).
- Commands (`php pork.php <command>`):
  - `run` (default): the tick loop of section 9 and the 24-hour sweep;
  - `sweep`: one reconciliation pass, then exit;
  - `check`: validates `.env`, database access, the three meta tables, the GameData files and the folders, and exits non-zero on failure.
- Status: the skeleton (configuration, loop, change detection, heartbeat, sweep, quarantine, atomic writes) and the three exporters (characters, organization stats, online lists) are written. They have been rendered against an in-memory database only; the first run on the local database is the next check. An entity that cannot be exported (ambiguous nick, section 8) is logged and skipped without stopping the others. A nick outside `[A-Za-z0-9_-]` is not a safe folder name: it is logged and not exported. The XML files start with the declaration `<?xml version="1.0" encoding="utf-8"?>`. Published file names and the HTTP mapping of `data_type` are in section 3.1.
- Logging: standard output and standard error, so any supervisor can capture it. The process must stop cleanly on a stop request and finish its current atomic write.
- Web files (`Tools\PorkForAor\web`, copied by the operator to the export root; the web server configuration stays with the server owner):
  - `apache.htaccess`: local test configuration. It maps `?data_type=json` on `bio.xml`, `basicstats.xml` and `online.xml` to the `.json` file, sets the content types and removes the `ETag` of the online files (sections 3.1 and 12.4).
  - `org.php`: organization directory modeled on the retail `people/lookup/orgs.html` page. `org.php` lists the letters A to Z and Others for each dimension of its `DIMENSIONS` constant (11 and 10), each linking to `org.php?l=x&dim=y`. `l=x` lists the organizations whose name starts with that letter, `l=others` the other names, sorted by name without case. The columns are name (link to the stats page), members, max level, side, government and dimension. It reads only the published `basicstats.json` files, never the database, and caches the list in the system temp folder until a file changes. A dimension with no published folder shows a message. The retail character lookup form is not reproduced. It needs PHP in the web server, unlike the exported files.
- Configuration: a `.env` file in the application folder. `.env` is git-ignored and never committed; a `.env.example` with placeholders is committed.

| Variable | Default | Role |
|---|---|---|
| `PORK_DB_HOST`, `PORK_DB_PORT`, `PORK_DB_NAME`, `PORK_DB_USER`, `PORK_DB_PASSWORD` | none | MariaDB/MySQL access, with a read-only account |
| `PORK_EXPORT_PATH` | none | Export root served over HTTP. Absolute, or relative to the application folder. |
| `PORK_STATE_PATH` | `state` in the application folder | Watermarks and quarantine folder, outside the served folder. Same volume as the export root, so that moves are renames. |
| `AO_REBIRTH_GAMEDATA_PATH` | none | Existing variable that selects the GameData folder (section 4.3). Not redefined. |
| `PORK_DIMENSION` | `11` | Dimension in routes and files |
| `PORK_TICK_SECONDS` | `30` | Tick and heartbeat period (sections 9 and 12.3) |
| `PORK_OVERLAP_SECONDS` | `60` | Overlap of change detection (section 9) |
| `PORK_SWEEP_HOURS` | `24` | Sweep period (section 9.1) |
| `PORK_QUARANTINE_DAYS` | `7` | Retention of orphan files; `0` deletes at once |
| `PORK_SWEEP_ABORT_PERCENT` | `5` | Share of files above which the sweep aborts its removal step |
| `PORK_DEBUG_HEARTBEAT` | `0` | `1` writes one timing line per pass (section 12.5) |

- Service files:
  - Windows: a launcher script in `Tools\PorkForAor` that runs `run` in the foreground. It can be started at boot with Task Scheduler. A native Windows service needs a third-party wrapper, which is not added.
  - Linux: Mike does the servicing on his side, following his own specifications. Nothing Linux-specific is committed to the public repository. The application only has to run in the foreground, log to standard output and stop cleanly (above).
