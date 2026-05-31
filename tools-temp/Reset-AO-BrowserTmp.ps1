$prefsRoot = "C:\Users\Mike\AppData\Local\Funcom\Anarchy Online\70dad3e6\Anarchy Online\Prefs"
$tmpPath = Join-Path $prefsRoot "Browser\tmp"
$accountPrefs = Join-Path $prefsRoot "anarchyaccnt01\Prefs.xml"

if (Test-Path -LiteralPath $tmpPath) {
    Get-ChildItem -LiteralPath $tmpPath -Force -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if (Test-Path -LiteralPath $accountPrefs) {
    $content = Get-Content -LiteralPath $accountPrefs -Raw
    $updated = $content -replace '<Value name="VideoAdds" value="true" />', '<Value name="VideoAdds" value="false" />'
    if ($updated -ne $content) {
        Set-Content -LiteralPath $accountPrefs -Value $updated -Encoding UTF8
    }
}

Write-Host "AO browser tmp reset complete."
