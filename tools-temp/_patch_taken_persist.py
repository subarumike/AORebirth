# -*- coding: utf-8 -*-
"""Persist daily Taken by accountKey (AO browser has no CharacterID)."""
from pathlib import Path
import shutil

# --- claim.php: accept accountKey on GET ---
claim_src = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\icc-rk-local-web\daily\claim.php")
claim = claim_src.read_text(encoding="utf-8")

old_resolve_block = """$accountKey = daily_resolve_account_key($characterId, $characterName);
$hasIdentity = ($accountKey !== '');
$state = $hasIdentity ? daily_load_account_state($accountKey, $month) : daily_empty_state($month);
$takenInts = daily_normalize_taken(isset($state['Taken']) ? $state['Taken'] : array());
"""

new_resolve_block = """$accountKey = daily_resolve_account_key($characterId, $characterName);
// Web stores accountKey after first Zone grant (CharacterID often missing in AO browser).
if ($accountKey === '' && isset($_GET['accountKey'])) {
    $accountKey = daily_safe_key($_GET['accountKey']);
    if ($accountKey === 'unknown') {
        $accountKey = '';
    }
}
if ($accountKey === '' && isset($body['accountKey'])) {
    $accountKey = daily_safe_key($body['accountKey']);
    if ($accountKey === 'unknown') {
        $accountKey = '';
    }
}
$hasIdentity = ($accountKey !== '');
$state = $hasIdentity ? daily_load_account_state($accountKey, $month) : daily_empty_state($month);
$takenInts = daily_normalize_taken(isset($state['Taken']) ? $state['Taken'] : array());
// Migrate free-test LastClaimUtc → LastGrantedUtc so once/day + taken board stay consistent.
if ($hasIdentity && empty($state['LastGrantedUtc']) && !empty($state['LastClaimUtc'])) {
    $state['LastGrantedUtc'] = strval($state['LastClaimUtc']);
    daily_write_all('account-' . daily_safe_key($accountKey) . '.json', json_encode($state));
}
"""

if old_resolve_block not in claim:
    raise SystemExit("claim.php resolve block missing")
claim = claim.replace(old_resolve_block, new_resolve_block)

# Ensure GET payload always exposes board when accountKey known
claim = claim.replace(
    "'hasIdentity' => $hasIdentity,",
    "'hasIdentity' => $hasIdentity,\n    'boardAccount' => $accountKey,",
)

tmp_claim = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_claim_persist.php")
tmp_claim.write_text(claim, encoding="utf-8")
for p in [
    claim_src,
    Path(r"C:\xampp\htdocs\uwg.daily.icc-rk\claim.php"),
    Path(r"C:\xampp\htdocs\daily\claim.php"),
]:
    shutil.copyfile(str(tmp_claim), str(p))
    print("claim", p)

# --- index.html persistence ---
idx = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\icc-rk-local-web\daily\index.html")
text = idx.read_text(encoding="utf-8")

# Add accountKey var near freeTestMode
if "var knownAccountKey" not in text:
    text = text.replace(
        "var freeTestMode = false;",
        "var freeTestMode = false;\n    var knownAccountKey = \"\";",
    )

old_storage = """    function currentIdentityKey() {
      var idn = resolveIdentity();
      if (idn.id) return "c" + String(idn.id);
      if (idn.name) return "n" + String(idn.name).toLowerCase();
      return "";
    }
    function storageKeys() {
      var m = utcMonth();
      var idk = identityKey || currentIdentityKey() || "anon";
      return {
        count: "aorebirth.daily.v3." + m + "." + idk + ".claimedCount",
        last: "aorebirth.daily.v3." + m + "." + idk + ".lastClaimUtc",
        taken: "aorebirth.daily.v3." + m + "." + idk + ".taken"
      };
    }
    function resetTakenMap() {
      TAKEN_DAYS = {};
    }
    function loadLocal() {
      claimedCount = 0;
      lastClaimUtc = "";
      resetTakenMap();
      try {
        if (!window.localStorage) return;
        var k = storageKeys();
        var n = parseInt(localStorage.getItem(k.count), 10);
        if (!isNaN(n) && n > 0) claimedCount = n > TOTAL ? TOTAL : n;
        var last = localStorage.getItem(k.last);
        if (last) lastClaimUtc = last;
        var takenRaw = localStorage.getItem(k.taken);
        if (takenRaw) {
          var arr = eval("(" + takenRaw + ")");
          var i;
          if (arr && arr.length) {
            for (i = 0; i < arr.length; i++) markTaken(parseInt(arr[i], 10));
          }
        }
      } catch (e2) {}
    }
    function saveLocal() {
      try {
        if (!window.localStorage) return;
        if (!identityKey) return;
        var k = storageKeys();
        localStorage.setItem(k.count, String(claimedCount));
        localStorage.setItem(k.last, lastClaimUtc);
        var takenArr = [];
        var d;
        for (d = 1; d <= TOTAL; d++) {
          if (TAKEN_DAYS[d]) takenArr.push(d);
        }
        localStorage.setItem(k.taken, "[" + takenArr.join(",") + "]");
      } catch (e3) {}
    }
    function refreshIdentity() {
      var next = currentIdentityKey();
      if (next !== identityKey) {
        identityKey = next;
        loadLocal();
        paint();
      }
      return identityKey;
    }"""

