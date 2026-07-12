# Ordinary Weapon-Hit Observation Matrix

This matrix defines the minimum capture set needed before any ordinary weapon formula can be promoted. It is evidence-only and does not activate production formula code.

Each row requires a single attacker, a single target, one equipped weapon, no reflect/absorb/shield/proc/nano/DoT/environmental damage, complete attack and health packet ordering, and pre/post health values that reconcile exactly to the observed damage.

| Category | Required attacker state | Weapon requirement | Target state | Hold constant | Minimum observations | Required packet/log evidence | Candidate distinctions |
| --- | --- | --- | --- | --- | ---: | --- | --- |
| base roll variation | same AR/Add All Off/add damage, no temporary modifiers | known min/max, known damage type | same matching AC | attacker, weapon, target, normal hit | 30 normal hits | attack packet, damage packet/text, health before/after | roll range, inclusive bounds, hidden fixed bonuses |
| attack-rating variation | same weapon and target, controlled AR values | single-skill weapon | same matching AC | weapon, target, add damage, normal hit | 3 AR levels | stat snapshot before each hit plus hit packets | AR multiplier ordering and truncation |
| target AC variation | same attacker and weapon | same single-skill weapon | controlled matching AC values | attacker, weapon, add damage, normal hit | 3 AC levels | target stat snapshot and health delta | AC divisor, before/after floor ordering |
| minimum-floor boundary | low base roll and high AC | low-minimum weapon | high matching AC | attacker, damage type, add damage | 10 hits around floor | packet order and target health deltas | AO min floor versus legacy fallback floor |
| critical versus normal | stable crit chance or forced known crit state | known critical bonus source | same target AC | attacker, weapon, target | 10 normal and 10 critical | hit-kind evidence plus health deltas | max-plus-bonus versus roll-plus-bonus, critical AC handling |
| type-specific add damage | controlled type-specific stat | fixed damage type weapon | same target AC | attacker AR, weapon, target | 3 add-damage levels | attacker stat snapshot and hit result | add-damage eligibility and ordering |
| possible universal add damage | controlled universal source if found | any known weapon | same target AC | attacker AR, weapon, target | 3 levels | source proof plus hit result | universal add source and stacking order |
| AMSCap boundary | controlled AR below/equal/above cap | weapon with positive AMSCap | same target AC | add damage, hit kind | 3 AR levels per cap state | weapon template stat and attacker stat snapshots | cap before/after Add All Off, zero/absence semantics |
| single-skill weapon | one attack skill at 100 percent | weapon with one attack skill | same target AC | hit kind, add damage | 10 hits | template attack-skill dictionary plus packets | direct skill-to-AR mapping |
| multi-skill weapon | two supplied attack skills | weapon with weighted skills | same target AC | hit kind, add damage | 10 hits | template skill weights and attacker stat snapshots | weighted-skill integer truncation |
| AR below 1000 | AR below 1000 | same weapon | same target AC | all modifiers | 10 hits | stat snapshot and packet order | pre-1000 scaling |
| AR exactly 1000 | AR exactly 1000 | same weapon | same target AC | all modifiers | 10 hits | stat snapshot and packet order | boundary rounding |
| AR above 1000 | AR above 1000 | same weapon | same target AC | all modifiers | 10 hits | stat snapshot and packet order | post-1000 scaling/factors |

Promotion gate:

1. Every observation must validate as complete.
2. Health delta must equal the observed hit damage.
3. Critical state must be known, not inferred from damage size alone.
4. Missing armor, Add All Off, AMSCap semantics, add damage, or packet ordering keeps the observation incomplete.
5. A formula is only promotable when exactly one candidate matches every complete observation and no candidate requires hidden modifiers or unresolved rounding assumptions.
