param(
    [string]$AoDir = "C:\Funcom\Anarchy Online",
    [string]$OutDir = "C:\Users\Mike\Documents\Cellao-Clean\CellAO\Documentation\AOClientDllFunctionMap"
)

$ErrorActionPreference = "Stop"

function Get-ToolPath {
    param([string]$Name)
    $cmd = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -eq $cmd) {
        throw "Required tool not found in PATH: $Name"
    }
    return $cmd.Source
}

function Get-UndnamePath {
    $known = @(
        "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Tools\MSVC\14.50.35717\bin\Hostx64\x64\undname.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\18\Community\VC\Tools\MSVC\14.50.35717\bin\Hostx64\x64\undname.exe"
    )

    foreach ($path in $known) {
        if (Test-Path -LiteralPath $path) {
            return $path
        }
    }

    $cmd = Get-Command undname.exe -ErrorAction SilentlyContinue
    if ($null -ne $cmd) {
        return $cmd.Source
    }

    return $null
}

function Get-Category {
    param([string]$DllName)
    $thirdParty = @(
        "ACE.dll",
        "Awesomium.dll",
        "icudt42.dll",
        "mfc100.dll",
        "mpir.dll",
        "mss32.dll",
        "msvcp100.dll",
        "msvcr100.dll",
        "PATCHW32.DLL"
    )

    foreach ($name in $thirdParty) {
        if ([string]::Equals($name, $DllName, [StringComparison]::OrdinalIgnoreCase)) {
            return "ThirdPartyRuntime"
        }
    }

    return "AOClient"
}

$undnameCache = @{}
function ConvertFrom-MsvcDecoratedName {
    param(
        [string]$Symbol,
        [string]$UndnamePath
    )

    if ([string]::IsNullOrWhiteSpace($Symbol) -or -not $Symbol.StartsWith("?") -or [string]::IsNullOrWhiteSpace($UndnamePath)) {
        return ""
    }

    if ($undnameCache.ContainsKey($Symbol)) {
        return $undnameCache[$Symbol]
    }

    try {
        $output = & $UndnamePath $Symbol 2>$null
        $line = $output | Where-Object { $_ -like 'is :- "*' } | Select-Object -First 1
        if ($null -ne $line -and $line -match 'is :- "(.+)"') {
            $undnameCache[$Symbol] = $matches[1]
        }
        else {
            $undnameCache[$Symbol] = ""
        }
    }
    catch {
        $undnameCache[$Symbol] = ""
    }

    return $undnameCache[$Symbol]
}

function Get-SymbolHintKind {
    param([string]$Text)
    if ($Text -match '^N3Msg_') { return "N3Msg" }
    if ($Text -match 'IIR_t$') { return "IIRType" }
    if ($Text -match '^Feedback_') { return "Feedback" }
    if ($Text -match '^e_[A-Za-z0-9_]+$') { return "ClientStatName" }
    if ($Text -match 'Corpse|corpse|CORPSE') { return "Corpse" }
    if ($Text -match 'Attack|Fight|Combat|Loot|DynelDead|ToClientDynelDead') { return "CombatLoot" }
    if ($Text -match '^[a-z0-9]+[-_][a-z0-9_-]+\.ani$') { return "AnimationFile" }
    return "Other"
}

function ConvertTo-JsonLine {
    param([object]$Value)
    return ($Value | ConvertTo-Json -Compress -Depth 6)
}

$objdump = Get-ToolPath "objdump.exe"
$strings = Get-ToolPath "strings.exe"
$undname = Get-UndnamePath

$aoRoot = (Resolve-Path -LiteralPath $AoDir).Path
$outRoot = $OutDir
if (-not (Test-Path -LiteralPath $outRoot)) {
    New-Item -ItemType Directory -Force -Path $outRoot | Out-Null
}

$dlls = Get-ChildItem -LiteralPath $aoRoot -File -Filter "*.dll" | Sort-Object Name

$exports = New-Object System.Collections.Generic.List[object]
$decoratedFunctionStrings = New-Object System.Collections.Generic.List[object]
$symbolHints = New-Object System.Collections.Generic.List[object]
$summary = New-Object System.Collections.Generic.List[object]

