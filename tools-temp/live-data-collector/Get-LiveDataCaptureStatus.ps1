param(
    [string]$RepoRoot = "",
    [string]$OutputRoot = "",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Resolve-RepoRoot {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

function Get-NormalizedPath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ""
    }

    try {
        return [System.IO.Path]::GetFullPath($Path).TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar
        )
    } catch {
        return ""
    }
}

function Test-PathInsideRoot {
    param(
        [string]$Path,
        [string]$Root
    )

    $normalizedPath = Get-NormalizedPath $Path
    $normalizedRoot = Get-NormalizedPath $Root
    if ([string]::IsNullOrWhiteSpace($normalizedPath) -or [string]::IsNullOrWhiteSpace($normalizedRoot)) {
        return $false
    }

    $rootPrefix = $normalizedRoot + [System.IO.Path]::DirectorySeparatorChar
    return $normalizedPath.Equals($normalizedRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
        $normalizedPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-EffectiveCapturePath {
    param(
        [string]$MetaPath,
        [string]$EmbeddedCaptureDir,
        [string]$OutputRoot
    )

    $metaDirectory = Split-Path -Parent $MetaPath
    if ([string]::IsNullOrWhiteSpace($EmbeddedCaptureDir)) {
        return [pscustomobject]@{
            CaptureDir = $metaDirectory
            Warning = "embedded capture_dir missing; using capture_meta parent"
            EmbeddedCaptureDir = ""
        }
    }

    $normalizedEmbedded = Get-NormalizedPath $EmbeddedCaptureDir
    if ([string]::IsNullOrWhiteSpace($normalizedEmbedded)) {
        return [pscustomobject]@{
            CaptureDir = $metaDirectory
            Warning = "embedded capture_dir invalid; using capture_meta parent"
            EmbeddedCaptureDir = $EmbeddedCaptureDir
        }
    }

    if (Test-PathInsideRoot $normalizedEmbedded $OutputRoot) {
        return [pscustomobject]@{
            CaptureDir = $EmbeddedCaptureDir
            Warning = ""
            EmbeddedCaptureDir = $EmbeddedCaptureDir
        }
    }

    return [pscustomobject]@{
        CaptureDir = $metaDirectory
        Warning = "embedded capture_dir outside OutputRoot; using capture_meta parent"
        EmbeddedCaptureDir = $EmbeddedCaptureDir
    }
}

$RepoRoot = Resolve-RepoRoot $RepoRoot
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $RepoRoot "tools-temp\live-pcaps"
}
$OutputRoot = Get-NormalizedPath $OutputRoot

$collectorRoot = Join-Path $RepoRoot "tools-temp\live-data-collector"
$activePath = Join-Path $collectorRoot "active_capture.json"

if ($DryRun) {
    "RepoRoot=$RepoRoot"
    "ActivePath=$activePath"
    "OutputRoot=$OutputRoot"
}

if (Test-Path -LiteralPath $activePath) {
    $active = Get-Content -Raw -LiteralPath $activePath | ConvertFrom-Json
    $proc = Get-Process -Id $active.pid -ErrorAction SilentlyContinue
    if ($proc) {
        "ACTIVE pid=$($proc.Id) capture_dir=$($active.capture_dir)"
        return
    }

    "STALE active_capture.json points at exited pid=$($active.pid) capture_dir=$($active.capture_dir)"
    return
}

$latest = Get-ChildItem -LiteralPath $OutputRoot -Recurse -Filter capture_meta.json -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($latest) {
    $meta = Get-Content -Raw -LiteralPath $latest.FullName | ConvertFrom-Json
    $effectiveCapture = Get-EffectiveCapturePath $latest.FullName ([string]$meta.capture_dir) $OutputRoot
    $captureDir = $effectiveCapture.CaptureDir
    $status = if ($meta.status) { [string]$meta.status } else { "unknown" }
    $mode = if ($meta.mode) { [string]$meta.mode } else { "unknown" }
    if ([string]::IsNullOrWhiteSpace($effectiveCapture.Warning)) {
        "INACTIVE latest_capture=$captureDir status=$status mode=$mode"
    } else {
        "INACTIVE latest_capture=$captureDir status=$status mode=$mode warning=`"$($effectiveCapture.Warning)`" embedded_capture_dir=`"$($effectiveCapture.EmbeddedCaptureDir)`""
    }
}
else {
    "INACTIVE no capture history found"
}
