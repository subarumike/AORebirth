Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat')
$count = 0
foreach ($item in [AORebirth.Core.Items.ItemLoader]::ItemList.Values) {
  if ($item.ItemType -ne 0) { continue }
  if ($item.Quality -ne 1) { continue }
  if ($item.Events.Count -ne 0) { continue }
  Write-Output "misc item=$($item.ID)"
  $count++
  if ($count -ge 15) { break }
}
