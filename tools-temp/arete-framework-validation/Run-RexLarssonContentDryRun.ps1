[CmdletBinding()]
param(
    [string]$RepoRoot,
    [string]$BuiltDir,
    [string]$ManifestPath,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Equal {
    param(
        [object]$Expected,
        [object]$Actual,
        [string]$Message
    )

    if ($Expected -ne $Actual) {
        throw "$Message Expected='$Expected' Actual='$Actual'."
    }
}

function Assert-GreaterOrEqual {
    param(
        [int]$Minimum,
        [int]$Actual,
        [string]$Message
    )

    if ($Actual -lt $Minimum) {
        throw "$Message Minimum='$Minimum' Actual='$Actual'."
    }
}

function Get-RequiredType {
    param(
        [System.Reflection.Assembly]$Assembly,
        [string]$Name
    )

    $type = $Assembly.GetType($Name, $false)
    Assert-True ($null -ne $type) "Missing framework type: $Name"
    return $type
}

function New-Instance {
    param([System.Type]$Type)

    return [System.Activator]::CreateInstance($Type)
}

function New-InstanceWithArgs {
    param(
        [System.Type]$Type,
        [object[]]$Arguments
    )

    return [System.Activator]::CreateInstance($Type, $Arguments)
}

function Invoke-ObjectMethod {
    param(
        [object]$Object,
        [string]$Name,
        [object[]]$Arguments
    )

    $method = $Object.GetType().GetMethod($Name)
    Assert-True ($null -ne $method) "Missing method $($Object.GetType().FullName).$Name"
    return $method.Invoke($Object, $Arguments)
}

function Get-PropertyValue {
    param(
        [object]$Object,
        [string]$Name
    )

    $property = $Object.GetType().GetProperty($Name)
    Assert-True ($null -ne $property) "Missing property $($Object.GetType().FullName).$Name"
    return $property.GetValue($Object, $null)
}

function Get-ValidationErrors {
    param([object]$Validation)

    $errors = @()
    foreach ($error in (Get-PropertyValue $Validation 'Errors')) {
        $errors += [string]$error
    }

    return @($errors)
}

function Get-ObjectiveProgressRecord {
    param(
        [object[]]$Progress,
        [string]$MissionId,
        [string]$ObjectiveId
    )

    foreach ($record in $Progress) {
        $missionMatches = (Get-PropertyValue $record 'MissionId') -eq $MissionId
        $objectiveMatches = (Get-PropertyValue $record 'ObjectiveId') -eq $ObjectiveId

        if ($missionMatches -and $objectiveMatches) {
            return $record
        }
    }

    throw "Objective progress record was not found for $MissionId / $ObjectiveId."
}

function Write-ObjectiveProgress {
    param([object]$Record)

    $missionId = Get-PropertyValue $Record 'MissionId'
    $objectiveId = Get-PropertyValue $Record 'ObjectiveId'
    $currentCount = Get-PropertyValue $Record 'CurrentCount'
    $requiredCount = Get-PropertyValue $Record 'RequiredCount'
    $completed = Get-PropertyValue $Record 'Completed'
    $matchedCount = Get-PropertyValue $Record 'MatchedEvidenceCount'
    $ignoredCount = Get-PropertyValue $Record 'IgnoredEvidenceCount'
    $lastReference = Get-PropertyValue $Record 'LastMatchedEvidenceReference'

    Write-Host "$missionId $objectiveId progress: $currentCount/$requiredCount completed=$completed matchedEvidence=$matchedCount ignoredEvidence=$ignoredCount"
    Write-Host "  last matched evidence: $lastReference"
}

function Assert-ValidationValid {
    param(
        [object]$Validation,
        [string]$Message
    )

    $isValid = [bool](Get-PropertyValue $Validation 'IsValid')
    $errors = Get-ValidationErrors $Validation
    Assert-True $isValid "$Message Errors: $($errors -join '; ')"
}

function Write-Options {
    param(
        [string]$NodeId,
        [object[]]$Options
    )

    Write-Host "Node $NodeId options:"
    foreach ($option in $Options) {
        $index = Get-PropertyValue $option 'Index'
        $text = Get-PropertyValue $option 'Text'
        Write-Host "  [$index] $text"
    }
}

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
}
else {
    $RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
}

