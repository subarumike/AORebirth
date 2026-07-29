-- Tables / columns that may reference character Id=25
SELECT TABLE_NAME, COLUMN_NAME
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'cellao_codex_clean'
  AND (
    COLUMN_NAME LIKE '%character%'
    OR COLUMN_NAME LIKE '%Character%'
    OR COLUMN_NAME = 'containerinstance'
    OR COLUMN_NAME = 'OwnerInstance'
    OR COLUMN_NAME = 'ownerinstance'
    OR COLUMN_NAME = 'charid'
    OR COLUMN_NAME = 'CharId'
  )
ORDER BY TABLE_NAME, COLUMN_NAME;
