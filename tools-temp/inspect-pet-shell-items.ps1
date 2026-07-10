Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat')
foreach ($itemId in @(43328, 46083, 46343, 46344, 96196, 43553)) {
  $item = [AORebirth.Core.Items.ItemLoader]::ItemList[$itemId]
  if ($null -eq $item) { Write-Output "missing $itemId"; continue }
  Write-Output "=== item $itemId ql=$($item.Quality) type=$($item.ItemType) ==="
  foreach ($ev in $item.Events) {
    foreach ($fn in $ev.Functions) {
      $args = ($fn.Arguments.Values | ForEach-Object { $_.ToString() }) -join ','
      Write-Output " event=$($ev.EventType) fn=$($fn.FunctionType) args=$args"
    }
  }
}
