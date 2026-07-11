Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat')
[AORebirth.Core.Nanos.NanoLoader]::CacheAllNanos('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\nanos.dat')

$needles = @(
    'Calling of Medinos', 'Calling of Salvinous', 'Calling of Valentyia', 'Calling of Sanoo', 'Calling of Belamorte',
    'Summon Anger Manifestation', 'Summon Fury Externalization', 'Summon Rage Materialization',
    'Summon Wrath Incarnation', 'Summon Frenzy Embodiment', 'Summon Demon', 'Summon Herald',
    'Summon Spirit', 'Nano Crystal'
)

foreach ($entry in [AORebirth.Core.Items.ItemLoader]::ItemList.GetEnumerator()) {
    $item = $entry.Value
    $name = $item.Name
    if ([string]::IsNullOrWhiteSpace($name)) { continue }
    foreach ($needle in $needles) {
        if ($name -like "*$needle*") {
            $link = $item.getItemAttribute(12)
            $uploadNano = 0
            if ($item.Events -ne $null) {
                foreach ($ev in $item.Events) {
                    if ($ev.Functions -eq $null) { continue }
                    foreach ($fn in $ev.Functions) {
                        if ($fn.Arguments -ne $null -and $fn.Arguments.Values.Count -gt 0) {
                            $v = $fn.Arguments.Values[0].AsInt32()
                            if ($v -gt 1000) { $uploadNano = $v }
                        }
                    }
                }
            }
            Write-Output ("item={0}|name={1}|attr12={2}|uploadNano={3}" -f $entry.Key, $name, $link, $uploadNano)
            break
        }
    }
}
