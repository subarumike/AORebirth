Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat') | Out-Null
foreach ($itemId in @(204709, 96235, 43328)) {
  $item = [AORebirth.Core.Items.ItemLoader]::ItemList[$itemId]
  Write-Output "=== $itemId ql=$($item.Quality) ==="
  foreach ($ev in $item.Events) {
    foreach ($fn in $ev.Functions) {
      $args = ($fn.Arguments.Values | ForEach-Object { $_.ToString() }) -join ','
      Write-Output " fn=$($fn.FunctionType) args=$args"
    }
  }
  foreach ($act in $item.Actions) {
    foreach ($req in $act.Requirements) {
      if ($req.Statnumber -eq 60) { Write-Output " profReq=$($req.Value)" }
    }
  }
}
