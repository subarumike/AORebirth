Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
[AORebirth.Core.Nanos.NanoLoader]::CacheAllNanos('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\nanos.dat')
$n = [AORebirth.Core.Nanos.NanoLoader]::NanoList[125746]
$i = 0
foreach ($ev in $n.Events) {
    if ($ev.EventType.ToString() -ne 'OnUse') { continue }
    foreach ($fn in $ev.Functions) {
        if ($fn.FunctionType -ne 53167) { continue }
        $args = @(); foreach ($a in $fn.Arguments.Values) { $args += $a.ToString() }
        $reqs = @()
        if ($fn.Requirements -ne $null) {
            foreach ($r in $fn.Requirements) { $reqs += ($r.GetType().Name + ':' + $r.ToString()) }
        }
        Write-Output "[$i] hash=$($args[0]) type=$($args[1]) reqs=$($reqs -join ' | ')"
        $i++
    }
}
