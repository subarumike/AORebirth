Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\Utility.dll'
Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Database.dll'
[AORebirth.Core.Nanos.NanoLoader]::CacheAllNanos('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\nanos.dat')

$n = [AORebirth.Core.Nanos.NanoLoader]::NanoList[125746]
Write-Output "nano125746 dur=$($n.getItemAttribute(8)) ncu=$($n.getItemAttribute(11))"

$hashes = @('MT09','CULP','JZSX','FPJG','BSLX','RJWL','UKCW','BSLX')
foreach ($h in $hashes) {
    $t = [AORebirth.Database.Dao.MobTemplateDao]::Instance.GetMobTemplateByHash($h)
    if ($t -ne $null) { Write-Output "mob $h FOUND" } else { Write-Output "mob $h MISSING" }
}

# check if any MT* templates exist via sql grep alternative - list known mob hashes starting MT
$sql = Get-Content 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\SqlTables\mobtemplate.sql' -Raw -ErrorAction SilentlyContinue
if ($sql) {
    foreach ($h in @('MT09','CULP','JZSX','FPJG','BSLX','RJWL','UKCW','Belamorte','TEWB')) {
        if ($sql -match "'$h'") { Write-Output "sql has $h" } else { Write-Output "sql missing $h" }
    }
}
