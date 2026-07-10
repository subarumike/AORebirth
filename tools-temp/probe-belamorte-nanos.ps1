Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat')
[AORebirth.Core.Nanos.NanoLoader]::CacheAllNanos('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\nanos.dat')

function Show-Nano($id) {
    if (-not [AORebirth.Core.Nanos.NanoLoader]::NanoList.ContainsKey($id)) {
        Write-Output "NANO $id MISSING"
        return
    }
    $nano = [AORebirth.Core.Nanos.NanoLoader]::NanoList[$id]
    Write-Output "NANO $id strain=$($nano.NanoStrain()) dur=$($nano.getItemAttribute(8)) ncu=$($nano.getItemAttribute(11))"
    foreach ($ev in $nano.Events) {
        Write-Output "  event=$($ev.EventType)"
        foreach ($fn in $ev.Functions) {
            $args = @(); foreach ($a in $fn.Arguments.Values) { $args += $a.ToString() }
            Write-Output "    fn=$($fn.FunctionType) args=$($args -join ', ')"
        }
    }
}

foreach ($id in @(205017, 144230, 144380, 144530, 43723, 43729, 43734, 43743, 43920)) {
    Show-Nano $id
}

foreach ($itemId in @(125754, 144817, 144819, 144821)) {
    if ([AORebirth.Core.Items.ItemLoader]::ItemList.ContainsKey($itemId)) {
        $i = [AORebirth.Core.Items.ItemLoader]::ItemList[$itemId]
        Write-Output "ITEM $itemId attr12=$($i.getItemAttribute(12)) attr11=$($i.getItemAttribute(11))"
    }
}
