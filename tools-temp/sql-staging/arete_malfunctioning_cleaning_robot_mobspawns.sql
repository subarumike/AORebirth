-- AO Rebirth - Arete Malfunctioning Cleaning Robot minimal local spawn rows.
--
-- Purpose:
--   Add exactly five captured Malfunctioning Cleaning Robot mobspawns in
--   Arete Landing so the gated Rex B18C objective-progress smoke can be
--   tested against runtime NPC deaths.
--
-- Scope:
--   Mission: 5514B18C / "Terminate 5 Malfunctioning Cleaning Robots"
--   Playfield: 6553 Arete Landing
--   Target name: Malfunctioning Cleaning Robot
--
-- Evidence:
--   Capture folder:
--     tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-194454
--
--   Selected identities all have named SimpleCharFullUpdate evidence with:
--     * Name="Malfunctioning Cleaning Robot"
--     * Level=1
--     * Health=12/12
--     * MonsterData=297023
--     * MonsterScale=200
--     * VisualFlags=31
--     * CharacterFlags=268964353
--     * AccountFlags=0
--     * Expansions=0
--     * RunSpeedBase=5
--     * HeadMesh=null
--     * captured position and heading
--     * later CharacterAction Action=Death evidence
--
--   Selected identity evidence:
--     0x78D3ACB7 / 2027138231: full update events.log:229, death events.log:3813
--     0x78D3ACC5 / 2027138245: full update events.log:2650, death events.log:3413
--     0x78D3ACC6 / 2027138246: full update events.log:2720, death events.log:3247
--     0x78D3ACC9 / 2027138249: full update events.log:3392, death events.log:3565
--     0x78D3ACD3 / 2027138259: full update events.log:5106, death events.log:5374
--
-- Runtime safety facts:
--   Playfield.LoadMobSpawns reads mobspawns plus mobspawns_stats and
--   instantiates rows directly through NonPlayerCharacterHandler.
--   There is no local mobtemplate row with MonsterData=297023, and the
--   mobspawn loader does not require one for runtime spawning.
--
--   Empty blob values are intentional so the existing mobspawn loader can
--   deserialize empty waypoint/weapon/nano/mesh collections without null
--   access.
--
--   The captured SCFU does not fully decode actor-baseline fields such as
--   breed, side, sex, race, profession, current nano, NPC family, and LOS
--   height. AO Rebirth's SimpleCharFullUpdate packet builder and playfield
--   heartbeat still read those stats for every NPC. The extra baseline rows
--   below are therefore runtime safety scaffolding, not gameplay semantics:
--   they use neutral/default values or the existing mob-creation baseline
--   where the old stat engine would otherwise emit sentinel defaults or index
--   derived-stat tables with invalid values.

START TRANSACTION;

DELETE FROM mobspawns_stats
WHERE Playfield = 6553
  AND Id IN (2027138231, 2027138245, 2027138246, 2027138249, 2027138259);

DELETE FROM mobspawns
WHERE Playfield = 6553
  AND Id IN (2027138231, 2027138245, 2027138246, 2027138249, 2027138259);

INSERT INTO mobspawns
    (Id, Playfield, X, Y, Z, HeadingX, HeadingY, HeadingZ, HeadingW, Name,
     Textures0, Textures1, Textures2, Textures3, Textures4,
     Waypoints, Weaponpairs, RunningNanos, MobMeshs, AdditionalMeshs, KnuBotScriptName)
VALUES
    (2027138231, 6553, 3608.66138, 51.745, 795.9552, 0, 0.7361878, 0, 0.6767774,
     'Malfunctioning Cleaning Robot', 0, 0, 0, 0, 0, X'', X'', X'', X'', X'', ''),
    (2027138245, 6553, 3598.61523, 51.745, 774.0247, 0, 0.7383381, 0, 0.6744308,
     'Malfunctioning Cleaning Robot', 0, 0, 0, 0, 0, X'', X'', X'', X'', X'', ''),
    (2027138246, 6553, 3606.319, 51.745, 801.3757, 0, 0.734597, 0, 0.6785036,
     'Malfunctioning Cleaning Robot', 0, 0, 0, 0, 0, X'', X'', X'', X'', X'', ''),
    (2027138249, 6553, 3617.60181, 51.745, 783.974731, 0, 0.7344052, 0, 0.6787114,
     'Malfunctioning Cleaning Robot', 0, 0, 0, 0, 0, X'', X'', X'', X'', X'', ''),
    (2027138259, 6553, 3607.9126, 51.745, 796.260254, 0, 0.7348164, 0, 0.678266,
     'Malfunctioning Cleaning Robot', 0, 0, 0, 0, 0, X'', X'', X'', X'', X'', '');

