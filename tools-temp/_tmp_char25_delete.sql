START TRANSACTION;

DELETE FROM charactersuploadednanos WHERE CharacterId=25;
DELETE FROM charactersactivenanos WHERE CharacterId=25;
DELETE FROM charactersmeshs WHERE CharacterId=25;
DELETE FROM charactersperks WHERE CharacterId=25;
DELETE FROM characterstimers WHERE CharacterId=25;
DELETE FROM items WHERE ContainerInstance=25;
DELETE FROM instanceditems WHERE ContainerInstance=25;
DELETE FROM missionflags WHERE CharacterId=25;
DELETE FROM missionobjectiveobservations WHERE CharacterId=25;
DELETE FROM missionobjectiveprogress WHERE CharacterId=25;
DELETE FROM missionrewardledger WHERE CharacterId=25;
DELETE FROM missionstates WHERE CharacterId=25;
DELETE FROM gmi_order WHERE buyer_character_id=25 OR seller_character_id=25;
DELETE FROM gmi_trade_log WHERE character_id=25 OR other_character_id=25;
DELETE FROM gmi_vault_item WHERE character_id=25;
DELETE FROM gmi_vault WHERE character_id=25;
DELETE FROM receivedmessages WHERE PlayerId=25 OR ReceivedId=25;
DELETE FROM stats WHERE type=50000 AND instance=25;
DELETE FROM characters WHERE Id=25;

COMMIT;

-- Verify leftovers
SELECT 'characters' t, COUNT(*) c FROM characters WHERE Id=25
UNION ALL SELECT 'charactersuploadednanos', COUNT(*) FROM charactersuploadednanos WHERE CharacterId=25
UNION ALL SELECT 'charactersactivenanos', COUNT(*) FROM charactersactivenanos WHERE CharacterId=25
UNION ALL SELECT 'charactersmeshs', COUNT(*) FROM charactersmeshs WHERE CharacterId=25
UNION ALL SELECT 'charactersperks', COUNT(*) FROM charactersperks WHERE CharacterId=25
UNION ALL SELECT 'characterstimers', COUNT(*) FROM characterstimers WHERE CharacterId=25
UNION ALL SELECT 'items', COUNT(*) FROM items WHERE ContainerInstance=25
UNION ALL SELECT 'instanceditems', COUNT(*) FROM instanceditems WHERE ContainerInstance=25
UNION ALL SELECT 'missionflags', COUNT(*) FROM missionflags WHERE CharacterId=25
UNION ALL SELECT 'missionobjectiveobservations', COUNT(*) FROM missionobjectiveobservations WHERE CharacterId=25
UNION ALL SELECT 'missionobjectiveprogress', COUNT(*) FROM missionobjectiveprogress WHERE CharacterId=25
UNION ALL SELECT 'missionrewardledger', COUNT(*) FROM missionrewardledger WHERE CharacterId=25
UNION ALL SELECT 'missionstates', COUNT(*) FROM missionstates WHERE CharacterId=25
UNION ALL SELECT 'gmi_order', COUNT(*) FROM gmi_order WHERE buyer_character_id=25 OR seller_character_id=25
UNION ALL SELECT 'gmi_trade_log', COUNT(*) FROM gmi_trade_log WHERE character_id=25 OR other_character_id=25
UNION ALL SELECT 'gmi_vault', COUNT(*) FROM gmi_vault WHERE character_id=25
UNION ALL SELECT 'gmi_vault_item', COUNT(*) FROM gmi_vault_item WHERE character_id=25
UNION ALL SELECT 'receivedmessages', COUNT(*) FROM receivedmessages WHERE PlayerId=25 OR ReceivedId=25
UNION ALL SELECT 'stats', COUNT(*) FROM stats WHERE type=50000 AND instance=25;
