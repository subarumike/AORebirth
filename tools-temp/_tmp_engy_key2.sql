SELECT Id, containertype, containerinstance, containerplacement, lowid, highid, quality
FROM items
WHERE containerinstance=25 AND lowid=28577;
SELECT Id, containertype, containerinstance, lowid, highid
FROM items
WHERE lowid=28577
LIMIT 30;
