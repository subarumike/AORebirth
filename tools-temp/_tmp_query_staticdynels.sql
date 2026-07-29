SELECT Playfield, COUNT(*) AS cnt FROM staticdynels WHERE Playfield BETWEEN 4676 AND 4699 OR Playfield BETWEEN 4310 AND 4313 GROUP BY Playfield ORDER BY Playfield;
