$ErrorActionPreference = "Stop"
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))

& (Join-Path $PSScriptRoot "stop-local-backend.ps1")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Get-ChildItem -LiteralPath $repoRoot -Directory -Recurse -Force |
    Where-Object { $_.Name -in @("bin", "obj") } |
    Sort-Object FullName -Descending |
    Remove-Item -Recurse -Force

dotnet restore (Join-Path $repoRoot "KLTN_SU26_SNM_BE.sln")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet clean (Join-Path $repoRoot "KLTN_SU26_SNM_BE.sln")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build (Join-Path $repoRoot "KLTN_SU26_SNM_BE.sln") --no-restore
exit $LASTEXITCODE
