<#
.SYNOPSIS
    Stops the throwaway demo nats-server started by demo/start-nats.ps1.
#>
param()

$ErrorActionPreference = "Stop"

$containerName = "lazynats-demo-nats"

Write-Host "== Stopping demo nats-server ==" -ForegroundColor Cyan
docker stop $containerName *> $null
Write-Host "Stopped (container was --rm, so it has also been removed)." -ForegroundColor Green
