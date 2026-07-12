import unittest
from extract_enemy_catalog import build_catalog

class EnemyCatalogTests(unittest.TestCase):
    def test_stable_keys_duplicate_names_and_zone_links(self):
        dump={"source":"fixture.idx","record_types":[
            {"type":1040023,"count":2,"records":[{"id":10,"size":8,"strings":["sewer snake"]},{"id":11,"size":8,"strings":["sewer snake"]}]},
            {"type":1000001,"count":1,"records":[{"id":127,"size":4,"strings":["Subway"]}]},
            {"type":1000014,"count":1,"records":[{"id":127,"size":8,"strings":["Snake room"]}]}
        ]}
        rows=build_catalog(dump,["fixture.idx"])
        self.assertEqual(["rdb-1040023-10","rdb-1040023-11"],[x["CanonicalEnemyKey"] for x in rows])
        self.assertEqual([[127],[127]],[x["PlayfieldIds"] for x in rows])
        self.assertEqual(["NO_REFERENCE_FOUND"],rows[0]["WeaponCategories"])
    def test_malformed_missing_name_is_preserved(self):
        dump={"source":"fixture.idx","record_types":[{"type":1040023,"count":1,"records":[{"id":99,"size":0,"strings":[]}]}]}
        row=build_catalog(dump,["fixture.idx"])[0]
        self.assertIsNone(row["DisplayName"]); self.assertTrue(row["UnresolvedFields"])
if __name__=="__main__": unittest.main()
