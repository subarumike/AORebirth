SELECT p.id, p.name, COUNT(s.Id) AS statue_count
FROM playfields p
LEFT JOIN staticdynels s ON s.Playfield = p.id
WHERE p.id BETWEEN 4676 AND 4699
GROUP BY p.id, p.name
ORDER BY p.id;
