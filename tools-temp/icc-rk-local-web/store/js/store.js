(function () {
  var grid = document.getElementById("storeGrid");
  var status = document.getElementById("storeStatus");
  var catalog = {
    convenience: [
      { name: "Nophex Vanity Voucher", price: 360 },
      { name: "Nophex Equipment Voucher", price: 480 },
      { name: "Bundle of Opulence", price: 24000 },
      { name: "Hiisi Initiate Bundle", price: 2400 },
      { name: "Leet Bundle", price: 620 },
      { name: "Character Slot", price: 1200 },
      { name: "The Pack of Beacons", price: 1800 },
      { name: "Heckler Juice - Level 200", price: 900 }
    ],
    special: [
      { name: "Special Pack A", price: 1500 },
      { name: "Special Pack B", price: 2200 }
    ],
    nanos: [
      { name: "Nano Pack Starter", price: 400 },
      { name: "Nano Pack Advanced", price: 1200 }
    ],
    vehicles: [
      { name: "Starter Glider Skin", price: 800 },
      { name: "Yalmaha Voucher", price: 5000 }
    ]
  };
  var active = "convenience";

  function render() {
    if (!grid) return;
    var items = catalog[active] || [];
    var html = "";
    for (var i = 0; i < items.length; i++) {
      var item = items[i];
      html += '<div class="card"><div class="label">' + item.name +
        '</div><div class="price">' + item.price + " F</div></div>";
    }
    grid.innerHTML = html;
  }

  function setActiveTab(name) {
    active = name;
    var tabs = document.getElementsByTagName("a");
    for (var i = 0; i < tabs.length; i++) {
      if (tabs[i].className.indexOf("tab") >= 0) {
        if (tabs[i].getAttribute("data-tab") === name) {
          tabs[i].className = "tab active";
        } else {
          tabs[i].className = "tab";
        }
      }
    }
    render();
  }

  var tabs = document.getElementsByTagName("a");
  for (var t = 0; t < tabs.length; t++) {
    (function (link) {
      if (link.className.indexOf("tab") < 0) return;
      link.onclick = function () {
        setActiveTab(link.getAttribute("data-tab"));
        return false;
      };
    })(tabs[t]);
  }

  var claimBtn = document.getElementById("claimItemsBtn");
  if (claimBtn) {
    claimBtn.onclick = function () {
      if (status) status.innerHTML = "No pending store claims on this local stub.";
      return false;
    };
  }

  render();
})();
