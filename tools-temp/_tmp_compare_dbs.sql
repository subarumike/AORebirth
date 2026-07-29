SELECT 'test_gardens' AS db, COUNT(*) AS cnt FROM cellao_codex_test.staticdynels WHERE Playfield BETWEEN 4676 AND 4699;
SELECT 'clean_gardens' AS db, COUNT(*) AS cnt FROM cellao_codex_clean.staticdynels WHERE Playfield BETWEEN 4676 AND 4699;
SELECT 'test_4311' AS db, COUNT(*) AS cnt FROM cellao_codex_test.staticdynels WHERE Playfield=4311;
SELECT 'clean_4311' AS db, COUNT(*) AS cnt FROM cellao_codex_clean.staticdynels WHERE Playfield=4311;
