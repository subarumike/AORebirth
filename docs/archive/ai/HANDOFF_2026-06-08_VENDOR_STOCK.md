
We are working on a local Anarchy Online server using CellAO.

Repo:
C:\Users\Mike\Documents\Cellao-Clean

AO client:
C:\Funcom\Anarchy Online

Database:
Use ONLY `cellao_codex_clean`.
Do NOT touch any other MySQL database.
Do NOT change schemas or wipe data unless Mike explicitly approves.

Important workflow rule:
After code changes that affect server binaries:
1. Kill engines.
2. Build binaries.
3. Restart Chat/Login/Zone.
4. Do not start WebEngine unless explicitly needed.

Stop engines:
Get-Process ChatEngine,LoginEngine,ZoneEngine,WebEngine,MSBuild,VBCSCompiler -ErrorAction SilentlyContinue | Stop-Process -Force

Build:
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' 'CellAO\CellAO.sln' /t:Build /p:Configuration=Debug /m

Start engines, no WebEngine:
$built='C:\Users\Mike\Documents\Cellao-Clean\CellAO\Built\Debug'
Start-Process -FilePath (Join-Path $built 'ChatEngine.exe') -WorkingDirectory $built -WindowStyle Hidden
Start-Sleep 1
Start-Process -FilePath (Join-Path $built 'LoginEngine.exe') -WorkingDirectory $built -WindowStyle Hidden
Start-Sleep 1
Start-Process -FilePath (Join-Path $built 'ZoneEngine.exe') -WorkingDirectory $built -WindowStyle Hidden

Mike’s testing workflow:
- Codex runs tools/commands/servers.
- Mike does live client playtesting.
- Do not ask Mike to run commands unless absolutely necessary.
- For live packet/data truth, ALWAYS use AOSharp capture tools. Do not guess.

Current branch/state:
Branch: master
Git tree was clean after latest local commit.
Latest local commits:
- 5452a22 Align superior ICC weapon shop stock
- 8727965 Add current-client ICC shop data tooling
- 41dab1e Add current-client shop data verification

Important: `8727965` was pushed to origin/master. `5452a22` was committed locally after that; check whether it has been pushed before assuming remote is current.

Most recent completed repair:
Superior ICC Weapons shop stock was aligned from live AOSharp capture.

Live capture session used:
C:\Users\Mike\Documents\Cellao-Clean\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260607-202610

Live evidence:
- Player: Youwillmezz
- Computer Literacy captured as 2015
- Superior ICC Weapons terminal: `(VendingMachine:12E22C4B)`
- VendingMachineFullUpdate static/template identity: `297432`
- ShopUpdate rows: 150 slots
- The live full-update did NOT carry usable BuyModifier/SellModifier stats. They logged as 0 because those stats are not sent in that packet.

Repair applied:
- Replaced local `WpS` Superior ICC Weapons stock with the exact 150 rows from live terminal `(VendingMachine:12E22C4B)`.
- Updated source SQL:
  C:\Users\Mike\Documents\Cellao-Clean\CellAO\Libraries\Source\CellAO.Database\SqlTables\shopinventorytemplates.sql
- Updated live evidence CSV:
  C:\Users\Mike\Documents\Cellao-Clean\tools-temp\current-client-data-verification\live-icc-accessory-weapon-stock.csv
- Regenerated shop audit:
  C:\Users\Mike\Documents\Cellao-Clean\tools-temp\current-client-data-verification\shop-inventory-template-audit.csv
- Regenerated report timestamp:
  C:\Users\Mike\Documents\Cellao-Clean\docs\reports\current-client-data-verification.md
- Updated runtime DB rows in `cellao_codex_clean` only.

Verification after repair:
- WpS live rows: 150
- WpS source SQL rows: 150
- WpS DB rows: 150
- mismatches: 0
- `git diff --check`: clean
- verification script ran:
  C:\Users\Mike\Documents\Cellao-Clean\tools-temp\current-client-data-verification\Invoke-CurrentClientDataVerification.ps1
- Audit still reports known broader issues:
  - VendorDbIssues: 4
  - ShopInventoryIssues: 1
  - StatelVendorIssues: 832
  These are not from the WpS repair.

