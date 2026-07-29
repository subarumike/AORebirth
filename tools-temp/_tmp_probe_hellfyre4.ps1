Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Database.dll' -ErrorAction SilentlyContinue
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat') | Out-Null

# How are ItemTemplate.Flags vs attr0 stored in Stats dictionary?
$t = [AORebirth.Core.Items.ItemLoader]::ItemList[295757]
Write-Output ("FlagsField={0}" -f $t.Flags)
if ($t.Stats -ne $null) {
    Write-Output ("StatsCount={0}" -f $t.Stats.Count)
    foreach ($k in @(0,79,12,209,88)) {
        if ($t.Stats.ContainsKey($k)) { Write-Output ("Stats[{0}]={1}" -f $k, $t.Stats[$k]) }
    }
}

# Item constructor Flags
$item = New-Object AORebirth.Core.Items.Item(1, 295757, 295757)
Write-Output ("Item.Flags={0} GetAttr0={1} GetIcon={2} GetMesh209={3}" -f $item.Flags, $item.GetAttribute(0), $item.GetAttribute(79), $item.GetAttribute(209))

# Find rocket/launcher named items via ItemNames if available
Write-Output '---search names via reflection---'
$daoType = [AppDomain]::CurrentDomain.GetAssemblies() | ForEach-Object { $_.GetTypes() } | Where-Object { $_.Name -eq 'ItemNamesDao' } | Select-Object -First 1
if ($daoType) {
    Write-Output ("found " + $daoType.FullName)
} else {
    Write-Output 'ItemNamesDao type not loaded'
}

# Compare attr for reward items
foreach ($id in @(223349,223361,215265,223365,154361,264084)) {
    if (-not [AORebirth.Core.Items.ItemLoader]::ItemList.ContainsKey($id)) { Write-Output ("missing $id"); continue }
    $x = [AORebirth.Core.Items.ItemLoader]::ItemList[$id]
    Write-Output ("id={0} icon={1} mesh12={2} mesh209={3} a0={4} slot={5}" -f $id, $x.getItemAttribute(79), $x.getItemAttribute(12), $x.getItemAttribute(209), $x.getItemAttribute(0), $x.getItemAttribute(88))
}
