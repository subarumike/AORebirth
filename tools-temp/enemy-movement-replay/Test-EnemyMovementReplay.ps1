param(
    [string]$CsvPath = (Join-Path $PSScriptRoot 'sample-melee-ranged-chase.csv')
)

$ErrorActionPreference = 'Stop'

function Assert-Equal {
    param(
        [object]$Actual,
        [object]$Expected,
        [string]$Message
    )

    if ("$Actual" -ne "$Expected") {
        throw "$Message Expected '$Expected' but got '$Actual'."
    }
}

$contractPath = Join-Path $PSScriptRoot 'EnemyMovementReplayContract.cs'
Add-Type -Path $contractPath

$rows = Import-Csv $CsvPath
if ($rows.Count -eq 0) {
    throw "Replay fixture has no rows: $CsvPath"
}

$contract = [CellAO.Tools.EnemyMovementReplay.EnemyMovementReplayContract]::new()
$checked = 0

foreach ($row in $rows) {
    $key = "$($row.case_id)|$($row.npc)"
    $decision = $contract.Apply(
        $key,
        [double]$row.distance,
        [double]$row.attack_range,
        $row.attack_kind,
        $row.event)

    $label = "$($row.case_id) step $($row.step) $($row.packet_evidence)"
    Assert-Equal $decision.State $row.expected_state "$label state mismatch."
    Assert-Equal $decision.Action $row.expected_action "$label action mismatch."
    $checked++
}

Write-Host "[PASS] Enemy movement replay passed ($checked rows)."
