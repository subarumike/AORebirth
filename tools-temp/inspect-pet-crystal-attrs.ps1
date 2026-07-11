Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat')
[AORebirth.Core.Nanos.NanoLoader]::CacheAllNanos('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\nanos.dat')

$crystalIds = @(
    125751, 125748, 125749, 125750, 125754,
    43330, 43780, 43777, 43773, 43770, 43737,
    43328, 96235, 204709
)

foreach ($itemId in $crystalIds) {
    if (-not [AORebirth.Core.Items.ItemLoader]::ItemList.ContainsKey($itemId)) {
        Write-Output "item=$itemId missing"
        continue
    }
    $item = [AORebirth.Core.Items.ItemLoader]::ItemList[$itemId]
    $attrs = @()
    foreach ($a in 0..20) {
        $v = $item.getItemAttribute($a)
        if ($v -ne 0 -and $v -ne 1234567890) { $attrs += "a$a=$v" }
    }
    $uploadNano = 0
    if ($item.Events -ne $null) {
        foreach ($ev in $item.Events) {
            if ($ev.Functions -eq $null) { continue }
            foreach ($fn in $ev.Functions) {
                if ($fn.Arguments -ne $null) {
                    foreach ($arg in $fn.Arguments.Values) {
                        $v = $arg.AsInt32()
                        if ($v -gt 1000) { $uploadNano = $v }
                    }
                }
            }
        }
    }
    Write-Output ("item={0}|attrs={1}|uploadNano={2}" -f $itemId, ($attrs -join ';'), $uploadNano)
}