Current engines:
ChatEngine/LoginEngine/ZoneEngine were running after the repair.
WebEngine was not started.

AOSharp live capture tooling:
- Capture plugin:
  C:\Users\Mike\Documents\Cellao-Clean\tools-temp\AOSharpLiveCapture
- Injector:
  C:\Users\Mike\Documents\Cellao-Clean\tools-temp\AOSharpLiveInjector
- Use AOSharp tooling for live evidence. Do not tell Mike to type `/aocap` commands unless the workflow has changed; we inject externally and read the generated files.

Recent injector pattern:
$aoPid = (Get-Process AnarchyOnline | Where-Object { $_.MainWindowTitle -like '*Youwillmezz*' } | Select-Object -First 1 -ExpandProperty Id)
$injector = 'C:\Users\Mike\Documents\Cellao-Clean\tools-temp\AOSharpLiveInjector\bin\Debug\AOSharpLiveInjector.exe'
$plugin = 'C:\Users\Mike\Documents\Cellao-Clean\tools-temp\AOSharpLiveCapture\bin\Debug\AOSharpLiveCapture.dll'
$log = "C:\Users\Mike\Documents\Cellao-Clean\tools-temp\AOSharpLiveInjector\bin\Debug\AOSharpLiveInjector-vendor-pricing-$aoPid.log"
Start-Process -FilePath $injector -ArgumentList @('--pid', $aoPid, '--plugin', $plugin, '--log', $log) -WorkingDirectory (Split-Path $injector) -WindowStyle Hidden

Do not use `$pid` as a PowerShell variable; it is reserved. Use `$aoPid`.

Important earlier solved systems:
- Sit/stand fixed. Do not undo.
- Equipment visuals fixed: weapon appears in hand, fires correctly, persists through logout.
- Armor visuals and equipment slot updates work.
- Implants work with surgery clinic and Treatment lockout.
- Shop X/ESC close works.
- Shop buy/sell works in both directions after prior repairs.
- Credit loot message fixed: now displays correct corpse credits.
- Death/respawn white screen fixed.
- Fair Trade/Borealis/Newland statel mapping improved, but broader statel/vendor cleanup remains.

Current likely next test:
Have Mike log into local, open Superior ICC Weapons, and compare the first visible rows/prices against the latest live capture. Since WpS stock now matches the latest captured live ShopUpdate, remaining mismatch would likely be pricing formula/modifier rather than stock contents.

Important pricing evidence:
AOSharp source has:
C:\Users\Mike\Documents\Cellao-Clean\tools-temp\external\aosharp\AOSharp.Common\Math\AOMath.cs

`SellPrice(int value, int compLit, float shopModifier = 4f)`:
value * shopModifier * (100 + compLit / 40) / 2500

AOSharp Item.cs uses:
vendor.GetStat(Stat.SellModifier) / 1000

CellAO current server pricing code is in:
C:\Users\Mike\Documents\Cellao-Clean\CellAO\Server\ZoneEngine\Core\MessageHandlers\TradeMessageHandler.cs

Do not change pricing from theory alone. First compare local UI after WpS stock alignment, then capture exact live buy/sell behavior if still wrong.

Current-client shop/data audit files:
- C:\Users\Mike\Documents\Cellao-Clean\docs\reports\current-client-data-verification.md
- C:\Users\Mike\Documents\Cellao-Clean\tools-temp\current-client-data-verification\data-file-version-audit.csv
- C:\Users\Mike\Documents\Cellao-Clean\tools-temp\current-client-data-verification\vendor-db-audit.csv
- C:\Users\Mike\Documents\Cellao-Clean\tools-temp\current-client-data-verification\shop-inventory-template-audit.csv
- C:\Users\Mike\Documents\Cellao-Clean\tools-temp\current-client-data-verification\statel-vendor-coverage.csv
- C:\Users\Mike\Documents\Cellao-Clean\tools-temp\current-client-data-verification\live-icc-accessory-weapon-stock.csv

Standing rule from Mike:
If we do not know, capture live data with AOSharp. Do not guess.
```
