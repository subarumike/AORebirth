Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat') | Out-Null
[AORebirth.Core.Nanos.NanoLoader]::CacheAllNanos('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\nanos.dat') | Out-Null
foreach ($itemId in @(43328, 96196)) {
  $item = [AORebirth.Core.Items.ItemLoader]::ItemList[$itemId]
  Write-Output "=== item $itemId actions=$($item.Actions.Count) ==="
  foreach ($act in $item.Actions) {
    Write-Output " action type=$($act.ActionType) reqs=$($act.Requirements.Count)"
    foreach ($req in $act.Requirements) {
      Write-Output "  req type=$($req.RequirementType) stat=$($req.Stat) val=$($req.Value)"
    }
  }
}
$n = [AORebirth.Core.Nanos.NanoLoader]::NanoList[43718]
Write-Output "=== nano 43718 ==="
foreach ($ev in $n.Events) {
  foreach ($fn in $ev.Functions) {
    Write-Output " fn=$($fn.FunctionType) reqs=$($fn.Requirements.Count)"
    foreach ($req in $fn.Requirements) {
      Write-Output "  req type=$($req.RequirementType) stat=$($req.Stat) val=$($req.Value)"
    }
  }
}
