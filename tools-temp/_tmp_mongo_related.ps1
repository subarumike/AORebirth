Add-Type -Path 'AORebirth\Built\Debug\AORebirth.Core.dll'
[AORebirth.Core.Nanos.NanoLoader]::CacheAllNanos('AORebirth\Built\Debug\nanos.dat') | Out-Null

foreach ($id in @(26397,26398,100198,100197,100193,100192,100191,100190)) {
  if (-not [AORebirth.Core.Nanos.NanoLoader]::NanoList.ContainsKey($id)) {
    Write-Output "MISSING $id"
    continue
  }
  $n = [AORebirth.Core.Nanos.NanoLoader]::NanoList[$id]
  Write-Output ("=== nano {0} NCU={1} dur8={2} range287={3} strain={4} atr210={5} ===" -f $id, $n.getItemAttribute(11), $n.getItemAttribute(8), $n.getItemAttribute(287), $n.NanoStrain(), $n.getItemAttribute(210))
  foreach ($ev in $n.Events) {
    foreach ($fn in $ev.Functions) {
      $args = ($fn.Arguments.Values | ForEach-Object { $_.ToString() }) -join ','
      Write-Output (" event={0} fn={1}({2}) target={3} args=[{4}]" -f $ev.EventType, [int]$fn.FunctionType, $fn.FunctionType, $fn.Target, $args)
    }
  }
}

Write-Output '--- nanos mentioning Mongo in SystemText ---'
foreach ($kv in [AORebirth.Core.Nanos.NanoLoader]::NanoList.GetEnumerator()) {
  $n = $kv.Value
  if ($n.Events -eq $null) { continue }
  foreach ($ev in $n.Events) {
    foreach ($fn in $ev.Functions) {
      if ([int]$fn.FunctionType -ne 53057) { continue }
      foreach ($a in $fn.Arguments.Values) {
        $s = $a.ToString()
        if ($s -like '*Mongo*') {
          Write-Output (" nano={0} text={1}" -f $kv.Key, $s)
        }
      }
    }
  }
}
