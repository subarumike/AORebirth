Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat')
foreach ($id in @(53167, 53051, 53050)) {
  $hits = 0
  foreach ($item in [AORebirth.Core.Items.ItemLoader]::ItemList.Values) {
    foreach ($ev in $item.Events) {
      if ($ev.EventType -ne [AORebirth.Enums.EventType]::OnUse) { continue }
      foreach ($fn in $ev.Functions) {
        if ($fn.FunctionType -eq $id) {
          Write-Output "item=$($item.ID) fn=$id name=$($item.Name)"
          $hits++
          if ($hits -ge 5) { break }
        }
      }
      if ($hits -ge 5) { break }
    }
    if ($hits -ge 5) { break }
  }
}
