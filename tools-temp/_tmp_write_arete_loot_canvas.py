# -*- coding: utf-8 -*-
"""Generate Arete loot canvas from aggregated JSON."""
import json
import pathlib

ROOT = pathlib.Path(r"C:\Users\nermi\source\repos\AORebirth")
data = json.loads((ROOT / r"tools-temp\_arete_loot_part1_part2.json").read_text(encoding="utf-8"))

# Prefer display names for unnamed
for m in data["mobs"]:
    if m["name"] == "(unnamed)" and m["monsterData"] == "17714" and m["creditsMax"] == 35:
        m["name"] = "Supreme Collector of Waste (unnamed in capture)"
    elif m["name"] == "(unnamed)" and m["monsterData"] == "17687":
        m["name"] = "Rollerrat? unique corpse (unnamed, md=17687)"

canvas_dir = pathlib.Path(r"C:\Users\nermi\.cursor\projects\c-Users-nermi-source-repos-AORebirth\canvases")
out = canvas_dir / "arete-loot-part1-part2.canvas.tsx"

mobs_js = json.dumps(data["mobs"], indent=2)

tsx = f'''import {{
  Callout,
  Card,
  CardBody,
  CardHeader,
  Divider,
  H1,
  H2,
  H3,
  Pill,
  Row,
  Select,
  Stack,
  Stat,
  Table,
  Text,
  useCanvasState,
  useHostTheme,
}} from "cursor/canvas";

type Drop = {{
  lowId: number;
  highId: number;
  ql: number;
  name: string;
  label: string;
  observedOnCorpses: number;
  ratePct: number;
  totalQty: number;
}};

type Mob = {{
  name: string;
  monsterData: string;
  corpses: number;
  empty: number;
  levels: number[];
  creditsMin: number;
  creditsMax: number;
  creditsAvg: number;
  drops: Drop[];
}};

const MOBS: Mob[] = {mobs_js};

const TOTAL_CORPSES = MOBS.reduce((n, m) => n + m.corpses, 0);
const TOTAL_EMPTY = MOBS.reduce((n, m) => n + m.empty, 0);
const TOTAL_DROP_ROWS = MOBS.reduce((n, m) => n + m.drops.length, 0);

export default function AreteLootPart1Part2() {{
  const theme = useHostTheme();
  const [selected, setSelected] = useCanvasState<string>("mob", "ALL");

  const options = [
    {{ value: "ALL", label: "All mobs" }},
    ...MOBS.map((m) => ({{
      value: m.name + "|" + m.monsterData,
      label: m.name + " (" + m.corpses + " corpses)",
    }})),
  ];

  const filtered =
    selected === "ALL"
      ? MOBS
      : MOBS.filter((m) => m.name + "|" + m.monsterData === selected);

  const flatRows = filtered.flatMap((m) =>
    m.drops.length === 0
      ? [
          {{
            mob: m.name,
            md: m.monsterData,
            corpses: String(m.corpses),
            empty: String(m.empty),
            credits: m.creditsMin + "-" + m.creditsMax,
            item: "(empty — no items)",
            aoid: "",
            ql: "",
            rate: "",
            hits: "",
          }},
        ]
      : m.drops.map((d) => ({{
          mob: m.name,
          md: m.monsterData,
          corpses: String(m.corpses),
          empty: String(m.empty),
          credits: m.creditsMin + "-" + m.creditsMax,
          item: d.name || d.label,
          aoid: d.lowId === d.highId ? String(d.lowId) : d.lowId + "-" + d.highId,
          ql: String(d.ql),
          rate: d.ratePct + "%",
          hits: d.observedOnCorpses + "/" + m.corpses,
        }})),
  );

  const summaryRows = filtered.map((m) => ({{
    mob: m.name,
    md: m.monsterData,
    levels: m.levels.length ? m.levels.join(",") : "?",
    corpses: String(m.corpses),
    empty: String(m.empty),
    emptyPct: ((100 * m.empty) / Math.max(1, m.corpses)).toFixed(0) + "%",
    credits: m.creditsMin + "-" + m.creditsMax + " (avg " + m.creditsAvg + ")",
    uniqueDrops: String(m.drops.length),
  }}));

  return (
    <Stack gap={20} style={{{{ padding: 20, maxWidth: 1200 }}}}>
      <Stack gap={6}>
        <H1>Arete mob loot — capture part 1 + part 2</H1>
        <Text tone="secondary">
          Source: corpse-loot-observations.csv · initial opens only · playfield 1044525 ·
          observed drops (not complete tables)
        </Text>
      </Stack>

      <Row gap={16} wrap>
        <Stat value={{String(TOTAL_CORPSES)}} label="Corpses opened" />
        <Stat value={{String(MOBS.length)}} label="Mob entries" />
        <Stat value={{String(TOTAL_EMPTY)}} label="Empty corpses" />
        <Stat value={{String(TOTAL_DROP_ROWS)}} label="Observed drop rows" />
      </Row>

      <Callout tone="neutral">
        Rates are capture frequencies (corpses with item / corpses opened). Live AO uses
        independent roll slots — treat this as seed evidence, not a finished drop table.
      </Callout>

      <Row gap={12} align="center">
        <Text weight="semibold">Filter</Text>
        <Select
          value={{selected}}
          onChange={{setSelected}}
          options={{options}}
        />
        <Pill tone="neutral">{{filtered.length}} mob(s)</Pill>
      </Row>

      <Card>
        <CardHeader>Mob summary</CardHeader>
        <CardBody style={{{{ padding: 0 }}}}>
          <Table
            headers={{["Mob", "MD", "Levels", "Corpses", "Empty", "Empty%", "Credits", "Drop rows"]}}
            rows={{summaryRows.map((r) => [
              r.mob,
              r.md,
              r.levels,
              r.corpses,
              r.empty,
              r.emptyPct,
              r.credits,
              r.uniqueDrops,
            ])}}
            columnAlign={{["left", "right", "left", "right", "right", "right", "left", "right"]}}
          />
        </CardBody>
      </Card>

      <Divider />

      <H2>Observed drops</H2>
      <Text tone="secondary">
        Item names resolved from itemnames.sql. AOID shows LowId or LowId-HighId range.
      </Text>

      <Card>
        <CardHeader trailing={{<Pill>{{flatRows.length}} rows</Pill>}}>
          Drop detail
        </CardHeader>
        <CardBody style={{{{ padding: 0 }}}}>
          <Table
            headers={{["Mob", "MD", "Item", "AOID", "QL", "Rate", "Hits", "Credits"]}}
            rows={{flatRows.map((r) => [
              r.mob,
              r.md,
              r.item,
              r.aoid,
              r.ql,
              r.rate,
              r.hits,
              r.credits,
            ])}}
            columnAlign={{["left", "right", "left", "right", "right", "right", "right", "left"]}}
          />
        </CardBody>
      </Card>

      <H3>Files</H3>
      <Text tone="secondary" style={{{{ color: theme.textSecondary }}}}>
        tools-temp/_arete_loot_part1_part2.csv · tools-temp/_arete_loot_part1_part2.json
      </Text>
    </Stack>
  );
}}
'''

out.write_text(tsx, encoding="utf-8")
print("Wrote", out, "bytes", out.stat().st_size)
