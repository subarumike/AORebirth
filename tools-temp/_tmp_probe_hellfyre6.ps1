Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat') | Out-Null

Write-Output '---items with icon=264083---'
$c=0
foreach ($kv in [AORebirth.Core.Items.ItemLoader]::ItemList.GetEnumerator()) {
    if ($kv.Value.getItemAttribute(79) -eq 264083) {
        Write-Output ("id={0} slot={1} mesh={2}" -f $kv.Key, $kv.Value.getItemAttribute(88), $kv.Value.getItemAttribute(12))
        $c++
        if ($c -ge 15) { break }
    }
}

Write-Output '---items with icon near sunglasses common---'
# Classic AO sunglasses often icon ~117xx from hud samples; also search mesh 264083 used as ICON by mistake
foreach ($icon in @(264083,264084,264085,264086,154361,11708,11749,11752,11753)) {
    $n=0
    foreach ($kv in [AORebirth.Core.Items.ItemLoader]::ItemList.GetEnumerator()) {
        if ($kv.Value.getItemAttribute(79) -eq $icon) {
            if ($n -eq 0) { Write-Output ("icon={0}" -f $icon) }
            Write-Output ("  id={0}" -f $kv.Key)
            $n++
            if ($n -ge 3) { break }
        }
    }
}

# Compare 266989 (mesh12=9013) vs 295757 - maybe we should override mesh12 for hellfyre like 266989
Write-Output '---266989 vs 295757 attrs differing---'
$a=[AORebirth.Core.Items.ItemLoader]::ItemList[266989]
$b=[AORebirth.Core.Items.ItemLoader]::ItemList[295757]
$keys = New-Object 'System.Collections.Generic.HashSet[int]'
foreach ($k in $a.Stats.Keys) { [void]$keys.Add($k) }
foreach ($k in $b.Stats.Keys) { [void]$keys.Add($k) }
foreach ($k in ($keys | Sort-Object)) {
    $va = $a.getItemAttribute($k)
    $vb = $b.getItemAttribute($k)
    if ($va -ne $vb) {
        Write-Output ("a{0}: 266989={1} 295757={2}" -f $k, $va, $vb)
    }
}
