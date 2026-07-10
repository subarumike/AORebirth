Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
[AORebirth.Core.Nanos.NanoLoader]::CacheAllNanos('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\nanos.dat')
$max = 0; $min = [int]::MaxValue
foreach ($k in [AORebirth.Core.Nanos.NanoLoader]::NanoList.Keys) {
    if ($k -gt $max) { $max = $k }
    if ($k -lt $min) { $min = $k }
}
Write-Output "count=$([AORebirth.Core.Nanos.NanoLoader]::NanoList.Count) min=$min max=$max"

$path = 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Datafiles\nanos.dat'
if (Test-Path $path) {
    [AORebirth.Core.Nanos.NanoLoader]::NanoList.Clear()
    [AORebirth.Core.Nanos.NanoLoader]::CacheAllNanos($path)
    $max2=0
    foreach ($k in [AORebirth.Core.Nanos.NanoLoader]::NanoList.Keys) { if ($k -gt $max2) { $max2 = $k } }
    Write-Output "datafiles count=$([AORebirth.Core.Nanos.NanoLoader]::NanoList.Count) max=$max2 has205017=$([AORebirth.Core.Nanos.NanoLoader]::NanoList.ContainsKey(205017))"
}