INSERT INTO mobspawns_stats
    (Id, Playfield, Stat, Value)
VALUES
    -- 0x78D3ACB7 / 2027138231
    (2027138231, 6553, 0, 268964353), -- CharacterFlags.
    (2027138231, 6553, 1, 12),        -- Life / max health.
    (2027138231, 6553, 27, 12),       -- Health / current health.
    (2027138231, 6553, 54, 1),        -- Level.
    (2027138231, 6553, 64, 0),        -- HeadMesh=null.
    (2027138231, 6553, 156, 5),       -- RunSpeedBase.
    (2027138231, 6553, 359, 297023),  -- MonsterData.
    (2027138231, 6553, 360, 200),     -- MonsterScale.
    (2027138231, 6553, 389, 0),       -- Expansions.
    (2027138231, 6553, 660, 0),       -- AccountFlags.
    (2027138231, 6553, 673, 31),      -- VisualFlags.

    -- 0x78D3ACC5 / 2027138245
    (2027138245, 6553, 0, 268964353),
    (2027138245, 6553, 1, 12),
    (2027138245, 6553, 27, 12),
    (2027138245, 6553, 54, 1),
    (2027138245, 6553, 64, 0),
    (2027138245, 6553, 156, 5),
    (2027138245, 6553, 359, 297023),
    (2027138245, 6553, 360, 200),
    (2027138245, 6553, 389, 0),
    (2027138245, 6553, 660, 0),
    (2027138245, 6553, 673, 31),

    -- 0x78D3ACC6 / 2027138246
    (2027138246, 6553, 0, 268964353),
    (2027138246, 6553, 1, 12),
    (2027138246, 6553, 27, 12),
    (2027138246, 6553, 54, 1),
    (2027138246, 6553, 64, 0),
    (2027138246, 6553, 156, 5),
    (2027138246, 6553, 359, 297023),
    (2027138246, 6553, 360, 200),
    (2027138246, 6553, 389, 0),
    (2027138246, 6553, 660, 0),
    (2027138246, 6553, 673, 31),

    -- 0x78D3ACC9 / 2027138249
    (2027138249, 6553, 0, 268964353),
    (2027138249, 6553, 1, 12),
    (2027138249, 6553, 27, 12),
    (2027138249, 6553, 54, 1),
    (2027138249, 6553, 64, 0),
    (2027138249, 6553, 156, 5),
    (2027138249, 6553, 359, 297023),
    (2027138249, 6553, 360, 200),
    (2027138249, 6553, 389, 0),
    (2027138249, 6553, 660, 0),
    (2027138249, 6553, 673, 31),

    -- 0x78D3ACD3 / 2027138259
    (2027138259, 6553, 0, 268964353),
    (2027138259, 6553, 1, 12),
    (2027138259, 6553, 27, 12),
    (2027138259, 6553, 54, 1),
    (2027138259, 6553, 64, 0),
    (2027138259, 6553, 156, 5),
    (2027138259, 6553, 359, 297023),
    (2027138259, 6553, 360, 200),
    (2027138259, 6553, 389, 0),
    (2027138259, 6553, 660, 0),
    (2027138259, 6553, 673, 31);

-- Runtime actor-baseline scaffolding required by the existing heartbeat and
-- SimpleCharFullUpdate paths. These rows are intentionally separate from the
-- captured SCFU-readable evidence above.
INSERT INTO mobspawns_stats
    (Id, Playfield, Stat, Value)
