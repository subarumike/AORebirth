param(
    [string]$RepoRoot = "C:\Users\Mike\Documents\Cellao-Clean",
    [string]$MapPath = "C:\Users\Mike\Documents\Cellao-Clean\CellAO\Documentation\AOClientDllFunctionMap\ao_client_dll_combat_corpse_loot_readable_functions.csv",
    [string]$OutDir = "C:\Users\Mike\Documents\Cellao-Clean\CellAO\Documentation\AOClientDllFunctionMap"
)

$ErrorActionPreference = "Stop"

function Get-Aliases {
    param([string]$Name)

    $aliases = New-Object System.Collections.Generic.List[string]
    $aliases.Add($Name)

    $base = $Name -replace '^N3Msg_', ''
    $base = $base -replace 'IIR_t$', ''
    $base = $base -replace '_t$', ''

    if ($base -ne $Name) {
        $aliases.Add($base)
    }

    if ($base.Length -gt 0) {
        $aliases.Add($base + "Message")
        $aliases.Add($base + "MessageHandler")
    }

    $manual = @{
        "N3Msg_DefaultAttack" = @("Attack", "AttackMessage", "AttackMessageHandler", "AttackInfo", "AttackInfoMessage", "FightingTarget", "DoCombatTick")
        "N3Msg_StopAttack" = @("StopFight", "StopFightMessage", "StopFightMessageHandler", "FightingTarget")
        "N3Msg_SetLootAccess" = @("LootAccess", "SetLootAccess", "HasAlwaysLootable", "Corpse", "CorpseInstance")
        "N3Msg_UseItem" = @("UseItem", "GenericCmd", "GenericCmdMessage", "GenericCmdMessageHandler", "GenericCmdAction.Use")
        "N3Msg_ContainerAddItem" = @("ContainerAddItem", "ContainerAddItemMessage", "ContainerAddItemMessageHandler")
        "N3Msg_DefaultActionOnDynel" = @("DefaultActionOnDynel", "GenericCmd", "LookAt", "UseStatel", "OnUse", "OnTrade")
        "N3Msg_RequestInfoPacket" = @("InfoRequest", "CharacterInfoPacket", "CharacterInfoPacketMessageHandler", "LookAt")
        "N3Msg_CanAttack" = @("CanAttack", "AttackMessageHandler", "FightingTarget")
        "N3Msg_GetAttackingID" = @("GetAttackingID", "FightingTarget")
        "N3Msg_IsAttacking" = @("IsAttacking", "FightingTarget")
        "N3Msg_GetCurrentFightMode" = @("FightModeUpdate", "FightModeUpdateMessage", "FightMode")
        "N3Msg_GetCorrectActionID" = @("GetCorrectActionID", "ActionType", "GenericCmdAction", "CharacterActionType")
        "ToClientDynelDead" = @("DynelDead", "KillNpcTarget", "MarkNpcDead", "CorpseFullUpdate", "Despawn", "DeadTimer")
        "GetAttackRange" = @("AttackRange", "Range", "CanAttack")
        "CorpseFullUpdateIIR" = @("CorpseFullUpdate", "CorpseFullUpdateMessage")
        "BankCorpseIIR" = @("BankCorpse", "Corpse")
        "AttackInfoIIR" = @("AttackInfo", "AttackInfoMessage")
        "CharacterActionIIR" = @("CharacterAction", "CharacterActionMessage", "CharacterActionMessageHandler")
        "StopFightIIR" = @("StopFight", "StopFightMessage", "StopFightMessageHandler")
        "FightModeUpdate" = @("FightModeUpdate", "FightModeUpdateMessage")
        "Feedback" = @("Feedback", "FeedbackMessage", "FeedbackMessageHandler")
    }

    if ($manual.ContainsKey($Name)) {
        foreach ($alias in $manual[$Name]) {
            $aliases.Add($alias)
        }
    }

    if ($manual.ContainsKey($base)) {
        foreach ($alias in $manual[$base]) {
            $aliases.Add($alias)
        }
    }

    return @($aliases | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
}

function Get-HitArea {
    param([string[]]$Paths)

    if ($Paths | Where-Object { $_ -match '\\Server\\ZoneEngine\\Core\\MessageHandlers\\' }) {
        return "ZoneMessageHandler"
    }
    if ($Paths | Where-Object { $_ -match '\\Server\\ZoneEngine\\Core\\Playfields\\|\\Server\\ZoneEngine\\Core\\Controllers\\|\\Libraries\\Source\\CellAO.Core\\' }) {
        return "ServerRuntime"
    }
    if ($Paths | Where-Object { $_ -match '\\AOtomation\.Messaging\\.*\\Messages\\N3Messages\\' }) {
        return "MessageModel"
    }
    if ($Paths | Where-Object { $_ -match '\\CellAO.Enums\\|\\CellAO.Stats\\|\\XML Data\\Stats\.xml' }) {
        return "EnumOrStats"
    }
    if ($Paths | Where-Object { $_ -match '\\SqlTables\\|\\CellAO.Database\\' }) {
        return "DatabaseModel"
    }
    if ($Paths.Count -gt 0) {
        return "OtherCode"
    }

    return "Missing"
}

function Get-Status {
    param(
        [string]$Area,
        [bool]$HasExact
    )

    if ($Area -eq "Missing") {
        return "Missing"
    }
    if ($Area -eq "ZoneMessageHandler" -or $Area -eq "ServerRuntime") {
        if ($HasExact) {
            return "ImplementedOrHookedExact"
        }
        return "ImplementedOrHookedByAlias"
    }
    if ($Area -eq "MessageModel") {
        return "MessageModelOnly"
    }
    if ($Area -eq "EnumOrStats" -or $Area -eq "DatabaseModel") {
        return "DataOnly"
    }

    return "PartialOrUnknown"
}

function Find-AliasHits {
    param(
        [string[]]$Aliases,
        [object[]]$Files,
        [int]$MaxHits = 8
    )

    $hits = New-Object System.Collections.Generic.List[object]
    foreach ($alias in $Aliases) {
        foreach ($file in $Files) {
            if ($file.Text.IndexOf($alias, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
                continue
            }

            $lines = $file.Lines
            for ($i = 0; $i -lt $lines.Count; $i++) {
                if ($lines[$i].IndexOf($alias, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    $hits.Add([pscustomobject]@{
                        Alias = $alias
                        Path = $file.RelativePath
                        Line = $i + 1
                        Text = ($lines[$i].Trim() -replace '\s+', ' ')
                    })

                    if ($hits.Count -ge $MaxHits) {
                        return $hits.ToArray()
                    }
                }
            }
        }
    }

    return $hits.ToArray()
}

$codeRoots = @(
    (Join-Path $RepoRoot "CellAO\Server"),
    (Join-Path $RepoRoot "CellAO\Libraries"),
    (Join-Path $RepoRoot "Tools")
) | Where-Object { Test-Path -LiteralPath $_ }

$extensions = @("*.cs", "*.xml", "*.sql", "*.config", "*.csproj")
$files = New-Object System.Collections.Generic.List[object]
foreach ($root in $codeRoots) {
    foreach ($ext in $extensions) {
        Get-ChildItem -LiteralPath $root -Recurse -File -Filter $ext -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -notmatch '\\bin\\|\\obj\\|\\packages\\' } |
            ForEach-Object {
                $text = Get-Content -Raw -LiteralPath $_.FullName
                $files.Add([pscustomobject]@{
                    FullName = $_.FullName
                    RelativePath = $_.FullName.Substring($RepoRoot.Length + 1)
                    Text = $text
                    Lines = @($text -split "`r?`n")
                })
            }
    }
}

$rows = Import-Csv -LiteralPath $MapPath
$results = New-Object System.Collections.Generic.List[object]

foreach ($row in $rows) {
    $aliases = Get-Aliases $row.Name
    $hits = Find-AliasHits -Aliases $aliases -Files $files -MaxHits 10
    $paths = @($hits | Select-Object -ExpandProperty Path -Unique)
    $hasExact = @($hits | Where-Object { $_.Alias -eq $row.Name }).Count -gt 0
    $area = Get-HitArea $paths
    $status = Get-Status $area $hasExact

    $results.Add([pscustomobject]@{
        Program = $row.Program
        EntryPoint = $row.EntryPoint
        Name = $row.Name
        Namespace = $row.Namespace
        Signature = $row.Signature
        Status = $status
        BestArea = $area
        HasExactNameHit = $hasExact
        AliasList = ($aliases -join "; ")
        HitCountSampled = $hits.Count
        HitPaths = ($paths -join "; ")
        HitSamples = (($hits | ForEach-Object { "{0}:{1} [{2}] {3}" -f $_.Path, $_.Line, $_.Alias, $_.Text }) -join " || ")
    })
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$outCsv = Join-Path $OutDir "cellao_cross_reference_combat_corpse_loot.csv"
$results |
    Sort-Object Status,Program,Name |
    Export-Csv -NoTypeInformation -Encoding UTF8 -Path $outCsv

$summary = $results | Group-Object Status | Sort-Object Name | ForEach-Object {
    [pscustomobject]@{ Status = $_.Name; Count = $_.Count }
}
$summaryCsv = Join-Path $OutDir "cellao_cross_reference_combat_corpse_loot_summary.csv"
$summary | Export-Csv -NoTypeInformation -Encoding UTF8 -Path $summaryCsv

$highValueNames = @(
    "N3Msg_DefaultAttack",
    "N3Msg_StopAttack",
    "N3Msg_SetLootAccess",
    "N3Msg_UseItem",
    "N3Msg_ContainerAddItem",
    "N3Msg_DefaultActionOnDynel",
    "N3Msg_RequestInfoPacket",
    "N3Msg_GetCurrentFightMode",
    "N3Msg_GetCorrectActionID",
    "N3Msg_CanAttack",
    "ToClientDynelDead",
    "GetAttackRange"
)

$highValue = $results | Where-Object { $highValueNames -contains $_.Name } | Sort-Object { [array]::IndexOf($highValueNames, $_.Name) }, Program
$highValueCsv = Join-Path $OutDir "cellao_cross_reference_high_value.csv"
$highValue | Export-Csv -NoTypeInformation -Encoding UTF8 -Path $highValueCsv

$mdPath = Join-Path $OutDir "CellAOCrossReference.md"
$generatedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz"
$md = New-Object System.Collections.Generic.List[string]
$md.Add("# CellAO Cross-Reference for AO Client Combat/Corpse/Loot Functions")
$md.Add("")
$md.Add("Generated: $generatedAt")
$md.Add("")
$md.Add("Input map: $MapPath")
$md.Add("")
$md.Add("Outputs:")
$md.Add("- cellao_cross_reference_combat_corpse_loot.csv - all mapped combat/corpse/loot rows with CellAO hit samples.")
$md.Add("- cellao_cross_reference_combat_corpse_loot_summary.csv - grouped status counts.")
$md.Add("- cellao_cross_reference_high_value.csv - the small set of functions most relevant to current combat/corpse/loot work.")
$md.Add("")
$md.Add("## Status Counts")
$md.Add("")
$md.Add("| status | count |")
$md.Add("| --- | ---: |")
foreach ($item in $summary) {
    $md.Add("| $($item.Status) | $($item.Count) |")
}
$md.Add("")
$md.Add("## High-Value Rows")
$md.Add("")
$md.Add("| client function | status | best CellAO area | key hit paths |")
$md.Add("| --- | --- | --- | --- |")
foreach ($item in $highValue) {
    $paths = $item.HitPaths
    if ($paths.Length -gt 180) {
        $paths = $paths.Substring(0, 177) + "..."
    }
    $md.Add("| $($item.Name) | $($item.Status) | $($item.BestArea) | $paths |")
}
$md.Add("")
$md.Add("## Reading the CSV")
$md.Add("")
$md.Add("- ImplementedOrHookedExact: exact client function/message name appears in a server runtime or handler area.")
$md.Add("- ImplementedOrHookedByAlias: a likely CellAO equivalent exists under a different name.")
$md.Add("- MessageModelOnly: AOtomation has a message class/serializer model, but ZoneEngine has no obvious handler/runtime path.")
$md.Add("- DataOnly: only enum, stat, database, or schema support was found.")
$md.Add("- Missing: no sampled code hit found for the generated aliases.")
$md.Add("")
$md.Add("Important: generated documentation/maps are excluded from the search so hits represent existing CellAO code, not this new mapping work.")
$md | Set-Content -Encoding UTF8 -Path $mdPath

Write-Host "Cross-reference complete."
Write-Host "All rows: $outCsv"
Write-Host "Summary: $summaryCsv"
Write-Host "High value: $highValueCsv"
Write-Host "Doc: $mdPath"
