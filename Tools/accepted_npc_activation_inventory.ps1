param([switch]$Validate, [switch]$SelfTest)

# Read-only source projection. This tool never loads an engine, client, SQL connection,
# runtime capture, or native resource database. Generated artifacts are evidence only.
$ErrorActionPreference = 'Stop'
$inventoryRoot = Split-Path -Parent $PSScriptRoot
$legacyRoot = 'AORebirth/Server/ZoneEngine'
$sourceCache = @{}
$sourceHashes = @{}
$sourceLineOffsets = @{}
$factsCache = @{}
function Read-Source([string]$Path) {
    if (-not $sourceCache.ContainsKey($Path)) {
        $full = Join-Path $inventoryRoot $Path
        $sourceCache[$Path] = [IO.File]::ReadAllText($full).Replace("`r`n", "`n")
        $sourceHashes[$Path] = (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant()
        $sourceLineOffsets[$Path] = @([regex]::Matches($sourceCache[$Path], "`n") | ForEach-Object { $_.Index })
    }
    return $sourceCache[$Path]
}
function Source-Ref([string]$Path, [int]$Offset = 0) {
    $null = Read-Source $Path
    $offsets = $sourceLineOffsets[$Path]; $lo = 0; $hi = $offsets.Count
    while ($lo -lt $hi) {
        $mid = [int][Math]::Floor(($lo + $hi) / 2)
        if ($offsets[$mid] -lt $Offset) { $lo = $mid + 1 } else { $hi = $mid }
    }
    return [ordered]@{ path = $Path; line = 1 + $lo; sha256 = $sourceHashes[$Path] }
}
function Relative-Path([string]$Path) {
    return $Path.Substring($inventoryRoot.Length + 1).Replace('\', '/')
}
# Lexical balanced scanner, not a C# interpreter. Strings and comments cannot alter
# nesting. Unresolved expressions remain exact source expressions, never guessed values.
function Find-Boundary([string]$Source, [int]$Start, [bool]$Value = $false) {
    $depth = 0; $quoted = [char]0; $lineComment = $false; $blockComment = $false
    for ($i = $Start; $i -lt $Source.Length; $i++) {
        $c = $Source[$i]; $next = if ($i + 1 -lt $Source.Length) { $Source[$i + 1] } else { [char]0 }
        if ($lineComment) { if ($c -eq "`n") { $lineComment = $false }; continue }
        if ($blockComment) { if ($c -eq '*' -and $next -eq '/') { $blockComment = $false; $i++ }; continue }
        if ($quoted -ne [char]0) {
            if ($c -eq '\') { $i++; continue }
            if ($c -eq $quoted) { $quoted = [char]0 }
            continue
        }
        if ($c -eq '/' -and $next -eq '/') { $lineComment = $true; $i++; continue }
        if ($c -eq '/' -and $next -eq '*') { $blockComment = $true; $i++; continue }
        if ($c -eq '"' -or $c -eq "'") { $quoted = $c; continue }
        if ($Value -and $depth -eq 0 -and ($c -eq ',' -or $c -eq '}')) { return $i }
        if ($c -eq '{' -or $c -eq '(' -or $c -eq '[') { $depth++ }
        if ($c -eq '}' -or $c -eq ')' -or $c -eq ']') {
            $depth--
            if (-not $Value -and $depth -eq 0) { return $i + 1 }
        }
    }
    throw "Unterminated C# source expression at $Start"
}
function Source-Facts([string]$Path) {
    if ($factsCache.ContainsKey($Path)) { return $factsCache[$Path] }
    $source = Read-Source $Path
    $facts = [ordered]@{}
    $patterns = [ordered]@{
        identity = '\b(SourceNpcId|SourceNpcInstance|SourceIdentity|ConfiguredIdentity|NpcInstance|CapturedIdentity)\b'
        playfield = '\b(\w*Playfield\w*|\w*Resource\w*)\s*(=|==|!=)'
        template = '\b(TemplateHash|TemplateId|SpawnMobFromTemplate|InstantiateMobSpawn)\b'
        level = '\b(Level|CapturedLevel|SetLevel|ResolveLevel|ResolveHealth)\b|StatIds\.level'
        stats = '\b(Health|MonsterData|RunSpeed|NpcFamily|SetBaseValueWithoutTriggering)\b'
        appearance = '\b(Textures|Meshes|HeadMesh|AppearanceValue|Appearance|ExtendedTexture|VisualFlags)\b'
        capabilities = '\b(KnuBot|SetKnuBot|Vendor|Shop|Dialogue|CombatContract|TryPrepareCombat|DoNotDoTimers|Respawn|Corpse|Loot)\w*\b'
    }
    foreach ($entry in $patterns.GetEnumerator()) {
        $lines = [Collections.Generic.List[int]]::new()
        $offset = 0
        foreach ($line in $source.Split("`n")) {
            if ($line -match $entry.Value) { $lines.Add((Source-Ref $Path $offset).line) }
            $offset += $line.Length + 1
        }
        $facts[$entry.Key] = $lines.ToArray()
    }
    $factsCache[$Path] = $facts
    return $facts
}
function Identity-Anchors([string[]]$Paths, [string]$Identity) {
    if ($Identity -notmatch '^0x[0-9a-fA-F]{1,8}$') { return @() }
    $numeric = [Convert]::ToUInt32($Identity.Substring(2), 16)
    $literal = '0x' + $numeric.ToString('X8')
    $pattern = '(?i)\b(?:' + [regex]::Escape($literal) + '|' + $numeric.ToString([Globalization.CultureInfo]::InvariantCulture) + ')\b'
    foreach ($path in $Paths) {
        foreach ($match in [regex]::Matches((Read-Source $path), $pattern)) { Source-Ref $path $match.Index }
    }
}
function Commercial-Capability($Spec) {
    $source = Read-Source $Spec.path
    $constructor = [regex]::Match($source, 'new AcceptedNpcBinding\(')
    if (-not $constructor.Success) { throw 'No exact accepted NPC capability declaration' }
    $start = $source.IndexOf('(', $constructor.Index); $finish = Find-Boundary $source $start
    $body = $source.Substring($start, $finish - $start)
    $value = [regex]::Match($body, ',\s*(true|false|content\.HasCapturedStock)\s*\)$')
    if (-not $value.Success) { throw 'Unrecognized accepted vendor capability expression; do not infer from quest hand-ins' }
    $expression = $value.Groups[1].Value
    if ($expression -eq 'true' -or $expression -eq 'false') {
        return [ordered]@{ value = $expression -eq 'true'; expression = $expression; source = Source-Ref $Spec.path $constructor.Index }
    }
    if ($null -eq $Spec.definition) { throw 'Captured stock capability lacks its exact provider declaration' }
    $providerPath = $Spec.definition.path; $provider = Read-Source $providerPath
    if ($provider -notmatch 'this\.HasCapturedStock\s*=\s*stock\s*!=\s*null') { throw 'Captured stock capability contract changed' }
    $lineStart = if ($Spec.definition.line -eq 1) { 0 } else { $sourceLineOffsets[$providerPath][$Spec.definition.line - 2] + 1 }
    $callStart = $provider.IndexOf('(', $lineStart); $callEnd = Find-Boundary $provider $callStart
    # Create's existing optional stockEvidence argument follows stock for Container
    # Supplier. It is provenance text, not a second stock/capability decision.
    $stock = [regex]::Match($provider.Substring($callStart, $callEnd - $callStart), ',\s*(\w+Stock)\(\)\s*(?:,\s*\w+StockEvidence\s*)?\)$')
    if (-not $stock.Success) { throw 'Captured vendor row stock expression needs explicit source review' }
    $factory = [regex]::Match($provider, 'private static CapturedSubwayVendorStockDefinition\[\]\s+' + [regex]::Escape($stock.Groups[1].Value) + '\(\)\s*\{\s*return new\[\]\s*\{')
    if (-not $factory.Success) { throw 'Captured vendor stock is not an explicit non-null immutable array' }
    return [ordered]@{ value = $true; expression = $expression; source = Source-Ref $Spec.path $constructor.Index
        stockSource = Source-Ref $providerPath $factory.Index }
}
if ($SelfTest) {
    $sample = 'new Npc { Name = "brace }", Values = new[] { 1, 2 /* } */ }, X = 3 } tail'
    $start = $sample.IndexOf('{'); $finish = Find-Boundary $sample $start
    if ($sample.Substring($finish) -cne ' tail') { throw 'Balanced initializer/string/comment self-test failed' }
    $start = $sample.IndexOf('new[]'); $finish = Find-Boundary $sample $start $true
    if ($sample.Substring($start, $finish - $start) -cne 'new[] { 1, 2 /* } */ }') { throw 'Nested field expression self-test failed' }
    $sample = "{ // ignored }`n Name = `"text`", X = Call(2, 3) } suffix"
    if ($sample.Substring((Find-Boundary $sample 0)) -cne ' suffix') { throw 'Line-comment self-test failed' }
    Write-Output 'NPC_INVENTORY_LEXICAL_SELFTEST=PASS'
    exit 0
}

$projectPath = "$legacyRoot/ZoneEngine.csproj"
[xml]$project = Read-Source $projectPath
$compiled = @($project.SelectNodes("//*[local-name()='Compile']") | ForEach-Object {
    $full = [IO.Path]::GetFullPath((Join-Path (Join-Path $inventoryRoot $legacyRoot) $_.Include))
    if (-not $full.StartsWith($inventoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Compile source escaped workspace' }
    Relative-Path $full
} | Sort-Object -Unique)
$fileByType = @{}
foreach ($path in $compiled) {
    if (-not (Test-Path -LiteralPath (Join-Path $inventoryRoot $path))) { throw "Missing compiled source: $path" }
    foreach ($match in [regex]::Matches((Read-Source $path), '\b(?:class|struct)\s+(\w+)')) {
        $name = $match.Groups[1].Value
        if (-not $fileByType.ContainsKey($name)) { $fileByType[$name] = [Collections.Generic.List[string]]::new() }
        if (-not $fileByType[$name].Contains($path)) { $fileByType[$name].Add($path) }
    }
}

$systemsPath = "$legacyRoot/Core/Playfields/PlayfieldRuntimeSystems.cs"
$systems = Read-Source $systemsPath
$modules = @([regex]::Matches($systems, 'new\s+(\w+ContentModule)\s*\(') | ForEach-Object {
    $name = $_.Groups[1].Value
    if (-not $fileByType.ContainsKey($name)) { throw "Registered module is not compiled: $name" }
    $path = $fileByType[$name][0]; $body = Read-Source $path
    [ordered]@{ name = $name; registration = Source-Ref $systemsPath $_.Index; source = Source-Ref $path
        invokesNpcDispatcher = $body.Contains('registration.RegisterCapturedNpcSpawns()')
        facts = Source-Facts $path }
})
if ($modules.Count -ne 19) { throw 'Registered module set changed; review this inventory scope before promotion' }
$unregistered = @($fileByType.Keys | Where-Object { $_ -match 'ContentModule$' -and $_ -notin $modules.name } | Sort-Object)

$dispatcherPath = "$legacyRoot/Core/Playfields/NPCRuntimeService.cs"
$dispatcher = Read-Source $dispatcherPath
$method = [regex]::Match($dispatcher, 'internal void SpawnCapturedNpcContent\(Identity playfieldIdentity\)\s*\{')
if (-not $method.Success) { throw 'NPC dispatcher signature changed' }
$begin = $dispatcher.IndexOf('{', $method.Index)
$end = Find-Boundary $dispatcher $begin
$dispatcherBody = $dispatcher.Substring($begin, $end - $begin)
$fields = @{}
foreach ($match in [regex]::Matches($dispatcher, 'private readonly (\w+) (\w+);')) { $fields[$match.Groups[2].Value] = $match.Groups[1].Value }
$calls = @([regex]::Matches($dispatcherBody, '(this\.)?(\w+)\.(SpawnForPlayfield|StartForPlayfield|TrySpawnForPlayfield|ActivatePlayfield)\s*\(') | ForEach-Object {
    $type = $_.Groups[2].Value
    if ($_.Groups[1].Success) { $type = $fields[$type] }
    [ordered]@{ type = $type; method = $_.Groups[3].Value; source = Source-Ref $dispatcherPath ($begin + $_.Index) }
})
# Separate sibling spawn surfaces: registration of captured vendor machines, DB
# mob rows, and post-entry/delayed spawners must not disappear behind the dispatcher.
$types = @($calls.type | Sort-Object -Unique) + @('CapturedSubwayVendorRuntimeService', 'PlayfieldDbMobSpawnRuntimeService',
    'OrdinaryEnemyRuntimeService', 'PetRuntimeService', 'ThrakGardenKeySilvertailTransform')
$surfaces = [Collections.Generic.List[object]]::new()
$sourceActors = [Collections.Generic.List[object]]::new()
$initializerTypes = 'new\s+(\w*(?:Npc|MobSpawn))\s*\{'
foreach ($type in ($types | Sort-Object -Unique)) {
    if (-not $fileByType.ContainsKey($type)) { throw "Entry-point type has no compiled definition: $type" }
    $paths = [Collections.Generic.List[string]]::new()
    foreach ($path in $fileByType[$type]) { $paths.Add($path) }
    # Follow only explicit content/data providers, not arbitrary runtime dependencies.
    foreach ($path in @($paths.ToArray())) {
        foreach ($match in [regex]::Matches((Read-Source $path), '\b(\w+(?:ContentProvider|SpawnData|Rules))\b')) {
            $dependency = $match.Groups[1].Value
            if ($fileByType.ContainsKey($dependency)) {
                foreach ($dependencyPath in $fileByType[$dependency]) { if (-not $paths.Contains($dependencyPath)) { $paths.Add($dependencyPath) } }
            }
        }
    }
    $rows = 0
    foreach ($path in $paths) {
        $source = Read-Source $path
        foreach ($match in [regex]::Matches($source, $initializerTypes)) {
            $start = $source.IndexOf('{', $match.Index)
            $finish = Find-Boundary $source $start
            $body = $source.Substring($start, $finish - $start)
            $nameMatch = [regex]::Match($body, '\bName\s*=\s*"([^"\r\n]+)"')
            if (-not $nameMatch.Success) { continue }
            $members = [ordered]@{}
            foreach ($assignment in [regex]::Matches($body, '\b(\w+)\s*=\s*(?!=|>)')) {
                $valueStart = $assignment.Index + $assignment.Length
                $valueEnd = Find-Boundary $body $valueStart $true
                $members[$assignment.Groups[1].Value] = $body.Substring($valueStart, $valueEnd - $valueStart).Trim()
            }
            $reference = Source-Ref $path $match.Index
            $sourceActors.Add([ordered]@{ sourceKey = "$path`:$($reference.line)"; surface = $type; name = $nameMatch.Groups[1].Value
                source = $reference; constructorType = $match.Groups[1].Value; expressions = $members
                originalIdentity = if ($members.Contains('SourceNpcId')) { $members['SourceNpcId'] } else { $null }
                identityNote = 'Only explicit SourceNpcId is extracted; name/coordinates/comments are not manufactured source identities.'
                status = $null; currentNewEngineStatus = $null; assessmentStatus = 'CONSUMER_EXPANSION_PENDING'
                statusScope = 'Outside the completed accepted-mapping ledger until per-record consumer eligibility/alias expansion is assessed; this does not imply missing source data.' })
            $rows++
        }
    }
    $surfaces.Add([ordered]@{ type = $type; entryPoints = @($calls | Where-Object { $_.type -eq $type }); sources = @($paths | ForEach-Object { Source-Ref $_ })
        sourceFacts = @($paths | ForEach-Object { [ordered]@{ source = Source-Ref $_; fieldSourceLines = Source-Facts $_ } }); namedInitializerRecordCount = $rows
        status = $null; currentNewEngineStatus = $null; assessmentStatus = 'CONSUMER_EXPANSION_PENDING'
        actorExpansion = if ($rows -gt 0) { 'STATIC_NAMED_INITIALIZERS_EXTRACTED_NOT_UNIQUE_RUNTIME_ACTORS' } else { 'SOURCE_CALL_OR_RUNTIME_EXPANSION_PENDING' }
        note = 'Registration is Legacy source authority, not proof every conditional record spawns concurrently. New exact connections are listed separately.' })
}

