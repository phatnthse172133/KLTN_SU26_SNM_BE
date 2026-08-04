param(
    [int]$Port = 5282,
    [int]$TimeoutSeconds = 15
)

$ErrorActionPreference = "Stop"
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))

function Get-CommandLine([int]$ProcessId) {
    try {
        return (Get-CimInstance Win32_Process -Filter "ProcessId = $ProcessId" -ErrorAction Stop).CommandLine
    }
    catch {
        return $null
    }
}

function Test-IsThisBackend([Diagnostics.Process]$Process) {
    if ($Process.ProcessName -eq "PresentationLayer") {
        try {
            return [IO.Path]::GetFullPath($Process.MainModule.FileName).StartsWith($repoRoot, [StringComparison]::OrdinalIgnoreCase)
        }
        catch {
            return $false
        }
    }

    if ($Process.ProcessName -notin @("dotnet", "iisexpress", "testhost", "vstest.console")) {
        return $false
    }

    $commandLine = Get-CommandLine $Process.Id
    return -not [string]::IsNullOrWhiteSpace($commandLine) -and (
        $commandLine.IndexOf($repoRoot, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        $commandLine -match "PresentationLayer(?:\\|/|\.dll|\.csproj)"
    )
}

$candidateIds = @(
    Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess
    Get-Process -Name PresentationLayer -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty Id
) | Sort-Object -Unique

$stoppedIds = @()
foreach ($processId in $candidateIds) {
    $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
    if ($null -eq $process) { continue }
    if (-not (Test-IsThisBackend $process)) {
        Write-Error "Port $Port is owned by process $processId ($($process.ProcessName)), but it cannot be verified as this repository. Nothing was stopped."
        exit 2
    }

    Write-Host "Stopping $($process.ProcessName) (PID $processId)..."
    Stop-Process -Id $processId
    $stoppedIds += $processId
}

$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
do {
    $listener = Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue
    $remaining = $stoppedIds | Where-Object { Get-Process -Id $_ -ErrorAction SilentlyContinue }
    if ($null -eq $listener -and $remaining.Count -eq 0) {
        Write-Host "Backend stopped; port $Port is free."
        exit 0
    }
    Start-Sleep -Milliseconds 250
} while ([DateTime]::UtcNow -lt $deadline)

Write-Error "Timed out waiting for Backend processes or port $Port to be released."
exit 3
