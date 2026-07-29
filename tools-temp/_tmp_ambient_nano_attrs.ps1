Add-Type -Path 'AORebirth\Built\Debug\AORebirth.Core.dll'
[AORebirth.Core.Nanos.NanoLoader]::CacheAllNanos('AORebirth\Built\Debug\nanos.dat') | Out-Null
foreach ($id in @(302365,300495,300496,300497,300498)) {
  if (-not [AORebirth.Core.Nanos.NanoLoader]::NanoList.ContainsKey([int]$id)) { Write-Output "MISSING $id"; continue }
  $n = [AORebirth.Core.Nanos.NanoLoader]::NanoList[[int]$id]
  Write-Output ("=== {0} name={1} strain={2} ===" -f $id, $n.Name, $n.NanoStrain())
  foreach ($a in 1..600) {
    $v = $n.getItemAttribute($a)
    if ($v -ne 1234567890 -and $v -ne 0) {
      Write-Output (" attr {0}={1}" -f $a, $v)
    }
  }
  foreach ($ev in $n.Events) {
    foreach ($fn in $ev.Functions) {
      $args = ($fn.Arguments.Values | ForEach-Object { $_.ToString() }) -join ','
      Write-Output (" event={0} fn={1} target={2} args=[{3}]" -f $ev.EventType, [int]$fn.FunctionType, $fn.Target, $args)
    }
  }
}
