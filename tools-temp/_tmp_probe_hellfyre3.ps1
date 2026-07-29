Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat') | Out-Null

# Compare weaponmesh / mesh for known guns vs hellfyre
$ids = @(295757,264084,266989,121591,125036,268712,265090)
foreach ($id in $ids) {
    if (-not [AORebirth.Core.Items.ItemLoader]::ItemList.ContainsKey($id)) { Write-Output ("missing " + $id); continue }
    $t = [AORebirth.Core.Items.ItemLoader]::ItemList[$id]
    Write-Output ("id={0} icon={1} mesh12={2} mesh209={3} wmr={4} wml={5} ovr={6} ovl={7} flagsProp={8} a0={9} type={10} slot={11}" -f `
        $id, $t.getItemAttribute(79), $t.getItemAttribute(12), $t.getItemAttribute(209), `
        $t.getItemAttribute(1006), $t.getItemAttribute(1007), $t.getItemAttribute(1009), $t.getItemAttribute(1010), `
        $t.Flags, $t.getItemAttribute(0), $t.ItemType, $t.getItemAttribute(88))
}

# Find items with icon 154361 (shared with mesh twin of hellfyre)
Write-Output '---icon154361 sample---'
$c=0
foreach ($kv in [AORebirth.Core.Items.ItemLoader]::ItemList.GetEnumerator()) {
    if ($kv.Value.getItemAttribute(79) -eq 154361) {
        Write-Output ("id={0} mesh={1} slot={2}" -f $kv.Key, $kv.Value.getItemAttribute(12), $kv.Value.getItemAttribute(88))
        $c++
        if ($c -ge 8) { break }
    }
}
