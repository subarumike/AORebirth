<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Item Store</title>
  <link rel="stylesheet" href="css/icc.css" />
</head>
<body class="page-store">
  <div class="chrome">
    <div class="chrome-title">Item Store</div>
    <div class="address-bar">vgtp://uwg.store.icc-rk/index.app</div>
  </div>

  <main class="panel store-panel">
    <header class="store-head">
      <div class="char">
        <div class="level">Level 1</div>
        <div class="name" id="charName">Getkeep</div>
        <div class="head-btns">
          <button type="button" class="btn small" id="claimItemsBtn">Claim Items</button>
          <button type="button" class="btn small ghost">Membership</button>
        </div>
      </div>
      <div class="funds">
        <span class="fp" title="Funcom Points">0 + 600</span>
        <button type="button" class="btn small accent">ADD [F]</button>
      </div>
    </header>

    <nav class="tabs" id="storeTabs">
      <button type="button" class="tab active" data-tab="convenience">Convenience</button>
      <button type="button" class="tab" data-tab="special">Special</button>
      <button type="button" class="tab" data-tab="nanos">Nanos</button>
      <button type="button" class="tab" data-tab="vehicles">Vehicles</button>
    </nav>

    <div class="store-grid" id="storeGrid"></div>
    <p id="storeStatus" class="status" aria-live="polite"></p>
  </main>

  <script src="js/store.js"></script>
</body>
</html>
