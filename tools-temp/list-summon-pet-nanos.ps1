Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
[AORebirth.Core.Nanos.NanoLoader]::CacheAllNanos('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\nanos.dat')
$count = 0
foreach ($n in [AORebirth.Core.Nanos.NanoLoader]::NanoList.Values) {
  foreach ($ev in $n.Events) {
    foreach ($fn in $ev.Functions) {
      if ($fn.FunctionType -eq 53167 -or $fn.FunctionType -eq 53181) {
        $args = ($fn.Arguments.Values | ForEach-Object { $_.ToString() }) -join ','
        Write-Output "nano=$($n.ID) event=$($ev.EventType) fn=$($fn.FunctionType) args=$args"
        $count++
      }
      if ($fn.FunctionType -eq 53181) {
        $args = ($fn.Arguments.Values | ForEach-Object { $_.ToString() }) -join ','
        Write-Output "summonpets nano=$($n.ID) args=$args"
      }
    }
  }
}
Write-Output "total=$count"