new_storage = """    function currentIdentityKey() {
      var idn = resolveIdentity();
      if (idn.id) return "c" + String(idn.id);
      if (idn.name) return "n" + String(idn.name).toLowerCase();
      return "";
    }
    function accountStoragePrefix() {
      var m = utcMonth();
      var ak = knownAccountKey || "pending";
      return "aorebirth.daily.acct.v1." + m + "." + ak;
    }
    function storageKeys() {
      var p = accountStoragePrefix();
      return {
        count: p + ".claimedCount",
        last: p + ".lastClaimUtc",
        granted: p + ".lastGrantedUtc",
        taken: p + ".taken",
        account: "aorebirth.daily.acct.v1.accountKey"
      };
    }
    function resetTakenMap() {
      TAKEN_DAYS = {};
    }
    function loadStoredAccountKey() {
      try {
        if (!window.localStorage) return;
        var ak = localStorage.getItem("aorebirth.daily.acct.v1.accountKey");
        if (ak) knownAccountKey = String(ak).toLowerCase();
      } catch (eAk) {}
    }
    function setKnownAccountKey(ak) {
      if (!ak) return;
      var prev = knownAccountKey;
      knownAccountKey = String(ak).toLowerCase();
      try {
        if (window.localStorage) {
          localStorage.setItem("aorebirth.daily.acct.v1.accountKey", knownAccountKey);
        }
      } catch (eSet) {}
      // Move pending-session board onto the real account key.
      if (prev !== knownAccountKey) {
        saveLocal();
      }
    }
    function loadLocal() {
      claimedCount = 0;
      lastClaimUtc = "";
      // keep lastGrantedUtc unless storage has a value
      resetTakenMap();
      try {
        if (!window.localStorage) return;
        loadStoredAccountKey();
        var k = storageKeys();
        var n = parseInt(localStorage.getItem(k.count), 10);
        if (!isNaN(n) && n > 0) claimedCount = n > TOTAL ? TOTAL : n;
        var last = localStorage.getItem(k.last);
        if (last) lastClaimUtc = last;
        var granted = localStorage.getItem(k.granted);
        if (granted) lastGrantedUtc = granted;
        var takenRaw = localStorage.getItem(k.taken);
        if (takenRaw) {
          var arr = eval("(" + takenRaw + ")");
          var i;
          if (arr && arr.length) {
            for (i = 0; i < arr.length; i++) markTaken(parseInt(arr[i], 10));
          }
        }
      } catch (e2) {}
    }
    function saveLocal() {
      try {
        if (!window.localStorage) return;
        var k = storageKeys();
        if (knownAccountKey) {
          localStorage.setItem(k.account, knownAccountKey);
        }
        localStorage.setItem(k.count, String(claimedCount));
        localStorage.setItem(k.last, lastClaimUtc || "");
        localStorage.setItem(k.granted, lastGrantedUtc || "");
        var takenArr = [];
        var d;
        for (d = 1; d <= TOTAL; d++) {
          if (TAKEN_DAYS[d]) takenArr.push(d);
        }
        localStorage.setItem(k.taken, "[" + takenArr.join(",") + "]");
      } catch (e3) {}
    }
    function refreshIdentity() {
      var next = currentIdentityKey();
      if (next !== identityKey) {
        identityKey = next;
        // Account board is shared — do not wipe Taken on character switch.
        paint();
      }
      return identityKey;
    }"""

if old_storage not in text:
    raise SystemExit("storage block missing/mismatch")
text = text.replace(old_storage, new_storage)
print("patched storage")

# applyClaimResult — store accountKey, always saveLocal
old_apply_result = """    function applyClaimResult(j) {
      if (!j) return;
      if (j.taken && j.taken.length) {
        resetTakenMap();
        var i;
        for (i = 0; i < j.taken.length; i++) markTaken(parseInt(j.taken[i], 10));
      } else if (j.day > 0) {
        markTaken(parseInt(j.day, 10));
      }
      if (j.lastGrantedUtc) lastGrantedUtc = String(j.lastGrantedUtc);
      else if (j.ok === true) lastGrantedUtc = utcDay();
      if (j.lastClaimUtc) lastClaimUtc = String(j.lastClaimUtc);
      claimedCount = 0;
      var d;
      for (d = 1; d <= TOTAL; d++) {
        if (TAKEN_DAYS[d]) claimedCount++;
      }
      saveLocal();
    }"""

