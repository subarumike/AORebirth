Add-Type -Path 'c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\AORebirth.Core.dll'
[AORebirth.Core.Nanos.NanoLoader]::CacheAllNanos('c:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug\nanos.dat') | Out-Null
foreach ($n in [AORebirth.Core.Nanos.NanoLoader]::NanoList.Values) {
  $name = $n.getItemAttribute(0).ToString()
  if ($name -match 'Calling|Summon Bel|Summon ') {
    foreach ($ev in $n.Events) {
      foreach ($fn in $ev.Functions) {
        if ($fn.FunctionType -eq 53167 -or $fn.FunctionType -eq 53181) {
          $args = ($fn.Arguments.Values | ForEach-Object { $_.ToString() }) -join ','
          Write-Output "nano=$($n.ID) name=$name fn=$($fn.FunctionType) args=$args"
        }
      }
    }
  }
}
