$sql = Get-Content 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\SqlTables\itemnames.sql' -Raw
foreach ($id in @(125754, 125753, 125755)) {
    $m = [regex]::Match($sql, "\(\s*$id\s*,\s*'([^']+)'\s*,\s*'([^']+)'\s*,\s*'?(\d+)'?\s*\)")
    if ($m.Success) {
        Write-Output "id=$id name=$($m.Groups[1].Value) type=$($m.Groups[2].Value) col4=$($m.Groups[3].Value)"
    } else {
        Write-Output "id=$id not found"
    }
}

foreach ($needle in @('Belamorte', 'Calling of Belamorte')) {
    $matches = [regex]::Matches($sql, "\(\s*(\d+)\s*,\s*'([^']*$needle[^']*)'\s*,\s*'([^']+)'\s*,\s*'?(\d+)'?\s*\)")
    foreach ($m in $matches) {
        Write-Output "match id=$($m.Groups[1].Value) name=$($m.Groups[2].Value) col4=$($m.Groups[4].Value)"
    }
}