foreach ($dll in $dlls) {
    Write-Host "Mapping $($dll.Name)"
    $category = Get-Category $dll.Name
    $dllExportCount = 0
    $dllDecoratedCount = 0
    $dllHintCount = 0

    $objdumpOutput = & $objdump -p $dll.FullName 2>$null
    foreach ($line in $objdumpOutput) {
        if ($line -match '^\s*\[\s*(?<slot>\d+)\]\s+\+base\[\s*(?<ordinal>\d+)\]\s+(?<value>[0-9A-Fa-f]+)\s+(?<name>.+?)\s*$') {
            $name = $matches["name"].Trim()
            $undecorated = ConvertFrom-MsvcDecoratedName $name $undname
            $exports.Add([pscustomobject]@{
                Dll = $dll.Name
                Category = $category
                Slot = [int]$matches["slot"]
                Ordinal = [int]$matches["ordinal"]
                ValueHex = $matches["value"]
                Symbol = $name
                Undecorated = $undecorated
            })
            $dllExportCount++
        }
    }

    $seenDecorated = @{}
    $seenHints = @{}
    $stringOutput = & $strings -a $dll.FullName 2>$null
    foreach ($text in $stringOutput) {
        $s = $text.Trim()
        if ([string]::IsNullOrWhiteSpace($s)) {
            continue
        }

        if ($s -match '^\?.+@@') {
            if (-not $seenDecorated.ContainsKey($s)) {
                $seenDecorated[$s] = $true
                $decoratedFunctionStrings.Add([pscustomobject]@{
                    Dll = $dll.Name
                    Category = $category
                    Symbol = $s
                    Undecorated = (ConvertFrom-MsvcDecoratedName $s $undname)
                })
                $dllDecoratedCount++
            }
            continue
        }

        $isHint = $s -match '^N3Msg_' `
            -or $s -match 'IIR_t$' `
            -or $s -match '^Feedback_' `
            -or $s -match '^e_[A-Za-z0-9_]+$' `
            -or $s -match 'Corpse|corpse|CORPSE|Attack|Fight|Combat|Loot|DynelDead|ToClientDynelDead' `
            -or $s -match '^[a-z0-9]+[-_][a-z0-9_-]+\.ani$'

        if ($isHint -and -not $seenHints.ContainsKey($s)) {
            $seenHints[$s] = $true
            $symbolHints.Add([pscustomobject]@{
                Dll = $dll.Name
                Category = $category
                Kind = (Get-SymbolHintKind $s)
                Symbol = $s
            })
            $dllHintCount++
        }
    }

    $summary.Add([pscustomobject]@{
        Dll = $dll.Name
        Category = $category
        SizeBytes = $dll.Length
        LastWriteTime = $dll.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
        ExportCount = $dllExportCount
        DecoratedFunctionStringCount = $dllDecoratedCount
        AoSymbolHintCount = $dllHintCount
    })
}

$summaryPath = Join-Path $outRoot "ao_client_dll_summary.csv"
$exportsPath = Join-Path $outRoot "ao_client_dll_exports.csv"
$decoratedPath = Join-Path $outRoot "ao_client_dll_decorated_function_strings.csv"
$hintsPath = Join-Path $outRoot "ao_client_dll_symbol_hints.csv"
$exportsJsonlPath = Join-Path $outRoot "ao_client_dll_exports.jsonl"
$decoratedJsonlPath = Join-Path $outRoot "ao_client_dll_decorated_function_strings.jsonl"
$hintsJsonlPath = Join-Path $outRoot "ao_client_dll_symbol_hints.jsonl"

$summary | Sort-Object Category,Dll | Export-Csv -NoTypeInformation -Encoding UTF8 -Path $summaryPath
$exports | Sort-Object Category,Dll,Ordinal,Symbol | Export-Csv -NoTypeInformation -Encoding UTF8 -Path $exportsPath
$decoratedFunctionStrings | Sort-Object Category,Dll,Symbol | Export-Csv -NoTypeInformation -Encoding UTF8 -Path $decoratedPath
$symbolHints | Sort-Object Category,Dll,Kind,Symbol | Export-Csv -NoTypeInformation -Encoding UTF8 -Path $hintsPath

$exports | Sort-Object Category,Dll,Ordinal,Symbol | ForEach-Object { ConvertTo-JsonLine $_ } | Set-Content -Encoding UTF8 -Path $exportsJsonlPath
$decoratedFunctionStrings | Sort-Object Category,Dll,Symbol | ForEach-Object { ConvertTo-JsonLine $_ } | Set-Content -Encoding UTF8 -Path $decoratedJsonlPath
$symbolHints | Sort-Object Category,Dll,Kind,Symbol | ForEach-Object { ConvertTo-JsonLine $_ } | Set-Content -Encoding UTF8 -Path $hintsJsonlPath

$readmePath = Join-Path $outRoot "README.md"
$generatedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz"
$readme = @"
# AO Client DLL Function Map

Generated: $generatedAt

Source directory: $aoRoot

Tools:
- objdump: $objdump
- strings: $strings
- undname: $undname

Files:
- ao_client_dll_summary.csv - per-DLL counts and categories.
- ao_client_dll_exports.csv / .jsonl - PE export table symbols. These are the most reliable function map entries.
- ao_client_dll_decorated_function_strings.csv / .jsonl - MSVC decorated function-like strings found by strings; useful but inferred.
- ao_client_dll_symbol_hints.csv / .jsonl - AO-specific non-function hints such as N3Msg names, IIR classes, feedback ids, stat strings, corpse/combat/loot strings, and animation filenames.

Notes:
- AOClient means a DLL that appears to be part of Anarchy Online's client code.
- ThirdPartyRuntime means bundled runtime or vendor libraries, kept in the map for completeness.
- Undecorated is filled when Visual Studio undname.exe can decode the MSVC decorated symbol.
- Some strings are class names, vtables, or RTTI rather than callable functions. Prefer export rows when implementing packet paths.
"@
$readme | Set-Content -Encoding UTF8 -Path $readmePath

Write-Host "Done."
Write-Host "Summary: $summaryPath"
Write-Host "Exports: $exportsPath"
Write-Host "Decorated function strings: $decoratedPath"
Write-Host "Symbol hints: $hintsPath"
