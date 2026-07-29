SELECT Id, Playfield, Instance, customevents IS NOT NULL AS has_events, LENGTH(customevents) AS event_len
FROM staticdynels
WHERE Playfield IN (4677,4676,4310)
LIMIT 20;
