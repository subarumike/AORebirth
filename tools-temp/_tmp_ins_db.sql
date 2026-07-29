SHOW TABLES LIKE '%static%';
SHOW TABLES LIKE '%statel%';
DESCRIBE staticdynels;
SELECT playfield, type, instance, HEX(instance) AS inst_hex, id1, id2
FROM staticdynels
WHERE instance IN (-1073347953, -1073085936, -1073216945)
   OR (playfield = 655 AND type = 51005)
LIMIT 30;
