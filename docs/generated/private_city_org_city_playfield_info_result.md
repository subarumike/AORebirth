# Private City Org City Playfield Info Result

Date: 2026-06-22

## Evidence

- Character `Mikedoc` has organization membership in `stats`: `StatIds.clan=1`, `StatIds.clanlevel=0`.
- Organization `Testing Org` existed as `organizations.Id=1`, `LeaderId=18`, but had `CityID=0`.
- Recent city entry attempt used `GenericCmd Use` against `Terminal:-1073741169`.
- Teleport data maps that terminal from playfield `655` to destination playfield `1702`.
- `CharacterInfoPacketMessageHandler` sent `CityPlayfieldId=0` for all character info packets.

## Change

- `CharacterInfoPacketMessageHandler` now resolves `organizations.CityID` for the target character's organization and sends it as `CharacterInfoPacket.CityPlayfieldId`.
- Missing or invalid organization city data still sends `0`.

## Local DB Patch

Pre-update:

```sql
SELECT Id, Name, LeaderId, CityID FROM organizations WHERE Id = 1;
-- 1, Testing Org, 18, 0
```

Applied:

```sql
UPDATE organizations SET CityID = 1702 WHERE Id = 1 AND CityID <> 1702;
```

Rows changed: `1`.

Post-update:

```sql
SELECT Id, Name, LeaderId, CityID FROM organizations WHERE Id = 1;
-- 1, Testing Org, 18, 1702
```

Rollback:

```sql
UPDATE organizations SET CityID = 0 WHERE Id = 1;
```

## Remaining Scope

- This does not implement full city ownership, city bank, city persistence, or city-building semantics.
- User gameplay testing is required to confirm whether the client now accepts the city entry.
