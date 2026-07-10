Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat')
[AORebirth.Core.Nanos.NanoLoader]::CacheAllNanos('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\nanos.dat')

function Show-Nano($id) {
    if (-not [AORebirth.Core.Nanos.NanoLoader]::NanoList.ContainsKey($id)) {
        Write-Output "NANO $id missing"
        return
    }
    $nano = [AORebirth.Core.Nanos.NanoLoader]::NanoList[$id]
    Write-Output "NANO id=$id name=$($nano.Name) strain=$($nano.NanoStrain()) duration=$($nano.getItemAttribute(8)) ncu=$($nano.getItemAttribute(11))"
    if ($nano.Events -ne $null) {
        foreach ($ev in $nano.Events) {
            Write-Output "  event type=$($ev.EventType)"
            if ($ev.Functions -ne $null) {
                foreach ($fn in $ev.Functions) {
                    $args = @()
                    if ($fn.Arguments -ne $null -and $fn.Arguments.Values -ne $null) {
                        foreach ($arg in $fn.Arguments.Values) { $args += $arg.ToString() }
                    }
                    Write-Output "    fn=$($fn.FunctionType) args=$($args -join ', ')"
                }
            }
        }
    }
}

Show-Nano 205017
Show-Nano 43723

Write-Output '--- Belamorte items ---'
foreach ($entry in [AORebirth.Core.Items.ItemLoader]::ItemList.GetEnumerator()) {
    if ($entry.Value.Name -like '*Belamorte*') {
        $i = $entry.Value
        Write-Output "item id=$($entry.Key) name=$($i.Name) attr12=$($i.getItemAttribute(12))"
    }
}

Write-Output '--- Belamorte nanos ---'
foreach ($entry in [AORebirth.Core.Nanos.NanoLoader]::NanoList.GetEnumerator()) {
    if ($entry.Value.Name -like '*Belamorte*') {
        Show-Nano $entry.Key
    }
}

Write-Output '--- Calling nanos (first 20) ---'
$count = 0
foreach ($entry in [AORebirth.Core.Nanos.NanoLoader]::NanoList.GetEnumerator()) {
    if ($entry.Value.Name -like 'Calling*') {
        Show-Nano $entry.Key
        $count++
        if ($count -ge 20) { break }
    }
}
