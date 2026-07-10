Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat') | Out-Null
foreach ($item in [AORebirth.Core.Items.ItemLoader]::ItemList.Values) {
  foreach ($ev in $item.Events) {
    if ($ev.EventType -ne [AORebirth.Enums.EventType]::OnUse) { continue }
    foreach ($fn in $ev.Functions) {
      if ($fn.FunctionType -ne 53167) { continue }
      $arg0 = $fn.Arguments.Values[0].ToString()
      if ($arg0 -like 'PT02*' -or $arg0 -like 'PT01*') {
        if ($item.Quality -le 50) {
          Write-Output "item=$($item.ID) ql=$($item.Quality) args=$arg0"
        }
      }
    }
  }
}
