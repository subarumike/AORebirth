DESCRIBE cellao_codex_clean.items;
SELECT id, LENGTH(stats), HEX(LEFT(stats, 32)) FROM cellao_codex_clean.items WHERE id=296780;
SELECT COUNT(*) FROM cellao_codex_clean.items WHERE id BETWEEN 296770 AND 296790;
