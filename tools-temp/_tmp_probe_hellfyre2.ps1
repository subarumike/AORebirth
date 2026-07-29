Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat') | Out-Null

# items sharing mesh 264083
Write-Output '---mesh264083---'
$n = 0
foreach ($kv in [AORebirth.Core.Items.ItemLoader]::ItemList.GetEnumerator()) {
    $m = $kv.Value.getItemAttribute(12)
    $m2 = $kv.Value.getItemAttribute(209)
    if ($m -eq 264083 -or $m2 -eq 264083) {
        $icon = $kv.Value.getItemAttribute(79)
        Write-Output ("id={0} icon={1} flags={2} type={3} slot88={4}" -f $kv.Key, $icon, $kv.Value.Flags, $kv.Value.ItemType, $kv.Value.getItemAttribute(88))
        $n++
        if ($n -ge 15) { break }
    }
}

# Item.Flags property path: try ItemNamesDao for name
try {
    $nameType = [AORebirth.Database.Dao.ItemNamesDao]
    Write-Output ("ItemNamesDao type ok")
} catch {
    Write-Output ("ItemNamesDao err " + $_.Exception.Message)
}

# Compare similar rocket / glasses icons nearby
Write-Output '---nearby icons---'
foreach ($icon in @(264790,264791,264792,264793,264794,264795,264796,264797,264798,264799,264800,264080,264081,264082,264083,264084)) {
    $hits = 0
    foreach ($kv in [AORebirth.Core.Items.ItemLoader]::ItemList.GetEnumerator()) {
        if ($kv.Value.getItemAttribute(79) -eq $icon) {
            Write-Output ("icon={0} id={1}" -f $icon, $kv.Key)
            $hits++
            if ($hits -ge 3) { break }
        }
    }
}

# Dump ALL non-default attrs for 295757
Write-Output '---all attrs 295757---'
$t = [AORebirth.Core.Items.ItemLoader]::ItemList[295757]
for ($a = 0; $a -le 1100; $a++) {
    $v = $t.getItemAttribute($a)
    if ($v -ne 0 -and $v -ne 1234567890) {
        Write-Output ("a{0}={1}" -f $a, $v)
    }
}
