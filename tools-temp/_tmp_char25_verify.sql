SELECT DISTINCT type, COUNT(*) c FROM stats WHERE instance=25 GROUP BY type;
SELECT COUNT(*) AS mail FROM receivedmessages WHERE PlayerId=25 OR ReceivedId=25;
SELECT COUNT(*) AS orgs FROM organizations WHERE LeaderId=25;
SELECT Id, Name, Username FROM characters WHERE Id=25 OR Name LIKE '%Engyfirst%';
