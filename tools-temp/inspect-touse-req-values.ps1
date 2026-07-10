Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat') | Out-Null
foreach ($itemId in @(43328, 96196, 46343, 46344, 46083)) {
  $item = [AORebirth.Core.Items.ItemLoader]::ItemList[$itemId]
  $reqVals = @($item.Actions | ForEach-Object { $_.Requirements | ForEach-Object { $_.Value } }) -join ','
  Write-Output "item=$itemId ql=$($item.Quality) ToUseReqVals=$reqVals"
}
