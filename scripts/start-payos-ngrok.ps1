param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^https?://')]
    [string]$BackendUrl,
    [int]$TimeoutSeconds = 20
)

$ErrorActionPreference = 'Stop'
$backend = $BackendUrl.TrimEnd('/')

try {
    $health = Invoke-WebRequest -Uri "$backend/health" -UseBasicParsing -TimeoutSec 5
    if ($health.StatusCode -lt 200 -or $health.StatusCode -ge 300) {
        throw "Backend health returned HTTP $($health.StatusCode)."
    }
}
catch {
    throw "Backend is not healthy at $backend/health. Start it first (use HTTP if the local HTTPS certificate is not trusted). $($_.Exception.Message)"
}

if (-not (Get-Command ngrok -ErrorAction SilentlyContinue)) {
    throw 'ngrok was not found on PATH. Install ngrok and configure the rotated authtoken outside this repository.'
}

$process = Start-Process -FilePath 'ngrok' -ArgumentList @('http', $backend) -PassThru -WindowStyle Hidden
$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
$publicUrl = $null
do {
    if ($process.HasExited) { throw "ngrok exited with code $($process.ExitCode)." }
    try {
        $tunnels = Invoke-RestMethod -Uri 'http://127.0.0.1:4040/api/tunnels' -TimeoutSec 2
        $publicUrl = $tunnels.tunnels | Where-Object { $_.public_url -like 'https://*' } | Select-Object -First 1 -ExpandProperty public_url
    }
    catch { Start-Sleep -Milliseconds 300 }
} while (-not $publicUrl -and [DateTime]::UtcNow -lt $deadline)

if (-not $publicUrl) {
    Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
    throw 'ngrok started but no HTTPS tunnel became available before the timeout.'
}

Write-Host "Public URL: $publicUrl"
Write-Host "PayOS webhook: $publicUrl/api/webhooks/payos"
Write-Host "Return URL: $publicUrl/api/payment-callback/payos/return"
Write-Host "Cancel URL: $publicUrl/api/payment-callback/payos/cancel"
Write-Host "ngrok PID: $($process.Id)"