new_apply_result = """    function applyClaimResult(j) {
      if (!j) return;
      if (j.accountKey) setKnownAccountKey(j.accountKey);
      if (j.taken && j.taken.length) {
        resetTakenMap();
        var i;
        for (i = 0; i < j.taken.length; i++) markTaken(parseInt(j.taken[i], 10));
      } else if (j.day > 0) {
        markTaken(parseInt(j.day, 10));
      }
      if (j.lastGrantedUtc) lastGrantedUtc = String(j.lastGrantedUtc);
      else if (j.ok === true || j.claimedToday) lastGrantedUtc = utcDay();
      if (j.lastClaimUtc) lastClaimUtc = String(j.lastClaimUtc);
      claimedCount = 0;
      var d;
      for (d = 1; d <= TOTAL; d++) {
        if (TAKEN_DAYS[d]) claimedCount++;
      }
      saveLocal();
    }"""

if old_apply_result not in text:
    raise SystemExit("applyClaimResult missing")
text = text.replace(old_apply_result, new_apply_result)

# pollClaimResult — already claimed today with taken[] = success for board
old_poll_fail = """            if (j && j.ok === false) {
              done(j.message || "Claim failed.", false);
              return;
            }"""
new_poll_fail = """            if (j && j.ok === false) {
              // Still apply board if Zone returned account Taken (e.g. already claimed today).
              if (j.taken && j.taken.length) {
                applyClaimResult(j);
                done(j.message || "Already claimed today.", true);
                return;
              }
              done(j.message || "Claim failed.", false);
              return;
            }"""
if old_poll_fail not in text:
    raise SystemExit("poll fail branch missing")
text = text.replace(old_poll_fail, new_poll_fail)

# doClaim done() — do NOT revert optimistic taken on timeout/fail (keep taken.png)
# Find and replace the revert block
old_done = """      var done = function (msg, ok) {
        claiming = false;
        if (!ok) {
          // Grant failed — undo optimistic lock so player can retry today.
          TAKEN_DAYS[optimisticDay] = false;
          lastGrantedUtc = "";
          claimedCount = 0;
          var d2;
          for (d2 = 1; d2 <= TOTAL; d2++) {
            if (TAKEN_DAYS[d2]) claimedCount++;
          }
          saveLocal();
          if (status) status.innerHTML = msg || "Claim failed. Try again.";
        } else {
          if (status) status.innerHTML = msg || ("Claimed day " + optimisticDay + ".");
          syncFromServer();
        }
        paint();
      };"""

new_done = """      var done = function (msg, ok) {
        claiming = false;
        // Keep optimistic taken.png even if poll times out — Zone may still have granted.
        // Board is corrected on next syncFromServer / reopen via accountKey.
        saveLocal();
        if (status) status.innerHTML = msg || (ok ? ("Claimed day " + optimisticDay + ".") : "Claim sent. If the item is missing, reopen daily rewards.");
        if (ok) syncFromServer();
        paint();
      };"""

if old_done not in text:
    raise SystemExit("done() block missing")
text = text.replace(old_done, new_done)

# applyServerState — accept boardAccount / accountKey even when character id missing
old_apply_server = """    function applyServerState(j) {
      if (!j) return;
      if (typeof j.freeTestMode === "boolean") freeTestMode = j.freeTestMode;
      // Without CharacterID, GET has no account Taken — keep local/result state.
      if (j.hasIdentity === false) return;
      if (typeof j.claimedCount === "number") claimedCount = j.claimedCount;
      if (j.lastGrantedUtc) lastGrantedUtc = String(j.lastGrantedUtc);
      if (j.lastClaimUtc) lastClaimUtc = String(j.lastClaimUtc);
      resetTakenMap();
      if (j.taken && j.taken.length) {
        var i;
        for (i = 0; i < j.taken.length; i++) markTaken(parseInt(j.taken[i], 10));
      }
      claimedCount = 0;
      var d;
      for (d = 1; d <= TOTAL; d++) {
        if (TAKEN_DAYS[d]) claimedCount++;
      }
      saveLocal();
    }"""

new_apply_server = """    function applyServerState(j) {
      if (!j) return;
      if (typeof j.freeTestMode === "boolean") freeTestMode = j.freeTestMode;
      if (j.accountKey) setKnownAccountKey(j.accountKey);
      else if (j.boardAccount) setKnownAccountKey(j.boardAccount);
      // Need account board data (from accountKey query or character identity).
      if (j.hasIdentity === false && !(j.taken && j.taken.length)) return;
      if (typeof j.claimedCount === "number") claimedCount = j.claimedCount;
      if (j.lastGrantedUtc) lastGrantedUtc = String(j.lastGrantedUtc);
      if (j.lastClaimUtc) lastClaimUtc = String(j.lastClaimUtc);
      if (j.taken && j.taken.length) {
        resetTakenMap();
        var i;
        for (i = 0; i < j.taken.length; i++) markTaken(parseInt(j.taken[i], 10));
      }
      claimedCount = 0;
      var d;
      for (d = 1; d <= TOTAL; d++) {
        if (TAKEN_DAYS[d]) claimedCount++;
      }
      saveLocal();
    }"""

