-- stats often uses type=50000 (SimpleChar) + instance=charId
SELECT 'stats' t, COUNT(*) c FROM stats WHERE type=50000 AND instance=25;
SELECT 'stats_any' t, COUNT(*) c FROM stats WHERE instance=25;
SELECT 'receivedmessages' t, COUNT(*) c FROM receivedmessages WHERE 1=0;
DESCRIBE receivedmessages;
DESCRIBE login;
DESCRIBE missionaccountflags;
DESCRIBE organizations;
SELECT Id, Username, AllowedCharacters FROM login WHERE AllowedCharacters LIKE '%25%' OR AllowedCharacters LIKE '%Engyfirst%';
SELECT * FROM missionaccountflags LIMIT 5;
