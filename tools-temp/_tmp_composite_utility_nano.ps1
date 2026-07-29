Add-Type -Path 'AORebirth\Built\Debug\AORebirth.Core.dll'
[AORebirth.Core.Nanos.NanoLoader]::CacheAllNanos('AORebirth\Built\Debug\nanos.dat') | Out-Null
foreach ($id in @(287047,287046,287045,223372,223373,220343,220344)) {
  if (-not [AORebirth.Core.Nanos.NanoLoader]::NanoList.ContainsKey($id)) {
    Write-Output "MISSING $id"
    continue
  }
  $n = [AORebirth.Core.Nanos.NanoLoader]::NanoList[$id]
  Write-Output ("=== nano {0} NCU={1} dur8={2} range287={3} strain={4} ===" -f $id, $n.getItemAttribute(11), $n.getItemAttribute(8), $n.getItemAttribute(287), $n.NanoStrain())
  foreach ($ev in $n.Events) {
    foreach ($fn in $ev.Functions) {
      $args = ($fn.Arguments.Values | ForEach-Object { $_.ToString() }) -join ','
      Write-Output (" event={0} fn={1}({2}) target={3} args=[{4}]" -f $ev.EventType, [int]$fn.FunctionType, $fn.FunctionType, $fn.Target, $args)
    }
  }
}
