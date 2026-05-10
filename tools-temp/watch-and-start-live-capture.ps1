$ErrorActionPreference = "Continue"

$repo = "C:\Users\Mike\Documents\Cellao-Clean"
$starter = Join-Path $repo "tools-temp\start-local-ao-capture.ps1"
$log = Join-Path $repo "tools-temp\live-capture-watch.log"
$startedAt = Get-Date

Remove-Item -LiteralPath $log -ErrorAction SilentlyContinue
"$(Get-Date -Format o) waiting for a fresh AnarchyOnline process started after $($startedAt.ToString('o'))" | Out-File -FilePath $log -Encoding utf8

$deadline = (Get-Date).AddMinutes(30)
while ((Get-Date) -lt $deadline) {
    $clients = @(Get-Process AnarchyOnline -ErrorAction SilentlyContinue |
        Where-Object {
            -not [string]::IsNullOrWhiteSpace($_.MainWindowTitle) -and
            $_.StartTime -ge $startedAt.AddSeconds(-2)
        })

    if ($clients.Count -eq 1) {
        "$(Get-Date -Format o) found pid=$($clients[0].Id) title=$($clients[0].MainWindowTitle)" | Add-Content -Path $log
        & powershell -NoProfile -ExecutionPolicy Bypass -File $starter -TargetPid $clients[0].Id | Add-Content -Path $log
        exit 0
    }

    if ($clients.Count -gt 1) {
        "$(Get-Date -Format o) multiple AO clients found" | Add-Content -Path $log
        foreach ($client in $clients) {
            "pid=$($client.Id) title=$($client.MainWindowTitle)" | Add-Content -Path $log
        }
        exit 2
    }

    Start-Sleep -Seconds 5
}

"$(Get-Date -Format o) timed out waiting for AO client" | Add-Content -Path $log
exit 1
