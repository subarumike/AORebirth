Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat')
[AORebirth.Core.Nanos.NanoLoader]::CacheAllNanos('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\nanos.dat')

$id = 125754

if ([AORebirth.Core.Items.ItemLoader]::ItemList.ContainsKey($id)) {
    $item = [AORebirth.Core.Items.ItemLoader]::ItemList[$id]
    Write-Output "ITEM id=$id name=$($item.Name)"
    for ($a = 0; $a -lt 20; $a++) {
        $v = $item.getItemAttribute($a)
        if ($v -ne 0) { Write-Output "  attr[$a]=$v" }
    }
} else {
    Write-Output "ITEM $id missing"
}

if ([AORebirth.Core.Nanos.NanoLoader]::NanoList.ContainsKey($id)) {
    $nano = [AORebirth.Core.Nanos.NanoLoader]::NanoList[$id]
    Write-Output "NANO id=$id name=$($nano.Name) strain=$($nano.NanoStrain()) duration=$($nano.getItemAttribute(8))"
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
} else {
    Write-Output "NANO $id missing from nanos.dat"
}

# search nanos with Belamorte in name
foreach ($entry in [AORebirth.Core.Nanos.NanoLoader]::NanoList.GetEnumerator()) {
    if ($entry.Value.Name -like '*Belamorte*') {
        Write-Output "MATCH nano id=$($entry.Key) name=$($entry.Value.Name)"
    }
}