if ([string]::IsNullOrWhiteSpace($BuiltDir)) {
    $BuiltDir = Join-Path $RepoRoot 'AORebirth\Built\Debug'
}
else {
    $BuiltDir = (Resolve-Path -LiteralPath $BuiltDir).Path
}

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $RepoRoot 'AORebirth\Server\ZoneEngine\Content\Arete\rex-larsson\manifest.json'
}

$ManifestPath = (Resolve-Path -LiteralPath $ManifestPath).Path
$zoneProject = Join-Path $RepoRoot 'AORebirth\Server\ZoneEngine\ZoneEngine.csproj'
$zoneEngine = Join-Path $BuiltDir 'ZoneEngine.exe'
$msbuild = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'

if (-not $SkipBuild) {
    Assert-True (Test-Path -LiteralPath $msbuild) "MSBuild was not found at $msbuild"
    Assert-True (Test-Path -LiteralPath $zoneProject) "ZoneEngine project was not found at $zoneProject"
    & $msbuild $zoneProject /t:Build /p:Configuration=Debug /p:BuildProjectReferences=false /p:GenerateSerializationAssemblies=Off /m:1 /nr:false /p:UseSharedCompilation=false /v:minimal | Out-Host
    Assert-True ($LASTEXITCODE -eq 0) "Focused ZoneEngine build failed."
}

Assert-True (Test-Path -LiteralPath $zoneEngine) "ZoneEngine build output was not found at $zoneEngine"

$assembly = [System.Reflection.Assembly]::LoadFrom($zoneEngine)

$aggregateValidatorType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.AreteAggregateContentValidator'
$dialoguePackLoaderType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.Dialogue.DialogueContentPackLoader'
$dialogueRegistryType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.Dialogue.DialogueContentRegistry'
$dialogueSessionServiceType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.Dialogue.DialogueSessionService'
$questRegistryType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.Quests.QuestContentRegistry'
$missionStateServiceType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.Quests.MissionStateService'
$objectivePlaybackServiceType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.Quests.ObjectivePlaybackService'

$aggregateValidator = New-Instance $aggregateValidatorType
$aggregateReport = Invoke-ObjectMethod $aggregateValidator 'ValidateManifestWithReport' @($ManifestPath)
Assert-True ([bool](Get-PropertyValue $aggregateReport 'IsValid')) "Rex aggregate validation failed. Errors: $((Get-ValidationErrors (Get-PropertyValue $aggregateReport 'ValidationResult')) -join '; ')"

Write-Host '[PASS] Rex aggregate validation passed.'
Write-Host "Loaded dialogue packs: $(Get-PropertyValue $aggregateReport 'LoadedDialoguePackCount')"
Write-Host "Loaded quest packs: $(Get-PropertyValue $aggregateReport 'LoadedQuestPackCount')"
Write-Host "Loaded NPC entries: $(Get-PropertyValue $aggregateReport 'LoadedNpcEntryCount')"
Write-Host "Loaded quest definitions: $(Get-PropertyValue $aggregateReport 'LoadedQuestDefinitionCount')"

$dialogueRegistry = New-Instance $dialogueRegistryType
$dialogueRegistryValidation = Invoke-ObjectMethod $dialogueRegistry 'LoadFromManifest' @($ManifestPath)
Assert-ValidationValid $dialogueRegistryValidation 'Rex dialogue registry load should pass.'

$questRegistry = New-Instance $questRegistryType
$questRegistryValidation = Invoke-ObjectMethod $questRegistry 'LoadFromManifest' @($ManifestPath)
Assert-ValidationValid $questRegistryValidation 'Rex quest registry load should pass.'

