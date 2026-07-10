Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat') | Out-Null
foreach ($prof in @(3,8,12)) {
  Write-Output "--- profession $prof ---"
  $count = 0
  foreach ($item in [AORebirth.Core.Items.ItemLoader]::ItemList.Values) {
    $hasSummon = $false
    foreach ($ev in $item.Events) {
      foreach ($fn in $ev.Functions) { if ($fn.FunctionType -eq 53167) { $hasSummon = $true } }
    }
    if (-not $hasSummon) { continue }
    $profReq = $null
    foreach ($act in $item.Actions) {
      foreach ($req in $act.Requirements) {
        if ($req.Statnumber -eq 60) { $profReq = $req.Value }
      }
    }
    if ($profReq -ne $prof) { continue }
    if ($item.Quality -gt 50) { continue }
    Write-Output "item=$($item.ID) ql=$($item.Quality)"
    $count++
    if ($count -ge 5) { break }
  }
}
