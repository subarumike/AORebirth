# Subway NPC combat reconciliation

32 reviewed Subway templates imported as explicitly blocked content. No NPC-specific C# content was introduced.

| NPC | Hash | Family | Identity | Family stats | Overlay | Override | Weapon | Combat-ready |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Basic Quality Armorer | BQAU | 0 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Basic Quality Weaponsdealer | BQWR | 0 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Basic Tools Merchant | BTMV | 0 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Container Supplier | CSBI | 0 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Looter | DCQV | 138 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Discarded Pet | DIPE | 138 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Disobedient Bot | DOPT | 138 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Architect Striker | EHDL | 149 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Empty Shell | EMSE | 148 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Fragmented Soul | FASO | 148 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Filth Flea | FLFE | 138 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Infector | INFE | 150 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Infected Attendant | INPE | 138 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Incomplete Rebuild | IORB | 148 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Lost Thought | LPPE | 138 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Melded Patterns | MDPA | 148 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Molested Molecules | MEML | 148 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Eumenides | MENI | 148 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Mugger | MUGG | 138 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Neural Burnout | NUBR | 148 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Shadow | NXZP | 150 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Premature Pattern | PMPE | 148 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Redundant Scan | RESC | 148 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Slum Runner | SLRU | 151 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Bloodcreeper | SS01 | 63 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Stim Fiend | STFE | 138 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Strike Foreman | STFO | 149 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Tailor | TAIL | 0 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Uncontrollable Anger | UNPE | 138 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Vergil Aeneid | VEAE | 138 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Violent Vagabond | VOVG | 3 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |
| Workman Striker | WOST | 149 | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |

The separate accepted export verifies 322 Subway bindings. Those capture-owned actors are not a proven bridge to these official hash placements. Existing accepted content remains available unchanged.

Family IDs: 0, 3, 63, 138, 148, 149, 150, 151. All lack an accepted reusable family definition. The developer sample families (1, 10001) and overlay (1) are retained under docs/accepted/npc/delmus for review, not installed as arbitrary runtime defaults. Effective NPC snapshots are not silently converted into universal family curves.

Weapons: 53 source definitions; 25 matched to captured endpoint pairs; 28 remain unknown. Captured quality/owner/capture associations are retained in NPC_WEAPON_RECONCILIATION.json. Multiple pairs are alternative observations, not simultaneous main/offhand weapons. Exact actor-to-placement loadout selection remains required.

See the JSON companion for every imported field, appearance, evidence profile, variant, source and blocker. Zero templates are claimed combat-accepted.