$coveragePath = 'docs/generated/capture_backed_npc_combat_active_coverage.json'
$coverage = (Read-Source $coveragePath) | ConvertFrom-Json
$combat = @($coverage.bindings | ForEach-Object {
    $binding = $_
    [pscustomobject][ordered]@{ bindingKey = $binding.bindingKey; coverageKey = $binding.coverageKey; surface = $binding.surface
        name = $binding.name; actorCount = $binding.actorCount; configuredSourceIdentity = $binding.configuredSourceIdentity
        sourceIdentityHint = $binding.runtimeSourceIdentityHint; playfieldOrResource = $binding.runtimePlayfieldOrResource
        level = $binding.level; levelCandidates = $binding.levelCandidates; monsterData = $binding.monsterData
        combatClassification = $binding.classification; combatReady = $binding.runtimeContractReady
        profileSelector = $binding.runtimeProfileSelector; evidenceCaptures = $binding.contentEvidenceCaptureIds
        sources = @($binding.contentSources | ForEach-Object { Source-Ref $_ })
        exactIdentityLiteralSourceAnchors = @(Identity-Anchors $binding.contentSources $binding.configuredSourceIdentity)
        fieldEvidenceSourceKeys = @($binding.contentSources)
        missingProjectionFields = 'Template/HP/appearance/noncombat capabilities are not fields in the combat manifest; use exact sourceFieldEvidence line references, never infer them from combat readiness.'
        status = if ($binding.surface -eq 'subway-merchants') { 'ACTIVE' } else { 'ACCEPTED_BUT_NOT_CONNECTED' }
        assessmentStatus = 'EXACT_SOURCE_BINDING_VIEW'
        statusScope = 'Exact accepted source binding activation, not combat certification; generic hash placement does not prove this binding.' }
})
if ($combat.Count -ne $coverage.bindingTotals.bindingRecordCount -or [int]($combat | Measure-Object actorCount -Sum).Sum -ne $coverage.bindingTotals.actorCount) {
    throw 'Combat view did not preserve every existing accepted binding/actor count'
}
foreach ($path in @($coverage.bindings.contentSources | Sort-Object -Unique)) { $null = Source-Facts $path }

