<#
.SYNOPSIS
    Starts a throwaway, JetStream-enabled nats-server (Docker) on port 4223, for populating and
    recording a lazynats demo, without disturbing whatever nats-server you already run on the
    default port 4222 for regular dev work.

    Since this isn't lazynats' zero-config default port, point lazynats and the other demo
    scripts at it explicitly: `dotnet run --project src/lazynats -- -s nats://localhost:4223`.

    Safe to re-run: removes any existing container of the same name first, so every run starts
    from a clean slate. Run demo/seed-data.ps1 next to provision the demo streams/buckets.
#>
param()

$ErrorActionPreference = "Stop"

$containerName = "lazynats-demo-nats"
$natsPort = 4223
$monitorPort = 8223

Write-Host "== Starting demo nats-server (Docker, JetStream enabled) on port $natsPort ==" -ForegroundColor Cyan
docker rm -f $containerName *> $null
docker run -d --rm --name $containerName -p "${natsPort}:4222" -p "${monitorPort}:8222" nats:latest -js -m 8222 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to start nats-server container - is Docker running?" -ForegroundColor Red
    exit $LASTEXITCODE
}

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
    Write-Host "nats-server did not become ready on port $natsPort in time." -ForegroundColor Red
    exit 1
}

Write-Host "nats-server ready at nats://localhost:$natsPort (monitoring on :$monitorPort)." -ForegroundColor Green
Write-Host "Next: run demo/seed-data.ps1 to provision the demo streams/buckets." -ForegroundColor Cyan
