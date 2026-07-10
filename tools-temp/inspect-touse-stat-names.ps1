Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat') | Out-Null
foreach ($itemId in @(43328, 96196, 46344)) {
  $item = [AORebirth.Core.Items.ItemLoader]::ItemList[$itemId]
  Write-Output "=== item $itemId ==="
  foreach ($act in $item.Actions) {
    foreach ($req in $act.Requirements) {
      $name = [AORebirth.Stats.StatNamesDefaults]::GetStatName($req.Statnumber)
      Write-Output "  ToUse req target=$($req.Target) stat=$($req.Statnumber) ($name) op=$($req.Operator) val=$($req.Value)"
    }
  }
}
