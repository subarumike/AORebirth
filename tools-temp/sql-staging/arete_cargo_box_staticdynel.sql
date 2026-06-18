-- AO Rebirth Arete Rex B18D Cargo Box placement patch.
--
-- Evidence:
-- - Capture folder tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-194454 has
--   the B18D GenericCmd Use target Terminal:56D9B4AF in events.log:6327,6333 and 7798.
-- - Capture folder tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-205724 has
--   exact Terminal:56D9B4AF SimpleItemFullUpdate evidence in events.log:5493-5494.
-- - Capture folder tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-214819 has
--   repeated exact Terminal:56D9B4AF SimpleItemFullUpdate evidence in events.log:14605-14606,
--   15738-15739, and 19521-19522.
-- - Capture folder tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-215831 has
--   exact Terminal:56D9B4AF SimpleItemFullUpdate evidence in events.log:10939-10940
--   and 11431-11432, with raw packet evidence in packets.hex.log:9058-9059 and 9328-9329.
-- - The rendered text "Cargo Box" is not used as the lookup source. The packet identity and
--   SimpleItemFullUpdate for Terminal:56D9B4AF are the source of truth for this row.
-- - Exact captured position: (3621.576, 51.745, 780.4768).
-- - Exact captured rotation: (0, -0.7101817, 0, 0.7040185).
-- - Exact captured stats: Flags=139265, StaticInstance=297277, ACGItemLevel=1,
--   ACGItemTemplateID=297277, ACGItemTemplateID2=297277, MultipleCount=1,
--   AnimPlay=0, AnimPos=0.
-- - Rejected local smoke attempts are intentionally not represented in this row:
--   nearby Terminal:57369E8E Junk anchor, template 285300, and Mesh=18794.
--
-- Scope:
-- - Placement only for this SQL file.
-- - No rewards, inventory mutation, XP/credits, or schema changes.

START TRANSACTION;

INSERT INTO `staticdynels`
    (`Type`, `Instance`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `stats`, `customevents`)
SELECT
    51005,
    1457108143,
    6553,
    3621.576,
    51.745,
    780.4768,
    0,
    -0.7101817,
    0,
    0.7040185,
    _binary 0x9892A5466C616773CE0002200192AE537461746963496E7374616E6365CE0004893D92AC4143474974656D4C6576656C0192B14143474974656D54656D706C6174654944CE0004893D92B24143474974656D54656D706C617465494432CE0004893D92AD4D756C7469706C65436F756E740192A8416E696D506C61790092A7416E696D506F730000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000,
    NULL
WHERE NOT EXISTS (
    SELECT 1
    FROM `staticdynels`
    WHERE `Type` = 51005
      AND `Instance` = 1457108143
      AND `Playfield` = 6553
);

UPDATE `staticdynels`
SET `X` = 3621.576,
    `Y` = 51.745,
    `Z` = 780.4768,
    `HeadingX` = 0,
    `HeadingY` = -0.7101817,
    `HeadingZ` = 0,
    `HeadingW` = 0.7040185,
    `stats` = _binary 0x9892A5466C616773CE0002200192AE537461746963496E7374616E6365CE0004893D92AC4143474974656D4C6576656C0192B14143474974656D54656D706C6174654944CE0004893D92B24143474974656D54656D706C617465494432CE0004893D92AD4D756C7469706C65436F756E740192A8416E696D506C61790092A7416E696D506F730000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000
WHERE `Type` = 51005
  AND `Instance` = 1457108143
  AND `Playfield` = 6553;

COMMIT;

SELECT COUNT(*) AS AreteStaticDynels
FROM `staticdynels`
WHERE `Playfield` = 6553;

SELECT `Id`, `Type`, `Instance`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, HEX(`stats`) AS `StatsHex`
FROM `staticdynels`
WHERE `Type` = 51005
  AND `Instance` = 1457108143
  AND `Playfield` = 6553;
