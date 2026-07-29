DESCRIBE cellao_codex_clean.staticdynels;
SELECT 'clean_gardens' AS label, COUNT(*) AS cnt FROM cellao_codex_clean.staticdynels WHERE Playfield BETWEEN 4676 AND 4699;
SELECT 'test_gardens' AS label, COUNT(*) AS cnt FROM cellao_codex_test.staticdynels WHERE Playfield BETWEEN 4676 AND 4699;
SELECT 'clean_sl_zones' AS label, COUNT(*) AS cnt FROM cellao_codex_clean.staticdynels WHERE Playfield IN (4310,4311,4312,4313,4320,4321,4322,4328,4540,4541,4542,4543,4544,4605,4872,4873,4880,4881);
SELECT 'test_sl_zones' AS label, COUNT(*) AS cnt FROM cellao_codex_test.staticdynels WHERE Playfield IN (4310,4311,4312,4313,4320,4321,4322,4328,4540,4541,4542,4543,4544,4605,4872,4873,4880,4881);
