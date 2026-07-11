Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat')
[AORebirth.Core.Nanos.NanoLoader]::CacheAllNanos('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\nanos.dat')

$itemId = 125754
if ([AORebirth.Core.Items.ItemLoader]::ItemList.ContainsKey($itemId)) {
    $item = [AORebirth.Core.Items.ItemLoader]::ItemList[$itemId]
    Write-Output "ITEM $itemId name=$($item.Name)"
    foreach ($a in @(1,2,3,4,5,6,7,8,75,407)) {
        $v = $item.getItemAttribute($a)
        if ($v -ne 0) { Write-Output "  attr[$a]=$v" }
    }
}
else {
    Write-Output "ITEM $itemId missing"
}

$nanoId = 125746
$n = [AORebirth.Core.Nanos.NanoLoader]::NanoList[$nanoId]
Write-Output "NANO $nanoId strain=$($n.NanoStrain())"
foreach ($ev in $n.Events) {
    if ($ev.EventType -ne [AORebirth.Enums.EventType]::OnUse) { continue }
    foreach ($fn in $ev.Functions) {
        $args = @()
        if ($fn.Arguments -ne $null -and $fn.Arguments.Values -ne $null) {
            foreach ($arg in $fn.Arguments.Values) { $args += $arg.ToString() }
        }
        Write-Output "  fn=$($fn.FunctionType) args=$($args -join ', ')"
    }
}
