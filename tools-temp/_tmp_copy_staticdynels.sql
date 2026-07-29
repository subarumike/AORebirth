-- Force-replace SL garden + zone staticdynels from test into clean
DELETE FROM cellao_codex_clean.staticdynels
WHERE Playfield BETWEEN 4676 AND 4699
   OR Playfield IN (4310,4311,4312,4313,4320,4321,4322,4328,4540,4541,4542,4543,4544,4605,4872,4873,4880,4881);

INSERT INTO cellao_codex_clean.staticdynels
  (Type, Instance, Playfield, X, Y, Z, HeadingX, HeadingY, HeadingZ, HeadingW, stats, customevents)
SELECT Type, Instance, Playfield, X, Y, Z, HeadingX, HeadingY, HeadingZ, HeadingW, stats, customevents
FROM cellao_codex_test.staticdynels
WHERE Playfield BETWEEN 4676 AND 4699
   OR Playfield IN (4310,4311,4312,4313,4320,4321,4322,4328,4540,4541,4542,4543,4544,4605,4872,4873,4880,4881);

SELECT 'clean_after' AS label, COUNT(*) AS cnt FROM cellao_codex_clean.staticdynels
WHERE Playfield BETWEEN 4676 AND 4699
   OR Playfield IN (4310,4311,4312,4313,4320,4321,4322,4328,4540,4541,4542,4543,4544,4605,4872,4873,4880,4881);
