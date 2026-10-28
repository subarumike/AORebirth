# Current Task

Consolidate completed original-client trap research and implement the supported
generated mission trap slice on `codex/mission-traps-recovery`, based on freshly
fetched public master `9a18a15a2d760aab896c708f532e31aa4078bb39`.

- Reuse completed interactivity Recovery 001/002/003 and existing private
  resource/packet evidence. Do not repeat Ghidra investigations or request captures.
- Repair missing Mine/TrapItem contracts; account for all 221 latest records
  independently from ChestFullUpdate failures.
- Reuse existing parents, locality, mission-flag transactions, item-use dispatch
  and nano/buff execution. Keep content in editable data.
- Freeze optional reviewed trap policy with newly accepted missions. Preserve
  existing mission/chest populations and loot policies.
- Separate detection, reveal, trigger, disarm, disable, malfunction and destruction.
  Unknown server decisions require explicit AORebirth configuration; incomplete
  mechanics stay disabled.
- Run explicitly requested bounded packet/resource/runtime/persistence and
  chest/door/mission checks through governed feature validation.
- Do not start/stop running engines, use the AO client, mutate player databases,
  merge master or deploy. Keep private evidence/content out of public Git.
- Complete `docs/reports/MISSION_TRAPS_RECOVERY_AND_IMPLEMENTATION.md`, then
  commit and push validated changes to the dedicated feature branch.