# Project actual connected catalog declarations. No hardcoded actor list/count.
# Supported declaration grammars are deliberately narrow; an unfamiliar new catalog
# fails the evidence gate rather than silently disappearing from the inventory.
$socialPath = 'AORebirth/Server/ZoneEngine_New/Core/Mobs/AcceptedSocialNpcCatalog.cs'
$social = Read-Source $socialPath
$adapterSpecs = [Collections.Generic.List[object]]::new()
foreach ($match in [regex]::Matches($social, '"legacy:(\w+):(\d+):([0-9A-Fa-f]{8})"')) {
    $adapterSpecs.Add([ordered]@{ key = $match.Value.Trim('"'); identity = $match.Groups[3].Value; pf = [int]$match.Groups[2].Value
        legacy = @($match.Groups[1].Value); path = $socialPath; offset = $match.Index })
}
foreach ($catalogMatch in [regex]::Matches($social, '\.Concat\((Accepted\w+Catalog)\.Definitions\)')) {
    $catalogName = $catalogMatch.Groups[1].Value
    $path = "AORebirth/Server/ZoneEngine_New/Core/Mobs/$catalogName.cs"; $source = Read-Source $path
    $prefix = [regex]::Match($source, '"legacy:(\w+):(\d+):"')
    $before = $adapterSpecs.Count
    foreach ($match in [regex]::Matches($source, '\bDefinition\((0x[0-9A-Fa-f]+),\s*(Create\w+)\)')) {
        if (-not $prefix.Success) { throw "Cannot resolve explicit adapter PF/source: $catalogName" }
        $adapterSpecs.Add([ordered]@{ key = $match.Groups[2].Value; identity = $match.Groups[1].Value.Substring(2); pf = [int]$prefix.Groups[2].Value
            legacy = @($prefix.Groups[1].Value); path = $path; offset = $match.Index })
    }
    foreach ($match in [regex]::Matches($source, '\bNpcDefinition\((\w+ContentProvider)\.SourceNpcInstance,')) {
        if (-not $prefix.Success) { throw "Cannot resolve provider adapter PF/source: $catalogName" }
        $provider = $match.Groups[1].Value; $providerPath = $fileByType[$provider][0]
        $id = [regex]::Match((Read-Source $providerPath), '\bSourceNpcInstance\s*=\s*unchecked\(\(int\)(0x[0-9A-Fa-f]+)\)')
        if (-not $id.Success) { throw "Cannot resolve exact source identity constant: $provider" }
        $adapterSpecs.Add([ordered]@{ key = $provider; identity = $id.Groups[1].Value.Substring(2); pf = [int]$prefix.Groups[2].Value
            legacy = @($prefix.Groups[1].Value, $provider); path = $path; offset = $match.Index })
    }
    $selectedProvider = [regex]::Match($source, '\b(\w+ContentProvider)\.Definitions\.Select\(content =>')
    if ($selectedProvider.Success) {
        if (-not $prefix.Success) { throw "Cannot resolve provider projection PF/source: $catalogName" }
        $provider = $selectedProvider.Groups[1].Value; $providerPath = $fileByType[$provider][0]
        foreach ($match in [regex]::Matches((Read-Source $providerPath), '\bCreate\(\s*(0x[0-9A-Fa-f]+),\s*(0x[0-9A-Fa-f]+),\s*"([^"\r\n]+)"')) {
            $adapterSpecs.Add([ordered]@{ key = $match.Groups[3].Value; identity = $match.Groups[1].Value.Substring(2); pf = [int]$prefix.Groups[2].Value
                legacy = @($provider); path = $path; offset = $selectedProvider.Index; definition = Source-Ref $providerPath $match.Index })
        }
    }
    if ($source.Contains('Placements.Select(content =>')) {
        $placementHeader = [regex]::Match($source, 'IReadOnlyList<Placement> Placements[^\{]*\{[^\{]*\{')
        # The property initializer is an explicit immutable array of helper calls.
        $array = [regex]::Match($source, 'Placements\s*\{\s*get;\s*\}\s*=\s*Array.AsReadOnly\(new\[\]\s*\{')
        if (-not $array.Success) { throw "Unsupported placement declaration: $catalogName" }
        $start = $source.LastIndexOf('{', $array.Index + $array.Length - 1)
        $finish = Find-Boundary $source $start
        foreach ($match in [regex]::Matches($source.Substring($start, $finish - $start), '\b(\w+)\((0x[0-9A-Fa-f]+),')) {
            $helper = $match.Groups[1].Value
            $method = [regex]::Match($source, 'static Placement ' + [regex]::Escape($helper) + '\([^\{]+\{')
            if (-not $method.Success) { throw "Missing typed placement helper: $helper" }
            $bodyStart = $source.IndexOf('{', $method.Index); $bodyEnd = Find-Boundary $source $bodyStart
            $body = $source.Substring($bodyStart, $bodyEnd - $bodyStart)
            $pf = [regex]::Match($body, 'return new\((\d+),\s*source,')
            $provider = [regex]::Match($body, '\b(\w+ContentProvider)\.Definitions')
            if (-not $pf.Success -or -not $provider.Success) { throw "Unresolved typed placement helper: $helper" }
            $legacyNames = @([regex]::Matches($source, '"(\w+Spawn)\.cs"') | ForEach-Object { $_.Groups[1].Value }) + @($provider.Groups[1].Value)
            $adapterSpecs.Add([ordered]@{ key = "$helper`:$($match.Groups[2].Value)"; identity = $match.Groups[2].Value.Substring(2); pf = [int]$pf.Groups[1].Value
                legacy = $legacyNames; path = $path; offset = $start + $match.Index; definition = Source-Ref $path ($start + $match.Index) })
        }
    }
    if ($adapterSpecs.Count -eq $before) { throw "Connected New catalog requires explicit source grammar review: $catalogName" }
}
$adapters = @($adapterSpecs | ForEach-Object {
    $capability = Commercial-Capability $_
    [ordered]@{ key = $_.key; contentNpcIdentity = 'SimpleChar:' + $_.identity.ToUpperInvariant(); playfield = $_.pf
        legacyTypes = $_.legacy; legacySources = @($_.legacy | ForEach-Object { Source-Ref $fileByType[$_][0] })
        newSource = Source-Ref $_.path $_.offset; declarationSource = $_.definition
        fieldSourceLines = Source-Facts $_.path; hasVendor = $capability.value; commercialCapabilityEvidence = $capability; status = 'ACTIVE'
        note = 'Actual connected catalog declaration, not a fixed actor list. Shop capability requires complete validated item catalog; consult exact capability source fields.' }
})
$activeIdentities = @($adapters.contentNpcIdentity)
foreach ($binding in $combat) {
    $identity = $binding.configuredSourceIdentity -replace '^0x', 'SimpleChar:'
    if ($identity -in $activeIdentities) { $binding.status = 'ACTIVE'; continue }
    if ([string]::IsNullOrWhiteSpace($binding.configuredSourceIdentity)) {
        $related = @($adapters | Where-Object {
            $_.playfield -eq $binding.playfieldOrResource -and @($_.legacySources | Where-Object { $_.path -in $binding.sources.path }).Count -gt 0
        })
        if ($related.Count -gt 0) {
            $binding.status = $null
            $binding.assessmentStatus = 'CROSS_VIEW_JOIN_PENDING_NO_SOURCE_ID_IN_COMBAT_MANIFEST'
        }
    }
}
$standalone = [Collections.Generic.List[object]]::new()
$activationPath = 'AORebirth/Server/ZoneEngine_New/Core/Mobs/AcceptedNpcActivationService.cs'
foreach ($match in [regex]::Matches((Read-Source $activationPath), '(Accepted\w+Catalog)\.StandaloneDefinitions')) {
    $path = "AORebirth/Server/ZoneEngine_New/Core/Mobs/$($match.Groups[1].Value).cs"; $source = Read-Source $path
    $provider = [regex]::Match($source, '(\w+ContentProvider)\.Vendors\.Select')
    if (-not $provider.Success) { throw 'Standalone catalog projection requires source review' }
    $providerPath = $fileByType[$provider.Groups[1].Value][0]
    foreach ($row in [regex]::Matches((Read-Source $providerPath), 'new\s+\w+VendorDefinition\(\s*"([^"\r\n]+)",\s*(\d+),\s*(\d+),')) {
        $standalone.Add([ordered]@{ name = $row.Groups[1].Value; sourceVendorInstance = [int]$row.Groups[2].Value; vendorTemplateId = [int]$row.Groups[3].Value
            source = Source-Ref $providerPath $row.Index; newSource = Source-Ref $path; status = 'ACTIVE'; npc = $false
            note = 'Separate accepted world machine, never counted as an NPC actor.' })
    }
}

