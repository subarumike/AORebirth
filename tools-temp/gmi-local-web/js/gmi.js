(function () {
  var MARKET_SLOTS = 21;
  var vaultData = { credits: 0, items: [], character: '', characterId: '' };
  var selectedIndex = -1;
  var mySellOrders = [];
  var myBuyOrders = [];
  var selectedSellOrderId = '';
  var selectedBuyCancelId = '';
  var searchOrders = [];
  var selectedBuyOrderId = '';
  var itemDetail = { lowId: 0, highId: 0, name: '', icon: 0 };
  var itemSellOrders = [];
  var itemBuyOrders = [];
  var selectedItemSellId = '';
  var selectedItemBuyId = '';
  var pendingBuyOrder = null;

  function $(id) { return document.getElementById(id); }

  function setText(el, text) {
    if (!el) return;
    var s = (text == null) ? '' : String(text);
    if (typeof el.innerText !== 'undefined') el.innerText = s;
    else el.textContent = s;
  }

  function setMsg(text, cls) {
    var el = $('depositMsg');
    if (!el) return;
    el.className = 'msg' + (cls ? ' ' + cls : '');
    el.textContent = text || '';
  }

  function setSearchMsg(text, cls) {
    var el = $('searchMsg');
    if (!el) return;
    el.className = 'msg' + (cls ? ' ' + cls : '');
    el.textContent = text || '';
  }

  function setItemMsg(text, cls) {
    var el = $('itemDetailMsg');
    if (!el) return;
    el.className = 'msg' + (cls ? ' ' + cls : '');
    el.textContent = text || '';
  }

  function showNotice(text) {
    var el = $('gmiNotice');
    if (!el) return;
    var now = new Date();
    var y = now.getFullYear();
    var mo = now.getMonth() + 1;
    var da = now.getDate();
    var hh = now.getHours();
    var mm = now.getMinutes();
    var ds =
      y + '-' +
      (mo < 10 ? '0' : '') + mo + '-' +
      (da < 10 ? '0' : '') + da + ' ' +
      (hh < 10 ? '0' : '') + hh + ':' +
      (mm < 10 ? '0' : '') + mm;
    el.style.display = 'block';
    el.innerHTML = '';
    if (typeof el.innerText !== 'undefined') {
      el.innerText = 'Important notice: At ' + ds + ' ' + text + '.';
    } else {
      el.textContent = 'Important notice: At ' + ds + ' ' + text + '.';
    }
  }

  function formatLogDate(isoOrDate) {
    if (!isoOrDate) return '';
    if (String(isoOrDate).indexOf('UTC') >= 0) return String(isoOrDate);
    try {
      var d = new Date(isoOrDate);
      if (isNaN(d.getTime())) return String(isoOrDate);
      var y = d.getFullYear();
      var mo = d.getMonth() + 1;
      var da = d.getDate();
      var hh = d.getHours();
      var mm = d.getMinutes();
      return y + '-' +
        (mo < 10 ? '0' : '') + mo + '-' +
        (da < 10 ? '0' : '') + da + ' ' +
        (hh < 10 ? '0' : '') + hh + ':' +
        (mm < 10 ? '0' : '') + mm;
    } catch (e) {
      return String(isoOrDate);
    }
  }

  function refreshLog() {
    var q = identityQueryExtra();
    q.push('limit=100');
    q.push('t=' + new Date().getTime());
    var xhr = new XMLHttpRequest();
    xhr.open('GET', 'log.php?' + q.join('&'), true);
    xhr.onreadystatechange = function () {
      if (xhr.readyState !== 4) return;
      var msg = $('logMsg');
      var table = $('logTable');
      if (!table) return;
      var tbody = table.getElementsByTagName('tbody')[0];
      try {
        var res = JSON.parse(xhr.responseText || '{}');
        tbody.innerHTML = '';
        var logs = (res.ok && res.logs) ? res.logs : [];
        if (!logs.length) {
          tbody.innerHTML = '<tr><td colspan="4" class="empty-row">No transactions yet.</td></tr>';
          if (msg) {
            msg.className = 'msg';
            msg.innerHTML = '';
            if (typeof msg.innerText !== 'undefined') msg.innerText = 'No buy/sell log entries yet.';
            else msg.textContent = 'No buy/sell log entries yet.';
          }
          return;
        }
        for (var i = 0; i < logs.length; i++) {
          var row = logs[i];
          var tr = document.createElement('tr');
          var when = row.date || formatLogDate(row.at);
          var typ = row.type || '';
          var body = row.message || '';
          var withWho = row.otherCharacter || '';
          tr.innerHTML =
            '<td>' + when + '</td>' +
            '<td>' + typ + '</td>' +
            '<td>' + body + '</td>' +
            '<td>' + withWho + '</td>';
          tbody.appendChild(tr);
        }
        if (msg) {
          msg.className = 'msg ok';
          if (typeof msg.innerText !== 'undefined') msg.innerText = logs.length + ' log entr' + (logs.length === 1 ? 'y' : 'ies') + '.';
          else msg.textContent = logs.length + ' log entries.';
        }
      } catch (e) {
        tbody.innerHTML = '<tr><td colspan="4" class="empty-row">Failed to load log.</td></tr>';
        if (msg) {
          msg.className = 'msg err';
          if (typeof msg.innerText !== 'undefined') msg.innerText = 'log.php error: ' + e;
          else msg.textContent = 'log.php error: ' + e;
        }
      }
    };
    xhr.send(null);
  }

  function formatCredits(n) {
    var x = Math.floor(Number(n) || 0);
    var s = String(x);
    var out = '';
    while (s.length > 3) {
      out = ',' + s.slice(-3) + out;
      s = s.slice(0, -3);
    }
    return s + out;
  }

  function formatExpire(iso) {
    if (!iso) return '';
    try {
      var d = new Date(iso);
      if (isNaN(d.getTime())) return String(iso);
      var m = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
      return d.getUTCFullYear() + '-' + m[d.getUTCMonth()] + '-' +
        (d.getUTCDate() < 10 ? '0' : '') + d.getUTCDate();
    } catch (e) {
      return String(iso);
    }
  }

  function showPanel(name) {
    var allTabs = document.getElementsByClassName('tab');
    for (var t = 0; t < allTabs.length; t++) {
      var tn = allTabs[t].getAttribute('data-tab');
      allTabs[t].className = 'tab' + (tn === name ? ' active' : '');
    }
    var panels = document.getElementsByClassName('panel');
    for (var p = 0; p < panels.length; p++) {
      panels[p].className =
        'panel' + (panels[p].id === 'panel-' + name ? ' active' : '');
    }
  }

  function openDepositWindow() {
    setMsg('');
    try {
      if (window.AnarchyOnline && typeof AnarchyOnline.OpenMarketSendWindow === 'function') {
        AnarchyOnline.OpenMarketSendWindow();
        setMsg('Opened client Deposit window. Send credits/items there.', 'ok');
        return;
      }
      if (window.AnarchyOnline && AnarchyOnline.OpenMarketSendWindow) {
        AnarchyOnline.OpenMarketSendWindow();
        setMsg('Opened client Deposit window. Send credits/items there.', 'ok');
        return;
      }
    } catch (e) {
      setMsg('OpenMarketSendWindow error: ' + e, 'err');
      return;
    }
    setMsg(
      'AnarchyOnline.OpenMarketSendWindow not found. Are you inside the AO Market browser?',
      'err');
  }

  function itemName(it) {
    var low = it.lowId || it.LowId || 0;
    var high = it.highId || it.HighId || 0;
    if (it.name) return it.name;
    if (it.Name) return it.Name;
    return 'Item ' + low + (high && high !== low ? '/' + high : '');
  }

  function itemIconHtml(it) {
    var icon = it.icon || it.Icon || 0;
    var low = it.lowId || it.LowId || 0;
    var id = icon > 0 ? icon : low;
    if (!id) {
      return '<span class="icon-cell"></span>';
    }
    return '<img class="icon-img" src="icon.php?id=' + id + '&low=' + low + '" width="24" height="24" alt="">';
  }

  function applyInvListHeight() {
    var wrap = $('vaultItemsWrap');
    if (!wrap) {
      wrap = document.querySelector
        ? document.querySelector('#panel-inventory .table-wrap.inv')
        : null;
    }
    if (!wrap) return;
    var h = 320;
    try {
      var avail = (document.body && document.body.clientHeight) ? document.body.clientHeight : 0;
      if (avail > 220) {
        // Leave room for account/deposit cards, search, action buttons, free count.
        h = avail - 210;
        if (h < 220) h = 220;
        if (h > 480) h = 480;
      }
    } catch (e) {}
    wrap.style.height = h + 'px';
    wrap.style.maxHeight = h + 'px';
    wrap.style.overflow = 'auto';
  }

  function resolveCharacterIdentity() {
    var id = vaultData.characterId || '';
    var name = vaultData.character || '';
    try {
      var ao = window.AnarchyOnline;
      if (ao) {
        if (ao.CharacterID != null && ao.CharacterID !== '') id = String(ao.CharacterID);
        else if (ao.CharacterId != null && ao.CharacterId !== '') id = String(ao.CharacterId);
        else if (typeof ao.GetCharacterID === 'function') id = String(ao.GetCharacterID() || id);
        if (ao.CharacterName) name = String(ao.CharacterName);
        else if (typeof ao.GetCharacterName === 'function') name = String(ao.GetCharacterName() || name);
      }
    } catch (e) {}
    return { id: id, name: name };
  }

  function vaultQuery() {
    var idn = resolveCharacterIdentity();
    var q = ['t=' + new Date().getTime()];
    if (idn.id) q.push('id=' + encodeURIComponent(idn.id));
    if (idn.name) q.push('name=' + encodeURIComponent(idn.name));
    return q.join('&');
  }

  function identityQueryExtra() {
    var idn = resolveCharacterIdentity();
    var q = [];
    if (idn.name) q.push('character=' + encodeURIComponent(idn.name));
    if (idn.id) q.push('characterId=' + encodeURIComponent(idn.id));
    return q;
  }

  function renderVault(data) {
    vaultData = data || { credits: 0, items: [] };
    var credits = vaultData.credits || 0;
    $('vaultCredits').textContent = formatCredits(credits);
    if ($('modalCreditsBalance')) {
      $('modalCreditsBalance').textContent = formatCredits(credits);
    }

    var tbody = $('vaultItems').getElementsByTagName('tbody')[0];
    tbody.innerHTML = '';
    var items = vaultData.items ? vaultData.items.slice() : [];
    if (selectedIndex >= items.length) {
      selectedIndex = items.length > 0 ? 0 : -1;
    }

    var filter = ($('invFilter').value || '').toLowerCase();

    for (var i = 0; i < items.length; i++) {
      (function (idx, it) {
        var name = itemName(it);
        var ql = it.quality || it.Quality || 0;
        var count = it.count || it.Count || 0;
        if (filter) {
          var hay = (name + ' ' + ql + ' ' + count).toLowerCase();
          if (hay.indexOf(filter) < 0) return;
        }
        var tr = document.createElement('tr');
        if (selectedIndex === idx) tr.className = 'selected';
        tr.onclick = function () {
          selectedIndex = idx;
          renderVault(vaultData);
        };
        var checked = selectedIndex === idx ? ' checked="checked"' : '';
        tr.innerHTML =
          '<td class="col-sel"><input type="radio" name="gmiInvSel" class="sel-radio"' + checked + '></td>' +
          '<td class="col-icon">' + itemIconHtml(it) + '</td>' +
          '<td class="col-ql">' + ql + '</td>' +
          '<td class="col-count">' + count + '</td>' +
          '<td>' + name + '</td>';
        tbody.appendChild(tr);
      })(i, items[i]);
    }

    var used = items.length;
    var free = Math.max(0, MARKET_SLOTS - used);
    for (var s = used; s < MARKET_SLOTS; s++) {
      var empty = document.createElement('tr');
      empty.innerHTML =
        '<td class="col-sel"></td><td class="col-icon"></td><td></td><td></td>' +
        '<td class="empty-slot">Empty Inventory Slot</td>';
      tbody.appendChild(empty);
    }

    $('slotFree').textContent = '(' + free + '/' + MARKET_SLOTS + ' free)';
    applyInvListHeight();
  }

  function refreshVault(quiet) {
    var xhr = new XMLHttpRequest();
    xhr.open('GET', 'vault.php?' + vaultQuery(), true);
    xhr.onreadystatechange = function () {
      if (xhr.readyState !== 4) return;
      if (xhr.status >= 200 && xhr.status < 300) {
        try {
          var data = JSON.parse(xhr.responseText || '{}');
          renderVault(data);
          if (!quiet) {
            if (data.note && !(data.credits > 0) && !(data.items && data.items.length)) {
              setMsg(data.note, 'err');
            } else {
              setMsg('Vault refreshed.', 'ok');
            }
          }
        } catch (e) {
          setMsg('Bad vault JSON: ' + e, 'err');
        }
      } else {
        setMsg('vault.php HTTP ' + xhr.status, 'err');
      }
    };
    xhr.send(null);
  }

  function queueWithdraw(params, okNotice) {
    var idn = resolveCharacterIdentity();
    var q = [];
    for (var k in params) {
      if (params.hasOwnProperty(k)) {
        q.push(encodeURIComponent(k) + '=' + encodeURIComponent(params[k]));
      }
    }
    if (idn.name) q.push('character=' + encodeURIComponent(idn.name));
    if (idn.id) q.push('characterId=' + encodeURIComponent(idn.id));
    if (!idn.name && !idn.id) {
      setMsg('Character identity missing — cannot withdraw for another player.', 'err');
      return;
    }

    var xhr = new XMLHttpRequest();
    xhr.open('GET', 'withdraw.php?' + q.join('&') + '&t=' + new Date().getTime(), true);
    xhr.onreadystatechange = function () {
      if (xhr.readyState !== 4) return;
      try {
        var res = JSON.parse(xhr.responseText || '{}');
        if (res.ok) {
          showNotice(okNotice || res.notice || 'withdraw queued');
          if (typeof res.credits === 'number' || res.items) {
            renderVault({
              credits: (typeof res.credits === 'number') ? res.credits : vaultData.credits,
              items: res.items || [],
              character: res.character || vaultData.character,
              characterId: res.characterId || vaultData.characterId
            });
            selectedIndex = -1;
          }
          setMsg(
            'Balance updated. Mail arrives after Zone runs (open Mail or Deposit once).',
            'ok');
          refreshVault(true);
        } else {
          setMsg(res.error || 'Withdraw failed.', 'err');
        }
      } catch (e) {
        setMsg('withdraw.php error: ' + e, 'err');
      }
    };
    xhr.send(null);
  }

  function postSell(index, price, count) {
    var q = identityQueryExtra();
    q.push('index=' + encodeURIComponent(index));
    q.push('price=' + encodeURIComponent(price));
    q.push('count=' + encodeURIComponent(count));
    q.push('t=' + new Date().getTime());
    var xhr = new XMLHttpRequest();
    xhr.open('GET', 'sell.php?' + q.join('&'), true);
    xhr.onreadystatechange = function () {
      if (xhr.readyState !== 4) return;
      try {
        var res = JSON.parse(xhr.responseText || '{}');
        if (res.ok) {
          showNotice(res.notice || 'item listed for sale');
          renderVault({
            credits: (typeof res.credits === 'number') ? res.credits : vaultData.credits,
            items: res.items || [],
            character: res.character || vaultData.character,
            characterId: res.characterId || vaultData.characterId
          });
          selectedIndex = -1;
          setMsg('Listed for sale. See My Orders.', 'ok');
          refreshMyOrders();
          if (itemDetail.lowId) openItemDetail(itemDetail, true);
        } else {
          setMsg(res.error || 'Sell failed.', 'err');
        }
      } catch (e) {
        setMsg('sell.php error: ' + e, 'err');
      }
    };
    xhr.send(null);
  }

  function findVaultIndexForLowId(lowId) {
    var items = vaultData.items || [];
    for (var i = 0; i < items.length; i++) {
      var lid = items[i].lowId || items[i].LowId || 0;
      if (Number(lid) === Number(lowId)) return i;
    }
    return -1;
  }

  function openSellModalForIndex(idx) {
    if (idx < 0 || !vaultData.items || !vaultData.items[idx]) {
      setMsg('Select an item with the radio button first.', 'err');
      setItemMsg('Deposit that item into Market Inventory first, then Create Sell Order.', 'err');
      return;
    }
    selectedIndex = idx;
    var it = vaultData.items[idx];
    var max = it.count || it.Count || 1;
    $('modalSellIcon').innerHTML = itemIconHtml(it);
    $('modalSellQl').textContent = String(it.quality || it.Quality || 0);
    $('modalSellName').textContent = itemName(it);
    $('sellCount').value = String(max);
    $('modalSellMax').textContent = '(max ' + max + ')';
    $('sellPrice').value = '0';
    $('modalSell').style.display = 'block';
  }

  function renderMySellTable() {
    var tbody = $('mySellOrders').getElementsByTagName('tbody')[0];
    tbody.innerHTML = '';
    if (!mySellOrders.length) {
      tbody.innerHTML = '<tr><td colspan="7" class="empty-row">No sell orders.</td></tr>';
      return;
    }
    for (var i = 0; i < mySellOrders.length; i++) {
      (function (ord) {
        var tr = document.createElement('tr');
        if (selectedSellOrderId === ord.id) tr.className = 'selected';
        tr.onclick = function () {
          selectedSellOrderId = ord.id;
          renderMySellTable();
        };
        var checked = selectedSellOrderId === ord.id ? ' checked="checked"' : '';
        tr.innerHTML =
          '<td class="col-sel"><input type="radio" name="gmiMySell"' + checked + '></td>' +
          '<td class="col-icon">' + itemIconHtml(ord) + '</td>' +
          '<td>' + (ord.name || '') + '</td>' +
          '<td class="price-pos">' + formatCredits(ord.unitPrice || 0) + '</td>' +
          '<td class="col-ql">' + (ord.quality || 0) + '</td>' +
          '<td class="col-count">' + (ord.count || 1) + '</td>' +
          '<td>' + formatExpire(ord.expiresAt) + '</td>';
        tbody.appendChild(tr);
      })(mySellOrders[i]);
    }
  }

  function renderMyBuyTable() {
    var table = $('myBuyOrders');
    if (!table) return;
    var tbody = table.getElementsByTagName('tbody')[0];
    tbody.innerHTML = '';
    if (!myBuyOrders.length) {
      tbody.innerHTML = '<tr><td colspan="8" class="empty-row">No buy orders.</td></tr>';
      return;
    }
    for (var i = 0; i < myBuyOrders.length; i++) {
      (function (ord) {
        var tr = document.createElement('tr');
        if (selectedBuyCancelId === ord.id) tr.className = 'selected';
        tr.onclick = function () {
          selectedBuyCancelId = ord.id;
          renderMyBuyTable();
        };
        var checked = selectedBuyCancelId === ord.id ? ' checked="checked"' : '';
        tr.innerHTML =
          '<td class="col-sel"><input type="radio" name="gmiMyBuy"' + checked + '></td>' +
          '<td class="col-icon">' + itemIconHtml(ord) + '</td>' +
          '<td>' + (ord.name || '') + '</td>' +
          '<td class="price-pos">' + formatCredits(ord.unitPrice || 0) + '</td>' +
          '<td class="col-ql">' + (ord.minQl || 1) + '</td>' +
          '<td class="col-ql">' + (ord.maxQl || 200) + '</td>' +
          '<td class="col-count">' + (ord.count || 1) + '</td>' +
          '<td>' + formatExpire(ord.expiresAt) + '</td>';
        tbody.appendChild(tr);
      })(myBuyOrders[i]);
    }
  }

  function refreshMyOrders() {
    var qSell = identityQueryExtra();
    qSell.push('mine=1');
    qSell.push('orderType=sell');
    qSell.push('t=' + new Date().getTime());
    var xhr = new XMLHttpRequest();
    xhr.open('GET', 'orders.php?' + qSell.join('&'), true);
    xhr.onreadystatechange = function () {
      if (xhr.readyState !== 4) return;
      try {
        var res = JSON.parse(xhr.responseText || '{}');
        mySellOrders = (res.ok && res.orders) ? res.orders : [];
        selectedSellOrderId = mySellOrders.length ? (selectedSellOrderId || mySellOrders[0].id) : '';
        var still = false;
        for (var i = 0; i < mySellOrders.length; i++) {
          if (mySellOrders[i].id === selectedSellOrderId) still = true;
        }
        if (!still) selectedSellOrderId = mySellOrders.length ? mySellOrders[0].id : '';
        renderMySellTable();
      } catch (e) {}
    };
    xhr.send(null);

    var qBuy = identityQueryExtra();
    qBuy.push('mine=1');
    qBuy.push('orderType=buy');
    qBuy.push('t=' + new Date().getTime());
    var xhr2 = new XMLHttpRequest();
    xhr2.open('GET', 'orders.php?' + qBuy.join('&'), true);
    xhr2.onreadystatechange = function () {
      if (xhr2.readyState !== 4) return;
      try {
        var res2 = JSON.parse(xhr2.responseText || '{}');
        myBuyOrders = (res2.ok && res2.orders) ? res2.orders : [];
        selectedBuyCancelId = myBuyOrders.length ? (selectedBuyCancelId || myBuyOrders[0].id) : '';
        var still2 = false;
        for (var j = 0; j < myBuyOrders.length; j++) {
          if (myBuyOrders[j].id === selectedBuyCancelId) still2 = true;
        }
        if (!still2) selectedBuyCancelId = myBuyOrders.length ? myBuyOrders[0].id : '';
        renderMyBuyTable();
      } catch (e2) {}
    };
    xhr2.send(null);
  }

  var searchQueried = false;

  function refreshMySellOrders() { refreshMyOrders(); }

  function clearSearchResults() {
    searchOrders = [];
    searchQueried = false;
    selectedBuyOrderId = '';
    var tbody = $('searchResults').getElementsByTagName('tbody')[0];
    if (tbody) {
      tbody.innerHTML = '<tr><td colspan="6" class="empty-row">Enter a name and click Search.</td></tr>';
    }
    setSearchMsg('', '');
    sizeSearchList();
  }

  function runSearch() {
    var filter = ($('searchName').value || '').replace(/^\s+|\s+$/g, '');
    if (!filter) {
      clearSearchResults();
      setSearchMsg('Type an item name, then click Search.', 'err');
      return;
    }
    searchQueried = true;
    var filterLower = filter.toLowerCase();
    var xhr = new XMLHttpRequest();
    xhr.open('GET', 'orders.php?orderType=sell&t=' + new Date().getTime(), true);
    xhr.onreadystatechange = function () {
      if (xhr.readyState !== 4) return;
      try {
        var res = JSON.parse(xhr.responseText || '{}');
        var all = (res.ok && res.orders) ? res.orders : [];
        searchOrders = [];
        for (var i = 0; i < all.length; i++) {
          var o = all[i];
          var hay = ((o.name || '') + ' ' + (o.sellerCharacter || '')).toLowerCase();
          if (hay.indexOf(filterLower) < 0) continue;
          searchOrders.push(o);
        }
        selectedBuyOrderId = '';
        renderSearchResults();
        setSearchMsg(searchOrders.length + ' sell order(s). Click a row for buy/sell orders.', 'ok');
        sizeSearchList();
      } catch (e) {
        setSearchMsg('orders.php error: ' + e, 'err');
      }
    };
    xhr.send(null);
  }

  function renderSearchResults() {
    var tbody = $('searchResults').getElementsByTagName('tbody')[0];
    tbody.innerHTML = '';
    if (!searchQueried) {
      tbody.innerHTML = '<tr><td colspan="6" class="empty-row">Enter a name and click Search.</td></tr>';
      return;
    }
    if (!searchOrders.length) {
      tbody.innerHTML = '<tr><td colspan="6" class="empty-row">No matching sell orders.</td></tr>';
      return;
    }
    for (var i = 0; i < searchOrders.length; i++) {
      (function (ord) {
        var tr = document.createElement('tr');
        tr.style.cursor = 'pointer';
        tr.onclick = function () {
          openItemDetail({
            lowId: ord.lowId || 0,
            highId: ord.highId || ord.lowId || 0,
            name: ord.name || '',
            icon: ord.icon || 0
          }, false);
        };
        tr.innerHTML =
          '<td class="col-icon">' + itemIconHtml(ord) + '</td>' +
          '<td>' + (ord.name || '') + '</td>' +
          '<td class="price-pos">' + formatCredits(ord.unitPrice || 0) + '</td>' +
          '<td class="col-ql">' + (ord.quality || 0) + '</td>' +
          '<td class="col-count">' + (ord.count || 1) + '</td>' +
          '<td>' + (ord.sellerCharacter || '') + '</td>';
        tbody.appendChild(tr);
      })(searchOrders[i]);
    }
  }

  function sizeSearchList() {
    var wrap = $('searchResultsWrap');
    if (!wrap) return;
    var h = 380;
    try {
      var avail = (document.body && document.body.clientHeight) ? document.body.clientHeight : 0;
      if (avail > 200) {
        h = avail - 140;
        if (h < 240) h = 240;
        if (h > 520) h = 520;
      }
    } catch (e) {}
    wrap.style.height = h + 'px';
    wrap.style.maxHeight = h + 'px';
    wrap.style.overflow = 'auto';
  }

  function findOpenSellOrderById(oid) {
    if (!oid) return null;
    var lists = [searchOrders, itemSellOrders, mySellOrders];
    for (var L = 0; L < lists.length; L++) {
      var arr = lists[L] || [];
      for (var i = 0; i < arr.length; i++) {
        if (arr[i].id === oid) return arr[i];
      }
    }
    return null;
  }

  function updateBuyQtyTotal() {
    if (!pendingBuyOrder) return;
    var unit = Number(pendingBuyOrder.unitPrice) || 0;
    var max = Number(pendingBuyOrder.count) || 1;
    var raw = $('buyQtyCount') ? $('buyQtyCount').value : '1';
    var n = parseInt(raw, 10);
    if (isNaN(n) || n < 1) n = 1;
    if (n > max) n = max;
    setText($('modalBuyQtyUnit'), formatCredits(unit));
    setText($('modalBuyQtyTotal'), formatCredits(unit * n));
  }

  function clampBuyQtyInput() {
    if (!pendingBuyOrder || !$('buyQtyCount')) return;
    var max = Number(pendingBuyOrder.count) || 1;
    var n = parseInt($('buyQtyCount').value, 10);
    if (isNaN(n) || n < 1) n = 1;
    if (n > max) n = max;
    $('buyQtyCount').value = String(n);
    updateBuyQtyTotal();
  }

  function openBuyQtyModal(ord) {
    if (!ord) {
      setSearchMsg('Select a sell order first.', 'err');
      setItemMsg('Select a sell order first.', 'err');
      alert('Select a sell order first.');
      return;
    }
    pendingBuyOrder = ord;
    var max = Number(ord.count) || 1;
    $('modalBuyQtyIcon').innerHTML = itemIconHtml(ord);
    setText($('modalBuyQtyQl'), String(ord.quality || 0));
    setText($('modalBuyQtyName'), ord.name || '');
    setText($('modalBuyQtyAvail'), String(max));
    $('buyQtyCount').value = String(max);
    setText($('modalBuyQtyMax'), '(max ' + max + ')');
    updateBuyQtyTotal();
    $('modalBuyQty').style.display = 'block';
  }

  function buySelected() {
    var oid = selectedBuyOrderId || selectedItemSellId;
    if (!oid) {
      setSearchMsg('Select a sell order first.', 'err');
      setItemMsg('Select a sell order first.', 'err');
      alert('Select a sell order first.');
      return;
    }
    var ord = findOpenSellOrderById(oid);
    if (!ord) {
      alert('Order not found in list. Refresh Search / Statistics and try again.');
      return;
    }
    openBuyQtyModal(ord);
  }

  function cancelBuyQty() {
    pendingBuyOrder = null;
    closeModals();
    return false;
  }

  function confirmBuyQty() {
    if (!pendingBuyOrder || !pendingBuyOrder.id) {
      alert('Select a sell order first.');
      return false;
    }
    clampBuyQtyInput();
    var max = Number(pendingBuyOrder.count) || 1;
    var buyCount = parseInt($('buyQtyCount').value, 10) || 1;
    if (buyCount < 1) buyCount = 1;
    if (buyCount > max) buyCount = max;
    var oid = pendingBuyOrder.id;
    var unit = Number(pendingBuyOrder.unitPrice) || 0;

    closeModals();
    pendingBuyOrder = null;

    var q = identityQueryExtra();
    q.push('orderId=' + encodeURIComponent(oid));
    q.push('count=' + encodeURIComponent(buyCount));
    q.push('t=' + new Date().getTime());
    var xhr = new XMLHttpRequest();
    xhr.open('GET', 'buy.php?' + q.join('&'), true);
    xhr.onreadystatechange = function () {
      if (xhr.readyState !== 4) return;
      try {
        var res = JSON.parse(xhr.responseText || '{}');
        if (res.ok) {
          showNotice(res.notice || 'purchase complete');
          var doneMsg = res.notice || ('Bought ' + buyCount + ' @ ' + formatCredits(unit));
          if (res.mailQueued) {
            doneMsg += ' Open Mail (or Deposit once) to receive it.';
          }
          setSearchMsg(doneMsg, 'ok');
          setItemMsg(doneMsg, 'ok');
          if (typeof res.credits === 'number') {
            renderVault({
              credits: res.credits,
              items: res.items || vaultData.items,
              character: res.character || vaultData.character,
              characterId: res.characterId || vaultData.characterId
            });
          }
          runSearch();
          refreshStats();
          if (itemDetail.lowId) openItemDetail(itemDetail, true);
        } else {
          var err = res.error || 'Buy failed.';
          setSearchMsg(err, 'err');
          setItemMsg(err, 'err');
          alert(err);
        }
      } catch (e) {
        var msg = 'buy.php error: ' + e;
        setSearchMsg(msg, 'err');
        setItemMsg(msg, 'err');
        alert(msg + '\nHTTP ' + xhr.status);
      }
    };
    xhr.send(null);
    return false;
  }

  window.gmiConfirmBuyQty = confirmBuyQty;
  window.gmiCancelBuyQty = cancelBuyQty;
  window.gmiUpdateBuyQtyTotal = function () {
    updateBuyQtyTotal();
  };

  function cancelOrder(orderId, done) {
    if (!orderId) {
      alert('Select an order to cancel.');
      return;
    }
    var q = identityQueryExtra();
    q.push('cancel=' + encodeURIComponent(orderId));
    q.push('t=' + new Date().getTime());
    var xhr = new XMLHttpRequest();
    xhr.open('GET', 'orders.php?' + q.join('&'), true);
    xhr.onreadystatechange = function () {
      if (xhr.readyState !== 4) return;
      try {
        var res = JSON.parse(xhr.responseText || '{}');
        if (res.ok) {
          showNotice(res.notice || 'order cancelled');
          if (typeof res.credits === 'number' || res.items) {
            renderVault({
              credits: (typeof res.credits === 'number') ? res.credits : vaultData.credits,
              items: res.items || [],
              character: vaultData.character,
              characterId: vaultData.characterId
            });
          }
          if (done) done();
          refreshMyOrders();
        } else {
          alert(res.error || 'Cancel failed.');
        }
      } catch (e) {
        alert('orders.php cancel error: ' + e);
      }
    };
    xhr.send(null);
  }

  function cancelSelectedSell() {
    cancelOrder(selectedSellOrderId, function () { selectedSellOrderId = ''; });
  }

  function cancelSelectedBuy() {
    cancelOrder(selectedBuyCancelId, function () { selectedBuyCancelId = ''; });
  }

  var pendingInfoItem = null;

  function truncateName(name, maxLen) {
    var s = name || '';
    if (!maxLen) maxLen = 22;
    if (s.length <= maxLen) return s;
    return s.slice(0, maxLen - 1) + '\u2026';
  }

  function openItemInfoPopup(row) {
    pendingInfoItem = row || null;
    if (!row) return;
    $('modalInfoIcon').innerHTML = itemIconHtml(row);
    setText($('modalInfoName'), row.name || ('Item ' + (row.lowId || '')));
    setText($('modalInfoIds'), 'ID ' + (row.lowId || 0) + (row.highId && row.highId !== row.lowId ? (' / ' + row.highId) : ''));
    setText($('modalInfoMonthSales'), String(row.monthSales || row.sales || 0));
    setText($('modalInfoOpenSell'), String(row.openSell != null ? row.openSell : (row.open || 0)));
    setText($('modalInfoOpenBuy'), String(row.openBuy != null ? row.openBuy : 0));
    setText($('modalInfoMonthBuys'), String(row.monthBuys || 0));
    $('modalItemInfo').style.display = 'block';
  }

  function closeItemInfoPopup() {
    pendingInfoItem = null;
    if ($('modalItemInfo')) $('modalItemInfo').style.display = 'none';
    return false;
  }

  function viewOrdersFromInfo() {
    var row = pendingInfoItem;
    closeItemInfoPopup();
    if (!row) return false;
    openItemDetail({
      lowId: row.lowId || 0,
      highId: row.highId || row.lowId || 0,
      name: row.name || '',
      icon: row.icon || 0
    }, false);
    return false;
  }

  window.gmiInfoClose = closeItemInfoPopup;
  window.gmiInfoViewOrders = viewOrdersFromInfo;

  function appendStatsRow(tbody, row, amountField, tipKind) {
    var tr = document.createElement('tr');
    tr.style.cursor = 'pointer';
    var fullName = row.name || '';
    var amount = row[amountField] || 0;
    var openSell = (row.openSell != null) ? row.openSell : (row.open || 0);
    var openBuy = (row.openBuy != null) ? row.openBuy : 0;
    var tip = tipKind === 'sell'
      ? ('Month sales: ' + amount + ' | Listed now: ' + openSell)
      : ('Open buy orders: ' + amount + ' | Month bought: ' + (row.monthBuys || 0));
    tr.title = tip + ' — ' + fullName;
    tr.onclick = function () {
      openItemDetail({
        lowId: row.lowId || 0,
        highId: row.highId || row.lowId || 0,
        name: fullName,
        icon: row.icon || 0
      }, false);
    };

    var tdIcon = document.createElement('td');
    tdIcon.className = 'col-icon';
    tdIcon.innerHTML = itemIconHtml(row);

    var tdAmt = document.createElement('td');
    tdAmt.className = 'stats-amt';
    setText(tdAmt, String(amount));

    var tdName = document.createElement('td');
    tdName.className = 'stats-name';
    setText(tdName, truncateName(fullName, 22));

    var tdInfo = document.createElement('td');
    tdInfo.className = 'col-info';
    var infoBtn = document.createElement('a');
    infoBtn.href = 'javascript:void(0)';
    infoBtn.className = 'info-btn';
    infoBtn.title = 'Item info';
    setText(infoBtn, 'i');
    infoBtn.onclick = function (ev) {
      if (ev) {
        if (ev.preventDefault) ev.preventDefault();
        if (ev.stopPropagation) ev.stopPropagation();
        ev.cancelBubble = true;
      }
      openItemInfoPopup(row);
      return false;
    };
    tdInfo.appendChild(infoBtn);

    tr.appendChild(tdIcon);
    tr.appendChild(tdAmt);
    tr.appendChild(tdName);
    tr.appendChild(tdInfo);
    tbody.appendChild(tr);
  }

  function refreshStats() {
    var xhr = new XMLHttpRequest();
    xhr.open('GET', 'stats.php?t=' + new Date().getTime(), true);
    xhr.onreadystatechange = function () {
      if (xhr.readyState !== 4) return;
      try {
        var res = JSON.parse(xhr.responseText || '{}');
        var sellBody = $('statsSell').getElementsByTagName('tbody')[0];
        var buyBody = $('statsBuy').getElementsByTagName('tbody')[0];
        sellBody.innerHTML = '';
        buyBody.innerHTML = '';
        var sell = (res.ok && res.sell) ? res.sell : [];
        var buy = (res.ok && res.buy) ? res.buy : [];

        // Build lookup so each popup can show both sell+buy stats for the same item.
        var byLow = {};
        function mergeStat(list, side) {
          for (var i = 0; i < list.length; i++) {
            var r = list[i];
            var id = String(r.lowId || 0);
            var key = id !== '0' ? id : ('n:' + String(r.name || '').toLowerCase());
            if (!byLow[key]) {
              byLow[key] = {
                lowId: r.lowId || 0,
                highId: r.highId || r.lowId || 0,
                name: r.name || '',
                icon: r.icon || 0,
                monthSales: 0,
                monthBuys: 0,
                openSell: 0,
                openBuy: 0
              };
            }
            if (r.name) byLow[key].name = r.name;
            if (r.icon) byLow[key].icon = r.icon;
            if (typeof r.monthSales === 'number') byLow[key].monthSales = r.monthSales;
            if (typeof r.monthBuys === 'number') byLow[key].monthBuys = r.monthBuys;
            if (typeof r.openSell === 'number') byLow[key].openSell = r.openSell;
            if (typeof r.openBuy === 'number') byLow[key].openBuy = r.openBuy;
            if (typeof r.open === 'number' && side === 'sell' && !r.openSell) byLow[key].openSell = r.open;
            if (typeof r.open === 'number' && side === 'buy' && !r.openBuy) byLow[key].openBuy = r.open;
            r._statKey = key;
          }
        }
        mergeStat(sell, 'sell');
        mergeStat(buy, 'buy');

        if (!sell.length) {
          sellBody.innerHTML = '<tr><td colspan="4" class="empty-row">No sell stats yet.</td></tr>';
        } else {
          for (var i = 0; i < sell.length; i++) {
            var sk = sell[i]._statKey || String(sell[i].lowId || 0);
            appendStatsRow(sellBody, byLow[sk] || sell[i], 'monthSales', 'sell');
          }
        }
        if (!buy.length) {
          buyBody.innerHTML = '<tr><td colspan="4" class="empty-row">No buy stats yet.</td></tr>';
        } else {
          for (var j = 0; j < buy.length; j++) {
            var bk = buy[j]._statKey || String(buy[j].lowId || 0);
            appendStatsRow(buyBody, byLow[bk] || buy[j], 'monthBuys', 'buy');
          }
        }
        sizeStatsLists();
      } catch (e) {}
    };
    xhr.send(null);
  }

  function sizeStatsLists() {
    var wraps = [$('statsSellWrap'), $('statsBuyWrap')];
    // Fill most of the Market browser content area (AO embedded window).
    var h = 420;
    try {
      var avail = (document.body && document.body.clientHeight) ? document.body.clientHeight : 0;
      if (avail > 200) {
        h = avail - 90;
        if (h < 260) h = 260;
        if (h > 520) h = 520;
      }
    } catch (e) {}
    for (var i = 0; i < wraps.length; i++) {
      if (!wraps[i]) continue;
      wraps[i].style.height = h + 'px';
      wraps[i].style.maxHeight = h + 'px';
      wraps[i].style.overflow = 'auto';
    }
  }

  function openItemDetail(info, keepPanel) {
    itemDetail = {
      lowId: Number(info.lowId) || 0,
      highId: Number(info.highId || info.lowId) || 0,
      name: info.name || ('Item ' + info.lowId),
      icon: Number(info.icon) || Number(info.lowId) || 0
    };
    if (!keepPanel) showPanel('itemdetail');
    else showPanel('itemdetail');

    $('itemDetailTitle').innerHTML =
      itemIconHtml(itemDetail) + ' &nbsp; ' + itemDetail.name +
      ' <span class="muted">(id ' + itemDetail.lowId + ')</span>';
    setItemMsg('Loading orders…', '');

    var done = 0;
    function finish() {
      done++;
      if (done < 2) return;
      renderItemOrderTables();
      setItemMsg(
        itemSellOrders.length + ' sell / ' + itemBuyOrders.length + ' buy open.',
        'ok');
    }

    var xhrS = new XMLHttpRequest();
    xhrS.open(
      'GET',
      'orders.php?orderType=sell&lowId=' + encodeURIComponent(itemDetail.lowId) +
        '&t=' + new Date().getTime(),
      true);
    xhrS.onreadystatechange = function () {
      if (xhrS.readyState !== 4) return;
      try {
        var res = JSON.parse(xhrS.responseText || '{}');
        itemSellOrders = (res.ok && res.orders) ? res.orders : [];
        selectedItemSellId = itemSellOrders.length ? itemSellOrders[0].id : '';
      } catch (e) {
        itemSellOrders = [];
        selectedItemSellId = '';
      }
      finish();
    };
    xhrS.send(null);

    var xhrB = new XMLHttpRequest();
    xhrB.open(
      'GET',
      'orders.php?orderType=buy&lowId=' + encodeURIComponent(itemDetail.lowId) +
        '&t=' + new Date().getTime(),
      true);
    xhrB.onreadystatechange = function () {
      if (xhrB.readyState !== 4) return;
      try {
        var res2 = JSON.parse(xhrB.responseText || '{}');
        itemBuyOrders = (res2.ok && res2.orders) ? res2.orders : [];
        selectedItemBuyId = itemBuyOrders.length ? itemBuyOrders[0].id : '';
      } catch (e2) {
        itemBuyOrders = [];
        selectedItemBuyId = '';
      }
      finish();
    };
    xhrB.send(null);
  }

  function renderItemOrderTables() {
    var sellBody = $('itemSellOrders').getElementsByTagName('tbody')[0];
    var buyBody = $('itemBuyOrders').getElementsByTagName('tbody')[0];
    sellBody.innerHTML = '';
    buyBody.innerHTML = '';

    if (!itemSellOrders.length) {
      sellBody.innerHTML =
        '<tr><td colspan="5" class="empty-row">No sell orders for this item.</td></tr>';
    } else {
      for (var i = 0; i < itemSellOrders.length; i++) {
        (function (ord) {
          var tr = document.createElement('tr');
          if (selectedItemSellId === ord.id) tr.className = 'selected';
          tr.onclick = function () {
            selectedItemSellId = ord.id;
            renderItemOrderTables();
          };
          tr.innerHTML =
            '<td class="col-icon">' + itemIconHtml(ord) + '</td>' +
            '<td class="price-pos">' + formatCredits(ord.unitPrice || 0) + '</td>' +
            '<td class="col-ql">' + (ord.quality || 0) + '</td>' +
            '<td class="col-count">' + (ord.count || 1) + '</td>' +
            '<td>' + (ord.sellerCharacter || '') + '</td>';
          sellBody.appendChild(tr);
        })(itemSellOrders[i]);
      }
    }

    if (!itemBuyOrders.length) {
      buyBody.innerHTML =
        '<tr><td colspan="6" class="empty-row">No buy orders for this item.</td></tr>';
    } else {
      for (var j = 0; j < itemBuyOrders.length; j++) {
        (function (b) {
          var trb = document.createElement('tr');
          if (selectedItemBuyId === b.id) trb.className = 'selected';
          trb.onclick = function () {
            selectedItemBuyId = b.id;
            renderItemOrderTables();
          };
          trb.innerHTML =
            '<td class="col-icon">' + itemIconHtml(b) + '</td>' +
            '<td class="price-pos">' + formatCredits(b.unitPrice || 0) + '</td>' +
            '<td class="col-ql">' + (b.minQl || 1) + '</td>' +
            '<td class="col-ql">' + (b.maxQl || 200) + '</td>' +
            '<td class="col-count">' + (b.count || 1) + '</td>' +
            '<td>' + (b.buyerCharacter || '') + '</td>';
          buyBody.appendChild(trb);
        })(itemBuyOrders[j]);
      }
    }
  }

  function sellToSelectedBuyOrder() {
    if (!selectedItemBuyId) {
      setItemMsg('Select a buy order first.', 'err');
      return;
    }
    var idx = findVaultIndexForLowId(itemDetail.lowId);
    var q = identityQueryExtra();
    q.push('orderId=' + encodeURIComponent(selectedItemBuyId));
    if (idx >= 0) q.push('index=' + encodeURIComponent(idx));
    q.push('t=' + new Date().getTime());
    var xhr = new XMLHttpRequest();
    xhr.open('GET', 'fulfillbuy.php?' + q.join('&'), true);
    xhr.onreadystatechange = function () {
      if (xhr.readyState !== 4) return;
      try {
        var res = JSON.parse(xhr.responseText || '{}');
        if (res.ok) {
          showNotice(res.notice || 'sold to buy order');
          setItemMsg(res.notice || 'Sold at buyer price.', 'ok');
          if (typeof res.credits === 'number' || res.items) {
            renderVault({
              credits: (typeof res.credits === 'number') ? res.credits : vaultData.credits,
              items: res.items || vaultData.items,
              character: res.character || vaultData.character,
              characterId: res.characterId || vaultData.characterId
            });
          }
          selectedItemBuyId = '';
          openItemDetail(itemDetail, true);
          refreshStats();
          refreshMyOrders();
        } else {
          setItemMsg(res.error || 'Sell to buy order failed.', 'err');
        }
      } catch (e) {
        setItemMsg('fulfillbuy.php error: ' + e, 'err');
      }
    };
    xhr.send(null);
  }

  function postBuyOrder(price, count, minQl, maxQl) {
    var q = identityQueryExtra();
    q.push('lowId=' + encodeURIComponent(itemDetail.lowId));
    q.push('highId=' + encodeURIComponent(itemDetail.highId || itemDetail.lowId));
    q.push('name=' + encodeURIComponent(itemDetail.name || ''));
    q.push('icon=' + encodeURIComponent(itemDetail.icon || 0));
    q.push('price=' + encodeURIComponent(price));
    q.push('count=' + encodeURIComponent(count));
    q.push('minQl=' + encodeURIComponent(minQl));
    q.push('maxQl=' + encodeURIComponent(maxQl));
    q.push('t=' + new Date().getTime());
    var xhr = new XMLHttpRequest();
    xhr.open('GET', 'buyorder.php?' + q.join('&'), true);
    xhr.onreadystatechange = function () {
      if (xhr.readyState !== 4) return;
      try {
        var res = JSON.parse(xhr.responseText || '{}');
        if (res.ok) {
          showNotice(res.notice || 'buy order placed');
          setItemMsg(res.notice || 'Buy order placed.', 'ok');
          if (typeof res.credits === 'number') {
            renderVault({
              credits: res.credits,
              items: res.items || vaultData.items,
              character: vaultData.character,
              characterId: vaultData.characterId
            });
          }
          openItemDetail(itemDetail, true);
          refreshStats();
          refreshMyOrders();
        } else {
          setItemMsg(res.error || 'Buy order failed.', 'err');
        }
      } catch (e) {
        setItemMsg('buyorder.php error: ' + e, 'err');
      }
    };
    xhr.send(null);
  }

  function wireTabs() {
    var tabs = document.getElementsByClassName('tab');
    for (var i = 0; i < tabs.length; i++) {
      tabs[i].onclick = function (ev) {
        if (ev && ev.preventDefault) ev.preventDefault();
        var name = this.getAttribute('data-tab');
        showPanel(name);
        if (name === 'inventory') {
          refreshVault(true);
          applyInvListHeight();
        } else if (name === 'orders') {
          refreshMyOrders();
        } else if (name === 'search') {
          clearSearchResults();
          sizeSearchList();
        } else if (name === 'statistics') {
          refreshStats();
          sizeStatsLists();
        } else if (name === 'log') {
          refreshLog();
        }
        return false;
      };
    }
  }

  function closeModals() {
    $('modalCredits').style.display = 'none';
    $('modalItem').style.display = 'none';
    if ($('modalSell')) $('modalSell').style.display = 'none';
    if ($('modalBuyOrder')) $('modalBuyOrder').style.display = 'none';
    if ($('modalBuyQty')) $('modalBuyQty').style.display = 'none';
    if ($('modalItemInfo')) $('modalItemInfo').style.display = 'none';
  }

  window.onload = function () {
    applyInvListHeight();
    wireTabs();

    $('btnDeposit').onclick = function (ev) {
      if (ev && ev.preventDefault) ev.preventDefault();
      openDepositWindow();
      return false;
    };
    $('btnRefresh').onclick = function (ev) {
      if (ev && ev.preventDefault) ev.preventDefault();
      applyInvListHeight();
      refreshVault(false);
      return false;
    };
    $('invFilter').onkeyup = function () { renderVault(vaultData); };
    $('btnSearch').onclick = function (ev) {
      if (ev && ev.preventDefault) ev.preventDefault();
      runSearch();
      return false;
    };
    if ($('btnBuyQtyCancel')) {
      $('btnBuyQtyCancel').onclick = function (ev) {
        if (ev && ev.preventDefault) ev.preventDefault();
        pendingBuyOrder = null;
        closeModals();
        return false;
      };
    }
    if ($('btnBuyQtySend')) {
      $('btnBuyQtySend').onclick = function (ev) {
        if (ev && ev.preventDefault) ev.preventDefault();
        confirmBuyQty();
        return false;
      };
    }
    if ($('buyQtyCount')) {
      $('buyQtyCount').onkeyup = function () { updateBuyQtyTotal(); };
      $('buyQtyCount').onchange = function () { updateBuyQtyTotal(); };
    }
    if ($('btnCancelSell')) {
      $('btnCancelSell').onclick = function (ev) {
        if (ev && ev.preventDefault) ev.preventDefault();
        cancelSelectedSell();
        return false;
      };
    }
    if ($('btnCancelBuy')) {
      $('btnCancelBuy').onclick = function (ev) {
        if (ev && ev.preventDefault) ev.preventDefault();
        cancelSelectedBuy();
        return false;
      };
    }

    $('btnWithdrawCredits').onclick = function (ev) {
      if (ev && ev.preventDefault) ev.preventDefault();
      $('modalCreditsBalance').textContent = formatCredits(vaultData.credits || 0);
      $('withdrawAmount').value = '0';
      $('modalCredits').style.display = 'block';
      return false;
    };
    $('btnCreditsCancel').onclick = function (ev) {
      if (ev && ev.preventDefault) ev.preventDefault();
      closeModals();
      return false;
    };
    $('btnCreditsSend').onclick = function (ev) {
      if (ev && ev.preventDefault) ev.preventDefault();
      var amount = parseInt($('withdrawAmount').value, 10) || 0;
      closeModals();
      queueWithdraw(
        { kind: 'credits', amount: amount },
        'you withdrew ' + formatCredits(amount) + ' credits');
      return false;
    };

    $('btnWithdrawItem').onclick = function (ev) {
      if (ev && ev.preventDefault) ev.preventDefault();
      if (selectedIndex < 0 || !vaultData.items || !vaultData.items[selectedIndex]) {
        setMsg('Select an item with the radio button first.', 'err');
        return false;
      }
      var it = vaultData.items[selectedIndex];
      var max = it.count || it.Count || 1;
      $('modalItemIcon').innerHTML = itemIconHtml(it);
      $('modalItemQl').textContent = String(it.quality || it.Quality || 0);
      $('modalItemName').textContent = itemName(it);
      $('withdrawItemCount').value = String(max);
      $('modalItemMax').textContent = '(max ' + max + ')';
      $('modalItem').style.display = 'block';
      return false;
    };
    $('btnItemCancel').onclick = function (ev) {
      if (ev && ev.preventDefault) ev.preventDefault();
      closeModals();
      return false;
    };
    $('btnItemSend').onclick = function (ev) {
      if (ev && ev.preventDefault) ev.preventDefault();
      var count = parseInt($('withdrawItemCount').value, 10) || 1;
      var idx = selectedIndex;
      closeModals();
      queueWithdraw(
        { kind: 'item', index: idx, count: count },
        'you withdrew item');
      return false;
    };

    if ($('btnSellItem')) {
      $('btnSellItem').onclick = function (ev) {
        if (ev && ev.preventDefault) ev.preventDefault();
        openSellModalForIndex(selectedIndex);
        return false;
      };
    }
    if ($('btnSellCancel')) {
      $('btnSellCancel').onclick = function (ev) {
        if (ev && ev.preventDefault) ev.preventDefault();
        closeModals();
        return false;
      };
    }
    if ($('btnSellSend')) {
      $('btnSellSend').onclick = function (ev) {
        if (ev && ev.preventDefault) ev.preventDefault();
        var price = parseInt($('sellPrice').value, 10) || 0;
        var count = parseInt($('sellCount').value, 10) || 1;
        var idx = selectedIndex;
        closeModals();
        if (price <= 0) {
          setMsg('Enter a positive sell price.', 'err');
          setItemMsg('Enter a positive sell price.', 'err');
          return false;
        }
        postSell(idx, price, count);
        return false;
      };
    }

    if ($('btnItemDetailBack')) {
      $('btnItemDetailBack').onclick = function (ev) {
        if (ev && ev.preventDefault) ev.preventDefault();
        showPanel('statistics');
        refreshStats();
        return false;
      };
    }
    if ($('btnCreateSell')) {
      $('btnCreateSell').onclick = function (ev) {
        if (ev && ev.preventDefault) ev.preventDefault();
        refreshVault(true);
        var idx = findVaultIndexForLowId(itemDetail.lowId);
        if (idx < 0) {
          setItemMsg(
            'No matching item in your Market Inventory. Deposit it first, then Create Sell Order.',
            'err');
          return false;
        }
        openSellModalForIndex(idx);
        return false;
      };
    }
    if ($('btnBuyFromItem')) {
      $('btnBuyFromItem').onclick = function (ev) {
        if (ev && ev.preventDefault) ev.preventDefault();
        selectedBuyOrderId = selectedItemSellId;
        buySelected();
        return false;
      };
    }
    if ($('btnCreateBuy')) {
      $('btnCreateBuy').onclick = function (ev) {
        if (ev && ev.preventDefault) ev.preventDefault();
        $('modalBuyName').textContent = itemDetail.name || ('Item ' + itemDetail.lowId);
        $('buyOrderPrice').value = '0';
        $('buyOrderCount').value = '1';
        $('buyOrderMinQl').value = '1';
        $('buyOrderMaxQl').value = '200';
        $('modalBuyOrder').style.display = 'block';
        return false;
      };
    }
    if ($('btnSellToBuy')) {
      $('btnSellToBuy').onclick = function (ev) {
        if (ev && ev.preventDefault) ev.preventDefault();
        sellToSelectedBuyOrder();
        return false;
      };
    }
    if ($('btnBuyOrderCancel')) {
      $('btnBuyOrderCancel').onclick = function (ev) {
        if (ev && ev.preventDefault) ev.preventDefault();
        closeModals();
        return false;
      };
    }
    if ($('btnBuyOrderSend')) {
      $('btnBuyOrderSend').onclick = function (ev) {
        if (ev && ev.preventDefault) ev.preventDefault();
        var price = parseInt($('buyOrderPrice').value, 10) || 0;
        var count = parseInt($('buyOrderCount').value, 10) || 1;
        var minQl = parseInt($('buyOrderMinQl').value, 10) || 1;
        var maxQl = parseInt($('buyOrderMaxQl').value, 10) || 200;
        closeModals();
        if (price <= 0) {
          setItemMsg('Enter a positive unit price.', 'err');
          return false;
        }
        postBuyOrder(price, count, minQl, maxQl);
        return false;
      };
    }

    refreshVault(true);
  };
})();
