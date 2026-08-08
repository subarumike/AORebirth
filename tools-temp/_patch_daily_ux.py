# -*- coding: utf-8 -*-
from pathlib import Path
import shutil

src = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\icc-rk-local-web\daily\index.html")
text = src.read_text(encoding="utf-8")

text = text.replace(
    """    a.slot {
      display:block; position:relative;
      border:2px solid #6a7380; background:#000;
      text-decoration:none; cursor:pointer; overflow:hidden;
    }""",
    """    a.slot {
      display:block; position:relative;
      border:2px solid #6a7380; background:#000;
      text-decoration:none; cursor:default; overflow:hidden;
    }""",
)

text = text.replace(
    """    a.slot.taken {
      cursor:pointer;
      border-color:#8a93a0;
    }""",
    """    a.slot.taken {
      cursor:default;
      border-color:#8a93a0;
    }""",
)

text = text.replace(
    """          a.onmouseout = function () {
            scheduleHidePopup();
          };
          a.onclick = (function (d) {
            return function () {
              selectedDay = d;
              showInfoPanel(d, "");
              paint();
              return false;
            };
          })(day);
          td.appendChild(a);""",
    """          a.onmouseout = function () {
            scheduleHidePopup();
          };
          // Days are hover-info only — never clickable / selectable.
          a.onclick = function () { return false; };
          td.appendChild(a);""",
)

text = text.replace(
    """        if (isTaken(d)) {
          a.className = "slot taken" + (selectedDay === d ? " selected" : "");
        } else if (d === next && !claimedToday()) {
          a.className = "slot next" + (selectedDay === d ? " selected" : "");
        } else {
          a.className = "slot" + (selectedDay === d ? " selected" : "");
        }
      }
      if (next > 0) {
        selectedDay = next;
      }""",
    """        if (isTaken(d)) {
          a.className = "slot taken";
        } else if (d === next && !claimedToday()) {
          a.className = "slot next";
        } else {
          a.className = "slot";
        }
      }
      if (next > 0) {
        selectedDay = next;
      }""",
)

old_doclaim = """    function doClaim() {
      if (claiming) return false;
      if (claimedToday()) {
        if (status) status.innerHTML = "Daily reward already claimed today on this account.";
        paint();
        return false;
      }
      claiming = true;
      if (claimBtn) {
        claimBtn.className = "claim disabled";
        claimBtn.style.display = "none";
      }
      var idn = resolveIdentity();
      var token = "c" + String(new Date().getTime()) + Math.floor(Math.random() * 100000);

      var done = function (msg, ok) {
        if (status) status.innerHTML = msg || "";
        claiming = false;
        paint();
        if (ok) syncFromServer();
      };
      try {
        // 1) Arm Zone pending  2) fire CharacterAction 263  3) poll result token
        var x = new XMLHttpRequest();
        x.open("POST", "claim.php", false);
        x.setRequestHeader("Content-Type", "application/json");
        x.send(
          "{\\"month\\":\\"" + utcMonth() + "\\""
          + ",\\"claimToken\\":\\"" + token + "\\""
          + ",\\"characterId\\":" + (idn.id ? parseInt(idn.id, 10) || 0 : 0)
          + ",\\"character\\":\\"" + String(idn.name || "").replace(/\\\\/g, "\\\\\\\\").replace(/"/g, "\\\\\\"") + "\\""
          + "}"
        );
        try {
          var j = eval("(" + x.responseText + ")");
          if (j && j.ok === false) {
            done(j.message || "Claim failed.", false);
            return false;
          }
          if (j && j.claimToken) token = String(j.claimToken);
        } catch (e2) {}
        if (status) status.innerHTML = "Claiming next available reward...";
        tryNativeClaim();
        pollClaimResult(token, 20, done);
      } catch (e3) {
        done("Claim failed to reach server.", false);
      }
      return false;
    }"""

# Read actual doClaim from file for exact match - use simpler unique markers
start = text.find("    function doClaim() {")
end = text.find("    function resolveIdentity() {")
if start < 0 or end < 0:
    raise SystemExit("doClaim/resolveIdentity markers missing")