VALUES
    -- 0x78D3ACB7 / 2027138231
    (2027138231, 6553, 4, 1),   -- Breed: valid derived-stat table index.
    (2027138231, 6553, 18, 0),  -- Stamina: heal interval/delta input.
    (2027138231, 6553, 21, 0),  -- Psychic: nano interval/delta input.
    (2027138231, 6553, 33, 0),  -- Side: neutral packet baseline.
    (2027138231, 6553, 37, 1),  -- TitleLevel: valid derived-stat table index.
    (2027138231, 6553, 47, 1),  -- Fatness: avoid sentinel packet value.
    (2027138231, 6553, 59, 0),  -- Sex: neutral packet baseline.
    (2027138231, 6553, 60, 15), -- Profession: existing runtime mob baseline.
    (2027138231, 6553, 89, 1),  -- Race: default packet baseline.
    (2027138231, 6553, 132, 5), -- NanoEnergyPool: nano delta/max nano input.
    (2027138231, 6553, 152, 5), -- BodyDevelopment: life/heal delta input.
    (2027138231, 6553, 173, 3), -- CurrentMovementMode: default run mode.
    (2027138231, 6553, 214, 1), -- CurrentNano: packet/max nano input.
    (2027138231, 6553, 368, 15),-- VisualProfession: existing runtime mob baseline.
    (2027138231, 6553, 455, 0), -- NPCFamily: neutral baseline; family unresolved.
    (2027138231, 6553, 466, 15),-- LOSHeight: default small NPC LOS height.

    -- 0x78D3ACC5 / 2027138245
    (2027138245, 6553, 4, 1),
    (2027138245, 6553, 18, 0),
    (2027138245, 6553, 21, 0),
    (2027138245, 6553, 33, 0),
    (2027138245, 6553, 37, 1),
    (2027138245, 6553, 47, 1),
    (2027138245, 6553, 59, 0),
    (2027138245, 6553, 60, 15),
    (2027138245, 6553, 89, 1),
    (2027138245, 6553, 132, 5),
    (2027138245, 6553, 152, 5),
    (2027138245, 6553, 173, 3),
    (2027138245, 6553, 214, 1),
    (2027138245, 6553, 368, 15),
    (2027138245, 6553, 455, 0),
    (2027138245, 6553, 466, 15),

    -- 0x78D3ACC6 / 2027138246
    (2027138246, 6553, 4, 1),
    (2027138246, 6553, 18, 0),
    (2027138246, 6553, 21, 0),
    (2027138246, 6553, 33, 0),
    (2027138246, 6553, 37, 1),
    (2027138246, 6553, 47, 1),
    (2027138246, 6553, 59, 0),
    (2027138246, 6553, 60, 15),
    (2027138246, 6553, 89, 1),
    (2027138246, 6553, 132, 5),
    (2027138246, 6553, 152, 5),
    (2027138246, 6553, 173, 3),
    (2027138246, 6553, 214, 1),
    (2027138246, 6553, 368, 15),
    (2027138246, 6553, 455, 0),
    (2027138246, 6553, 466, 15),

    -- 0x78D3ACC9 / 2027138249
    (2027138249, 6553, 4, 1),
    (2027138249, 6553, 18, 0),
    (2027138249, 6553, 21, 0),
    (2027138249, 6553, 33, 0),
    (2027138249, 6553, 37, 1),
    (2027138249, 6553, 47, 1),
    (2027138249, 6553, 59, 0),
    (2027138249, 6553, 60, 15),
    (2027138249, 6553, 89, 1),
    (2027138249, 6553, 132, 5),
    (2027138249, 6553, 152, 5),
    (2027138249, 6553, 173, 3),
    (2027138249, 6553, 214, 1),
    (2027138249, 6553, 368, 15),
    (2027138249, 6553, 455, 0),
    (2027138249, 6553, 466, 15),

    -- 0x78D3ACD3 / 2027138259
    (2027138259, 6553, 4, 1),
    (2027138259, 6553, 18, 0),
    (2027138259, 6553, 21, 0),
    (2027138259, 6553, 33, 0),
    (2027138259, 6553, 37, 1),
    (2027138259, 6553, 47, 1),
    (2027138259, 6553, 59, 0),
    (2027138259, 6553, 60, 15),
    (2027138259, 6553, 89, 1),
    (2027138259, 6553, 132, 5),
    (2027138259, 6553, 152, 5),
    (2027138259, 6553, 173, 3),
    (2027138259, 6553, 214, 1),
    (2027138259, 6553, 368, 15),
    (2027138259, 6553, 455, 0),
    (2027138259, 6553, 466, 15);

COMMIT;
