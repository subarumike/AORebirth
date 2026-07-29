<!DOCTYPE html>
<html lang="en">
<head>
  <base href="/daily/">
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <meta http-equiv="Cache-Control" content="no-cache, no-store, must-revalidate">
  <meta http-equiv="Pragma" content="no-cache">
  <meta http-equiv="Expires" content="0">
  <title>Daily Login Rewards</title>
  <link rel="stylesheet" href="css/icc.css" />
</head>
<body class="page-daily">
  <div class="chrome">
    <div class="chrome-title">Daily Login Rewards</div>
    <div class="address-bar">vgtp://uwg.daily.icc-rk/index.app</div>
  </div>

  <main class="panel daily-panel">
    <header class="panel-head">
      <h1>Daily Login Rewards</h1>
      <p class="sub">Local AO Rebirth stub — claim today’s gift on this host.</p>
    </header>

    <div class="reward-grid" id="rewardGrid"></div>

    <div class="actions">
      <button type="button" id="claimBtn" class="btn primary">Claim Today</button>
      <span id="claimStatus" class="status" aria-live="polite"></span>
    </div>
  </main>

  <script src="js/daily.js"></script>
</body>
</html>
