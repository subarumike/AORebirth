# AORebirth Risk Register

| ID | Severity | Type | Risk | Mitigation |
| --- | --- | --- | --- | --- |
| AR-R001 | HIGH | Ownership | Playfield remains loot/corpse god object | extract services behind parity fixtures |
| AR-R002 | HIGH | Duplication | four loot sources have implicit precedence | normalized assignments and fail-closed resolver |
| AR-R003 | HIGH | Lifecycle | population types own separate respawn paths | keyed global scheduler and cancellation scopes |
| AR-R004 | HIGH | Data model | no spawn group/camp/encounter records | versioned schemas and adapters |
| AR-R005 | HIGH | Persistence | durable world state resets or becomes stale | explicit durable/ephemeral policy and recovery tests |
| AR-R006 | HIGH | Evidence | community dyna data could be promoted as fact | inactive proposals with confidence/provenance |
| AR-R007 | MEDIUM | Performance | immediate visibility packet bursts | budgets, queue metrics, optional pacing after evidence |
| AR-R008 | MEDIUM | Performance | global pool scans enter future hot paths | index/owner registries and source guardrails |
| AR-R009 | MEDIUM | Testing | source assertions couple tests to implementation | state-machine fixtures and deterministic seams |
| AR-R010 | MEDIUM | Content | generated C# arrays become unwieldy | validated indexed data loader |
| AR-R011 | MEDIUM | Protocol | team/personal loot and multi-access are unproven | explicit EVIDENCE_BLOCKED policies |
| AR-R012 | MEDIUM | Migration | broad population activation can regress client stability | quarantined bounded rollout and rollback manifests |
| AR-R013 | LOW | Maintenance | obsolete/orig/deprecated files confuse ownership | removal plan after replacement validation |
