Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat') | Out-Null
foreach ($item in [AORebirth.Core.Items.ItemLoader]::ItemList.Values) {
  $hasSummon = $false
  foreach ($ev in $item.Events) {
    foreach ($fn in $ev.Functions) {
      if ($fn.FunctionType -eq 53167) { $hasSummon = $true; break }
    }
    if ($hasSummon) { break }
  }
  if (-not $hasSummon) { continue }
  $profReq = $null
  $mcReq = $null
  foreach ($act in $item.Actions) {
    foreach ($req in $act.Requirements) {
      if ($req.Statnumber -eq 60) { $profReq = $req.Value }
      if ($req.Statnumber -eq 130) { $mcReq = $req.Value }
    }
  }
  if ($profReq -eq 8) {
    Write-Output "bureau item=$($item.ID) ql=$($item.Quality) mcReq=$mcReq"
  }
}
