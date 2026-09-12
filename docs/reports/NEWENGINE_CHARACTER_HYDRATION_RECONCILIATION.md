# Character/inventory DAO and retail hydration reconciliation

## Scope and Git provenance

Cutover source `codex/newengine-production-cutover-001` remains at
`827c7fb50a9d860b6f671c73baebc2447c282dff`. This is executable source (interior
door routing), not a report-only commit. The historical failed-staging report is
`d5d66e20acf903a14e9afbb80b3b45af25dc141b`; its deployed source was `7f157d4f`.
The later hydration, vital, scale and interior fixes were already in the current
cutover candidate. Reimplementing the old `Stats.Count > 0` gate would ignore them.

The common ancestor of cutover and tested DAO source is exactly the cutover head.
Git merge-tree returned `712aa45fb401670eb157853a5308f4d71916481d` with no conflicts.
The new `codex/newengine-retail-hydration-dao-001` worktree starts at that cutover
head and fast-forwards to tested DAO source
`29ab800bb3537f9e36f8a7b1273d2e605780065a`. This incorporates implementation
`4ac021764fc932283995f030134a08fd3090eb6d`, interface manifest
`15e8941f19c5b8b183b3f75030acee8c094f5088`, database manifest `29ab800b`, and their
accepted account/character/mission DAO-stack ancestry. None of these three commits
was present at the original cutover head; no cherry-picks or conflict resolutions
were needed. No other developer branch is imported. Origin/master at inspection:
`7be49b22b4c0116af9c48dc88a44f86ba802f7c7`.

Worktree: `C:\Users\Mike\Documents\AORebirth\tools-temp\retail-hydration-dao001`.
Both source worktrees, master, production, schemas and Legacy are preserved.

## Reproduced cause and actual repairs

The local saved staging backup
`tools-temp/linux-staging-50d/backups/pre-hydration-553981c8-stats.sql` contains
the exact 23 stat rows already represented in `IncompleteReportedHydration`.
This is saved staging evidence, not a newly queried production database.
The historical raw builder produces flags `1234567890 / 0x499602D2`, including
`Tower=0x00020000`, and health damage `19-1000=-981`. The regression executes
that builder and rejects its actual payload. Its transform/name are fixture
values, not a claim of a complete original character snapshot.

The inherited 26-stat manifest already rejects sparse input through
`CharacterHydrationService`. This repair also enforces it at `PlayerHydrator.Apply`
and at `SpawnService.SpawnPlayer` before constructing players, Online ownership,
inventory application or registry changes. Reconnect validates retained state and
the actual two packets before stealing/binding the new session. Active-nano
attachment and its final validation precede world/session publication.

`PlayerSpawnPayloadValidator.RequireValidMessages` validates the actual SCFU and
FullCharacter pair. It checks ordinary-player shape, identity, appearance, primary
abilities, required stat presence, duplicate consistency, health and expansion
agreement. Its recursive numeric guard covers optional fields, inventory, mesh,
texture and nested packet values, not just Flags. Serialized count/conditional
shape also round-trips through the production AOtomation codec in regression tests.
The connected client invokes the same check before CharInPlay, including its
outstanding-ticket restart path. Synthetic validation is not retail-client proof.

FullCharacter's counted stat tuples now omit absent optional stats. Explicit zero
values remain present; explicit sentinels throw. Checked narrowing rejects values
that would otherwise wrap in byte/short groups. Required fields never use omission
to bypass the aggregate contract. Conditional visible first/last names now come
from the existing character DTO, rather than being silently omitted.

## Required aggregate and authoritative sources

`CharacterHydrationResult` is the existing aggregate; no competing DTO is added.
All persistent character/stat/item/nano adapters remain behind
`ICharacterPersistenceDao`. Directory/admission authority remains `ICharacterDao`.
DAO methods added/changed by this repair: none. SQL/schema changes: none.

