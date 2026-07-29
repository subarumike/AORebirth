SELECT s.Playfield, s.Instance, s.X, s.Y, s.Z, n.id AS template_id, n.name
FROM staticdynels s
JOIN itemnames n ON n.id = (
  -- template id is embedded in msgpack; use known mapping via static instance in repo for now
  0
)
WHERE s.Playfield BETWEEN 4676 AND 4699
LIMIT 1;
