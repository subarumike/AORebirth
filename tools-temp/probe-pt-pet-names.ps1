Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
[AORebirth.Core.Nanos.NanoLoader]::CacheAllNanos('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\nanos.dat')

foreach ($hash in @('PT50','PT51','PT52','PT53','PT54','PT55','PT56')) {
    Write-Output "--- $hash ---"
    $seen = @{}
    foreach ($entry in [AORebirth.Core.Nanos.NanoLoader]::NanoList.GetEnumerator()) {
        $nano = $entry.Value
        if ($nano.Events -eq $null) { continue }
        foreach ($ev in $nano.Events) {
            if ($ev.Functions -eq $null) { continue }
            foreach ($fn in $ev.Functions) {
                if ($fn.FunctionType -ne 53167) { continue }
                $args = @()
                if ($fn.Arguments -ne $null -and $fn.Arguments.Values -ne $null) {
                    foreach ($arg in $fn.Arguments.Values) { $args += $arg.ToString() }
                }
                if ($args.Count -lt 2 -or $args[0] -ne $hash) { continue }
                $key = "$($entry.Key)|$($args[1])|$($nano.Name)"
                if ($seen.ContainsKey($key)) { continue }
                $seen[$key] = $true
                Write-Output "nano=$($entry.Key) type=$($args[1]) name=$($nano.Name)"
            }
        }
    }
}
