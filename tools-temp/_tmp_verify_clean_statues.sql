SELECT Playfield, COUNT(*) AS cnt FROM cellao_codex_clean.staticdynels WHERE Playfield BETWEEN 4676 AND 4699 GROUP BY Playfield ORDER BY Playfield;
SELECT Playfield, COUNT(*) AS cnt FROM cellao_codex_test.staticdynels WHERE Playfield BETWEEN 4676 AND 4699 GROUP BY Playfield ORDER BY Playfield;
SELECT COUNT(*) AS clean_passage_names FROM cellao_codex_clean.itemnames WHERE name LIKE 'Passage to %';
SELECT COUNT(*) AS test_passage_names FROM cellao_codex_test.itemnames WHERE name LIKE 'Passage to %';
SELECT Id, Type, Instance, Playfield, X, Y, Z FROM cellao_codex_clean.staticdynels WHERE Playfield=4677 ORDER BY Instance;
SELECT Id, Type, Instance, Playfield, X, Y, Z FROM cellao_codex_test.staticdynels WHERE Playfield=4677 ORDER BY Instance;
