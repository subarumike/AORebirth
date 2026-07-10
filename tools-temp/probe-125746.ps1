Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
[AORebirth.Core.Nanos.NanoLoader]::CacheAllNanos('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\nanos.dat')
foreach ($id in @(125746, 205017)) {
    if ([AORebirth.Core.Nanos.NanoLoader]::NanoList.ContainsKey($id)) {
        $n = [AORebirth.Core.Nanos.NanoLoader]::NanoList[$id]
        Write-Output "NANO $id strain=$($n.NanoStrain())"
        foreach ($ev in $n.Events) {
            foreach ($fn in $ev.Functions) {
                $args=@(); foreach($a in $fn.Arguments.Values){$args+=$a.ToString()}
                Write-Output "  ev=$($ev.EventType) fn=$($fn.FunctionType) args=$($args -join ', ')"
            }
        }
    } else { Write-Output "NANO $id MISSING" }
}

$enumType = [AORebirth.Enums.FunctionType]
foreach ($name in [enum]::GetNames($enumType)) {
    $val = [int][enum]::Parse($enumType, $name)
    if ($val -eq 53019) { Write-Output "53019 = $name" }
}