$officialFiles = @(Get-ChildItem -LiteralPath (Join-Path $inventoryRoot 'docs/generated/playfields/placements') -Filter 'pf_*.json' -File | Sort-Object Name)
$official = [Collections.Generic.List[object]]::new(); $rawCount = 0
foreach ($file in $officialFiles) {
    $relative = Relative-Path $file.FullName
    $document = (Read-Source $relative) | ConvertFrom-Json
    $rawCount += @($document.Records).Count
    foreach ($record in $document.Records) {
        if (-not ($record.RuntimeActivationAuthorized -eq $true -and $record.IdentityResolved -eq $true -and $record.BehaviorReady -eq $true -and -not [string]::IsNullOrWhiteSpace($record.ExistingAoRebirthProfile))) { continue }
        $official.Add([ordered]@{ key = $record.OfficialSpawnRecordId; source = Source-Ref $relative
            playfield = $record.PlayfieldId; sourceNpcId = $record.SourceNpcId; existingProfile = $record.ExistingAoRebirthProfile
            hash = $record.CanonicalAcgHashText; resolvedMobTemplateHash = $record.ResolvedMobTemplateHash
            mobTemplateEvidenceSource = $record.MobTemplateEvidenceSource; levelMinimum = $record.LevelMinimum; levelMaximum = $record.LevelMaximum
            position = @($record.PositionX, $record.PositionY, $record.PositionZ)
            status = if (-not [string]::IsNullOrWhiteSpace($record.ResolvedMobTemplateHash) -and -not [string]::IsNullOrWhiteSpace($record.MobTemplateEvidenceSource)) { 'ACTIVE' } else { 'ACCEPTED_BUT_NOT_CONNECTED' }
            note = 'Separate placement/hash layer; existing accepted profile must be reconciled, not counted again as another actor. ACTIVE here denotes authorization inputs, not runtime instantiation proof.' })
    }
}
$bundlePath = "$legacyRoot/Core/Missions/MissionAcgCapturedLayoutCatalog.g.cs"
$bundles = @([regex]::Matches((Read-Source $bundlePath), '"(capture-[0-9a-fA-F-]+)"') | ForEach-Object {
    [ordered]@{ bundleKey = $_.Groups[1].Value; source = Source-Ref $bundlePath $_.Index; status = 'ACTIVE'
        newMaterializer = Source-Ref 'AORebirth/Server/ZoneEngine_New/Core/Missions/GeneratedMissionWorld.cs'
        newNpcFactory = Source-Ref 'AORebirth/Server/ZoneEngine_New/Core/Mobs/GeneratedMissionNpcFactory.cs'
        identity = 'Per-accepted-instance persisted identity; never a fabricated global source NPC ID.'
        stats = 'Typed accepted object Level/HP plus MissionNpcDifficultyPolicy/MissionNpcCombatPolicy; retained across reconnect.'
        note = 'Dynamic mission objects are separate from static placement totals; exact selectable bundle consumer tests required.' }
})

