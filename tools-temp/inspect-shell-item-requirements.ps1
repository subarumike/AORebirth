Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat') | Out-Null
foreach ($itemId in @(43328, 96196, 46083, 46344)) {
  $item = [AORebirth.Core.Items.ItemLoader]::ItemList[$itemId]
  Write-Output "=== item $itemId ql=$($item.Quality) ==="
  foreach ($ev in $item.Events) {
    Write-Output " event=$($ev.EventType)"
    foreach ($fn in $ev.Functions) {
      $args = ($fn.Arguments.Values | ForEach-Object { $_.ToString() }) -join ','
      Write-Output "  fn=$($fn.FunctionType) args=$args reqs=$($fn.Requirements.Count)"
      foreach ($req in $fn.Requirements) {
        Write-Output "    req type=$($req.RequirementType) stat=$($req.Stat) op=$($req.Operator) val=$($req.Value)"
      }
    }
  }
}
