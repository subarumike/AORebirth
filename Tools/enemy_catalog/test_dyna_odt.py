import json,tempfile,unittest,zipfile
from pathlib import Path
from import_dyna_odt import parse

ROOT=Path(__file__).resolve().parents[2]
NORMALIZED=ROOT/"docs/generated/enemy_catalog/sources/dyna_boss_list_1.normalized.json"
class DynaOdtTests(unittest.TestCase):
 def test_minimal_odt_zip_xml_parsing_and_no_mobs(self):
  xml='''<office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0"><office:body><office:spreadsheet><table:table><table:table-row><table:table-cell><text:p>Monster Type</text:p></table:table-cell></table:table-row><table:table-row><table:table-cell><text:p>Nighthowler</text:p></table:table-cell><table:table-cell><text:p>15</text:p></table:table-cell><table:table-cell><text:p>(no mobs)</text:p></table:table-cell><table:table-cell><text:p>Omni Forest</text:p></table:table-cell><table:table-cell><text:p>10x20</text:p></table:table-cell><table:table-cell><text:p>ignored</text:p></table:table-cell></table:table-row></table:table></office:spreadsheet></office:body></office:document-content>'''
  with tempfile.TemporaryDirectory() as d:
   p=Path(d)/"fixture.odt"
   with zipfile.ZipFile(p,"w") as z:z.writestr("content.xml",xml)
   rows,_=parse(p);self.assertEqual(1,len(rows));self.assertTrue(rows[0]["BossOnlyCamp"]);self.assertIsNone(rows[0]["MinionMinimumLevel"]);self.assertEqual("Nighthowler",rows[0]["SourceFamilyName"])
 def test_real_normalized_aggregates(self):
  doc=json.loads(NORMALIZED.read_text(encoding="utf-8")); rows=doc["rows"]
  self.assertEqual(174,len(rows));self.assertEqual(9,len({r["Zone"] for r in rows}));self.assertEqual(174,sum(bool(r["CoordinateText"]) for r in rows));self.assertEqual(174,sum(r["ApproximateBossLevel"] is not None for r in rows));self.assertEqual(4,sum(r["BossOnlyCamp"] for r in rows))
  counts={name:sum(r["SourceFamilyName"]==name for r in rows) for name in ("Rhinomen","Snakes","Spiders","Cyborgs","Androids")};self.assertEqual({"Rhinomen":19,"Snakes":13,"Spiders":13,"Cyborgs":5,"Androids":3},counts)
  night=[r for r in rows if r["SourceFamilyName"]=="Nighthowler"][0];self.assertEqual(("Omni Forest",15),(night["Zone"],night["ApproximateBossLevel"]))
  pareet=[r for r in rows if r["SourceFamilyName"]=="Pareets"][0];self.assertEqual("Pareets",pareet["SourceFamilyName"]);self.assertEqual("reet",pareet["NormalizedFamilyName"])
  self.assertTrue(any(r["SourceFamilyName"]=="Hammerbeasts" and r["NormalizedFamilyName"]=="hammerbeast" for r in rows));self.assertTrue(any(r["SourceFamilyName"]=="Manteze" and r["NormalizedFamilyName"]=="manteze" for r in rows))
if __name__=="__main__":unittest.main()
