Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat')
$ids = @(43324,43715,43718,43723,43729,29318)
foreach ($id in $ids) {
  if ([AORebirth.Core.Items.ItemLoader]::ItemList.ContainsKey($id)) {
    $item = [AORebirth.Core.Items.ItemLoader]::ItemList[$id]
    Write-Output "id=$id name=$($item.Name)"
  } else {
    Write-Output "id=$id missing"
  }
}
