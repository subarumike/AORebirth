-- PRE-DATA ROLLBACK ONLY. The governed runner refuses this path when any bot row exists.
DROP TABLE `bot_audit_events`;
DROP TABLE `bot_scopes`;
DROP TABLE `bot_credentials`;
DROP TABLE `bot_principals`;
