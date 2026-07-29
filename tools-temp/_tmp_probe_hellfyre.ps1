Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat') | Out-Null

$id = 295757
if (-not [AORebirth.Core.Items.ItemLoader]::ItemList.ContainsKey($id)) {
    Write-Output 'MISSING'
    exit 1
}

$t = [AORebirth.Core.Items.ItemLoader]::ItemList[$id]
Write-Output ("ID={0} Name={1} Flags={2} Type={3} QL={4}" -f $t.ID, $t.Name, $t.Flags, $t.ItemType, $t.Quality)

$attrs = @(79, 209, 1006, 1007, 1009, 1010, 19, 88, 212, 54, 282, 283, 284, 285, 286, 30, 76, 360, 361, 12, 294, 295, 296, 297)
foreach ($a in $attrs) {
    $v = $t.getItemAttribute($a)
    if ($v -ne 1234567890) {
        Write-Output ("attr{0}={1}" -f $a, $v)
    }
}

Write-Output '---events---'
if ($t.Events) {
    foreach ($ev in $t.Events) {
        Write-Output ("event={0}" -f $ev.EventType)
        if ($ev.Functions) {
            foreach ($fn in $ev.Functions) {
                $args = @()
                if ($fn.Arguments -and $fn.Arguments.Values) {
                    foreach ($arg in $fn.Arguments.Values) {
                        try { $args += $arg.AsInt32() } catch { $args += $arg.ToString() }
                    }
                }
                Write-Output ("  fn={0} args=[{1}]" -f $fn.FunctionType, ($args -join ','))
            }
        }
    }
}

# Find items with icon 264797
Write-Output '---icon264797---'
$count = 0
foreach ($kv in [AORebirth.Core.Items.ItemLoader]::ItemList.GetEnumerator()) {
    $icon = $kv.Value.getItemAttribute(79)
    if ($icon -eq 264797) {
        Write-Output ("id={0} name={1}" -f $kv.Key, $kv.Value.Name)
        $count++
        if ($count -ge 10) { break }
    }
}

# Find sunglasses-like names near wrong icon
Write-Output '---glasssearch---'
foreach ($kv in [AORebirth.Core.Items.ItemLoader]::ItemList.GetEnumerator()) {
    $n = $kv.Value.Name
    if ($n -and ($n -match 'glass|sunglass|goggle|shades|Hellfyre|Rocket Launcher')) {
        $icon = $kv.Value.getItemAttribute(79)
        Write-Output ("id={0} icon={1} name={2}" -f $kv.Key, $icon, $n)
    }
}
