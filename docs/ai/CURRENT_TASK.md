# Current Task

## Active

### Local — Arete main quest + implant crafting (pushed)

#### Arete main quest
- Mason / Vernon / Lorelei / Vaughn / Sarah / Stan / Shipping Manifest / ICC exit path
- Deliver tip → Stan trade factory → reward + Sarah / Buy Nano tips
- Bill FinishTrade no longer steals Stan Accept

#### Implant crafting
- Any-QL `IsImplant` recipes via robust resolve (reverse drag + Low/High/relations)
- Dapper `DBTradeSkill` column map (`Id1`/`Id2`/`ResultIds`/`QlRangePercent`)
- Tradeskill window accepts inventory slot 0
- UseItemOnItem derives result QL from implant (+ NanoProg bump)
- Mason Arete tip still QL1 Overflow

#### Retest (restart engines)
1. Zone console: large `Cached N trade skill entries` (~100k)
2. Cluster + Basic Implant both drag orders
3. Mason tip QL1 Overflow
4. Stan factory deliver with active tip

### Upstream (merged from origin)
Subway PF127 + Temple of Three Winds PF647 continue on master (TOTW Defender / Yatila–Betany slice). See `docs/project/PROJECT_STATE.md` and TOTW evidence docs for that track.
