Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat')
$item = [AORebirth.Core.Items.ItemLoader]::ItemList[125754]
Write-Output "name field empty=$([string]::IsNullOrEmpty($item.Name))"
for ($a = 0; $a -lt 30; $a++) {
    $v = $item.getItemAttribute($a)
    if ($v -ne 0 -and $v -ne 1234567890) { Write-Output "attr[$a]=$v" }
}
if ($item.Events -ne $null) {
    foreach ($ev in $item.Events) {
        Write-Output "event type=$($ev.EventType)"
        foreach ($fn in $ev.Functions) {
            $args = @(); foreach ($a in $fn.Arguments.Values) { $args += $a.ToString() }
            Write-Output "  fn=$($fn.FunctionType) args=$($args -join ', ')"
        }
    }
}
