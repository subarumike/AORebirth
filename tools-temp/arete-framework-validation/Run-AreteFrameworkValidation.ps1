[CmdletBinding()]
param(
    [string]$RepoRoot,
    [string]$BuiltDir,
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

function Set-PropertyValue {
    param(
        [object]$Object,
        [string]$Name,
        [object]$Value
    )

    $property = $Object.GetType().GetProperty($Name)
    Assert-True ($null -ne $property) "Missing property $($Object.GetType().FullName).$Name"
    $property.SetValue($Object, $Value, $null)
}

function Add-ListItem {
    param(
        [object]$Object,
        [string]$PropertyName,
        [object]$Item
    )

    $property = $Object.GetType().GetProperty($PropertyName)
    Assert-True ($null -ne $property) "Missing property $($Object.GetType().FullName).$PropertyName"
    $list = $property.GetValue($Object, $null)
    if ($null -eq $list) {
        $elementType = $property.PropertyType.GetGenericArguments()[0]
        $listType = ([System.Collections.Generic.List`1]).MakeGenericType($elementType)
        $list = [System.Activator]::CreateInstance($listType)
        $setter = $property.GetSetMethod($true)
        Assert-True ($null -ne $setter) "Cannot initialize list property $($Object.GetType().FullName).$PropertyName"
        $setter.Invoke($Object, [object[]]@($list))
    }

    [void]$list.Add($Item)
}

function New-TypedArray {
    param(
        [System.Type]$ElementType,
        [object[]]$Items
    )

    $array = [System.Array]::CreateInstance($ElementType, $Items.Count)
    for ($index = 0; $index -lt $Items.Count; $index++) {
        $array.SetValue($Items[$index], $index)
    }

    return ,$array
}

function Invoke-RegistryLoad {
    param(
        [object]$Registry,
        [System.Type]$PackType,
        [object[]]$Packs
    )

    $loadMethod = $Registry.GetType().GetMethod('Load')
    Assert-True ($null -ne $loadMethod) "Missing Load method on $($Registry.GetType().FullName)."
    $packArray = New-TypedArray $PackType $Packs
    $arguments = New-Object 'object[]' 1
    $arguments[0] = $packArray
    return $loadMethod.Invoke($Registry, $arguments)
}

function Invoke-RegistryLoadFromFiles {
    param(
        [object]$Registry,
        [string[]]$FilePaths
    )

    $loadMethod = $Registry.GetType().GetMethod('LoadFromFiles')
    Assert-True ($null -ne $loadMethod) "Missing LoadFromFiles method on $($Registry.GetType().FullName)."
    $fileArray = New-TypedArray ([string]) $FilePaths
    $arguments = New-Object 'object[]' 1
    $arguments[0] = $fileArray
    return $loadMethod.Invoke($Registry, $arguments)
}

function Invoke-RegistryLoadFromDirectory {
    param(
        [object]$Registry,
        [string]$DirectoryPath
    )

    $loadMethod = $Registry.GetType().GetMethod('LoadFromDirectory')
    Assert-True ($null -ne $loadMethod) "Missing LoadFromDirectory method on $($Registry.GetType().FullName)."
    $arguments = New-Object 'object[]' 1
    $arguments[0] = $DirectoryPath
    return $loadMethod.Invoke($Registry, $arguments)
}

function Invoke-RegistryLoadFromManifest {
    param(
        [object]$Registry,
        [string]$ManifestPath
    )

    $loadMethod = $Registry.GetType().GetMethod('LoadFromManifest')
    Assert-True ($null -ne $loadMethod) "Missing LoadFromManifest method on $($Registry.GetType().FullName)."
    $arguments = New-Object 'object[]' 1
    $arguments[0] = $ManifestPath
    return $loadMethod.Invoke($Registry, $arguments)
}

function Get-SampleContentRoot {
    return (Join-Path $PSScriptRoot 'sample-content')
}

function Get-SampleContentPath {
    param([string]$RelativePath)

    $path = Join-Path (Get-SampleContentRoot) $RelativePath
    Assert-True (Test-Path -LiteralPath $path) "Missing sample content file: $RelativePath"
    return $path
}

function Get-SampleContentDirectoryPath {
    param([string]$RelativePath)

    $path = Join-Path (Get-SampleContentRoot) $RelativePath
    Assert-True (Test-Path -LiteralPath $path -PathType Container) "Missing sample content directory: $RelativePath"
    return $path
}

function Get-ValidationErrors {
    param([object]$Validation)

    $errors = @()
    foreach ($error in (Get-PropertyValue $Validation 'Errors')) {
        $errors += [string]$error
    }

    return @($errors)
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

function Assert-ValidationContains {
    param(
        [object]$Validation,
        [string]$ExpectedText,
        [string]$Message
    )

    $isValid = [bool](Get-PropertyValue $Validation 'IsValid')
    $errors = Get-ValidationErrors $Validation
    Assert-True (-not $isValid) "$Message Expected validation failure containing '$ExpectedText'."
    $matches = @($errors | Where-Object { $_ -like "*$ExpectedText*" })
    Assert-True ($matches.Count -gt 0) "$Message Missing '$ExpectedText'. Errors: $($errors -join '; ')"
}

function Assert-ResultValid {
    param(
        [object]$Result,
        [string]$Message
    )

    Assert-ValidationValid (Get-PropertyValue $Result 'Validation') $Message
}

function Assert-ResultContains {
    param(
        [object]$Result,
        [string]$ExpectedText,
        [string]$Message
    )

    Assert-ValidationContains (Get-PropertyValue $Result 'Validation') $ExpectedText $Message
}

function Run-Case {
    param(
        [string]$Name,
        [scriptblock]$Case
    )

    try {
        & $Case
        Write-Host "[PASS] $Name"
    }
    catch {
        Write-Host "[FAIL] $Name"
        Write-Host $_.Exception.Message
        Write-Host $_.ScriptStackTrace
        throw
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

$aggregateContentValidatorType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.AreteAggregateContentValidator'
$aggregateValidationReportType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.AreteAggregateValidationReport'
$conditionReferenceValidatorType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.AreteConditionReferenceValidator'

$dialoguePackType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.Dialogue.DialogueContentPack'
$dialoguePackLoaderType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.Dialogue.DialogueContentPackLoader'
$dialogueNpcType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.Dialogue.DialogueNpcEntry'
$dialogueNodeType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.Dialogue.DialogueNode'
$dialogueOptionType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.Dialogue.DialogueOption'
$dialogueActionType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.Dialogue.DialogueAction'
$dialogueRegistryType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.Dialogue.DialogueContentRegistry'
$dialogueActionReferenceValidatorType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.Dialogue.DialogueActionReferenceValidator'
$dialogueMissionActionAdapterType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.Dialogue.DialogueMissionActionAdapter'
$dialogueSessionServiceType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.Dialogue.DialogueSessionService'

$questPackType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.Quests.QuestContentPack'
$questDefinitionType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.Quests.QuestDefinition'
$questStepType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.Quests.QuestStep'
$questObjectiveType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.Quests.QuestObjective'
$questLinkType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.Quests.QuestChainLinkMetadata'
$questRegistryType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.Quests.QuestContentRegistry'
$missionStateServiceType = Get-RequiredType $assembly 'ZoneEngine.Core.Arete.Quests.MissionStateService'

function New-AggregateContentValidator {
    return New-Instance $aggregateContentValidatorType
}

function Invoke-AggregateValidateFiles {
    param(
        [string[]]$DialogueFiles,
        [string[]]$QuestFiles
    )

    $validator = New-AggregateContentValidator
    $validateMethod = $validator.GetType().GetMethod('ValidateFiles')
    Assert-True ($null -ne $validateMethod) "Missing ValidateFiles method on $($validator.GetType().FullName)."
    $arguments = New-Object 'object[]' 2
    $arguments[0] = New-TypedArray ([string]) $DialogueFiles
    $arguments[1] = New-TypedArray ([string]) $QuestFiles
    return $validateMethod.Invoke($validator, $arguments)
}

function Invoke-AggregateValidateManifest {
    param([string]$ManifestPath)

    $validator = New-AggregateContentValidator
    return Invoke-ObjectMethod $validator 'ValidateManifest' @($ManifestPath)
}

function Invoke-AggregateValidateDirectory {
    param([string]$DirectoryPath)

    $validator = New-AggregateContentValidator
    return Invoke-ObjectMethod $validator 'ValidateDirectory' @($DirectoryPath)
}

function Invoke-AggregateValidateFilesReport {
    param(
        [string[]]$DialogueFiles,
        [string[]]$QuestFiles
    )

    $validator = New-AggregateContentValidator
    $validateMethod = $validator.GetType().GetMethod('ValidateFilesWithReport')
    Assert-True ($null -ne $validateMethod) "Missing ValidateFilesWithReport method on $($validator.GetType().FullName)."
    $arguments = New-Object 'object[]' 2
    $arguments[0] = New-TypedArray ([string]) $DialogueFiles
    $arguments[1] = New-TypedArray ([string]) $QuestFiles
    return $validateMethod.Invoke($validator, $arguments)
}

function Invoke-AggregateValidateManifestReport {
    param([string]$ManifestPath)

    $validator = New-AggregateContentValidator
    return Invoke-ObjectMethod $validator 'ValidateManifestWithReport' @($ManifestPath)
}

function Invoke-AggregateValidateDirectoryReport {
    param([string]$DirectoryPath)

    $validator = New-AggregateContentValidator
    return Invoke-ObjectMethod $validator 'ValidateDirectoryWithReport' @($DirectoryPath)
}

function Get-AggregateReportStage {
    param(
        [object]$Report,
        [string]$StageName
    )

    foreach ($stage in (Get-PropertyValue $Report 'Stages')) {
        $name = [string](Get-PropertyValue $stage 'Name')
        if ([string]::Equals($name, $StageName, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $stage
        }
    }

    return $null
}

function Assert-AggregateReportStagePresent {
    param(
        [object]$Report,
        [string]$StageName
    )

    $stage = Get-AggregateReportStage $Report $StageName
    Assert-True ($null -ne $stage) "Aggregate report should include $StageName stage."
    return $stage
}

function Assert-AggregateReportStageErrorCount {
    param(
        [object]$Report,
        [string]$StageName,
        [int]$ExpectedCount,
        [string]$Message
    )

    $stage = Assert-AggregateReportStagePresent $Report $StageName
    Assert-Equal $ExpectedCount (Get-PropertyValue $stage 'ErrorCount') $Message
}

function Invoke-DialogueLoadFromFiles {
    param([string[]]$FilePaths)

    $loader = New-Instance $dialoguePackLoaderType
    $loadMethod = $loader.GetType().GetMethod('LoadFiles')
    Assert-True ($null -ne $loadMethod) "Missing LoadFiles method on $($loader.GetType().FullName)."
    $fileArray = New-TypedArray ([string]) $FilePaths
    $arguments = New-Object 'object[]' 1
    $arguments[0] = $fileArray
    return $loadMethod.Invoke($loader, $arguments)
}

function Invoke-DialogueLoadFromDirectory {
    param([string]$DirectoryPath)

    $loader = New-Instance $dialoguePackLoaderType
    $loadMethod = $loader.GetType().GetMethod('LoadDirectory')
    Assert-True ($null -ne $loadMethod) "Missing LoadDirectory method on $($loader.GetType().FullName)."
    $arguments = New-Object 'object[]' 1
    $arguments[0] = $DirectoryPath
    return $loadMethod.Invoke($loader, $arguments)
}

function Invoke-DialogueLoadFromManifest {
    param([string]$ManifestPath)

    $loader = New-Instance $dialoguePackLoaderType
    $loadMethod = $loader.GetType().GetMethod('LoadManifest')
    Assert-True ($null -ne $loadMethod) "Missing LoadManifest method on $($loader.GetType().FullName)."
    $arguments = New-Object 'object[]' 1
    $arguments[0] = $ManifestPath
    return $loadMethod.Invoke($loader, $arguments)
}

function New-DialogueOption {
    param(
        [string]$Id,
        [int]$Index,
        [AllowNull()][string]$NextNodeId,
        [AllowNull()][string]$TerminalAction
    )

    $option = New-Instance $dialogueOptionType
    Set-PropertyValue $option 'Id' $Id
    Set-PropertyValue $option 'Index' $Index
    Set-PropertyValue $option 'Text' "Synthetic option $Id"
    if ($null -ne $NextNodeId) {
        Set-PropertyValue $option 'NextNodeId' $NextNodeId
    }

    if (-not [string]::IsNullOrWhiteSpace($TerminalAction)) {
        $action = New-Instance $dialogueActionType
        Set-PropertyValue $action 'Type' $TerminalAction
        Add-ListItem $option 'Actions' $action
    }

    return $option
}

function New-DialogueAction {
    param(
        [string]$Type,
        [AllowNull()][string]$QuestId
    )

    $action = New-Instance $dialogueActionType
    Set-PropertyValue $action 'Type' $Type
    if ($null -ne $QuestId) {
        Set-PropertyValue $action 'QuestId' $QuestId
    }

    return $action
}

function New-DialogueOptionWithActions {
    param(
        [string]$Id,
        [int]$Index,
        [AllowNull()][string]$NextNodeId,
        [object[]]$Actions
    )

    $option = New-DialogueOption $Id $Index $NextNodeId $null
    foreach ($action in $Actions) {
        Add-ListItem $option 'Actions' $action
    }

    return $option
}

function New-DialogueNode {
    param(
        [string]$Id,
        [object[]]$Options
    )

    $node = New-Instance $dialogueNodeType
    Set-PropertyValue $node 'Id' $Id
    foreach ($option in $Options) {
        Add-ListItem $node 'Options' $option
    }

    return $node
}

function New-DialogueNpc {
    param(
        [AllowNull()][string]$Identity,
        [AllowNull()][string]$RootNodeId,
        [object[]]$Nodes
    )

    $npc = New-Instance $dialogueNpcType
    Set-PropertyValue $npc 'Id' 'synthetic-npc'
    if ($null -ne $Identity) {
        Set-PropertyValue $npc 'NpcIdentity' $Identity
    }
    Set-PropertyValue $npc 'Name' 'Synthetic NPC'
    Set-PropertyValue $npc 'RootNodeId' $RootNodeId
    foreach ($node in $Nodes) {
        Add-ListItem $npc 'Nodes' $node
    }

    return $npc
}

function New-DialoguePack {
    param(
        [string]$Id,
        [object[]]$Npcs
    )

    $pack = New-Instance $dialoguePackType
    Set-PropertyValue (Get-PropertyValue $pack 'Identity') 'Id' $Id
    foreach ($npc in $Npcs) {
        Add-ListItem $pack 'Npcs' $npc
    }

    return $pack
}

function New-ValidDialoguePack {
    param(
        [string]$PackId,
        [string]$NpcIdentity
    )

    $node = New-DialogueNode 'node-a' @((New-DialogueOption 'option-close' 0 'close' $null))
    $npc = New-DialogueNpc $NpcIdentity 'node-a' @($node)
    return New-DialoguePack $PackId @($npc)
}

function New-DialogueRegistryWithPacks {
    param([object[]]$Packs)

    $registry = New-Instance $dialogueRegistryType
    $validation = Invoke-RegistryLoad $registry $dialoguePackType $Packs
    Assert-ValidationValid $validation 'Synthetic dialogue registry setup should validate.'
    return $registry
}

function New-DialogueSessionService {
    param([object]$Registry)

    return New-InstanceWithArgs -Type $dialogueSessionServiceType -Arguments @($Registry)
}

function New-DialogueMissionActionAdapter {
    param([object]$MissionStateService)

    return New-InstanceWithArgs -Type $dialogueMissionActionAdapterType -Arguments @($MissionStateService)
}

function Invoke-DialogueActionReferenceValidation {
    param(
        [object[]]$Packs,
        [object]$QuestRegistry
    )

    $validateMethod = $dialogueActionReferenceValidatorType.GetMethod('Validate')
    Assert-True ($null -ne $validateMethod) "Missing Validate method on $($dialogueActionReferenceValidatorType.FullName)."
    $packArray = New-TypedArray $dialoguePackType $Packs
    $arguments = New-Object 'object[]' 2
    $arguments[0] = $packArray
    $arguments[1] = $QuestRegistry
    return $validateMethod.Invoke($null, $arguments)
}

function Invoke-DialogueActionReferenceValidationFromLoadResult {
    param(
        [object]$DialogueLoadResult,
        [object]$QuestRegistry
    )

    $packs = @()
    foreach ($pack in (Get-PropertyValue $DialogueLoadResult 'Packs')) {
        $packs += $pack
    }

    return Invoke-DialogueActionReferenceValidation $packs $QuestRegistry
}

function Invoke-FileLoadedActionReferenceValidation {
    param(
        [string[]]$DialogueFiles,
        [string[]]$QuestFiles
    )

    $questRegistry = New-Instance $questRegistryType
    $questValidation = Invoke-RegistryLoadFromFiles $questRegistry $QuestFiles
    Assert-ValidationValid $questValidation 'File-loaded quest registry setup should validate.'

    $dialogueLoadResult = Invoke-DialogueLoadFromFiles $DialogueFiles
    Assert-ValidationValid (Get-PropertyValue $dialogueLoadResult 'Validation') 'File-loaded dialogue pack setup should validate.'

    return Invoke-DialogueActionReferenceValidationFromLoadResult $dialogueLoadResult $questRegistry
}

function Invoke-DirectoryLoadedActionReferenceValidation {
    param(
        [string]$DialogueDirectory,
        [string]$QuestDirectory
    )

    $questRegistry = New-Instance $questRegistryType
    $questValidation = Invoke-RegistryLoadFromDirectory $questRegistry $QuestDirectory
    Assert-ValidationValid $questValidation 'Directory-loaded quest registry setup should validate.'

    $dialogueLoadResult = Invoke-DialogueLoadFromDirectory $DialogueDirectory
    Assert-ValidationValid (Get-PropertyValue $dialogueLoadResult 'Validation') 'Directory-loaded dialogue pack setup should validate.'

    return Invoke-DialogueActionReferenceValidationFromLoadResult $dialogueLoadResult $questRegistry
}

function Invoke-ManifestLoadedActionReferenceValidation {
    param([string]$ManifestPath)

    $questRegistry = New-Instance $questRegistryType
    $questValidation = Invoke-RegistryLoadFromManifest $questRegistry $ManifestPath
    Assert-ValidationValid $questValidation 'Manifest-loaded quest registry setup should validate.'

    $dialogueLoadResult = Invoke-DialogueLoadFromManifest $ManifestPath
    Assert-ValidationValid (Get-PropertyValue $dialogueLoadResult 'Validation') 'Manifest-loaded dialogue pack setup should validate.'

    return Invoke-DialogueActionReferenceValidationFromLoadResult $dialogueLoadResult $questRegistry
}

function Invoke-AdapterExecuteAction {
    param(
        [object]$Adapter,
        [object]$Action
    )

    return Invoke-ObjectMethod $Adapter 'ExecuteAction' @($Action)
}

function Invoke-AdapterExecuteActionsForSession {
    param(
        [object]$Adapter,
        [AllowNull()][object]$Session,
        [object[]]$Actions
    )

    $actionArray = New-TypedArray $dialogueActionType $Actions
    $arguments = New-Object 'object[]' 2
    $arguments[0] = $Session
    $arguments[1] = $actionArray
    return (Invoke-ObjectMethod $Adapter 'ExecuteActionsForSession' $arguments)
}

function New-DialogueSessionPack {
    param([string]$NpcIdentity)

    $nodeA = New-DialogueNode 'node-a' @(
        (New-DialogueOption 'option-forward' 0 'node-b' $null),
        (New-DialogueOption 'option-close' 1 'close' $null)
    )
    $nodeB = New-DialogueNode 'node-b' @((New-DialogueOption 'option-close-b' 0 'close' $null))
    $npc = New-DialogueNpc $NpcIdentity 'node-a' @($nodeA, $nodeB)
    return New-DialoguePack 'dialogue-session-pack-a' @($npc)
}

function New-DialogueActionReferencePack {
    param(
        [string]$PackId,
        [string]$NpcIdentity,
        [object[]]$Actions
    )

    $node = New-DialogueNode 'node-a' @(
        (New-DialogueOptionWithActions 'option-action' 0 'close' $Actions)
    )
    $npc = New-DialogueNpc $NpcIdentity 'node-a' @($node)
    return New-DialoguePack $PackId @($npc)
}

function New-QuestObjective {
    param([string]$ObjectiveId)

    $objective = New-Instance $questObjectiveType
    Set-PropertyValue $objective 'ObjectiveId' $ObjectiveId
    Set-PropertyValue $objective 'Type' 'synthetic'
    return $objective
}

function New-QuestStep {
    param(
        [AllowNull()][string]$StepId,
        [object[]]$Objectives
    )

    $step = New-Instance $questStepType
    if ($null -ne $StepId) {
        Set-PropertyValue $step 'StepId' $StepId
    }
    Set-PropertyValue $step 'Name' 'Synthetic step'
    foreach ($objective in $Objectives) {
        Add-ListItem $step 'Objectives' $objective
    }

    return $step
}

function New-QuestDefinition {
    param(
        [AllowNull()][string]$QuestId,
        [AllowNull()][string]$InitialStepId,
        [object[]]$Steps
    )

    $quest = New-Instance $questDefinitionType
    if ($null -ne $QuestId) {
        Set-PropertyValue $quest 'QuestId' $QuestId
    }
    if ($null -ne $InitialStepId) {
        Set-PropertyValue $quest 'InitialStepId' $InitialStepId
    }
    foreach ($step in $Steps) {
        Add-ListItem $quest 'Steps' $step
    }

    return $quest
}

function New-QuestLink {
    param(
        [AllowNull()][string]$FromQuestId,
        [AllowNull()][string]$FromStepId,
        [AllowNull()][string]$ToQuestId,
        [AllowNull()][string]$ToStepId
    )

    $link = New-Instance $questLinkType
    Set-PropertyValue $link 'Id' 'synthetic-link'
    if ($null -ne $FromQuestId) {
        Set-PropertyValue $link 'FromQuestId' $FromQuestId
    }
    if ($null -ne $FromStepId) {
        Set-PropertyValue $link 'FromStepId' $FromStepId
    }
    if ($null -ne $ToQuestId) {
        Set-PropertyValue $link 'ToQuestId' $ToQuestId
    }
    if ($null -ne $ToStepId) {
        Set-PropertyValue $link 'ToStepId' $ToStepId
    }
    Set-PropertyValue $link 'Relationship' 'synthetic'
    return $link
}

function New-QuestPack {
    param(
        [string]$Id,
        [object[]]$Quests,
        [object[]]$Links
    )

    $pack = New-Instance $questPackType
    Set-PropertyValue (Get-PropertyValue $pack 'Identity') 'Id' $Id
    foreach ($quest in $Quests) {
        Add-ListItem $pack 'Quests' $quest
    }
    foreach ($link in $Links) {
        Add-ListItem $pack 'Links' $link
    }

    return $pack
}

function New-ValidQuestDefinition {
    param(
        [string]$QuestId,
        [string]$StepId
    )

    $step = New-QuestStep $StepId @()
    return New-QuestDefinition $QuestId $StepId @($step)
}

function New-ValidQuestPack {
    param(
        [string]$PackId,
        [string]$QuestId
    )

    return New-QuestPack $PackId @((New-ValidQuestDefinition $QuestId 'step-a')) @()
}

function New-QuestRegistryWithPacks {
    param([object[]]$Packs)

    $registry = New-Instance $questRegistryType
    $validation = Invoke-RegistryLoad $registry $questPackType $Packs
    Assert-ValidationValid $validation 'Synthetic quest registry setup should validate.'
    return $registry
}

function New-MissionStateService {
    param([object]$Registry)

    return New-InstanceWithArgs -Type $missionStateServiceType -Arguments @($Registry)
}

function Get-MissionStateName {
    param([object]$Result)

    $record = Get-PropertyValue $Result 'Record'
    Assert-True ($null -ne $record) 'Mission state result should include a record.'
    return [string](Get-PropertyValue $record 'State')
}

function Get-MissionStateNameFromService {
    param(
        [object]$MissionStateService,
        [string]$QuestId
    )

    $result = Invoke-ObjectMethod $MissionStateService 'GetMissionState' @($QuestId)
    Assert-ResultValid $result "Mission state query should pass for $QuestId."
    return (Get-MissionStateName $result)
}

function Assert-AdapterRecordedAction {
    param(
        [object]$AdapterResult,
        [bool]$ExpectedApplied,
        [string]$Message
    )

    $actionResults = @((Get-PropertyValue $AdapterResult 'ActionResults'))
    Assert-True ($actionResults.Count -gt 0) "$Message Adapter result should record at least one action."
    $recordedAction = Get-PropertyValue $actionResults[0] 'RecordedAction'
    Assert-Equal $ExpectedApplied ([bool](Get-PropertyValue $recordedAction 'WasApplied')) "$Message WasApplied mismatch."
    Assert-Equal $false ([bool](Get-PropertyValue $recordedAction 'MutatedCharacterState')) "$Message Adapter must not mutate character state."
}

function Get-ResultCollectionCount {
    param(
        [object]$Result,
        [string]$PropertyName
    )

    return @((Get-PropertyValue $Result $PropertyName)).Count
}

Run-Case 'dialogue valid empty registry' {
    $registry = New-Instance $dialogueRegistryType
    $validation = Invoke-RegistryLoad $registry $dialoguePackType @()
    Assert-ValidationValid $validation 'Empty dialogue registry should validate.'
    Assert-Equal 0 (Get-PropertyValue $registry 'PackCount') 'Empty dialogue registry should load zero packs.'
    Assert-Equal 0 (Get-PropertyValue $registry 'NpcCount') 'Empty dialogue registry should load zero NPCs.'
}

Run-Case 'dialogue duplicate pack ids' {
    $registry = New-Instance $dialogueRegistryType
    $validation = Invoke-RegistryLoad $registry $dialoguePackType @(
        (New-DialoguePack 'dialogue-pack-a' @()),
        (New-DialoguePack 'dialogue-pack-a' @())
    )
    Assert-ValidationContains $validation 'duplicate dialogue content pack id' 'Duplicate dialogue pack IDs should fail.'
}

Run-Case 'dialogue duplicate NPC identities' {
    $registry = New-Instance $dialogueRegistryType
    $validation = Invoke-RegistryLoad $registry $dialoguePackType @(
        (New-ValidDialoguePack 'dialogue-pack-a' 'SimpleChar:test-a'),
        (New-ValidDialoguePack 'dialogue-pack-b' 'SimpleChar:test-a')
    )
    Assert-ValidationContains $validation 'duplicate NPC identity' 'Duplicate NPC identities should fail.'
}

Run-Case 'dialogue missing NPC identity' {
    $registry = New-Instance $dialogueRegistryType
    $node = New-DialogueNode 'node-a' @((New-DialogueOption 'option-close' 0 'close' $null))
    $pack = New-DialoguePack 'dialogue-pack-a' @((New-DialogueNpc $null 'node-a' @($node)))
    $validation = Invoke-RegistryLoad $registry $dialoguePackType @($pack)
    Assert-ValidationContains $validation 'missing NPC identity' 'Missing NPC identity should fail.'
}

Run-Case 'dialogue missing node target' {
    $registry = New-Instance $dialogueRegistryType
    $node = New-DialogueNode 'node-a' @((New-DialogueOption 'option-a' 0 $null $null))
    $pack = New-DialoguePack 'dialogue-pack-a' @((New-DialogueNpc 'SimpleChar:test-a' 'node-a' @($node)))
    $validation = Invoke-RegistryLoad $registry $dialoguePackType @($pack)
    Assert-ValidationContains $validation 'missing dialogue node target' 'Missing dialogue node target should fail.'
}

Run-Case 'dialogue valid node target' {
    $registry = New-Instance $dialogueRegistryType
    $nodeA = New-DialogueNode 'node-a' @((New-DialogueOption 'option-a' 0 'node-b' $null))
    $nodeB = New-DialogueNode 'node-b' @((New-DialogueOption 'option-close' 0 'close' $null))
    $pack = New-DialoguePack 'dialogue-pack-a' @((New-DialogueNpc 'SimpleChar:test-a' 'node-a' @($nodeA, $nodeB)))
    $validation = Invoke-RegistryLoad $registry $dialoguePackType @($pack)
    Assert-ValidationValid $validation 'Valid dialogue node target should pass.'
    Assert-Equal 1 (Get-PropertyValue $registry 'PackCount') 'Valid dialogue registry should load one pack.'
    Assert-Equal 1 (Get-PropertyValue $registry 'NpcCount') 'Valid dialogue registry should load one NPC.'
}

Run-Case 'dialogue valid file-loaded pack' {
    $registry = New-Instance $dialogueRegistryType
    $validation = Invoke-RegistryLoadFromFiles $registry @(
        (Get-SampleContentPath 'dialogue\valid-pack-a.json')
    )
    Assert-ValidationValid $validation 'Valid file-loaded dialogue pack should pass.'
    Assert-Equal 1 (Get-PropertyValue $registry 'PackCount') 'Valid file-loaded dialogue registry should load one pack.'
    Assert-Equal 1 (Get-PropertyValue $registry 'NpcCount') 'Valid file-loaded dialogue registry should load one NPC.'
}

Run-Case 'dialogue invalid JSON parse failure' {
    $registry = New-Instance $dialogueRegistryType
    $validation = Invoke-RegistryLoadFromFiles $registry @(
        (Get-SampleContentPath 'dialogue\invalid-json.json')
    )
    Assert-ValidationContains $validation 'failed to parse JSON content file' 'Invalid dialogue JSON should fail parsing.'
}

Run-Case 'dialogue duplicate pack ids from files' {
    $registry = New-Instance $dialogueRegistryType
    $validation = Invoke-RegistryLoadFromFiles $registry @(
        (Get-SampleContentPath 'dialogue\valid-pack-a.json'),
        (Get-SampleContentPath 'dialogue\duplicate-pack-id.json')
    )
    Assert-ValidationContains $validation 'duplicate dialogue content pack id' 'Duplicate file-loaded dialogue pack IDs should fail.'
}

Run-Case 'dialogue missing NPC identity from file' {
    $registry = New-Instance $dialogueRegistryType
    $validation = Invoke-RegistryLoadFromFiles $registry @(
        (Get-SampleContentPath 'dialogue\missing-npc-identity.json')
    )
    Assert-ValidationContains $validation 'missing NPC identity' 'File-loaded missing NPC identity should fail.'
}

Run-Case 'dialogue missing node target from file' {
    $registry = New-Instance $dialogueRegistryType
    $validation = Invoke-RegistryLoadFromFiles $registry @(
        (Get-SampleContentPath 'dialogue\missing-node-target.json')
    )
    Assert-ValidationContains $validation 'missing dialogue node target' 'File-loaded missing dialogue node target should fail.'
}

Run-Case 'dialogue directory loads all valid packs' {
    $registry = New-Instance $dialogueRegistryType
    $validation = Invoke-RegistryLoadFromDirectory $registry (Get-SampleContentDirectoryPath 'dialogue-directory-valid')
    Assert-ValidationValid $validation 'Valid dialogue directory should pass.'
    Assert-Equal 2 (Get-PropertyValue $registry 'PackCount') 'Valid dialogue directory should load two packs.'
    Assert-Equal 2 (Get-PropertyValue $registry 'NpcCount') 'Valid dialogue directory should load two NPCs.'
}

Run-Case 'dialogue directory missing' {
    $registry = New-Instance $dialogueRegistryType
    $missingDirectory = Join-Path (Get-SampleContentRoot) 'missing-directory'
    $validation = Invoke-RegistryLoadFromDirectory $registry $missingDirectory
    Assert-ValidationContains $validation 'JSON content directory was not found' 'Missing dialogue directory should fail.'
}

Run-Case 'dialogue empty directory' {
    $registry = New-Instance $dialogueRegistryType
    $validation = Invoke-RegistryLoadFromDirectory $registry (Get-SampleContentDirectoryPath 'empty-directory')
    Assert-ValidationContains $validation 'did not contain JSON content files' 'Empty dialogue directory should fail.'
}

Run-Case 'manifest missing' {
    $registry = New-Instance $dialogueRegistryType
    $missingManifest = Join-Path (Get-SampleContentRoot) 'manifests\missing-manifest.json'
    $validation = Invoke-RegistryLoadFromManifest $registry $missingManifest
    Assert-ValidationContains $validation 'content manifest file was not found' 'Missing manifest should fail.'
}

Run-Case 'manifest invalid JSON' {
    $registry = New-Instance $dialogueRegistryType
    $validation = Invoke-RegistryLoadFromManifest $registry (Get-SampleContentPath 'manifests\invalid-json.json')
    Assert-ValidationContains $validation 'failed to parse JSON manifest' 'Invalid manifest JSON should fail parsing.'
}

Run-Case 'manifest references missing file' {
    $registry = New-Instance $dialogueRegistryType
    $validation = Invoke-RegistryLoadFromManifest $registry (Get-SampleContentPath 'manifests\missing-file-reference.json')
    Assert-ValidationContains $validation 'JSON content file was not found' 'Missing manifest-referenced file should fail.'
}

Run-Case 'manifest loads valid dialogue and quest files' {
    $dialogueRegistry = New-Instance $dialogueRegistryType
    $questRegistry = New-Instance $questRegistryType
    $manifest = Get-SampleContentPath 'manifests\valid-manifest.json'

    $dialogueValidation = Invoke-RegistryLoadFromManifest $dialogueRegistry $manifest
    $questValidation = Invoke-RegistryLoadFromManifest $questRegistry $manifest

    Assert-ValidationValid $dialogueValidation 'Valid manifest-loaded dialogue pack should pass.'
    Assert-ValidationValid $questValidation 'Valid manifest-loaded quest pack should pass.'
    Assert-Equal 1 (Get-PropertyValue $dialogueRegistry 'PackCount') 'Manifest-loaded dialogue registry should load one pack.'
    Assert-Equal 1 (Get-PropertyValue $dialogueRegistry 'NpcCount') 'Manifest-loaded dialogue registry should load one NPC.'
    Assert-Equal 1 (Get-PropertyValue $questRegistry 'PackCount') 'Manifest-loaded quest registry should load one pack.'
    Assert-Equal 1 (Get-PropertyValue $questRegistry 'QuestCount') 'Manifest-loaded quest registry should load one quest.'
}

Run-Case 'manifest reports duplicate pack IDs' {
    $dialogueRegistry = New-Instance $dialogueRegistryType
    $questRegistry = New-Instance $questRegistryType
    $manifest = Get-SampleContentPath 'manifests\duplicate-pack-ids.json'

    $dialogueValidation = Invoke-RegistryLoadFromManifest $dialogueRegistry $manifest
    $questValidation = Invoke-RegistryLoadFromManifest $questRegistry $manifest

    Assert-ValidationContains $dialogueValidation 'duplicate dialogue content pack id' 'Manifest duplicate dialogue pack IDs should fail.'
    Assert-ValidationContains $questValidation 'duplicate quest content pack id' 'Manifest duplicate quest pack IDs should fail.'
}

Run-Case 'manifest preserves validation errors from referenced files' {
    $dialogueRegistry = New-Instance $dialogueRegistryType
    $questRegistry = New-Instance $questRegistryType
    $manifest = Get-SampleContentPath 'manifests\preserve-validation-errors.json'

    $dialogueValidation = Invoke-RegistryLoadFromManifest $dialogueRegistry $manifest
    $questValidation = Invoke-RegistryLoadFromManifest $questRegistry $manifest

    Assert-ValidationContains $dialogueValidation 'missing NPC identity' 'Manifest-loaded dialogue validation errors should be preserved.'
    Assert-ValidationContains $questValidation 'missing quest step id' 'Manifest-loaded quest validation errors should be preserved.'
}

Run-Case 'dialogue session start valid session' {
    $registry = New-DialogueRegistryWithPacks @((New-DialogueSessionPack 'SimpleChar:session-a'))
    $service = New-DialogueSessionService $registry
    $result = Invoke-ObjectMethod $service 'StartSession' @('SimpleChar:session-a')
    Assert-ResultValid $result 'Valid dialogue session should start.'
    $session = Get-PropertyValue $result 'Session'
    Assert-True ([bool](Get-PropertyValue $session 'IsActive')) 'Started dialogue session should be active.'
    Assert-Equal 'node-a' (Get-PropertyValue $session 'CurrentNodeId') 'Started session should resolve the root node.'
}

Run-Case 'dialogue session start missing NPC fails' {
    $registry = New-DialogueRegistryWithPacks @()
    $service = New-DialogueSessionService $registry
    $result = Invoke-ObjectMethod $service 'StartSession' @('SimpleChar:missing-session')
    Assert-ResultContains $result 'dialogue NPC was not found' 'Missing dialogue NPC should fail cleanly.'
}

Run-Case 'dialogue session start NPC with missing start node fails' {
    $node = New-DialogueNode 'node-a' @((New-DialogueOption 'option-close' 0 'close' $null))
    $pack = New-DialoguePack 'dialogue-session-missing-start-pack' @(
        (New-DialogueNpc 'SimpleChar:session-missing-start' $null @($node))
    )
    $registry = New-DialogueRegistryWithPacks @($pack)
    $service = New-DialogueSessionService $registry
    $result = Invoke-ObjectMethod $service 'StartSession' @('SimpleChar:session-missing-start')
    Assert-ResultContains $result 'missing start dialogue node' 'Missing start node should fail cleanly.'
}

Run-Case 'dialogue session list options on node' {
    $registry = New-DialogueRegistryWithPacks @((New-DialogueSessionPack 'SimpleChar:session-list'))
    $service = New-DialogueSessionService $registry
    $result = Invoke-ObjectMethod $service 'StartSession' @('SimpleChar:session-list')
    Assert-ResultValid $result 'Dialogue session should start before listing options.'
    Assert-Equal 2 (Get-ResultCollectionCount $result 'AvailableOptions') 'Start node should expose two available options.'
}

Run-Case 'dialogue session select valid option advances node' {
    $registry = New-DialogueRegistryWithPacks @((New-DialogueSessionPack 'SimpleChar:session-advance'))
    $service = New-DialogueSessionService $registry
    $start = Invoke-ObjectMethod $service 'StartSession' @('SimpleChar:session-advance')
    Assert-ResultValid $start 'Dialogue session should start before selecting.'
    $session = Get-PropertyValue $start 'Session'
    $result = Invoke-ObjectMethod $service 'SelectOption' @($session, 0)
    Assert-ResultValid $result 'Selecting a valid dialogue option should pass.'
    Assert-Equal 'node-b' (Get-PropertyValue $session 'CurrentNodeId') 'Valid option should advance to node-b.'
}

Run-Case 'dialogue session select invalid option fails' {
    $registry = New-DialogueRegistryWithPacks @((New-DialogueSessionPack 'SimpleChar:session-invalid-option'))
    $service = New-DialogueSessionService $registry
    $start = Invoke-ObjectMethod $service 'StartSession' @('SimpleChar:session-invalid-option')
    Assert-ResultValid $start 'Dialogue session should start before invalid selection.'
    $result = Invoke-ObjectMethod $service 'SelectOption' @((Get-PropertyValue $start 'Session'), 99)
    Assert-ResultContains $result 'dialogue option was not available' 'Invalid dialogue option should fail cleanly.'
}

Run-Case 'dialogue session terminal end node closes session' {
    $registry = New-DialogueRegistryWithPacks @((New-DialogueSessionPack 'SimpleChar:session-close'))
    $service = New-DialogueSessionService $registry
    $start = Invoke-ObjectMethod $service 'StartSession' @('SimpleChar:session-close')
    Assert-ResultValid $start 'Dialogue session should start before closing.'
    $session = Get-PropertyValue $start 'Session'
    $result = Invoke-ObjectMethod $service 'SelectOption' @($session, 1)
    Assert-ResultValid $result 'Selecting a terminal dialogue option should pass.'
    Assert-True (-not [bool](Get-PropertyValue $session 'IsActive')) 'Terminal option should close the session.'
}

Run-Case 'quest valid empty registry' {
    $registry = New-Instance $questRegistryType
    $validation = Invoke-RegistryLoad $registry $questPackType @()
    Assert-ValidationValid $validation 'Empty quest registry should validate.'
    Assert-Equal 0 (Get-PropertyValue $registry 'PackCount') 'Empty quest registry should load zero packs.'
    Assert-Equal 0 (Get-PropertyValue $registry 'QuestCount') 'Empty quest registry should load zero quests.'
}

Run-Case 'quest duplicate pack ids' {
    $registry = New-Instance $questRegistryType
    $validation = Invoke-RegistryLoad $registry $questPackType @(
        (New-QuestPack 'quest-pack-a' @() @()),
        (New-QuestPack 'quest-pack-a' @() @())
    )
    Assert-ValidationContains $validation 'duplicate quest content pack id' 'Duplicate quest pack IDs should fail.'
}

Run-Case 'quest duplicate quest ids' {
    $registry = New-Instance $questRegistryType
    $pack = New-QuestPack 'quest-pack-a' @(
        (New-ValidQuestDefinition 'mission-a' 'step-a'),
        (New-ValidQuestDefinition 'mission-a' 'step-b')
    ) @()
    $validation = Invoke-RegistryLoad $registry $questPackType @($pack)
    Assert-ValidationContains $validation 'duplicate quest id' 'Duplicate quest IDs should fail.'
}

Run-Case 'quest missing quest id' {
    $registry = New-Instance $questRegistryType
    $pack = New-QuestPack 'quest-pack-a' @((New-QuestDefinition $null $null @())) @()
    $validation = Invoke-RegistryLoad $registry $questPackType @($pack)
    Assert-ValidationContains $validation 'missing quest id' 'Missing quest ID should fail.'
}

Run-Case 'quest missing step id' {
    $registry = New-Instance $questRegistryType
    $pack = New-QuestPack 'quest-pack-a' @((New-QuestDefinition 'mission-a' $null @((New-QuestStep $null @())))) @()
    $validation = Invoke-RegistryLoad $registry $questPackType @($pack)
    Assert-ValidationContains $validation 'missing quest step id' 'Missing quest step ID should fail.'
}

Run-Case 'quest duplicate step ids' {
    $registry = New-Instance $questRegistryType
    $pack = New-QuestPack 'quest-pack-a' @(
        (New-QuestDefinition 'mission-a' 'step-a' @(
            (New-QuestStep 'step-a' @()),
            (New-QuestStep 'step-a' @())
        ))
    ) @()
    $validation = Invoke-RegistryLoad $registry $questPackType @($pack)
    Assert-ValidationContains $validation 'duplicate quest step id' 'Duplicate quest step IDs should fail.'
}

Run-Case 'quest duplicate objective ids' {
    $registry = New-Instance $questRegistryType
    $step = New-QuestStep 'step-a' @(
        (New-QuestObjective 'objective-a'),
        (New-QuestObjective 'objective-a')
    )
    $pack = New-QuestPack 'quest-pack-a' @((New-QuestDefinition 'mission-a' 'step-a' @($step))) @()
    $validation = Invoke-RegistryLoad $registry $questPackType @($pack)
    Assert-ValidationContains $validation 'duplicate quest objective id' 'Duplicate quest objective IDs should fail.'
}

Run-Case 'quest missing chain endpoint' {
    $registry = New-Instance $questRegistryType
    $pack = New-QuestPack 'quest-pack-a' @((New-ValidQuestDefinition 'mission-a' 'step-a')) @(
        (New-QuestLink 'mission-a' 'step-a' $null $null)
    )
    $validation = Invoke-RegistryLoad $registry $questPackType @($pack)
    Assert-ValidationContains $validation 'missing to quest id' 'Missing quest chain endpoint should fail.'
}

Run-Case 'quest valid chain endpoint' {
    $registry = New-Instance $questRegistryType
    $pack = New-QuestPack 'quest-pack-a' @(
        (New-ValidQuestDefinition 'mission-a' 'step-a'),
        (New-ValidQuestDefinition 'mission-b' 'step-b')
    ) @((New-QuestLink 'mission-a' 'step-a' 'mission-b' 'step-b'))
    $validation = Invoke-RegistryLoad $registry $questPackType @($pack)
    Assert-ValidationValid $validation 'Valid quest chain endpoint should pass.'
    Assert-Equal 1 (Get-PropertyValue $registry 'PackCount') 'Valid quest registry should load one pack.'
    Assert-Equal 2 (Get-PropertyValue $registry 'QuestCount') 'Valid quest registry should load two quests.'
}

Run-Case 'mission state initial mission state is not started' {
    $registry = New-QuestRegistryWithPacks @((New-ValidQuestPack 'mission-state-pack-a' 'mission-state-a'))
    $service = New-MissionStateService $registry
    $result = Invoke-ObjectMethod $service 'GetMissionState' @('mission-state-a')
    Assert-ResultValid $result 'Known mission state query should pass.'
    Assert-Equal 'NotStarted' (Get-MissionStateName $result) 'Initial mission state should be NotStarted.'
}

Run-Case 'mission state offer mission records offered state' {
    $registry = New-QuestRegistryWithPacks @((New-ValidQuestPack 'mission-state-pack-offer' 'mission-state-offer'))
    $service = New-MissionStateService $registry
    $result = Invoke-ObjectMethod $service 'OfferMission' @('mission-state-offer')
    Assert-ResultValid $result 'Offering a known mission should pass.'
    Assert-Equal 'Offered' (Get-MissionStateName $result) 'Offered mission should record Offered state.'
}

Run-Case 'mission state accept mission records active state' {
    $registry = New-QuestRegistryWithPacks @((New-ValidQuestPack 'mission-state-pack-accept' 'mission-state-accept'))
    $service = New-MissionStateService $registry
    [void](Invoke-ObjectMethod $service 'OfferMission' @('mission-state-accept'))
    $result = Invoke-ObjectMethod $service 'AcceptMission' @('mission-state-accept')
    Assert-ResultValid $result 'Accepting an offered mission should pass.'
    Assert-Equal 'Active' (Get-MissionStateName $result) 'Accepted mission should record Active state.'
}

Run-Case 'mission state complete mission records completed state' {
    $registry = New-QuestRegistryWithPacks @((New-ValidQuestPack 'mission-state-pack-complete' 'mission-state-complete'))
    $service = New-MissionStateService $registry
    [void](Invoke-ObjectMethod $service 'OfferMission' @('mission-state-complete'))
    [void](Invoke-ObjectMethod $service 'AcceptMission' @('mission-state-complete'))
    $result = Invoke-ObjectMethod $service 'CompleteMission' @('mission-state-complete')
    Assert-ResultValid $result 'Completing an active mission should pass.'
    Assert-Equal 'Completed' (Get-MissionStateName $result) 'Completed mission should record Completed state.'
}

Run-Case 'mission state complete mission before active fails' {
    $registry = New-QuestRegistryWithPacks @((New-ValidQuestPack 'mission-state-pack-complete-early' 'mission-state-complete-early'))
    $service = New-MissionStateService $registry
    $result = Invoke-ObjectMethod $service 'CompleteMission' @('mission-state-complete-early')
    Assert-ResultContains $result 'mission is not active' 'Completing before Active state should fail.'
}

Run-Case 'mission state chain link unlocks next mission only after prerequisite completion' {
    $missionA = New-ValidQuestDefinition 'mission-chain-a' 'step-a'
    $missionB = New-ValidQuestDefinition 'mission-chain-b' 'step-b'
    $link = New-QuestLink 'mission-chain-a' 'step-a' 'mission-chain-b' 'step-b'
    $registry = New-QuestRegistryWithPacks @((New-QuestPack 'mission-chain-pack' @($missionA, $missionB) @($link)))
    $service = New-MissionStateService $registry

    $blocked = Invoke-ObjectMethod $service 'OfferMission' @('mission-chain-b')
    Assert-ResultContains $blocked 'mission prerequisite is not completed' 'Linked mission should be blocked before prerequisite completion.'

    [void](Invoke-ObjectMethod $service 'OfferMission' @('mission-chain-a'))
    [void](Invoke-ObjectMethod $service 'AcceptMission' @('mission-chain-a'))
    [void](Invoke-ObjectMethod $service 'CompleteMission' @('mission-chain-a'))

    $unlocked = Invoke-ObjectMethod $service 'OfferMission' @('mission-chain-b')
    Assert-ResultValid $unlocked 'Linked mission should be offerable after prerequisite completion.'
    Assert-Equal 'Offered' (Get-MissionStateName $unlocked) 'Unlocked linked mission should record Offered state.'
}

Run-Case 'mission state unknown mission ID fails cleanly' {
    $registry = New-QuestRegistryWithPacks @((New-ValidQuestPack 'mission-state-pack-known' 'mission-state-known'))
    $service = New-MissionStateService $registry
    $result = Invoke-ObjectMethod $service 'OfferMission' @('mission-state-unknown')
    Assert-ResultContains $result 'mission was not found' 'Unknown mission ID should fail cleanly.'
}

Run-Case 'dialogue action offers mission' {
    $registry = New-QuestRegistryWithPacks @((New-ValidQuestPack 'adapter-pack-offer' 'adapter-offer'))
    $missionService = New-MissionStateService $registry
    $adapter = New-DialogueMissionActionAdapter $missionService
    $result = Invoke-AdapterExecuteAction $adapter (New-DialogueAction 'OfferMission' 'adapter-offer')
    Assert-ResultValid $result 'Dialogue OfferMission action should pass.'
    Assert-AdapterRecordedAction $result $true 'Dialogue OfferMission action'
    Assert-Equal 'Offered' (Get-MissionStateNameFromService $missionService 'adapter-offer') 'Dialogue OfferMission should record Offered state.'
}

Run-Case 'dialogue action accepts offered mission' {
    $registry = New-QuestRegistryWithPacks @((New-ValidQuestPack 'adapter-pack-accept' 'adapter-accept'))
    $missionService = New-MissionStateService $registry
    [void](Invoke-ObjectMethod $missionService 'OfferMission' @('adapter-accept'))
    $adapter = New-DialogueMissionActionAdapter $missionService
    $result = Invoke-AdapterExecuteAction $adapter (New-DialogueAction 'AcceptMission' 'adapter-accept')
    Assert-ResultValid $result 'Dialogue AcceptMission action should pass for offered mission.'
    Assert-AdapterRecordedAction $result $true 'Dialogue AcceptMission action'
    Assert-Equal 'Active' (Get-MissionStateNameFromService $missionService 'adapter-accept') 'Dialogue AcceptMission should record Active state.'
}

Run-Case 'dialogue action completes active mission' {
    $registry = New-QuestRegistryWithPacks @((New-ValidQuestPack 'adapter-pack-complete' 'adapter-complete'))
    $missionService = New-MissionStateService $registry
    [void](Invoke-ObjectMethod $missionService 'OfferMission' @('adapter-complete'))
    [void](Invoke-ObjectMethod $missionService 'AcceptMission' @('adapter-complete'))
    $adapter = New-DialogueMissionActionAdapter $missionService
    $result = Invoke-AdapterExecuteAction $adapter (New-DialogueAction 'CompleteMission' 'adapter-complete')
    Assert-ResultValid $result 'Dialogue CompleteMission action should pass for active mission.'
    Assert-AdapterRecordedAction $result $true 'Dialogue CompleteMission action'
    Assert-Equal 'Completed' (Get-MissionStateNameFromService $missionService 'adapter-complete') 'Dialogue CompleteMission should record Completed state.'
}

Run-Case 'dialogue action fails active mission' {
    $registry = New-QuestRegistryWithPacks @((New-ValidQuestPack 'adapter-pack-fail' 'adapter-fail'))
    $missionService = New-MissionStateService $registry
    [void](Invoke-ObjectMethod $missionService 'OfferMission' @('adapter-fail'))
    [void](Invoke-ObjectMethod $missionService 'AcceptMission' @('adapter-fail'))
    $adapter = New-DialogueMissionActionAdapter $missionService
    $result = Invoke-AdapterExecuteAction $adapter (New-DialogueAction 'FailMission' 'adapter-fail')
    Assert-ResultValid $result 'Dialogue FailMission action should pass for active mission.'
    Assert-AdapterRecordedAction $result $true 'Dialogue FailMission action'
    Assert-Equal 'Failed' (Get-MissionStateNameFromService $missionService 'adapter-fail') 'Dialogue FailMission should record Failed state.'
}

Run-Case 'dialogue action abandons active mission' {
    $registry = New-QuestRegistryWithPacks @((New-ValidQuestPack 'adapter-pack-abandon' 'adapter-abandon'))
    $missionService = New-MissionStateService $registry
    [void](Invoke-ObjectMethod $missionService 'OfferMission' @('adapter-abandon'))
    [void](Invoke-ObjectMethod $missionService 'AcceptMission' @('adapter-abandon'))
    $adapter = New-DialogueMissionActionAdapter $missionService
    $result = Invoke-AdapterExecuteAction $adapter (New-DialogueAction 'AbandonMission' 'adapter-abandon')
    Assert-ResultValid $result 'Dialogue AbandonMission action should pass for active mission.'
    Assert-AdapterRecordedAction $result $true 'Dialogue AbandonMission action'
    Assert-Equal 'Abandoned' (Get-MissionStateNameFromService $missionService 'adapter-abandon') 'Dialogue AbandonMission should record Abandoned state.'
}

Run-Case 'dialogue action cannot complete mission before active' {
    $registry = New-QuestRegistryWithPacks @((New-ValidQuestPack 'adapter-pack-complete-early' 'adapter-complete-early'))
    $missionService = New-MissionStateService $registry
    $adapter = New-DialogueMissionActionAdapter $missionService
    $result = Invoke-AdapterExecuteAction $adapter (New-DialogueAction 'CompleteMission' 'adapter-complete-early')
    Assert-ResultContains $result 'mission is not active' 'Dialogue CompleteMission before Active should fail.'
    Assert-AdapterRecordedAction $result $false 'Dialogue CompleteMission before Active'
    Assert-Equal 'NotStarted' (Get-MissionStateNameFromService $missionService 'adapter-complete-early') 'Failed CompleteMission should leave mission NotStarted.'
}

Run-Case 'dialogue action cannot accept unknown mission' {
    $registry = New-QuestRegistryWithPacks @((New-ValidQuestPack 'adapter-pack-known' 'adapter-known'))
    $missionService = New-MissionStateService $registry
    $adapter = New-DialogueMissionActionAdapter $missionService
    $result = Invoke-AdapterExecuteAction $adapter (New-DialogueAction 'AcceptMission' 'adapter-unknown')
    Assert-ResultContains $result 'mission was not found' 'Dialogue AcceptMission unknown mission should fail.'
    Assert-AdapterRecordedAction $result $false 'Dialogue AcceptMission unknown mission'
}

Run-Case 'dialogue option can advance node and offer mission' {
    $questRegistry = New-QuestRegistryWithPacks @((New-ValidQuestPack 'adapter-pack-option-offer' 'adapter-option-offer'))
    $missionService = New-MissionStateService $questRegistry
    $adapter = New-DialogueMissionActionAdapter $missionService

    $offerAction = New-DialogueAction 'OfferMission' 'adapter-option-offer'
    $nodeA = New-DialogueNode 'node-a' @(
        (New-DialogueOptionWithActions 'option-offer' 0 'node-b' @($offerAction))
    )
    $nodeB = New-DialogueNode 'node-b' @((New-DialogueOption 'option-close' 0 'close' $null))
    $dialogueRegistry = New-DialogueRegistryWithPacks @(
        (New-DialoguePack 'adapter-dialogue-option-pack' @(
            (New-DialogueNpc 'SimpleChar:adapter-option' 'node-a' @($nodeA, $nodeB))
        ))
    )
    $dialogueService = New-DialogueSessionService $dialogueRegistry
    $start = Invoke-ObjectMethod $dialogueService 'StartSession' @('SimpleChar:adapter-option')
    Assert-ResultValid $start 'Dialogue session should start before option mission action.'
    $session = Get-PropertyValue $start 'Session'
    $options = @((Get-PropertyValue $start 'AvailableOptions'))
    $actions = @((Get-PropertyValue $options[0] 'Actions'))

    $adapterResult = Invoke-AdapterExecuteActionsForSession $adapter $session $actions
    Assert-ResultValid $adapterResult 'Dialogue option OfferMission adapter action should pass.'
    Assert-AdapterRecordedAction $adapterResult $true 'Dialogue option OfferMission'

    $select = Invoke-ObjectMethod $dialogueService 'SelectOption' @($session, 0)
    Assert-ResultValid $select 'Dialogue option should still advance the session node.'
    Assert-Equal 'node-b' (Get-PropertyValue $session 'CurrentNodeId') 'Dialogue option should advance to node-b.'
    Assert-Equal 'Offered' (Get-MissionStateNameFromService $missionService 'adapter-option-offer') 'Dialogue option action should offer mission.'
}

Run-Case 'dialogue option can end session' {
    $questRegistry = New-QuestRegistryWithPacks @()
    $missionService = New-MissionStateService $questRegistry
    $adapter = New-DialogueMissionActionAdapter $missionService

    $endAction = New-DialogueAction 'EndDialogue' $null
    $nodeA = New-DialogueNode 'node-a' @(
        (New-DialogueOptionWithActions 'option-end' 0 'node-b' @($endAction))
    )
    $nodeB = New-DialogueNode 'node-b' @()
    $dialogueRegistry = New-DialogueRegistryWithPacks @(
        (New-DialoguePack 'adapter-dialogue-end-pack' @(
            (New-DialogueNpc 'SimpleChar:adapter-end' 'node-a' @($nodeA, $nodeB))
        ))
    )
    $dialogueService = New-DialogueSessionService $dialogueRegistry
    $start = Invoke-ObjectMethod $dialogueService 'StartSession' @('SimpleChar:adapter-end')
    Assert-ResultValid $start 'Dialogue session should start before EndDialogue adapter action.'
    $session = Get-PropertyValue $start 'Session'
    $options = @((Get-PropertyValue $start 'AvailableOptions'))
    $actions = @((Get-PropertyValue $options[0] 'Actions'))

    $adapterResult = Invoke-AdapterExecuteActionsForSession $adapter $session $actions
    Assert-ResultValid $adapterResult 'Dialogue EndDialogue adapter action should pass.'
    Assert-AdapterRecordedAction $adapterResult $true 'Dialogue EndDialogue action'
    Assert-True ([bool](Get-PropertyValue $adapterResult 'EndedDialogue')) 'Adapter result should mark dialogue ended.'
    Assert-True (-not [bool](Get-PropertyValue $session 'IsActive')) 'EndDialogue adapter action should close the session.'
}

Run-Case 'dialogue action chain-linked mission remains blocked before prerequisite completion' {
    $missionA = New-ValidQuestDefinition 'adapter-chain-a' 'step-a'
    $missionB = New-ValidQuestDefinition 'adapter-chain-b' 'step-b'
    $link = New-QuestLink 'adapter-chain-a' 'step-a' 'adapter-chain-b' 'step-b'
    $registry = New-QuestRegistryWithPacks @((New-QuestPack 'adapter-chain-pack-blocked' @($missionA, $missionB) @($link)))
    $missionService = New-MissionStateService $registry
    $adapter = New-DialogueMissionActionAdapter $missionService
    $result = Invoke-AdapterExecuteAction $adapter (New-DialogueAction 'OfferMission' 'adapter-chain-b')
    Assert-ResultContains $result 'mission prerequisite is not completed' 'Adapter linked mission should be blocked before prerequisite completion.'
    Assert-AdapterRecordedAction $result $false 'Adapter linked mission blocked'
}

Run-Case 'dialogue action chain-linked mission becomes offerable after prerequisite completion' {
    $missionA = New-ValidQuestDefinition 'adapter-chain-complete-a' 'step-a'
    $missionB = New-ValidQuestDefinition 'adapter-chain-complete-b' 'step-b'
    $link = New-QuestLink 'adapter-chain-complete-a' 'step-a' 'adapter-chain-complete-b' 'step-b'
    $registry = New-QuestRegistryWithPacks @((New-QuestPack 'adapter-chain-pack-unlocked' @($missionA, $missionB) @($link)))
    $missionService = New-MissionStateService $registry
    $adapter = New-DialogueMissionActionAdapter $missionService

    Assert-ResultValid (Invoke-AdapterExecuteAction $adapter (New-DialogueAction 'OfferMission' 'adapter-chain-complete-a')) 'Adapter should offer prerequisite mission.'
    Assert-ResultValid (Invoke-AdapterExecuteAction $adapter (New-DialogueAction 'AcceptMission' 'adapter-chain-complete-a')) 'Adapter should accept prerequisite mission.'
    Assert-ResultValid (Invoke-AdapterExecuteAction $adapter (New-DialogueAction 'CompleteMission' 'adapter-chain-complete-a')) 'Adapter should complete prerequisite mission.'

    $result = Invoke-AdapterExecuteAction $adapter (New-DialogueAction 'OfferMission' 'adapter-chain-complete-b')
    Assert-ResultValid $result 'Adapter linked mission should be offerable after prerequisite completion.'
    Assert-AdapterRecordedAction $result $true 'Adapter linked mission unlocked'
    Assert-Equal 'Offered' (Get-MissionStateNameFromService $missionService 'adapter-chain-complete-b') 'Unlocked linked mission should record Offered state.'
}

Run-Case 'content action validation valid OfferMission references existing quest' {
    $questRegistry = New-QuestRegistryWithPacks @((New-ValidQuestPack 'action-reference-pack-offer' 'action-reference-offer'))
    $dialoguePack = New-DialogueActionReferencePack 'dialogue-action-reference-offer' 'SimpleChar:action-reference-offer' @(
        (New-DialogueAction 'OfferMission' 'action-reference-offer')
    )
    $validation = Invoke-DialogueActionReferenceValidation @($dialoguePack) $questRegistry
    Assert-ValidationValid $validation 'OfferMission should reference an existing mission.'
}

Run-Case 'content action validation valid AcceptMission references existing quest' {
    $questRegistry = New-QuestRegistryWithPacks @((New-ValidQuestPack 'action-reference-pack-accept' 'action-reference-accept'))
    $dialoguePack = New-DialogueActionReferencePack 'dialogue-action-reference-accept' 'SimpleChar:action-reference-accept' @(
        (New-DialogueAction 'AcceptMission' 'action-reference-accept')
    )
    $validation = Invoke-DialogueActionReferenceValidation @($dialoguePack) $questRegistry
    Assert-ValidationValid $validation 'AcceptMission should reference an existing mission.'
}

Run-Case 'content action validation valid CompleteMission references existing quest' {
    $questRegistry = New-QuestRegistryWithPacks @((New-ValidQuestPack 'action-reference-pack-complete' 'action-reference-complete'))
    $dialoguePack = New-DialogueActionReferencePack 'dialogue-action-reference-complete' 'SimpleChar:action-reference-complete' @(
        (New-DialogueAction 'CompleteMission' 'action-reference-complete')
    )
    $validation = Invoke-DialogueActionReferenceValidation @($dialoguePack) $questRegistry
    Assert-ValidationValid $validation 'CompleteMission should reference an existing mission.'
}

Run-Case 'content action validation valid FailMission references existing quest' {
    $questRegistry = New-QuestRegistryWithPacks @((New-ValidQuestPack 'action-reference-pack-fail' 'action-reference-fail'))
    $dialoguePack = New-DialogueActionReferencePack 'dialogue-action-reference-fail' 'SimpleChar:action-reference-fail' @(
        (New-DialogueAction 'FailMission' 'action-reference-fail')
    )
    $validation = Invoke-DialogueActionReferenceValidation @($dialoguePack) $questRegistry
    Assert-ValidationValid $validation 'FailMission should reference an existing mission.'
}

Run-Case 'content action validation valid AbandonMission references existing quest' {
    $questRegistry = New-QuestRegistryWithPacks @((New-ValidQuestPack 'action-reference-pack-abandon' 'action-reference-abandon'))
    $dialoguePack = New-DialogueActionReferencePack 'dialogue-action-reference-abandon' 'SimpleChar:action-reference-abandon' @(
        (New-DialogueAction 'AbandonMission' 'action-reference-abandon')
    )
    $validation = Invoke-DialogueActionReferenceValidation @($dialoguePack) $questRegistry
    Assert-ValidationValid $validation 'AbandonMission should reference an existing mission.'
}

Run-Case 'content action validation valid EndDialogue without mission ID' {
    $questRegistry = New-QuestRegistryWithPacks @()
    $dialoguePack = New-DialogueActionReferencePack 'dialogue-action-reference-end' 'SimpleChar:action-reference-end' @(
        (New-DialogueAction 'EndDialogue' $null)
    )
    $validation = Invoke-DialogueActionReferenceValidation @($dialoguePack) $questRegistry
    Assert-ValidationValid $validation 'EndDialogue should not require a mission ID.'
}

Run-Case 'content action validation mission action missing mission ID fails' {
    $questRegistry = New-QuestRegistryWithPacks @((New-ValidQuestPack 'action-reference-pack-missing' 'action-reference-known'))
    $dialoguePack = New-DialogueActionReferencePack 'dialogue-action-reference-missing' 'SimpleChar:action-reference-missing' @(
        (New-DialogueAction 'OfferMission' $null)
    )
    $validation = Invoke-DialogueActionReferenceValidation @($dialoguePack) $questRegistry
    Assert-ValidationContains $validation 'missing mission id' 'Mission actions should require mission IDs.'
}

Run-Case 'content action validation mission action references unknown mission fails' {
    $questRegistry = New-QuestRegistryWithPacks @((New-ValidQuestPack 'action-reference-pack-known' 'action-reference-known'))
    $dialoguePack = New-DialogueActionReferencePack 'dialogue-action-reference-unknown' 'SimpleChar:action-reference-unknown' @(
        (New-DialogueAction 'OfferMission' 'action-reference-unknown')
    )
    $validation = Invoke-DialogueActionReferenceValidation @($dialoguePack) $questRegistry
    Assert-ValidationContains $validation "mission id 'action-reference-unknown' was not found" 'Unknown mission references should fail.'
}

Run-Case 'content action validation unknown dialogue action type fails' {
    $questRegistry = New-QuestRegistryWithPacks @((New-ValidQuestPack 'action-reference-pack-unsupported' 'action-reference-unsupported'))
    $dialoguePack = New-DialogueActionReferencePack 'dialogue-action-reference-unsupported' 'SimpleChar:action-reference-unsupported' @(
        (New-DialogueAction 'UnsupportedSyntheticAction' 'action-reference-unsupported')
    )
    $validation = Invoke-DialogueActionReferenceValidation @($dialoguePack) $questRegistry
    Assert-ValidationContains $validation 'unsupported dialogue action type' 'Unsupported dialogue actions should fail.'
}

Run-Case 'content action validation mixed valid and invalid actions report all failures' {
    $questRegistry = New-QuestRegistryWithPacks @((New-ValidQuestPack 'action-reference-pack-mixed' 'action-reference-valid'))
    $dialoguePack = New-DialogueActionReferencePack 'dialogue-action-reference-mixed' 'SimpleChar:action-reference-mixed' @(
        (New-DialogueAction 'OfferMission' 'action-reference-valid'),
        (New-DialogueAction 'CompleteMission' $null),
        (New-DialogueAction 'AcceptMission' 'action-reference-unknown'),
        (New-DialogueAction 'UnsupportedSyntheticAction' 'action-reference-valid')
    )
    $validation = Invoke-DialogueActionReferenceValidation @($dialoguePack) $questRegistry
    Assert-ValidationContains $validation 'missing mission id' 'Mixed action validation should report missing mission IDs.'
    Assert-ValidationContains $validation "mission id 'action-reference-unknown' was not found" 'Mixed action validation should report unknown mission IDs.'
    Assert-ValidationContains $validation 'unsupported dialogue action type' 'Mixed action validation should report unsupported action types.'
    Assert-Equal 3 (@(Get-ValidationErrors $validation).Count) 'Mixed action validation should report all three failures.'
}

Run-Case 'file-loaded action validation OfferMission references file-loaded quest' {
    $validation = Invoke-FileLoadedActionReferenceValidation `
        -DialogueFiles @((Get-SampleContentPath 'action-reference\dialogue\offer-mission.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-ValidationValid $validation 'File-loaded OfferMission should reference a file-loaded mission.'
}

Run-Case 'file-loaded action validation AcceptMission references file-loaded quest' {
    $validation = Invoke-FileLoadedActionReferenceValidation `
        -DialogueFiles @((Get-SampleContentPath 'action-reference\dialogue\accept-mission.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-ValidationValid $validation 'File-loaded AcceptMission should reference a file-loaded mission.'
}

Run-Case 'file-loaded action validation CompleteMission references file-loaded quest' {
    $validation = Invoke-FileLoadedActionReferenceValidation `
        -DialogueFiles @((Get-SampleContentPath 'action-reference\dialogue\complete-mission.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-ValidationValid $validation 'File-loaded CompleteMission should reference a file-loaded mission.'
}

Run-Case 'file-loaded action validation FailMission references file-loaded quest' {
    $validation = Invoke-FileLoadedActionReferenceValidation `
        -DialogueFiles @((Get-SampleContentPath 'action-reference\dialogue\fail-mission.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-ValidationValid $validation 'File-loaded FailMission should reference a file-loaded mission.'
}

Run-Case 'file-loaded action validation AbandonMission references file-loaded quest' {
    $validation = Invoke-FileLoadedActionReferenceValidation `
        -DialogueFiles @((Get-SampleContentPath 'action-reference\dialogue\abandon-mission.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-ValidationValid $validation 'File-loaded AbandonMission should reference a file-loaded mission.'
}

Run-Case 'file-loaded action validation EndDialogue validates without mission ID' {
    $validation = Invoke-FileLoadedActionReferenceValidation `
        -DialogueFiles @((Get-SampleContentPath 'action-reference\dialogue\end-dialogue.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-ValidationValid $validation 'File-loaded EndDialogue should not require a mission ID.'
}

Run-Case 'file-loaded action validation mission action missing mission ID fails' {
    $validation = Invoke-FileLoadedActionReferenceValidation `
        -DialogueFiles @((Get-SampleContentPath 'action-reference\dialogue\missing-mission-id.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-ValidationContains $validation 'missing mission id' 'File-loaded mission actions should require mission IDs.'
}

Run-Case 'file-loaded action validation mission action references unknown mission fails' {
    $validation = Invoke-FileLoadedActionReferenceValidation `
        -DialogueFiles @((Get-SampleContentPath 'action-reference\dialogue\unknown-mission.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-ValidationContains $validation "mission id 'file-action-reference-unknown' was not found" 'File-loaded unknown mission references should fail.'
}

Run-Case 'file-loaded action validation unknown dialogue action type fails' {
    $validation = Invoke-FileLoadedActionReferenceValidation `
        -DialogueFiles @((Get-SampleContentPath 'action-reference\dialogue\unknown-action-type.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-ValidationContains $validation 'unsupported dialogue action type' 'File-loaded unsupported dialogue actions should fail.'
}

Run-Case 'file-loaded action validation manifest-loaded dialogue and quest packs validate together' {
    $validation = Invoke-ManifestLoadedActionReferenceValidation `
        (Get-SampleContentPath 'action-reference\manifests\valid-action-reference-manifest.json')

    Assert-ValidationValid $validation 'Manifest-loaded dialogue and quest packs should validate together.'
}

Run-Case 'file-loaded action validation directory-loaded dialogue and quest packs validate together' {
    $validation = Invoke-DirectoryLoadedActionReferenceValidation `
        -DialogueDirectory (Get-SampleContentDirectoryPath 'action-reference\dialogue-directory-valid') `
        -QuestDirectory (Get-SampleContentDirectoryPath 'action-reference\quest-directory-valid')

    Assert-ValidationValid $validation 'Directory-loaded dialogue and quest packs should validate together.'
}

Run-Case 'file-loaded action validation mixed valid and invalid actions report all failures' {
    $validation = Invoke-FileLoadedActionReferenceValidation `
        -DialogueFiles @((Get-SampleContentPath 'action-reference\dialogue\mixed-valid-invalid-actions.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-ValidationContains $validation 'missing mission id' 'File-loaded mixed validation should report missing mission IDs.'
    Assert-ValidationContains $validation "mission id 'file-action-reference-unknown' was not found" 'File-loaded mixed validation should report unknown mission IDs.'
    Assert-ValidationContains $validation 'unsupported dialogue action type' 'File-loaded mixed validation should report unsupported action types.'
    Assert-Equal 3 (@(Get-ValidationErrors $validation).Count) 'File-loaded mixed action validation should report all three failures.'
}

Run-Case 'aggregate validates explicit valid dialogue and quest files' {
    $validation = Invoke-AggregateValidateFiles `
        -DialogueFiles @((Get-SampleContentPath 'action-reference\dialogue\offer-mission.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-ValidationValid $validation 'Aggregate explicit valid dialogue and quest files should pass.'
}

Run-Case 'aggregate validates manifest-loaded dialogue and quest files' {
    $validation = Invoke-AggregateValidateManifest `
        (Get-SampleContentPath 'action-reference\manifests\valid-action-reference-manifest.json')

    Assert-ValidationValid $validation 'Aggregate manifest-loaded dialogue and quest files should pass.'
}

Run-Case 'aggregate validates directory-loaded dialogue and quest files' {
    $validation = Invoke-AggregateValidateDirectory `
        (Get-SampleContentDirectoryPath 'aggregate\directory-valid')

    Assert-ValidationValid $validation 'Aggregate directory-loaded dialogue and quest files should pass.'
}

Run-Case 'aggregate reports invalid JSON' {
    $validation = Invoke-AggregateValidateFiles `
        -DialogueFiles @((Get-SampleContentPath 'dialogue\invalid-json.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-ValidationContains $validation 'Load:' 'Aggregate invalid JSON should report the load stage.'
    Assert-ValidationContains $validation 'failed to parse JSON content file' 'Aggregate invalid JSON should preserve the parse failure.'
}

Run-Case 'aggregate reports missing file' {
    $missingDialogueFile = Join-Path (Get-SampleContentRoot) 'aggregate\missing-dialogue-file.json'
    $validation = Invoke-AggregateValidateFiles `
        -DialogueFiles @($missingDialogueFile) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-ValidationContains $validation 'Load:' 'Aggregate missing file should report the load stage.'
    Assert-ValidationContains $validation 'JSON content file was not found' 'Aggregate missing file should preserve the file failure.'
}

Run-Case 'aggregate reports duplicate dialogue pack ID' {
    $validation = Invoke-AggregateValidateFiles `
        -DialogueFiles @(
            (Get-SampleContentPath 'dialogue\valid-pack-a.json'),
            (Get-SampleContentPath 'dialogue\duplicate-pack-id.json')
        ) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-ValidationContains $validation 'DialoguePack:' 'Aggregate duplicate dialogue pack should report the dialogue pack stage.'
    Assert-ValidationContains $validation 'duplicate dialogue content pack id' 'Aggregate duplicate dialogue pack should preserve the duplicate error.'
}

Run-Case 'aggregate reports duplicate quest pack ID' {
    $validation = Invoke-AggregateValidateFiles `
        -DialogueFiles @((Get-SampleContentPath 'dialogue\valid-pack-a.json')) `
        -QuestFiles @(
            (Get-SampleContentPath 'quests\valid-pack-a.json'),
            (Get-SampleContentPath 'quests\duplicate-pack-id.json')
        )

    Assert-ValidationContains $validation 'QuestPack:' 'Aggregate duplicate quest pack should report the quest pack stage.'
    Assert-ValidationContains $validation 'duplicate quest content pack id' 'Aggregate duplicate quest pack should preserve the duplicate error.'
}

Run-Case 'aggregate reports missing NPC identity' {
    $validation = Invoke-AggregateValidateFiles `
        -DialogueFiles @((Get-SampleContentPath 'dialogue\missing-npc-identity.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-ValidationContains $validation 'DialoguePack:' 'Aggregate missing NPC identity should report the dialogue pack stage.'
    Assert-ValidationContains $validation 'missing NPC identity' 'Aggregate missing NPC identity should preserve the shape error.'
}

Run-Case 'aggregate reports missing dialogue node target' {
    $validation = Invoke-AggregateValidateFiles `
        -DialogueFiles @((Get-SampleContentPath 'dialogue\missing-node-target.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-ValidationContains $validation 'DialoguePack:' 'Aggregate missing dialogue node target should report the dialogue pack stage.'
    Assert-ValidationContains $validation 'missing dialogue node target' 'Aggregate missing dialogue node target should preserve the shape error.'
}

Run-Case 'aggregate reports missing quest ID' {
    $validation = Invoke-AggregateValidateFiles `
        -DialogueFiles @((Get-SampleContentPath 'dialogue\valid-pack-a.json')) `
        -QuestFiles @((Get-SampleContentPath 'quests\missing-quest-id.json'))

    Assert-ValidationContains $validation 'QuestPack:' 'Aggregate missing quest ID should report the quest pack stage.'
    Assert-ValidationContains $validation 'missing quest id' 'Aggregate missing quest ID should preserve the shape error.'
}

Run-Case 'aggregate reports missing quest step ID' {
    $validation = Invoke-AggregateValidateFiles `
        -DialogueFiles @((Get-SampleContentPath 'dialogue\valid-pack-a.json')) `
        -QuestFiles @((Get-SampleContentPath 'quests\missing-step-id.json'))

    Assert-ValidationContains $validation 'QuestPack:' 'Aggregate missing quest step ID should report the quest pack stage.'
    Assert-ValidationContains $validation 'missing quest step id' 'Aggregate missing quest step ID should preserve the shape error.'
}

Run-Case 'aggregate reports dialogue action referencing unknown mission' {
    $validation = Invoke-AggregateValidateFiles `
        -DialogueFiles @((Get-SampleContentPath 'action-reference\dialogue\unknown-mission.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-ValidationContains $validation 'ActionReference:' 'Aggregate unknown mission action should report the action reference stage.'
    Assert-ValidationContains $validation "mission id 'file-action-reference-unknown' was not found" 'Aggregate unknown mission action should preserve the reference error.'
}

Run-Case 'aggregate reports unknown dialogue action type' {
    $validation = Invoke-AggregateValidateFiles `
        -DialogueFiles @((Get-SampleContentPath 'action-reference\dialogue\unknown-action-type.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-ValidationContains $validation 'ActionReference:' 'Aggregate unknown dialogue action should report the action reference stage.'
    Assert-ValidationContains $validation 'unsupported dialogue action type' 'Aggregate unknown dialogue action should preserve the action type error.'
}

Run-Case 'aggregate reports mixed load shape and action-reference failures together' {
    $validation = Invoke-AggregateValidateFiles `
        -DialogueFiles @(
            (Get-SampleContentPath 'dialogue\invalid-json.json'),
            (Get-SampleContentPath 'dialogue\missing-npc-identity.json'),
            (Get-SampleContentPath 'action-reference\dialogue\unknown-mission.json')
        ) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-ValidationContains $validation 'Load:' 'Aggregate mixed failures should preserve load failures.'
    Assert-ValidationContains $validation 'DialoguePack:' 'Aggregate mixed failures should preserve dialogue shape failures.'
    Assert-ValidationContains $validation 'ActionReference:' 'Aggregate mixed failures should preserve action reference failures.'
    Assert-ValidationContains $validation 'failed to parse JSON content file' 'Aggregate mixed failures should include invalid JSON.'
    Assert-ValidationContains $validation 'missing NPC identity' 'Aggregate mixed failures should include missing NPC identity.'
    Assert-ValidationContains $validation "mission id 'file-action-reference-unknown' was not found" 'Aggregate mixed failures should include unknown mission reference.'
}

Run-Case 'aggregate condition-reference hook runs without implementing real conditions' {
    Assert-True ($null -ne $conditionReferenceValidatorType) 'Condition reference hook type should be present.'
    $validation = Invoke-AggregateValidateDirectory `
        (Get-SampleContentDirectoryPath 'aggregate\condition-hook')

    Assert-ValidationValid $validation 'Aggregate condition-reference hook should accept future synthetic conditions for now.'
}

Run-Case 'condition reference valid AlwaysTrue condition' {
    $validation = Invoke-AggregateValidateFiles `
        -DialogueFiles @((Get-SampleContentPath 'condition-reference\dialogue\always-true.json')) `
        -QuestFiles @((Get-SampleContentPath 'condition-reference\quests\mission.json'))

    Assert-ValidationValid $validation 'AlwaysTrue condition should validate without a mission ID.'
}

Run-Case 'condition reference valid AlwaysFalse condition' {
    $validation = Invoke-AggregateValidateFiles `
        -DialogueFiles @((Get-SampleContentPath 'condition-reference\dialogue\always-false.json')) `
        -QuestFiles @((Get-SampleContentPath 'condition-reference\quests\mission.json'))

    Assert-ValidationValid $validation 'AlwaysFalse condition should validate without a mission ID.'
}

Run-Case 'condition reference valid MissionOffered condition references existing quest' {
    $validation = Invoke-AggregateValidateFiles `
        -DialogueFiles @((Get-SampleContentPath 'condition-reference\dialogue\mission-offered.json')) `
        -QuestFiles @((Get-SampleContentPath 'condition-reference\quests\mission.json'))

    Assert-ValidationValid $validation 'MissionOffered condition should reference an existing mission.'
}

Run-Case 'condition reference valid MissionActive condition references existing quest' {
    $validation = Invoke-AggregateValidateFiles `
        -DialogueFiles @((Get-SampleContentPath 'condition-reference\dialogue\mission-active.json')) `
        -QuestFiles @((Get-SampleContentPath 'condition-reference\quests\mission.json'))

    Assert-ValidationValid $validation 'MissionActive condition should reference an existing mission.'
}

Run-Case 'condition reference valid MissionCompleted condition references existing quest' {
    $validation = Invoke-AggregateValidateFiles `
        -DialogueFiles @((Get-SampleContentPath 'condition-reference\dialogue\mission-completed.json')) `
        -QuestFiles @((Get-SampleContentPath 'condition-reference\quests\mission.json'))

    Assert-ValidationValid $validation 'MissionCompleted condition should reference an existing mission.'
}

Run-Case 'condition reference valid MissionNotStarted condition references existing quest' {
    $validation = Invoke-AggregateValidateFiles `
        -DialogueFiles @((Get-SampleContentPath 'condition-reference\dialogue\mission-not-started.json')) `
        -QuestFiles @((Get-SampleContentPath 'condition-reference\quests\mission.json'))

    Assert-ValidationValid $validation 'MissionNotStarted condition should reference an existing mission.'
}

Run-Case 'condition reference mission condition missing mission ID fails' {
    $validation = Invoke-AggregateValidateFiles `
        -DialogueFiles @((Get-SampleContentPath 'condition-reference\dialogue\missing-mission-id.json')) `
        -QuestFiles @((Get-SampleContentPath 'condition-reference\quests\mission.json'))

    Assert-ValidationContains $validation 'ConditionReference:' 'Missing mission ID should report the condition reference stage.'
    Assert-ValidationContains $validation 'missing mission id for condition' 'Mission conditions should require mission IDs.'
}

Run-Case 'condition reference mission condition references unknown mission fails' {
    $validation = Invoke-AggregateValidateFiles `
        -DialogueFiles @((Get-SampleContentPath 'condition-reference\dialogue\unknown-mission.json')) `
        -QuestFiles @((Get-SampleContentPath 'condition-reference\quests\mission.json'))

    Assert-ValidationContains $validation 'ConditionReference:' 'Unknown mission should report the condition reference stage.'
    Assert-ValidationContains $validation "mission id 'condition-reference-unknown' was not found" 'Mission conditions should reference known missions.'
}

Run-Case 'condition reference unknown condition type fails' {
    $validation = Invoke-AggregateValidateFiles `
        -DialogueFiles @((Get-SampleContentPath 'condition-reference\dialogue\unknown-condition-type.json')) `
        -QuestFiles @((Get-SampleContentPath 'condition-reference\quests\mission.json'))

    Assert-ValidationContains $validation 'ConditionReference:' 'Unknown condition type should report the condition reference stage.'
    Assert-ValidationContains $validation 'unsupported condition type' 'Unknown condition types should fail clearly.'
}

Run-Case 'condition reference dialogue option condition is validated' {
    $validation = Invoke-AggregateValidateFiles `
        -DialogueFiles @((Get-SampleContentPath 'condition-reference\dialogue\unknown-condition-type.json')) `
        -QuestFiles @((Get-SampleContentPath 'condition-reference\quests\mission.json'))

    Assert-ValidationContains $validation '.option' 'Dialogue option condition should be traversed.'
    Assert-ValidationContains $validation 'unsupported condition type' 'Dialogue option condition validation should report unsupported types.'
}

Run-Case 'condition reference quest step condition is validated' {
    $validation = Invoke-AggregateValidateFiles `
        -DialogueFiles @((Get-SampleContentPath 'condition-reference\dialogue\always-true.json')) `
        -QuestFiles @((Get-SampleContentPath 'condition-reference\quests\step-condition-unknown-mission.json'))

    Assert-ValidationContains $validation '.step' 'Quest step condition should be traversed.'
    Assert-ValidationContains $validation "mission id 'condition-reference-unknown' was not found" 'Quest step condition validation should report unknown missions.'
}

Run-Case 'condition reference quest objective condition is validated' {
    $validation = Invoke-AggregateValidateFiles `
        -DialogueFiles @((Get-SampleContentPath 'condition-reference\dialogue\always-true.json')) `
        -QuestFiles @((Get-SampleContentPath 'condition-reference\quests\objective-condition-unknown-mission.json'))

    Assert-ValidationContains $validation '.objective' 'Quest objective condition should be traversed.'
    Assert-ValidationContains $validation "mission id 'condition-reference-unknown' was not found" 'Quest objective condition validation should report unknown missions.'
}

Run-Case 'condition reference aggregate reports action-reference and condition-reference failures together' {
    $validation = Invoke-AggregateValidateFiles `
        -DialogueFiles @((Get-SampleContentPath 'condition-reference\dialogue\action-and-condition-failure.json')) `
        -QuestFiles @((Get-SampleContentPath 'condition-reference\quests\mission.json'))

    Assert-ValidationContains $validation 'ActionReference:' 'Mixed action/condition failures should preserve the action reference stage.'
    Assert-ValidationContains $validation 'ConditionReference:' 'Mixed action/condition failures should preserve the condition reference stage.'
    Assert-ValidationContains $validation "mission id 'condition-reference-unknown' was not found" 'Mixed validation should include the unknown action mission.'
    Assert-ValidationContains $validation 'unsupported condition type' 'Mixed validation should include the unknown condition type.'
}

Run-Case 'condition reference file-loaded validation works through explicit files' {
    $validation = Invoke-AggregateValidateFiles `
        -DialogueFiles @((Get-SampleContentPath 'condition-reference\dialogue\mission-active.json')) `
        -QuestFiles @((Get-SampleContentPath 'condition-reference\quests\step-condition.json'))

    Assert-ValidationValid $validation 'Explicit file-loaded condition references should validate.'
}

Run-Case 'condition reference file-loaded validation works through manifest loading' {
    $validation = Invoke-AggregateValidateManifest `
        (Get-SampleContentPath 'condition-reference\manifests\valid-condition-reference-manifest.json')

    Assert-ValidationValid $validation 'Manifest-loaded condition references should validate.'
}

Run-Case 'condition reference file-loaded validation works through directory loading' {
    $validation = Invoke-AggregateValidateDirectory `
        (Get-SampleContentDirectoryPath 'condition-reference\directory-valid')

    Assert-ValidationValid $validation 'Directory-loaded condition references should validate.'
}

Run-Case 'aggregate report valid has overall success' {
    $report = Invoke-AggregateValidateFilesReport `
        -DialogueFiles @((Get-SampleContentPath 'action-reference\dialogue\offer-mission.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-Equal $aggregateValidationReportType.FullName $report.GetType().FullName 'Aggregate report should return the report object type.'
    Assert-True ([bool](Get-PropertyValue $report 'IsValid')) 'Valid aggregate report should have overall success.'
    Assert-Equal 0 (Get-PropertyValue $report 'TotalErrorCount') 'Valid aggregate report should have zero errors.'
}

Run-Case 'aggregate report invalid has overall failure' {
    $report = Invoke-AggregateValidateFilesReport `
        -DialogueFiles @((Get-SampleContentPath 'condition-reference\dialogue\action-and-condition-failure.json')) `
        -QuestFiles @((Get-SampleContentPath 'condition-reference\quests\mission.json'))

    Assert-True (-not [bool](Get-PropertyValue $report 'IsValid')) 'Invalid aggregate report should have overall failure.'
    Assert-Equal 2 (Get-PropertyValue $report 'TotalErrorCount') 'Invalid aggregate report should count action and condition failures.'
}

Run-Case 'aggregate report includes Load stage' {
    $report = Invoke-AggregateValidateFilesReport `
        -DialogueFiles @((Get-SampleContentPath 'action-reference\dialogue\offer-mission.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    [void](Assert-AggregateReportStagePresent $report 'Load')
}

Run-Case 'aggregate report includes DialoguePack stage' {
    $report = Invoke-AggregateValidateFilesReport `
        -DialogueFiles @((Get-SampleContentPath 'action-reference\dialogue\offer-mission.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    [void](Assert-AggregateReportStagePresent $report 'DialoguePack')
}

Run-Case 'aggregate report includes QuestPack stage' {
    $report = Invoke-AggregateValidateFilesReport `
        -DialogueFiles @((Get-SampleContentPath 'action-reference\dialogue\offer-mission.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    [void](Assert-AggregateReportStagePresent $report 'QuestPack')
}

Run-Case 'aggregate report includes Registry stage' {
    $report = Invoke-AggregateValidateFilesReport `
        -DialogueFiles @((Get-SampleContentPath 'action-reference\dialogue\offer-mission.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    [void](Assert-AggregateReportStagePresent $report 'Registry')
}

Run-Case 'aggregate report includes ActionReference stage' {
    $report = Invoke-AggregateValidateFilesReport `
        -DialogueFiles @((Get-SampleContentPath 'action-reference\dialogue\offer-mission.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    [void](Assert-AggregateReportStagePresent $report 'ActionReference')
}

Run-Case 'aggregate report includes ConditionReference stage' {
    $report = Invoke-AggregateValidateFilesReport `
        -DialogueFiles @((Get-SampleContentPath 'action-reference\dialogue\offer-mission.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    [void](Assert-AggregateReportStagePresent $report 'ConditionReference')
}

Run-Case 'aggregate report counts loaded dialogue files' {
    $report = Invoke-AggregateValidateFilesReport `
        -DialogueFiles @((Get-SampleContentPath 'action-reference\dialogue\offer-mission.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-Equal 1 (Get-PropertyValue $report 'LoadedDialogueFileCount') 'Aggregate report should count loaded dialogue files.'
}

Run-Case 'aggregate report counts loaded quest files' {
    $report = Invoke-AggregateValidateFilesReport `
        -DialogueFiles @((Get-SampleContentPath 'action-reference\dialogue\offer-mission.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-Equal 1 (Get-PropertyValue $report 'LoadedQuestFileCount') 'Aggregate report should count loaded quest files.'
}

Run-Case 'aggregate report counts loaded dialogue packs' {
    $report = Invoke-AggregateValidateFilesReport `
        -DialogueFiles @((Get-SampleContentPath 'action-reference\dialogue\offer-mission.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-Equal 1 (Get-PropertyValue $report 'LoadedDialoguePackCount') 'Aggregate report should count loaded dialogue packs.'
}

Run-Case 'aggregate report counts loaded quest packs' {
    $report = Invoke-AggregateValidateFilesReport `
        -DialogueFiles @((Get-SampleContentPath 'action-reference\dialogue\offer-mission.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-Equal 1 (Get-PropertyValue $report 'LoadedQuestPackCount') 'Aggregate report should count loaded quest packs.'
}

Run-Case 'aggregate report counts dialogue NPC entries' {
    $report = Invoke-AggregateValidateFilesReport `
        -DialogueFiles @((Get-SampleContentPath 'action-reference\dialogue\offer-mission.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-Equal 1 (Get-PropertyValue $report 'LoadedNpcEntryCount') 'Aggregate report should count loaded dialogue NPC entries.'
}

Run-Case 'aggregate report counts quest definitions' {
    $report = Invoke-AggregateValidateFilesReport `
        -DialogueFiles @((Get-SampleContentPath 'action-reference\dialogue\offer-mission.json')) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-Equal 1 (Get-PropertyValue $report 'LoadedQuestDefinitionCount') 'Aggregate report should count loaded quest definitions.'
}

Run-Case 'aggregate report records per-stage failure counts' {
    $report = Invoke-AggregateValidateFilesReport `
        -DialogueFiles @((Get-SampleContentPath 'condition-reference\dialogue\action-and-condition-failure.json')) `
        -QuestFiles @((Get-SampleContentPath 'condition-reference\quests\mission.json'))

    Assert-AggregateReportStageErrorCount $report 'ActionReference' 1 'Aggregate report should count action-reference failures per stage.'
    Assert-AggregateReportStageErrorCount $report 'ConditionReference' 1 'Aggregate report should count condition-reference failures per stage.'
}

Run-Case 'aggregate report records mixed failures across multiple stages' {
    $report = Invoke-AggregateValidateFilesReport `
        -DialogueFiles @(
            (Get-SampleContentPath 'dialogue\invalid-json.json'),
            (Get-SampleContentPath 'dialogue\missing-npc-identity.json'),
            (Get-SampleContentPath 'action-reference\dialogue\unknown-mission.json')
        ) `
        -QuestFiles @((Get-SampleContentPath 'action-reference\quests\mission.json'))

    Assert-AggregateReportStageErrorCount $report 'Load' 1 'Aggregate report should count load failures per stage.'
    Assert-AggregateReportStageErrorCount $report 'DialoguePack' 1 'Aggregate report should count dialogue shape failures per stage.'
    Assert-AggregateReportStageErrorCount $report 'ActionReference' 1 'Aggregate report should count action-reference failures per stage.'
    Assert-Equal 3 (Get-PropertyValue $report 'TotalErrorCount') 'Aggregate report should preserve mixed failures across stages.'
}

Run-Case 'quest valid file-loaded pack' {
    $registry = New-Instance $questRegistryType
    $validation = Invoke-RegistryLoadFromFiles $registry @(
        (Get-SampleContentPath 'quests\valid-pack-a.json')
    )
    Assert-ValidationValid $validation 'Valid file-loaded quest pack should pass.'
    Assert-Equal 1 (Get-PropertyValue $registry 'PackCount') 'Valid file-loaded quest registry should load one pack.'
    Assert-Equal 1 (Get-PropertyValue $registry 'QuestCount') 'Valid file-loaded quest registry should load one quest.'
}

Run-Case 'quest directory loads all valid packs' {
    $registry = New-Instance $questRegistryType
    $validation = Invoke-RegistryLoadFromDirectory $registry (Get-SampleContentDirectoryPath 'quest-directory-valid')
    Assert-ValidationValid $validation 'Valid quest directory should pass.'
    Assert-Equal 2 (Get-PropertyValue $registry 'PackCount') 'Valid quest directory should load two packs.'
    Assert-Equal 2 (Get-PropertyValue $registry 'QuestCount') 'Valid quest directory should load two quests.'
}

Run-Case 'quest duplicate pack ids from files' {
    $registry = New-Instance $questRegistryType
    $validation = Invoke-RegistryLoadFromFiles $registry @(
        (Get-SampleContentPath 'quests\valid-pack-a.json'),
        (Get-SampleContentPath 'quests\duplicate-pack-id.json')
    )
    Assert-ValidationContains $validation 'duplicate quest content pack id' 'Duplicate file-loaded quest pack IDs should fail.'
}

Run-Case 'quest missing quest id from file' {
    $registry = New-Instance $questRegistryType
    $validation = Invoke-RegistryLoadFromFiles $registry @(
        (Get-SampleContentPath 'quests\missing-quest-id.json')
    )
    Assert-ValidationContains $validation 'missing quest id' 'File-loaded missing quest ID should fail.'
}

Run-Case 'quest missing step id from file' {
    $registry = New-Instance $questRegistryType
    $validation = Invoke-RegistryLoadFromFiles $registry @(
        (Get-SampleContentPath 'quests\missing-step-id.json')
    )
    Assert-ValidationContains $validation 'missing quest step id' 'File-loaded missing quest step ID should fail.'
}

Run-Case 'quest duplicate objective ids from file' {
    $registry = New-Instance $questRegistryType
    $validation = Invoke-RegistryLoadFromFiles $registry @(
        (Get-SampleContentPath 'quests\duplicate-objective-id.json')
    )
    Assert-ValidationContains $validation 'duplicate quest objective id' 'File-loaded duplicate objective IDs should fail.'
}

Run-Case 'quest missing chain endpoint from file' {
    $registry = New-Instance $questRegistryType
    $validation = Invoke-RegistryLoadFromFiles $registry @(
        (Get-SampleContentPath 'quests\missing-chain-endpoint.json')
    )
    Assert-ValidationContains $validation 'missing to quest id' 'File-loaded missing quest chain endpoint should fail.'
}

Run-Case 'quest valid chain endpoint from file' {
    $registry = New-Instance $questRegistryType
    $validation = Invoke-RegistryLoadFromFiles $registry @(
        (Get-SampleContentPath 'quests\valid-chain-endpoint.json')
    )
    Assert-ValidationValid $validation 'File-loaded valid quest chain endpoint should pass.'
    Assert-Equal 1 (Get-PropertyValue $registry 'PackCount') 'File-loaded valid chain registry should load one pack.'
    Assert-Equal 2 (Get-PropertyValue $registry 'QuestCount') 'File-loaded valid chain registry should load two quests.'
}

Write-Host '[PASS] Arete framework validation harness passed 131 cases.'
