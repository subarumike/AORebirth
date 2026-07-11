Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat')

$targets = @(
    125738, 125743, 125744, 125745, 125746,
    43324, 43733, 43723, 43734, 43735, 43732, 43737, 43718
)

foreach ($target in $targets) {
    $hits = @()
    foreach ($entry in [AORebirth.Core.Items.ItemLoader]::ItemList.GetEnumerator()) {
        $item = $entry.Value
        $uploadNano = 0
        if ($item.Events -eq $null) { continue }
        foreach ($ev in $item.Events) {
            if ($ev.Functions -eq $null) { continue }
            foreach ($fn in $ev.Functions) {
                if ($fn.Arguments -eq $null) { continue }
                foreach ($arg in $fn.Arguments.Values) {
                    try {
                        $v = $arg.AsInt32()
                        if ($v -eq $target) { $uploadNano = $v }
                    }
                    catch { }
                }
            }
        }
        if ($uploadNano -eq $target) {
            $hits += [string]$entry.Key
        }
    }
    Write-Output ("summon={0}|crystals={1}" -f $target, ($hits -join ','))
}