new_doclaim = r'''    function doClaim() {
      if (claiming) return false;
      if (claimedToday()) {
        if (status) status.innerHTML = "Daily reward already claimed today on this account.";
        paint();
        return false;
      }
      var day = nextDay();
      if (day < 1) {
        if (status) status.innerHTML = "All 28 rewards claimed this month.";
        paint();
        return false;
      }

      // Instant UI: mark taken.png + hide CLAIM (server midnight unlocks next day).
      markTaken(day);
      lastGrantedUtc = utcDay();
      lastClaimUtc = utcDay();
      claimedCount = 0;
      var dCount;
      for (dCount = 1; dCount <= TOTAL; dCount++) {
        if (TAKEN_DAYS[dCount]) claimedCount++;
      }
      saveLocal();
      claiming = true;
      if (claimBtn) {
        claimBtn.className = "claim disabled";
        claimBtn.style.display = "none";
      }
      if (status) status.innerHTML = "Claimed day " + day + ". Come back after 00:00 server time for the next reward.";
      paint();

      var idn = resolveIdentity();
      var token = "c" + String(new Date().getTime()) + Math.floor(Math.random() * 100000);
      var optimisticDay = day;

      var done = function (msg, ok) {
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
      };
      try {
        var x = new XMLHttpRequest();
        x.open("POST", "claim.php", false);
        x.setRequestHeader("Content-Type", "application/json");
        x.send(
          "{\"month\":\"" + utcMonth() + "\""
          + ",\"claimToken\":\"" + token + "\""
          + ",\"characterId\":" + (idn.id ? parseInt(idn.id, 10) || 0 : 0)
          + ",\"character\":\"" + String(idn.name || "").replace(/\\/g, "\\\\").replace(/"/g, "\\\"") + "\""
          + "}"
        );
        try {
          var j = eval("(" + x.responseText + ")");
          if (j && j.ok === false) {
            done(j.message || "Claim failed.", false);
            return false;
          }
          if (j && j.claimToken) token = String(j.claimToken);
        } catch (e2) {}
        tryNativeClaim();
        pollClaimResult(token, 20, done);
      } catch (e3) {
        done("Claim failed to reach server.", false);
      }
      return false;
    }

'''

text = text[:start] + new_doclaim + text[end:]

# Midnight UTC refresh so CLAIM reappears at 00:00 server time
if "scheduleMidnightRefresh" not in text:
    boot = """    loadLocal();
    refreshIdentity();
    loadRewards();
    buildGrid();
    paint();
    syncFromServer();
    setInterval(function () { refreshIdentity(); syncFromServer(); }, 5000);"""
    boot_new = """    function msUntilNextUtcMidnight() {
      var now = new Date();
      var next = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate() + 1, 0, 0, 0, 0));
      var ms = next.getTime() - now.getTime();
      return ms < 1000 ? 1000 : ms;
    }
    function scheduleMidnightRefresh() {
      setTimeout(function () {
        // 00:00 UTC — unlock CLAIM for the next untaken day.
        if (lastGrantedUtc && lastGrantedUtc !== utcDay()) {
          if (status) status.innerHTML = "New daily reward available.";
        }
        paint();
        syncFromServer();
        scheduleMidnightRefresh();
      }, msUntilNextUtcMidnight() + 250);
    }

    loadLocal();
    refreshIdentity();
    loadRewards();
    buildGrid();
    paint();
    syncFromServer();
    scheduleMidnightRefresh();
    setInterval(function () {
      refreshIdentity();
      // Also re-check day rollover every minute (backup for midnight timer).
      paint();
      syncFromServer();
    }, 60000);"""
    if boot not in text:
        raise SystemExit("boot block missing")
    text = text.replace(boot, boot_new)
    print("added midnight refresh")

text = text.replace(
    "Hover a day for details. Press CLAIM REWARD to take the next untaken reward for this account.",
    "Hover a day for details. Press CLAIM REWARD to take the next untaken reward (button returns at 00:00 server time).",
)
text = text.replace(
    "Hover a day for details, select it, then press CLAIM REWARD.",
    "Hover a day for details. Press CLAIM REWARD to take the next untaken reward (button returns at 00:00 server time).",
)

tmp = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_daily_index_ux.html")
tmp.write_text(text, encoding="utf-8")
targets = [
    Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\icc-rk-local-web\daily\index.html"),
    Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\icc-rk-local-web\daily\index.app"),
    Path(r"C:\xampp\htdocs\uwg.daily.icc-rk\index.html"),
    Path(r"C:\xampp\htdocs\uwg.daily.icc-rk\index.app"),
    Path(r"C:\xampp\htdocs\daily\index.html"),
    Path(r"C:\xampp\htdocs\daily\index.app"),
]
for p in targets:
    shutil.copyfile(str(tmp), str(p))
    print("copied", p)
print("OK")
