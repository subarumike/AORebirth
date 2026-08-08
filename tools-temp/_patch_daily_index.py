# -*- coding: utf-8 -*-
from pathlib import Path

paths = [
    Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\icc-rk-local-web\daily\index.html"),
    Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\icc-rk-local-web\daily\index.app"),
    Path(r"C:\xampp\htdocs\uwg.daily.icc-rk\index.html"),
    Path(r"C:\xampp\htdocs\uwg.daily.icc-rk\index.app"),
    Path(r"C:\xampp\htdocs\daily\index.html"),
    Path(r"C:\xampp\htdocs\daily\index.app"),
]

old_apply = '''    function applyServerState(j) {
      if (!j) return;
      // Never apply anonymous/shared taken onto the board.
      if (j.hasIdentity === false) return;
      if (typeof j.freeTestMode === "boolean") freeTestMode = j.freeTestMode;
      if (typeof j.claimedCount === "number") claimedCount = j.claimedCount;
      if (j.lastGrantedUtc) lastGrantedUtc = String(j.lastGrantedUtc);
      if (j.lastClaimUtc) lastClaimUtc = String(j.lastClaimUtc);
      else if (j.claimedToday) lastClaimUtc = utcDay();
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
    }'''

new_apply = '''    function applyServerState(j) {
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
    }'''

old_click = '''    function onClaimClick() {
      if (!canClaimNow()) {
        if (claimedToday()) {
          if (status) status.innerHTML = "Daily reward already claimed today on this account.";
        } else if (nextDay() < 1) {
          if (status) status.innerHTML = "All 28 rewards claimed this month.";
        } else {
          if (status) status.innerHTML = "No claimable daily reward right now.";
        }
        paint();
        return false;
      }
      var day = nextDay();
      if (!itemIdFor(day)) {
        if (status) status.innerHTML = "Day " + day + " has no reward configured yet.";
        return false;
      }
      selectedDay = day;
      return doClaim(day);
    }'''

new_click = '''    function onClaimClick() {
      if (claimedToday()) {
        if (status) status.innerHTML = "Daily reward already claimed today on this account.";
        paint();
        return false;
      }
      // Zone picks first untaken day for the account and grants the item.
      return doClaim();
    }'''

src = paths[0]
text = src.read_text(encoding="utf-8")
if "function pollClaimResult" not in text:
    raise SystemExit("index.html missing pollClaimResult — abort")
if old_apply in text:
    text = text.replace(old_apply, new_apply)
    print("patched applyServerState")
else:
    print("applyServerState already patched or mismatch")
if old_click in text:
    text = text.replace(old_click, new_click)
    print("patched onClaimClick")
else:
    print("onClaimClick already patched or mismatch")

# Ensure info hint
text = text.replace(
    "Hover a day for details, select it, then press CLAIM REWARD.",
    "Hover a day for details. Press CLAIM REWARD to take the next untaken reward for this account.",
)

tmp = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_daily_index_new.html")
tmp.write_text(text, encoding="utf-8")
import shutil
for p in paths:
    try:
        shutil.copyfile(str(tmp), str(p))
        print("copied", p)
    except Exception as ex:
        print("FAIL", p, ex)