| Field/stat | Persisted or derived source | Requirement / action |
|---|---|---|
| Identity, name, first/last names | characters through LoadCharacter; names may legitimately be empty except unique name | Validate positive identity and nonempty unique name; visible-name strings use stored values |
| Playfield, position, heading | characters through LoadCharacter | Validate playfield and finite transform before publication; never substitute character 9950 data |
| Flags | stats 0; creation persists 0x00081241 | Require stored value; reject sentinel/tower; GM presentation remains the existing runtime rule |
| Breed, Sex, Profession, Fatness | stats 4/59/60/47; original character-creation selections | Required; no generic appearance repair |
| Race | stats 89; creation persists race 1 | Require persisted value; do not fill missing saved characters with 1 |
| HeadMesh | stats 64; creation's selected HeadMesh | Required positive mesh; equipment meshes remain catalog-derived |
| VisualFlags | stats 673; creation persists 31 | Required persisted value; no sentinel-to-zero fallback |
| Scale | stats 360; creation's MonsterScale | Required positive value; keep accepted scale repair |
| Level, TitleLevel, Side | stats 54/37/33 | Required and range-checked |
| Strength, Agility, Stamina, Intelligence, Sense, Psychic | stats 16-21; breed-selected creation bases, subsequent persisted progression | Required positive bases; SCFU base fields and FullCharacter current values |
| BodyDevelopment, NanoPool | stats 152/132; creation persists starter bases | Required formula inputs; retain current calculators |
| Health, MaxHealth | stats 27/1; max rebased by MaxHealthCalculator using persisted progression | Require 0 <= current <= max and positive maximum before/after preparation |
| CurrentNano, MaxNanoEnergy | stats 214/221; maximum rebased by MaxNanoCalculator | Require valid current/max relationship; no invented current nano |
| RunSpeed | stats 156 | Required nonnegative base |
| Expansion | stats 389, copied from account Expansions at character creation | Proven persisted character authority for this path; no blanket all/none grant; later account-to-character synchronization is outside this repair |
| Inventory, bank/bag/worn identity and placements | LoadCarriedItems/LoadBankItems/LoadContainerItems and existing inventory adapter | Preserve item identity, source, stack CAS and all transaction owners |
| Uploaded/active nanos | shared DAO reads plus existing NanoService | Preserve active-nano lifecycle and quarantine; armor-content reconstruction is excluded |
| Optional SCFU sections | explicit optional fields and current runtime projections | Omit according to existing codec; never forward a sentinel |

The 26 required stat names and every Legacy/NewEngine FullCharacter stat field are
listed in `NEWENGINE_RETAIL_SPAWN_FIELD_MATRIX.json` and its Markdown view.
Creation provenance is `LoginEngine/Packets/CharacterName.cs:327-420`;
hydration consumes `LoadStats` rather than adding SQL or fabricating missing rows.
Legacy is comparison evidence, not proof that every emitted stat is necessary.

## Health and flags semantics

Legacy `SimpleCharFullUpdate.ConstructMessage`, current `Character.BuildSpawnMessage`
and the shared serializer agree: SCFU Health is displayed maximum, HealthDamage
is displayed maximum minus current; FullCharacter Health is current and MaxHealth
is maximum. The existing >65535 SCFU presentation scales both values while keeping
FullCharacter exact. Tests cover damaged/full/invalid/missing/sentinel/large-health
variants. No invalid negative damage or silent health fabrication is accepted.

Tower bit 17 controls the extra tower byte in the serializer. Player-vs-NPC
CharacterInfo is independently chosen by the runtime/player type and encoded in
SCFU flags. Tests check both; structurally decodable tower output is still rejected.
HasVisibleName bit 22 controls the persisted first/last-name strings. NpcStyleFlag28
is not substituted for ordinary-player classification.

## FullCharacter coverage and unresolved evidence

The deterministic comparison reviews 230 unique stat IDs, 237 Legacy entries and
211 NewEngine manifest entries. There are 23 Legacy-only IDs. Their retail-required
status remains UNKNOWN; their labels can differ between legacy comments and the
shared enum, so numeric IDs are authoritative in the matrix. Unknown trailing,
team/raid and skill-lock sections retain existing behavior. No new guessed fields
are emitted to match historical packet length.

Required stat gaps at current cutover baseline: zero in the 26-field manifest;
remaining bypass/semantic checks are repaired here. Known conditional SCFU name
gap: fixed through existing DAO fields. Broader FullCharacter reconciliation:
PARTIAL until actual retail consumption and unresolved field requirements are
established. Official capture events `20260623-042326/events.log:107,307` establish
two FullCharacter deliveries; the known staging pcap decoder reproduces the
1702-byte failed FullCharacter. Neither packet length proves field necessity.

Evidence searched/used: saved 23-row backup; existing failed staging pcap and its
approved decoder; official 20260623 capture event records; current/historical
cutover report; creation producer; shared DAO contracts/implementation; Legacy and
NewEngine SCFU/FullCharacter producers; shared serializers; aggregate and connected
fixtures. No AO stripdown/Rebuild checkout was accessed. Unrelated NPC captures and
armor reconstruction are not promoted into player field requirements.

## Validation and boundaries

Final exact-source logs, hashes, counts, push result and official-client gates are
recorded in `NEWENGINE_CUTOVER_VALIDATION_RECEIPT.md`. The earlier 536-test DAO
baseline was rerun before this repair. No historical client login/walking report
is relabelled acceptance of this new source. Production readiness remains NO until
official client CharInPlay, ChatEngine, world entry and lifecycle are observed.

## Retail gameplay result

Mike completed the requested EP1 gameplay test successfully. Login, world entry, both building transitions and logout/relogin are confirmed by user observation and server logs. See `NEWENGINE_RETAIL_GAMEPLAY_ACCEPTANCE.md` for exact evidence and the still-unverified direct CharInPlay/post-service-restart gates.
