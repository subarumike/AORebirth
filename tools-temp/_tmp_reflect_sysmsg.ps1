$p = (Resolve-Path 'tools-temp\AOSharpLiveCapture\bin\Debug\AOSharp.Common.dll').Path
$a = [Reflection.Assembly]::LoadFrom($p)
$types = $a.GetTypes() | Where-Object { $_.Name -match 'SystemMessage|ChatMessage' }
foreach ($t in $types) {
  Write-Host "TYPE $($t.FullName)"
  foreach ($prop in $t.GetProperties()) {
    Write-Host ("  P {0} {1}" -f $prop.Name, $prop.PropertyType.FullName)
  }
  foreach ($f in $t.GetFields()) {
    Write-Host ("  F {0} {1}" -f $f.Name, $f.FieldType.FullName)
  }
}
