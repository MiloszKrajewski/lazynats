<#
.SYNOPSIS
    Builds, natively AOT-publishes, and runs lazynats.AotProbe against a
    throwaway, probe-only nats-server (Docker) on port 14442, so it never
    conflicts with a "real" nats-server instance you might already have
    running on the default 4222.

.PARAMETER Rid
    Runtime identifier to publish for. Defaults to win-x64 (the only RID
    actually validated so far). Pass -Rid linux-x64 once cross-platform
    testing is available.
#>
param(
    [string]$Rid = "win-x64"
)

$ErrorActionPreference = "Stop"

$scriptRoot = $PSScriptRoot
$projectPath = Join-Path $scriptRoot "lazynats.AotProbe.csproj"
$publishDir = Join-Path $scriptRoot "bin/aot-probe-publish/$Rid"
$natsContainerName = "lazynats-aotprobe-nats"
$natsPort = 14442

Write-Host "== dotnet build (fast AOT/trim analyzer pass) ==" -ForegroundColor Cyan
dotnet build $projectPath
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "== dotnet publish -r $Rid --self-contained -p:PublishAot=true ==" -ForegroundColor Cyan
dotnet publish $projectPath -r $Rid -c Release --self-contained -p:PublishAot=true -o $publishDir
if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed." -ForegroundColor Red
    exit $LASTEXITCODE
}

$exeName = if ($Rid -like "win-*") { "lazynats.AotProbe.exe" } else { "lazynats.AotProbe" }
$exePath = Join-Path $publishDir $exeName

Write-Host ""
Write-Host "== Starting probe-only nats-server on port $natsPort (Docker) ==" -ForegroundColor Cyan
docker rm -f $natsContainerName *> $null
docker run -d --rm --name $natsContainerName -p "${natsPort}:4222" nats:latest | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to start nats-server container - is Docker running?" -ForegroundColor Red
    exit $LASTEXITCODE
}

try {
    $ready = $false
    for ($i = 0; $i -lt 20; $i++) {
        try {
            $client = New-Object System.Net.Sockets.TcpClient
            $client.Connect("localhost", $natsPort)
            $client.Close()
            $ready = $true
            break
        } catch {
            Start-Sleep -Milliseconds 250
        }
    }
    if (-not $ready) {
        throw "nats-server did not become ready on port $natsPort in time."
    }

    Write-Host ""
    Write-Host "== Running $exePath ==" -ForegroundColor Cyan
    & $exePath
}
finally {
    Write-Host ""
    Write-Host "== Stopping probe-only nats-server ==" -ForegroundColor Cyan
    docker stop $natsContainerName *> $null
}

# Probe pass/fail is informational output for a human to read right now (no
# CI integration yet), so the probe executable's exit code does not fail
# this script - only a build, publish, or nats-server startup failure does.
exit 0
