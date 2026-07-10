Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat')
foreach ($itemId in @(21601, 21605, 43328, 46083, 96196)) {
  $item = [AORebirth.Core.Items.ItemLoader]::ItemList[$itemId]
  Write-Output "=== $itemId ql=$($item.Quality) type=$($item.ItemType) events=$($item.Events.Count) ==="
  Write-Output " TurnsOnUse=$($item.TurnsOnUse()) ConfirmOnUse=$($item.ConfirmOnUse()) IsConsumable=$($item.IsConsumable())"
  foreach ($ev in $item.Events) {
    foreach ($fn in $ev.Functions) {
      $args = ($fn.Arguments.Values | ForEach-Object { $_.ToString() }) -join ','
      Write-Output "  event=$($ev.EventType) fn=$($fn.FunctionType) args=$args"
    }
  }
}