$dialogueLoader = New-Instance $dialoguePackLoaderType
$dialogueLoadResult = Invoke-ObjectMethod $dialogueLoader 'LoadManifest' @($ManifestPath)
Assert-ValidationValid (Get-PropertyValue $dialogueLoadResult 'Validation') 'Rex dialogue pack load should pass.'

$nodeCount = 0
$optionCount = 0
foreach ($pack in (Get-PropertyValue $dialogueLoadResult 'Packs')) {
    foreach ($npc in (Get-PropertyValue $pack 'Npcs')) {
        foreach ($node in (Get-PropertyValue $npc 'Nodes')) {
            $nodeCount++
            $options = @((Get-PropertyValue $node 'Options'))
            $optionCount += $options.Count
        }
    }
}

Assert-Equal 8 $nodeCount 'Rex dialogue pack should represent eight captured KnuBot answer-list nodes.'
Assert-Equal 15 $optionCount 'Rex dialogue pack should represent fifteen captured visible options.'

$dialogueSessionService = New-InstanceWithArgs $dialogueSessionServiceType @($dialogueRegistry)
$startResult = Invoke-ObjectMethod $dialogueSessionService 'StartSession' @('SimpleChar:782DE568')
Assert-ValidationValid (Get-PropertyValue $startResult 'Validation') 'Rex dialogue session should start.'

$currentResult = $startResult
$session = Get-PropertyValue $currentResult 'Session'
$visitedNodes = @()
$recordedActionCount = 0
$capturedFirstBranchNodes = @(
    'rex_194454_001',
    'rex_194454_002',
    'rex_194454_003',
    'rex_194454_004',
    'rex_194454_005'
)

while ($true) {
    $currentNode = Get-PropertyValue $currentResult 'CurrentNode'
    if ($null -eq $currentNode) {
        break
    }

    $currentNodeId = [string](Get-PropertyValue $currentNode 'Id')
    if ($visitedNodes -notcontains $currentNodeId) {
        $visitedNodes += $currentNodeId
    }

    $options = @((Get-PropertyValue $currentResult 'AvailableOptions'))
    Write-Options $currentNodeId $options

    if ($null -eq $session -or -not [bool](Get-PropertyValue $session 'IsActive')) {
        break
    }

    if ($capturedFirstBranchNodes -notcontains $currentNodeId) {
        break
    }

    $currentResult = Invoke-ObjectMethod $dialogueSessionService 'SelectOption' @($session, 0)
    Assert-ValidationValid (Get-PropertyValue $currentResult 'Validation') "Rex captured index-0 selection should pass at node $currentNodeId."
    $recordedActions = @((Get-PropertyValue $currentResult 'RecordedActions'))
    $recordedActionCount += $recordedActions.Count

    if (-not [bool](Get-PropertyValue $session 'IsActive')) {
        break
    }
}

Assert-True ($visitedNodes -contains 'rex_194454_001') 'Rex dry-run should visit first captured dialogue node.'
Assert-True ($visitedNodes -contains 'rex_194454_005') 'Rex dry-run should reach the first captured terminal Goodbye node.'
Assert-True (-not [bool](Get-PropertyValue $session 'IsActive')) 'Rex first captured dialogue branch should end in memory.'

$missionStateService = New-InstanceWithArgs $missionStateServiceType @($questRegistry)
$missionIds = @(
    'Mission:5514B18C',
    'Mission:5514B18D',
    'Mission:5514B18E'
)

foreach ($missionId in $missionIds) {
    $stateResult = Invoke-ObjectMethod $missionStateService 'GetMissionState' @($missionId)
    Assert-ValidationValid (Get-PropertyValue $stateResult 'Validation') "Mission state query should pass for $missionId."
    $record = Get-PropertyValue $stateResult 'Record'
    $state = (Get-PropertyValue $record 'State').ToString()
    Assert-Equal 'NotStarted' $state "Rex mission should remain not started without decoded mission action execution for $missionId."
    Write-Host "$missionId state: $state"
}

