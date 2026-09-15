# Current Task

Active: retire the Legacy ZoneEngine executable, project, build and launch paths
on `codex/retire-legacy-engine-20260915`, starting from accepted master `c5af4ac1`.
Mike explicitly authorized removal after the content cleanup reached master.

Retain shared mechanics, editable content and useful offline fixtures under their
current owners; remove the obsolete engine implementation. Preserve player DAO
semantics and prior NewEngine release/database-restore rollback requirements.
Run Windows and private-platform acceptance. No production operation or database
schema change is part of this task.
