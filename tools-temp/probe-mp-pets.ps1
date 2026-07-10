Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat')
[AORebirth.Core.Nanos.NanoLoader]::CacheAllNanos('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\nanos.dat')

$sql = Get-Content 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\SqlTables\itemnames.sql' -Raw
foreach ($needle in @('125754', 'Belamorte', '205017', 'Calling of Belamorte')) {
    if ($sql -match $needle) { Write-Output "itemnames contains: $needle" } else { Write-Output "itemnames missing: $needle" }
}

$itemId = 125754
$item = [AORebirth.Core.Items.ItemLoader]::ItemList[$itemId]
Write-Output "crystal attr12 (nano link)=$($item.getItemAttribute(12))"
$nanoId = $item.getItemAttribute(12)

Write-Output '--- summon nanos with PT in args ---'
foreach ($entry in [AORebirth.Core.Nanos.NanoLoader]::NanoList.GetEnumerator()) {
    $nano = $entry.Value
    if ($nano.Events -eq $null) { continue }
    foreach ($ev in $nano.Events) {
        if ($ev.Functions -eq $null) { continue }
        foreach ($fn in $ev.Functions) {
            if ($fn.FunctionType -ne 53167 -and $fn.FunctionType -ne 53181) { continue }
            $args = @()
            if ($fn.Arguments -ne $null -and $fn.Arguments.Values -ne $null) {
                foreach ($arg in $fn.Arguments.Values) { $args += $arg.ToString() }
            }
            $hash = if ($args.Count -gt 0) { $args[0] } else { '' }
            if ($hash -like 'PT*' -or $hash -like 'TEWB*') {
                Write-Output "nano=$($entry.Key) strain=$($nano.NanoStrain()) fn=$($fn.FunctionType) args=$($args -join ', ')"
            }
        }
    }
}

Write-Output '--- crystals linking missing nanos (sample) ---'
$missing = 0
foreach ($entry in [AORebirth.Core.Items.ItemLoader]::ItemList.GetEnumerator()) {
    $i = $entry.Value
    $linked = $i.getItemAttribute(12)
    if ($linked -le 0) { continue }
    if (-not [AORebirth.Core.Nanos.NanoLoader]::NanoList.ContainsKey($linked)) {
        if ($missing -lt 30) {
            Write-Output "item=$($entry.Key) attr12=$linked MISSING nano"
        }
        $missing++
    }
}
Write-Output "total items with missing linked nano: $missing"