$objectivePlaybackService = New-InstanceWithArgs $objectivePlaybackServiceType @($questRegistry)
$objectivePlaybackResult = Invoke-ObjectMethod $objectivePlaybackService 'ReplayStoredObjectiveEvidence' @()
Assert-ValidationValid (Get-PropertyValue $objectivePlaybackResult 'Validation') 'Rex objective playback should pass.'

$objectivePlaybackObservations = @((Get-PropertyValue $objectivePlaybackResult 'ObservationResults'))
$objectiveProgress = @((Get-PropertyValue $objectivePlaybackResult 'Progress'))

$b18cProgress = Get-ObjectiveProgressRecord `
    $objectiveProgress `
    'Mission:5514B18C' `
    'mission_5514B18C_objective_questfullupdate'
$b18dProgress = Get-ObjectiveProgressRecord `
    $objectiveProgress `
    'Mission:5514B18D' `
    'mission_5514B18D_objective_questfullupdate'
$b18eProgress = Get-ObjectiveProgressRecord `
    $objectiveProgress `
    'Mission:5514B18E' `
    'mission_5514B18E_objective_questfullupdate'

Assert-Equal 5 (Get-PropertyValue $b18cProgress 'CurrentCount') 'B18C objective playback should cap current progress at five robot kills.'
Assert-Equal 5 (Get-PropertyValue $b18cProgress 'RequiredCount') 'B18C objective playback should preserve required robot kill count.'
Assert-True ([bool](Get-PropertyValue $b18cProgress 'Completed')) 'B18C objective playback should complete when five matching robot deaths are observed.'
Assert-GreaterOrEqual 5 ([int](Get-PropertyValue $b18cProgress 'MatchedEvidenceCount')) 'B18C objective playback should have at least five matching robot death evidence rows.'

Assert-Equal 1 (Get-PropertyValue $b18dProgress 'CurrentCount') 'B18D objective playback should record the captured use interaction once.'
Assert-Equal 1 (Get-PropertyValue $b18dProgress 'RequiredCount') 'B18D objective playback should treat the single use interaction as one required observation.'
Assert-True ([bool](Get-PropertyValue $b18dProgress 'Completed')) 'B18D objective playback should complete the in-memory observation when use is observed.'

Assert-Equal 1 (Get-PropertyValue $b18eProgress 'CurrentCount') 'B18E objective playback should record the captured Rex talk once.'
Assert-Equal 1 (Get-PropertyValue $b18eProgress 'RequiredCount') 'B18E objective playback should treat the Rex talk as one required observation.'
Assert-True ([bool](Get-PropertyValue $b18eProgress 'Completed')) 'B18E objective playback should complete the in-memory observation when Rex talk is observed.'

Write-Host "Objective playback observations replayed: $($objectivePlaybackObservations.Count)"
Write-ObjectiveProgress $b18cProgress
Write-ObjectiveProgress $b18dProgress
Write-Host '  Cargo Box identity remains uncertain; Terminal:56D9B4AF is only a temporal candidate.'
Write-ObjectiveProgress $b18eProgress

foreach ($missionId in $missionIds) {
    $stateResult = Invoke-ObjectMethod $missionStateService 'GetMissionState' @($missionId)
    Assert-ValidationValid (Get-PropertyValue $stateResult 'Validation') "Mission state query after objective playback should pass for $missionId."
    $record = Get-PropertyValue $stateResult 'Record'
    $state = (Get-PropertyValue $record 'State').ToString()
    Assert-Equal 'NotStarted' $state "Objective playback must not mutate mission state for $missionId."
}

Write-Host "Visited dialogue nodes: $($visitedNodes -join ', ')"
Write-Host "Recorded safe dialogue actions: $recordedActionCount"
Write-Host 'Mission transitions executed: 0 (captured mission action meanings remain uncertain).'
Write-Host 'Objective playback mutated live character state: false.'
Write-Host '[PASS] Rex Larsson inactive content dry-run passed.'
