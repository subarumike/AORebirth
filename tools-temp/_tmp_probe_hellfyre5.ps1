Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Database.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\MySql.Data.dll' -ErrorAction SilentlyContinue

[AORebirth.Core.Items.ItemLoader]::CacheAllItems('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\items.dat') | Out-Null

# Try ItemNamesDao without DB - may fail
try {
    $dao = [AORebirth.Database.Dao.ItemNamesDao]::Instance
    Write-Output ("dao={0}" -f $dao)
    foreach ($id in @(295757,266989,264084,266995,273580)) {
        try {
            $n = $dao.Get($id)
            Write-Output ("id={0} name={1}" -f $id, $n.Name)
        } catch {
            Write-Output ("id={0} name-err={1}" -f $id, $_.Exception.Message)
        }
    }
} catch {
    Write-Output ("dao-err " + $_.Exception.Message)
}

# Find items whose icon equals known sunglass-looking - search mesh used by HUD glasses
# Typical HUD goggles slot=1 (Hud1). Find icons for Hud1-only items with mesh like glasses
Write-Output '---hud1 samples icon---'
$c=0
foreach ($kv in [AORebirth.Core.Items.ItemLoader]::ItemList.GetEnumerator()) {
    $slot = $kv.Value.getItemAttribute(88)
    if ($slot -eq 1) {
        $icon = $kv.Value.getItemAttribute(79)
        $mesh = $kv.Value.getItemAttribute(12)
        if ($icon -gt 0 -and $icon -ne 1234567890) {
            Write-Output ("id={0} icon={1} mesh={2}" -f $kv.Key, $icon, $mesh)
            $c++
            if ($c -ge 20) { break }
        }
    }
}

# Is 264797 used only by rocket family?
Write-Output '---all 264797---'
foreach ($kv in [AORebirth.Core.Items.ItemLoader]::ItemList.GetEnumerator()) {
    if ($kv.Value.getItemAttribute(79) -eq 264797) {
        Write-Output ("id={0} mesh12={1} mesh209={2} slot={3} a0={4}" -f $kv.Key, $kv.Value.getItemAttribute(12), $kv.Value.getItemAttribute(209), $kv.Value.getItemAttribute(88), $kv.Value.getItemAttribute(0))
    }
}
