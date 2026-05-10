param(
    [string]$RepoRoot = "C:\Users\Mike\Documents\Cellao-Clean",
    [string]$BuiltDir = "C:\Users\Mike\Documents\Cellao-Clean\CellAO\Built\Debug",
    [string]$ConfigPath = "C:\Users\Mike\Documents\Cellao-Clean\CellAO\Config\Config.xml",
    [string]$MysqlExe = "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $BuiltDir)) {
    throw "Built directory not found: $BuiltDir"
}

if (-not (Test-Path $ConfigPath)) {
    throw "Config file not found: $ConfigPath"
}

if (-not (Test-Path $MysqlExe)) {
    throw "mysql.exe not found: $MysqlExe"
}

[xml]$config = Get-Content -Path $ConfigPath
$connection = [string]$config.Config.MysqlConnection
$parts = @{}
foreach ($part in $connection.Split(';')) {
    if ([string]::IsNullOrWhiteSpace($part) -or $part -notmatch '=') {
        continue
    }

    $name, $value = $part.Split('=', 2)
    $parts[$name.Trim().ToLowerInvariant()] = $value.Trim()
}

$database = $parts["database"]
if ($database -ne "cellao_codex_clean") {
    throw "Refusing to query database '$database'. Expected only cellao_codex_clean."
}

$server = $parts["server"]
$user = $parts["uid"]
$password = $parts["pwd"]

Set-Location $BuiltDir

$assemblies = @(
    "CellAO.Enums.dll",
    "CellAO.Interfaces.dll",
    "SmokeLounge.AOtomation.Messaging.dll",
    "locales.dll",
    "MsgPack.dll",
    "Utility.dll",
    "CellAO.Core.dll"
)

foreach ($assembly in $assemblies) {
    $path = Join-Path $BuiltDir $assembly
    if (-not (Test-Path $path)) {
        throw "Required assembly not found: $path"
    }

    [Reflection.Assembly]::LoadFrom($path) | Out-Null
}

$itemsDat = Join-Path $BuiltDir "items.dat"
if (-not (Test-Path $itemsDat)) {
    throw "items.dat not found: $itemsDat"
}

[CellAO.Core.Items.ItemLoader]::ItemList.Clear()
[CellAO.Core.Items.ItemLoader]::CacheAllItems($itemsDat) | Out-Null

function Test-UniqueTemplate {
    param([int]$TemplateId)

    $template = $null
    if (-not [CellAO.Core.Items.ItemLoader]::ItemList.TryGetValue($TemplateId, [ref]$template)) {
        return $false
    }

    return $template.Stats.ContainsKey(0) -and $template.IsUnique()
}

$query = @"
SELECT
  'items' AS SourceTable,
  i.Id,
  i.ContainerType AS CharacterId,
  COALESCE(c.Name, '') AS CharacterName,
  i.ContainerInstance AS Page,
  i.ContainerPlacement AS Slot,
  i.LowId,
  i.HighId,
  i.Quality
FROM items i
LEFT JOIN characters c ON c.Id = i.ContainerType
UNION ALL
SELECT
  'instanceditems' AS SourceTable,
  i.Id,
  i.ContainerType AS CharacterId,
  COALESCE(c.Name, '') AS CharacterName,
  i.ContainerInstance AS Page,
  i.ContainerPlacement AS Slot,
  i.LowId,
  i.HighId,
  i.Quality
FROM instanceditems i
LEFT JOIN characters c ON c.Id = i.ContainerType
ORDER BY CharacterId, LowId, HighId, Page, Slot;
"@

$oldMysqlPwd = $env:MYSQL_PWD
$env:MYSQL_PWD = $password
try {
    $raw = & $MysqlExe --protocol=TCP -h $server -u $user --database=$database --batch --raw --execute=$query
}
finally {
    $env:MYSQL_PWD = $oldMysqlPwd
}

if (-not $raw) {
    Write-Output "RESULT PASS no inventory rows found in $database."
    exit 0
}

$rows = $raw | ConvertFrom-Csv -Delimiter "`t"
$uniqueRows = @(
    $rows | Where-Object {
        (Test-UniqueTemplate ([int]$_.LowId)) -or (Test-UniqueTemplate ([int]$_.HighId))
    }
)

$duplicates = @(
    $uniqueRows |
        Group-Object CharacterId, LowId, HighId |
        Where-Object { $_.Count -gt 1 }
)

if ($duplicates.Count -eq 0) {
    Write-Output "RESULT PASS no duplicate unique inventory items found. uniqueRows=$($uniqueRows.Count) totalRows=$($rows.Count)"
    exit 0
}

Write-Output "RESULT FAIL duplicate unique inventory items found. duplicateGroups=$($duplicates.Count) uniqueRows=$($uniqueRows.Count) totalRows=$($rows.Count)"
foreach ($group in $duplicates) {
    $items = @($group.Group)
    $first = $items[0]
    $locations = ($items | ForEach-Object { "$($_.SourceTable):page=$($_.Page):slot=$($_.Slot):row=$($_.Id)" }) -join ", "
    Write-Output "CharacterId=$($first.CharacterId) CharacterName=$($first.CharacterName) Item=$($first.LowId)/$($first.HighId) Copies=$($items.Count) Locations=$locations"
}

exit 2
