# Current Task

## Active

### Local (Any-QL implant tradeskill) — RETEST after “wrong item” fix

#### Root causes addressed
1. **Dapper column map** — `DBTradeSkill` properties now match MySQL (`Id1`/`Id2`/`ResultIds`/`QlRangePercent`) so recipes actually load
2. **Tradeskill window slot 0** — Source/Target Changed used `placement != 0`, which cleared items in inventory slot 0 (valid slot)
3. **UseItemOnItem QL** — was hardcoded 300; now `quality < 0` → implant QL + NanoProg bump
4. **Resolve** — reverse drag + Low/High/relation ID expand; clearer fail chat (recipe vs skill vs cluster QL%)

#### Retest (restart engines)
1. Watch Zone console: `Cached N trade skill entries` with N ≈ 100k (not 0 / not all skipped)
2. Cluster + Basic Implant QL 1/50/100, both drag orders
3. Mason Arete tip still QL1 Overflow
4. If fail: chat now prints tried Low/High IDs — paste that
