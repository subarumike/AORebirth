Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
[AORebirth.Core.Nanos.NanoLoader]::CacheAllNanos('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\nanos.dat') | Out-Null
foreach ($id in @(43324, 43718)) {
  $n = [AORebirth.Core.Nanos.NanoLoader]::NanoList[$id]
  Write-Output "nano=$id strain=$($n.NanoStrain()) ncu=$($n.NCUCost())"
  foreach ($key in @(78, 79, 80, 81, 82, 407, 408)) {
    Write-Output "  attr$key=$($n.getItemAttribute($key))"
  }
}