if old_apply_server not in text:
    raise SystemExit("applyServerState missing")
text = text.replace(old_apply_server, new_apply_server)

# syncFromServer — pass accountKey
old_sync = """    function syncFromServer() {
      refreshIdentity();
      var idn = resolveIdentity();
      try {
        var x = new XMLHttpRequest();
        var q = "claim.php?month=" + encodeURIComponent(utcMonth())
          + "&characterId=" + encodeURIComponent(idn.id || "")
          + "&character=" + encodeURIComponent(idn.name || "")
          + "&t=" + new Date().getTime();
        x.open("GET", q, true);
        x.onreadystatechange = function () {
          if (x.readyState === 4 && x.status === 200) {
            try {
              applyServerState(eval("(" + x.responseText + ")"));
              paint();
            } catch (e2) {}
          }
        };
        x.send(null);
      } catch (e3) {}
    }"""

new_sync = """    function syncFromServer() {
      refreshIdentity();
      loadStoredAccountKey();
      var idn = resolveIdentity();
      try {
        var x = new XMLHttpRequest();
        var q = "claim.php?month=" + encodeURIComponent(utcMonth())
          + "&characterId=" + encodeURIComponent(idn.id || "")
          + "&character=" + encodeURIComponent(idn.name || "")
          + "&accountKey=" + encodeURIComponent(knownAccountKey || "")
          + "&t=" + new Date().getTime();
        x.open("GET", q, true);
        x.onreadystatechange = function () {
          if (x.readyState === 4 && x.status === 200) {
            try {
              applyServerState(eval("(" + x.responseText + ")"));
              paint();
            } catch (e2) {}
          }
        };
        x.send(null);
      } catch (e3) {}
    }"""

if old_sync not in text:
    raise SystemExit("syncFromServer missing")
text = text.replace(old_sync, new_sync)

# POST claim also send accountKey
text = text.replace(
    """          "{\"month\":\"" + utcMonth() + "\""
          + ",\"claimToken\":\"" + token + "\""
          + ",\"characterId\":" + (idn.id ? parseInt(idn.id, 10) || 0 : 0)
          + ",\"character\":\"" + String(idn.name || "").replace(/\\/g, "\\\\").replace(/"/g, "\\\"") + "\""
          + "}"
        );""",
    """          "{\"month\":\"" + utcMonth() + "\""
          + ",\"claimToken\":\"" + token + "\""
          + ",\"accountKey\":\"" + String(knownAccountKey || "").replace(/\\/g, "\\\\").replace(/"/g, "\\\"") + "\""
          + ",\"characterId\":" + (idn.id ? parseInt(idn.id, 10) || 0 : 0)
          + ",\"character\":\"" + String(idn.name || "").replace(/\\/g, "\\\\").replace(/"/g, "\\\"") + "\""
          + "}"
        );""",
)

# Boot: loadStoredAccountKey before loadLocal
text = text.replace(
    """    loadLocal();
    refreshIdentity();
    loadRewards();""",
    """    loadStoredAccountKey();
    loadLocal();
    refreshIdentity();
    loadRewards();""",
)

tmp = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_daily_persist.html")
tmp.write_text(text, encoding="utf-8")
for p in [
    idx,
    Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\icc-rk-local-web\daily\index.app"),
    Path(r"C:\xampp\htdocs\uwg.daily.icc-rk\index.html"),
    Path(r"C:\xampp\htdocs\uwg.daily.icc-rk\index.app"),
    Path(r"C:\xampp\htdocs\daily\index.html"),
    Path(r"C:\xampp\htdocs\daily\index.app"),
]:
    shutil.copyfile(str(tmp), str(p))
    print("index", p)

# Fix indira LastGrantedUtc migrate now
import json
for ap in [
    Path(r"C:\xampp\htdocs\daily\data\claims\account-indira.json"),
    Path(r"C:\xampp\htdocs\uwg.daily.icc-rk\data\claims\account-indira.json"),
]:
    if not ap.exists():
        continue
    st = json.loads(ap.read_text(encoding="utf-8"))
    if not st.get("LastGrantedUtc") and st.get("LastClaimUtc"):
        st["LastGrantedUtc"] = st["LastClaimUtc"]
        ap.write_text(json.dumps(st), encoding="utf-8")
        print("migrated", ap)

print("OK")
