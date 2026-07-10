Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat') | Out-Null
foreach ($id in @(43328, 46083, 96196, 46343, 46344)) {
  if (-not [AORebirth.Core.Items.ItemLoader]::ItemList.ContainsKey($id)) { Write-Output "missing $id"; continue }
  $t = [AORebirth.Core.Items.ItemLoader]::ItemList[$id]
  $item = New-Object AORebirth.Core.Items.Item(1, $id, $id)
  Write-Output "template=$id ql=$($t.Quality) itemLow=$($item.LowID) itemHigh=$($item.HighID) itemQl=$($item.Quality)"
}
