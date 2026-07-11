Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat')
[AORebirth.Core.Nanos.NanoLoader]::CacheAllNanos('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\nanos.dat')

$summonIds = @(
    125738, 125743, 125744, 125745, 125746,
    43324, 43733, 43723, 43734, 43735, 43732, 43737, 43718
)

foreach ($sid in $summonIds) {
    $crystals = @()
    foreach ($entry in [AORebirth.Core.Items.ItemLoader]::ItemList.GetEnumerator()) {
        $item = $entry.Value
        $link = $item.getItemAttribute(12)
        if ($link -eq $sid) {
            $crystals += [string]$entry.Key
        }
    }

    $nanoName = ''
    if ([AORebirth.Core.Nanos.NanoLoader]::NanoList.ContainsKey($sid)) {
        $nanoName = [AORebirth.Core.Nanos.NanoLoader]::NanoList[$sid].Name
    }

    Write-Output ("summon={0}|name={1}|crystals={2}" -f $sid, $nanoName, ($crystals -join ','))
}
