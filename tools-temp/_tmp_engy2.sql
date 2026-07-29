SELECT Id, Name FROM characters WHERE Name='Engyfirst';
SELECT CharacterId, HighId, LowId, MultipleCount FROM inventory WHERE CharacterId=25 AND (HighId BETWEEN 120000 AND 130000 OR LowId BETWEEN 120000 AND 130000) LIMIT 30;
SHOW TABLES LIKE '%mission%';
