# Current Task

Active: remove compiled game-content definitions and Legacy content bridges from
NewEngine on `codex/data-driven-world-20260915`, starting at `323652db` in the
Mike-owned `build-verify/delmus-runtime-merge-20260914` worktree.

Runtime C# owns reusable mechanics and infrastructure; validated editable content
owns NPCs, world placements, vendor stock, quests, dialogue, rewards, weapon
assignments and nano bindings. Preserve evidence as provenance, never runtime
spawn permission. Unknown templates skip with diagnostics; no attackable fallback.

Inventory all consumed source, migrate content through existing loaders, add
editability and architecture guards, and run full Windows/private-platform
acceptance. Preserve player DAO persistence, Delmus's branch, and Legacy engine.
Production deployment is explicitly outside this task. See the content bridge
removal report under `docs/reports` for findings and validation status.
