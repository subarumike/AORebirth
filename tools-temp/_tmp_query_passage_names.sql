SELECT n.id, n.name
FROM itemnames n
WHERE n.name LIKE '%Passage to%'
ORDER BY n.id
LIMIT 30;
