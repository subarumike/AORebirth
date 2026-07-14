# Current Task

## Current Focus

Alien XP / Alien Level (AIXP) — capture-backed completion of kill rewards and AI level 1–30 progression.

## Done in this slice

- Award AIXP when killing mobs marked with Flags bit `0x4000` (Alien Invasion templates such as Alien Spider - Zix).
- Progress Alien Level 1–30 using existing `XPTable.TableAlienXP` thresholds.
- Enforce Rubi-Ka min-level gates: fill AIXP bar up to next AI need while gated (e.g. RK15/AI2 → 22500), then stop AIXP until next RK min; auto AI level when RK unlocks (login / RK level-up).
- Test spider: level 7, 500 AIXP/kill, respects bar/RK caps (no gate bypass).
- InvadersKilled / KilledByInvaders counters; no AIXP loss on death.

## Remaining

1. Capture live AXP-per-kill amounts vs mob level / con color (current formula is provisional: `max(10, mobLevel*25)` with grey-cap).
2. Confirm live chat and Stat packet order for AIXP / Alien Level-up.
3. IPR grants at AI 15 and AI 30 (deferred).
4. Mike live-validates kills on alien-flagged templates; if an already-stuck over-cap bar remains from older writes, clamp/reset once.

## Constraints

- Do not invent capture-shaped AXP wire packets without evidence.
- Keep regular RK combat XP path unchanged except the Alien award hook after kill XP.