# Enumerate every direct NPC creation call in the actual compiled Legacy project.
# This intentionally exposes dynamic/KnuBot/script surfaces beyond content modules.
$creationCalls = [Collections.Generic.List[object]]::new()
foreach ($path in $compiled) {
    $source = Read-Source $path
    foreach ($match in [regex]::Matches($source, '\b(?:SpawnMobFromTemplate|InstantiateMobSpawn|SpawnNpc|SpawnNPC)\s*\(')) {
        $lineEnd = $source.IndexOf("`n", $match.Index); if ($lineEnd -lt 0) { $lineEnd = $source.Length }
        $excludedTool = $path -eq "$legacyRoot/ChatCommands/Spawn.cs" -or $path -eq "$legacyRoot/Core/CombatTestMobArchetype.cs"
        $creationCalls.Add([ordered]@{ source = Source-Ref $path $match.Index
            expression = $source.Substring($match.Index, $lineEnd - $match.Index).Trim()
            mappedSurface = @($surfaces | Where-Object { $path -in $_.sources.path } | ForEach-Object { $_.type })
            status = if ($excludedTool) { 'REJECTED' } else { $null }
            assessmentStatus = if ($excludedTool) { 'EXPLICIT_TOOL_SCOPE_EXCLUSION' } else { 'DYNAMIC_FACTORY_CONTEXT_EXPANSION_PENDING' }
            note = if ($excludedTool) { 'Explicit GM/test spawn tool, not an ambient population or automatically accepted authored actor.' } else { 'Call site inventory, not an invented count of runtime DB/script/quest-generated actors.' } })
    }
}
$blockers = @(
    'ACTOR_CENSUS_NOT_EXHAUSTIVE: compiled module/dispatcher and selected direct creation methods in ZoneEngine.csproj are enumerated, but DB rows, dynamically compiled scripts, owner-generated pets, quest transforms and transitive content factories are not all expanded into deduplicated exact actor rows.',
    'STATIC_SOURCE_RECORDS_OVERLAP_COMBAT_BINDINGS: the source initializer table and combat binding table are alternative views; never sum them or infer identity from matching names/positions.',
    'NEW_BINDING_GAP: accepted Legacy profiles without exact New factory/placement/behavior adapters remain ACCEPTED_BUT_NOT_CONNECTED; combat resolver presence alone is not an activation bridge.',
    'DATABASE_ACTOR_ROWS_NOT_INSPECTED: live/deployed DB state is out of scope; compiled MobSpawnDao path is recorded, not queried.',
    'RAW_PLACEMENTS_NOT_ACCEPTED_CONTENT: only the explicit four-field accepted authorization predicate enters the separate official layer; other raw records are UNPROVEN, not automatically missing gameplay.',
    'DYNAMIC_INSTANCE_COUNTS_NOT_STATIC: generated five-bundle mission objects and conditional authored/escort spawns cannot be added to global static actor counts.'
)
$document = [ordered]@{
    formatVersion = 1; purpose = 'Evidence-only accepted NPC activation reconciliation; never runtime configuration.'
    reportedGameplayCheckpoint = 'Coordinating task recorded 420/420 focused tests PASS for the final pre-commit gap-closure checkpoint. Historical attribution only: this generator does not run gameplay tests or confer that result on future source changes.'
    sourceProject = Source-Ref $projectPath; generator = 'Tools/accepted_npc_activation_inventory.ps1'
    exhaustiveActorInventory = $false; exhaustiveInventoryStatus = 'INCOMPLETE'
    exhaustiveDeclaredScope = 'Compiled Legacy ZoneEngine project registration, central dispatcher and SpawnMobFromTemplate/InstantiateMobSpawn/SpawnNpc/SpawnNPC call sites; explicitly extracted static named initializer and existing combat binding views. Runtime-compiled scripts and other library creation APIs are not claimed exhaustive.'
    statusDefinitions = [ordered]@{ ACTIVE = 'Explicit New source connection within stated row scope; not live gameplay proof.'; ACCEPTED_BUT_NOT_CONNECTED = 'Accepted Legacy source behavior lacks this exact New binding.'; DATA_INCOMPLETE = 'A proven missing required authoritative source field, never unfinished analysis.'; UNPROVEN = 'No accepted runtime activation authority.'; REJECTED = 'Explicitly excluded from the selected registered source surface; not deletion of evidence.'; CONSUMER_EXPANSION_PENDING = 'Assessment incomplete; currentNewEngineStatus remains null and this record is outside the completed accepted-mapping ledger.' }
    counts = [ordered]@{ registeredModules = $modules.Count; dispatcherCalls = $calls.Count; entrySurfaces = $surfaces.Count
        staticNamedInitializerRecords = $sourceActors.Count; combatBindings = $combat.Count; combatActors = [int]($combat | Measure-Object actorCount -Sum).Sum
        combatReadyBindings = @($combat | Where-Object combatReady).Count; explicitNewSocialAdapters = @($adapters).Count
        commercialNpcShopCapabilities = @($adapters | Where-Object { $_.hasVendor }).Count
        standaloneAcceptedWorldShops = $standalone.Count
        generatedSelectableBundles = $bundles.Count; rawOfficialPlacementRecords = $rawCount; acceptedOfficialPlacementRecords = $official.Count
        compiledDirectCreationCalls = $creationCalls.Count }
    blockers = $blockers; registeredModules = $modules
    sourceFieldEvidence = @($factsCache.Keys | Sort-Object | ForEach-Object { [ordered]@{ key = $_; source = Source-Ref $_; fieldSourceLines = $factsCache[$_] } })
    unregisteredCompiledModules = @($unregistered | ForEach-Object { [ordered]@{ type = $_; status = 'REJECTED'; reason = 'Not registered in the selected PlayfieldRuntimeSystems constructor; no inference of active NPC spawning.'; source = Source-Ref $fileByType[$_][0] } })
    entrySurfaces = $surfaces.ToArray(); sourceActorRecords = $sourceActors.ToArray(); combatBindingView = $combat
    explicitNewSocialConnections = @($adapters); standaloneAcceptedWorldShops = $standalone.ToArray(); generatedMissionBundles = $bundles
    acceptedOfficialPlacementLayer = $official.ToArray(); compiledCreationCalls = $creationCalls.ToArray()
}
$json = ($document | ConvertTo-Json -Depth 35 -Compress) + "`n"
$lines = [Collections.Generic.List[string]]::new()
$lines.Add('# ZoneEngine_New accepted NPC activation inventory')
$lines.Add('')
$lines.Add('Evidence-only source projection. This is not a runtime catalog or a claim of exhaustive actor parity. Reproduce with `powershell -NoProfile -File Tools/accepted_npc_activation_inventory.ps1`; use `-Validate` to compare current outputs without writing and `-SelfTest` to test the lexical source projector.')
$lines.Add('')
$lines.Add('## Scope and decision')
$lines.Add('')
$lines.Add('The compiled Legacy registration/dispatcher/direct-creation-call surfaces are enumerated. Exact actor expansion is incomplete for the explicitly listed dynamic/database/transitive sources. That is a concrete inventory blocker, not evidence that accepted NPCs may be dropped. The current New adapters do not cover the complete Legacy activation surface.')
$lines.Add('')
$lines.Add('Unfinished consumer analysis uses assessmentStatus=CONSUMER_EXPANSION_PENDING with currentNewEngineStatus=null, outside the completed accepted-mapping ledger. It is not DATA_INCOMPLETE: that label requires an actually proven missing authoritative field. The whole exhaustive inventory remains INCOMPLETE until the pending consumer expansion is performed.')
$lines.Add('')
foreach ($entry in $document.counts.GetEnumerator()) { $lines.Add("- $($entry.Key): $($entry.Value)") }
$lines.Add('')
$lines.Add('Do not sum these overlapping views. Combat certification is independent of placement/social/vendor activation. In particular, Legacy unresolved combat contracts can retain passive visible actors. Raw placement records are not all accepted actors.')
$lines.Add('')
$lines.Add('The eight initially unmapped creation calls are reconciled: four GM Spawn commands and CombatTestMobArchetype are explicitly excluded from ambient accepted population; OrdinaryEnemyRuntimeService is the existing ordinary factory; PetRuntimeService is an owner/nano-generated factory; ThrakGardenKeySilvertailTransform is an accepted quest-triggered actor transform. The latter three are now separate source surfaces, not missing source identities fabricated from their runtime instance counts.')
$lines.Add('')
$lines.Add('## Registered modules')
$lines.Add('')
$lines.Add('| Module | NPC dispatcher | Exact source |')
$lines.Add('| --- | --- | --- |')
foreach ($module in $modules) { $lines.Add("| $($module.name) | $($module.invokesNpcDispatcher) | $($module.source.path):$($module.source.line) |") }
$lines.Add('')
$lines.Add('## Accepted NPC entry surfaces')
$lines.Add('')
$lines.Add('| Surface | Extracted named source records (not live count) | Authority |')
$lines.Add('| --- | ---: | --- |')
foreach ($surface in $surfaces) { $lines.Add("| $($surface.type) | $($surface.namedInitializerRecordCount) | $(($surface.sources.path) -join '<br>') |") }
$lines.Add('')
$lines.Add('Every surface retains exact source hashes and fieldSourceLines references for identity, PF, template, level, stats, appearance and capability expressions in the JSON. Source rows preserve exact initializer expressions. A missing field remains missing; names/positions/comments do not manufacture original IDs. Source-reference line arrays avoid duplicating entire runtime source files into the evidence artifact.')
$lines.Add('')
$lines.Add('If a combat-manifest row has no configured source identity while its exact Legacy source/PF has a connected New catalog, its cross-view join remains pending with null status. For example, the Thrak combat view omits IDs even though the garden provider supplies exact vendor IDs. Name matching is not used to manufacture that missing manifest join; the separate New catalog connections retain their real identities.')
$lines.Add('')
$lines.Add('## Existing combat-binding view')
$lines.Add('')
$lines.Add('| Surface | Bindings | Actors | Combat-ready bindings |')
$lines.Add('| --- | ---: | ---: | ---: |')
foreach ($group in ($combat | Group-Object surface | Sort-Object Name)) { $lines.Add("| $($group.Name) | $($group.Count) | $(($group.Group | Measure-Object actorCount -Sum).Sum) | $(@($group.Group | Where-Object combatReady).Count) |") }
$lines.Add('')
$lines.Add('## Explicit New connections')
$lines.Add('')
foreach ($adapter in $adapters) { $lines.Add("- ACTIVE: PF$($adapter.playfield) $($adapter.contentNpcIdentity), $($adapter.key); commercial shop capability=$($adapter.hasVendor); $($adapter.newSource.path). Declared shops still require complete actual item data.") }
$lines.Add('Quest hand-ins do not imply commercial vendor capability. Commercial NPC shop counts derive from the actual AcceptedNpcBinding final argument and explicit provider stock contract, not actor names or quest interaction availability.')
foreach ($shop in $standalone) { $lines.Add("- ACTIVE standalone world shop (not an NPC): $($shop.name), source vendor $($shop.sourceVendorInstance); $($shop.source.path):$($shop.source.line).") }
$lines.Add('- ACTIVE: five accepted generated mission bundle routes, via GeneratedMissionWorld and GeneratedMissionNpcFactory; dynamic persisted instance counts remain separate.')
$lines.Add('')
$lines.Add('## Official placement layer')
$lines.Add('')
$lines.Add("The current artifacts contain $($official.Count) records with RuntimeActivationAuthorized=true, IdentityResolved=true, BehaviorReady=true and a nonempty ExistingAoRebirthProfile, out of $rawCount raw records. These are separate placement/hash evidence for existing profiles, not extra unique actors. Missing exact New hash/template evidence does not revoke the accepted Legacy profile; the JSON preserves each exact record and its missing bridge fields.")
$lines.Add('')
$lines.Add('## Remaining blockers and exclusions')
$lines.Add('')
foreach ($blocker in $blockers) { $lines.Add("- $blocker") }
foreach ($name in $unregistered) { $lines.Add("- REJECTED from this registered source set: $name. Compiled source presence alone is not activation.") }
$lines.Add('')
$lines.Add('## Validation boundary')
$lines.Add('')
$lines.Add('The coordinating task recorded **420/420 focused tests PASS** for the final pre-commit gap-closure checkpoint, including exact commercial capability and real packaged stock/pricing checks. This is attributed historical test evidence, not a test executed by this generator or a PASS automatically conferred on future source changes.')
$lines.Add('')
$lines.Add('Generator validation proves reproducibility, source presence and declared registration scope only. No build, live session, client, capture, migration or database inspection is performed. Full runtime readiness remains the root acceptance decision; this evidence explicitly cannot certify exhaustive actor parity.')
$markdown = ($lines -join "`n") + "`n"
$outputRoot = Join-Path $inventoryRoot 'docs/evidence/ZONEENGINE_NEW_NPC_ACTIVATION_INVENTORY'
foreach ($output in @(@('.json', $json), @('.md', $markdown))) {
    $path = $outputRoot + $output[0]
    if ($Validate) {
        if (-not (Test-Path -LiteralPath $path) -or [IO.File]::ReadAllText($path).Replace("`r`n", "`n") -cne $output[1]) { throw "Stale inventory: $path" }
    } else { [IO.File]::WriteAllText($path, $output[1], [Text.UTF8Encoding]::new($false)) }
}
Write-Output 'NPC_ACTIVATION_INVENTORY=PASS'
Write-Output 'EXHAUSTIVE_ACTOR_PARITY=NOT_PROVEN'
Write-Output ("REGISTERED_MODULES={0} ENTRY_SURFACES={1} COMBAT_BINDINGS={2} STATIC_SOURCE_RECORDS={3} OFFICIAL_ACCEPTED={4}" -f $modules.Count, $surfaces.Count, $combat.Count, $sourceActors.Count, $official.Count)
