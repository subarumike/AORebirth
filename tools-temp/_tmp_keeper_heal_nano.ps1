Add-Type -Path 'AORebirth\Built\Debug\AORebirth.Core.dll'
[AORebirth.Core.Nanos.NanoLoader]::CacheAllNanos('AORebirth\Built\Debug\nanos.dat') | Out-Null
foreach ($id in @(300495,302365)) {
  if (-not [AORebirth.Core.Nanos.NanoLoader]::NanoList.ContainsKey($id)) {
    Write-Output "MISSING $id"
    continue
  }
  $n = [AORebirth.Core.Nanos.NanoLoader]::NanoList[$id]
  Write-Output ("=== {0} strain={1} ncu={2} dur={3} ===" -f $id, $n.NanoStrain(), $n.getItemAttribute(11), $n.getItemAttribute(8))
  foreach ($ev in $n.Events) {
    foreach ($fn in $ev.Functions) {
      $args = ($fn.Arguments.Values | ForEach-Object { $_.ToString() }) -join ','
      Write-Output (" event={0} fn={1}({2}) target={3} args=[{4}]" -f $ev.EventType, [int]$fn.FunctionType, $fn.FunctionType, $fn.Target, $args)
    }
  }
}
