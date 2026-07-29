(function () {
  var grid = document.getElementById("rewardGrid");
  var btn = document.getElementById("claimBtn");
  var status = document.getElementById("claimStatus");
  var key = "aorebirth.daily.claim";

  function claimed() {
    try {
      return window.localStorage && localStorage.getItem(key) === "1";
    } catch (e) {
      return false;
    }
  }

  function setClaimed() {
    try {
      if (window.localStorage) localStorage.setItem(key, "1");
    } catch (e) {}
  }

  function render() {
    if (!grid) return;
    var html = "";
    for (var i = 1; i <= 14; i++) {
      var cls = "card";
      var label = "Day " + i;
      if (i <= 3) {
        cls += " claimed";
      } else if (i === 4) {
        cls += claimed() ? " claimed" : " today";
        label = claimed() ? "Claimed" : "Today";
      }
      html += '<div class="' + cls + '">' + label + "</div>";
    }
    grid.innerHTML = html;
    if (claimed() && status) status.innerHTML = "1 rewards claimed.";
  }

  if (btn) {
    btn.onclick = function () {
      setClaimed();
      if (status) status.innerHTML = "1 rewards claimed. (local stub)";
      render();
      return false;
    };
  }

  render();
})();
