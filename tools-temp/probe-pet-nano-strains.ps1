Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
[AORebirth.Core.Nanos.NanoLoader]::CacheAllNanos('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\nanos.dat')

foreach ($id in @(125746, 43737, 125754)) {
    if ([AORebirth.Core.Nanos.NanoLoader]::NanoList.ContainsKey($id)) {
        $n = [AORebirth.Core.Nanos.NanoLoader]::NanoList[$id]
        Write-Output "nano $id name=$($n.Name) strain=$($n.NanoStrain())"
    }
    else {
        Write-Output "nano $id MISSING"
    }
}
