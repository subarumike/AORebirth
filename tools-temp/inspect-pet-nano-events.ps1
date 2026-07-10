Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
[AORebirth.Core.Nanos.NanoLoader]::CacheAllNanos('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\nanos.dat') | Out-Null
foreach ($id in @(43324, 43718)) {
  $n = [AORebirth.Core.Nanos.NanoLoader]::NanoList[$id]
  Write-Output "=== nano $id ==="
  foreach ($ev in $n.Events) {
    foreach ($fn in $ev.Functions) {
      $args = ($fn.Arguments.Values | ForEach-Object { $_.ToString() }) -join ','
      Write-Output " event=$($ev.EventType) fn=$($fn.FunctionType) args=$args"
    }
  }
}
