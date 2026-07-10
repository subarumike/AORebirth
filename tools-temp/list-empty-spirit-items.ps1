Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat')
$count = 0
foreach ($item in [AORebirth.Core.Items.ItemLoader]::ItemList.Values) {
  if ($item.ItemType -ne 5) { continue }
  if ($item.Events.Count -gt 0) { continue }
  if ($item.Quality -ne 1) { continue }
  Write-Output "spirit item=$($item.ID)"
  $count++
  if ($count -ge 10) { break }
}
