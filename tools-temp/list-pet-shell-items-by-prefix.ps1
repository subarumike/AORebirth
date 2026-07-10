Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat')
foreach ($prefix in @('PT50', 'PT51', 'PT52')) {
  Write-Output "--- $prefix ---"
  $count = 0
  foreach ($item in [AORebirth.Core.Items.ItemLoader]::ItemList.Values) {
    foreach ($ev in $item.Events) {
      foreach ($fn in $ev.Functions) {
        if ($fn.FunctionType -ne 53167) { continue }
        $arg0 = $fn.Arguments.Values[0].ToString()
        if ($arg0 -like "$prefix*") {
          $args = ($fn.Arguments.Values | ForEach-Object { $_.ToString() }) -join ','
          Write-Output "item=$($item.ID) ql=$($item.Quality) event=$($ev.EventType) args=$args"
          $count++
        }
      }
    }
  }
  Write-Output "count=$count"
}
