-- AO Rebirth - Rex Larsson minimal local spawn row.
--
-- Purpose:
--   Add exactly one Rex Larsson mobspawn so the existing gated Arete Rex
--   dialogue route can be smoke-tested in-client.
--
-- Evidence:
--   Capture folder:
--     tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-194454
--   Captured Rex evidence:
--     events.log [CHAR-SEEN] identity=(SimpleChar:782DE568)
--     enemy-state.csv row for (SimpleChar:782DE568)
--     npc_list.json Rex Larsson entry
--
-- Captured facts used:
--   Id/identity: 0x782DE568 / 2016273768
--   Name: Rex Larsson
--   Playfield: 6553 Arete Landing
--   Position: X=3624.599, Y=51.745, Z=787.7465
--   Level: 15
--   HP: 511/511
--   MonsterData: 26074
--   VisualFlags: 31
--
-- Runtime safety facts:
--   The existing mobspawn loader instantiates a Character and then the
--   playfield heartbeat ticks nano regeneration. These baseline stats keep
--   the inherited stat formulas in-range for a level-15 NPC spawn. They do
--   not add quest behavior, rewards, inventory, XP, credits, or mission-state
--   semantics.
--   HeadMesh and NPCFamily are required by the existing SimpleCharFullUpdate
--   packet builder to render a normal headed NPC and emit SimpleNpcInfo. The
--   exact Rex head/NPC family were not decoded from capture; these values are
--   the closest local project visual fallback from mobtemplate MonsterData
--   26074 ("A061 Seasoned Adventurer"). They are smoke-test scaffolding, not
--   final captured Rex semantics.
--
-- Notes:
--   The capture did not expose Rex's raw SimpleCharFullUpdate row, so this
--   patch does not claim exact heading, NPC family, dialogue behavior, quest
--   behavior, rewards, inventory, XP, credits, or mission-state semantics.
--   Empty blob values are intentional so the existing mobspawn loader can
--   safely deserialize the waypoint collection without null access.

START TRANSACTION;

-- Remove the earlier smoke-test row that used the incorrect decimal
-- conversion for 0x782DE568.
DELETE FROM mobspawns_stats
WHERE Id = 2016277864
  AND Playfield = 6553;

DELETE FROM mobspawns
WHERE Id = 2016277864;

DELETE FROM mobspawns_stats
WHERE Id = 2016273768
  AND Playfield = 6553;

DELETE FROM mobspawns
WHERE Id = 2016273768;

INSERT INTO mobspawns
    (Id, Playfield, X, Y, Z, HeadingX, HeadingY, HeadingZ, HeadingW, Name,
     Textures0, Textures1, Textures2, Textures3, Textures4,
     Waypoints, Weaponpairs, RunningNanos, MobMeshs, AdditionalMeshs, KnuBotScriptName)
VALUES
    (2016273768, 6553, 3624.599, 51.745, 787.7465, 0, 0, 0, 0, 'Rex Larsson',
     0, 0, 0, 0, 0,
     X'', X'', X'', X'', X'', '');

INSERT INTO mobspawns_stats
    (Id, Playfield, Stat, Value)
VALUES
    (2016273768, 6553, 0, 277352961), -- Flags: project packet sanity value plus IsImmune (blue/non-attackable).
    (2016273768, 6553, 1, 511),       -- Life / max health.
    (2016273768, 6553, 4, 1),         -- Breed: MonsterData 26074 client hint is solitus.
    (2016273768, 6553, 27, 511),      -- Health / current health.
    (2016273768, 6553, 37, 2),        -- TitleLevel: derived by existing StatTitleLevel for level 15.
    (2016273768, 6553, 47, 1),        -- Fatness: MonsterData 26074 client hint is fat male.
    (2016273768, 6553, 54, 15),       -- Level.
    (2016273768, 6553, 59, 6),        -- Sex: MonsterData 26074 client hint is male.
    (2016273768, 6553, 60, 15),       -- Profession: existing NPC factory runtime baseline.
    (2016273768, 6553, 64, 40691),    -- HeadMesh: local MonsterData 26074 visual fallback; exact Rex head not captured.
    (2016273768, 6553, 89, 1),        -- Race: existing human visual/template convention.
    (2016273768, 6553, 132, 5),       -- NanoEnergyPool: existing stat-engine default baseline.
    (2016273768, 6553, 156, 513),     -- RunSpeed: existing NPC factory runtime baseline.
    (2016273768, 6553, 214, 1),       -- CurrentNano: existing stat-engine default baseline.
    (2016273768, 6553, 359, 26074),   -- MonsterData.
    (2016273768, 6553, 360, 100),     -- MonsterScale: packet-sanity default; exact Rex scale not captured.
    (2016273768, 6553, 368, 15),      -- VisualProfession: mirrors existing NPC factory baseline.
    (2016273768, 6553, 389, 0),       -- Expansion: captured nearby NPC SCFU/server NPC creation value.
    (2016273768, 6553, 455, 120),     -- NPCFamily: local MonsterData 26074 visual fallback for SimpleNpcInfo.
    (2016273768, 6553, 466, 15),      -- LOSHeight: existing NPC factory runtime baseline.
    (2016273768, 6553, 660, 0),       -- AccountFlags: captured nearby NPC SCFU/server NPC creation value.
    (2016273768, 6553, 673, 31);      -- VisualFlags.

COMMIT;
