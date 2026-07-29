SELECT Playfield, AVG(X) AS ax, AVG(Y) AS ay, AVG(Z) AS az, COUNT(*) AS cnt
FROM staticdynels
WHERE Playfield BETWEEN 4676 AND 4699
GROUP BY Playfield
ORDER BY Playfield;
