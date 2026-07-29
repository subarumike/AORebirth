Add-Type -Path 'AORebirth\Built\Debug\AORebirth.Core.dll'
[AORebirth.Core.Nanos.NanoLoader]::CacheAllNanos('AORebirth\Built\Debug\nanos.dat') | Out-Null

foreach ($id in @(100198,100194,100197,100196,100195)) {
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

Write-Output '--- nanos that AreaCastNano 100194 or CastNano 100198 ---'
foreach ($kv in [AORebirth.Core.Nanos.NanoLoader]::NanoList.GetEnumerator()) {
  $n = $kv.Value
  if ($n.Events -eq $null) { continue }
  foreach ($ev in $n.Events) {
    foreach ($fn in $ev.Functions) {
      $ft = [int]$fn.FunctionType
      if ($ft -ne 53087 -and $ft -ne 53019 -and $ft -ne 53066) { continue }
      $args = @($fn.Arguments.Values | ForEach-Object { $_.ToString() })
      if ($args -contains '100194' -or $args -contains '100198') {
        Write-Output (" nano={0} fn={1} args=[{2}] strain={3} range={4}" -f $kv.Key, $ft, ($args -join ','), $n.NanoStrain(), $n.getItemAttribute(287))
      }
    }
  }
}

Write-Output '--- strain 51 nanos ---'
foreach ($kv in [AORebirth.Core.Nanos.NanoLoader]::NanoList.GetEnumerator()) {
  $n = $kv.Value
  try {
    if ($n.NanoStrain() -eq 51) {
      Write-Output (" nano={0} NCU={1} range={2} dur={3}" -f $kv.Key, $n.getItemAttribute(11), $n.getItemAttribute(287), $n.getItemAttribute(8))
    }
  } catch {}
}
