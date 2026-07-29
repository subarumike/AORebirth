SELECT Id, containerinstance, containerplacement, lowid, highid, quality, multiplecount
FROM items
WHERE containerinstance=25 AND (lowid=28577 OR highid=28577 OR lowid=11342 OR Name LIKE '%ission%')
LIMIT 50;
SELECT Id, containerinstance, lowid, highid, quality FROM items WHERE containerinstance=25 AND lowid=28577;
SELECT COUNT(*) AS cnt FROM items WHERE containerinstance=25;
