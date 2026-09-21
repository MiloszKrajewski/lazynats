<#
.SYNOPSIS
    Publishes randomized fake events on an interval against the demo nats-server, so
    lazynats' Live Feed has real-time activity to show while recording: varied subjects,
    headers, and payload types (JSON/Text/binary).

    Also occasionally nudges the `inventory` and `sessions` KV buckets seeded by
    demo/seed-data.ps1, so their revision counts / entry lists visibly change live too.

    Runs until interrupted with Ctrl+C.
#>
param(
    [string]$Server = "nats://localhost:4223",
    [int]$MinDelayMs = 800,
    [int]$MaxDelayMs = 2500
)

$ErrorActionPreference = "Stop"

$natsExe = Resolve-Path (Join-Path $PSScriptRoot "..\.bin\nats.exe")
$rng = [System.Random]::new()

function Invoke-Nats {
    param([string[]]$CliArgs)
    & $natsExe "-s" $Server @CliArgs | Out-Null
}

function Publish-RawBytes {
    # `nats pub` reads binary payloads from STDIN; piping a string to a native exe through
    # PowerShell's pipeline appends a trailing CRLF (see demo/seed-data.ps1), so this writes
    # the bytes directly to the child process's stdin stream instead.
    param([string]$Subject, [byte[]]$Bytes, [string[]]$Headers)

    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $natsExe
    $psi.ArgumentList.Add("-s")
    $psi.ArgumentList.Add($Server)
    $psi.ArgumentList.Add("pub")
    $psi.ArgumentList.Add($Subject)
    $psi.ArgumentList.Add("--force-stdin")
    foreach ($header in $Headers) {
        $psi.ArgumentList.Add("-H")
        $psi.ArgumentList.Add($header)
    }
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.UseShellExecute = $false

    $proc = [System.Diagnostics.Process]::Start($psi)
    $proc.StandardInput.BaseStream.Write($Bytes, 0, $Bytes.Length)
    $proc.StandardInput.BaseStream.Flush()
    $proc.StandardInput.Close()
    $proc.WaitForExit()
}

function New-Guid8 {
    return [Guid]::NewGuid().ToString("N").Substring(0, 8)
}

$customers = @("Ana Kowalski", "Ben Fischer", "Chen Wei", "Dana Osei", "Erik Lindqvist", "Fatima Noor")
$logSources = @("api", "worker", "scheduler")
$logLines = @{
    info  = @("request completed", "cache warmed", "healthcheck ok", "job scheduled")
    warn  = @("slow query detected", "retrying downstream call", "queue depth rising")
    error = @("downstream timeout", "unhandled exception in handler", "connection reset")
}
$chatUsers = @("alice", "bob", "carol", "dave")
$chatLines = @("anyone seen the new build?", "deploy's up", "can you check the dashboard?", "lgtm", "on it")
$skus = @("sku-1001", "sku-1002", "sku-1003", "sku-1004", "sku-1005", "sku-1006")

$eventKinds = @(
    "orders", "orders", "logs", "logs", "metrics", "metrics",
    "chat", "alerts", "alerts", "inventory", "session", "sensor"
)

Write-Host "Publishing fake events to $Server every ${MinDelayMs}-${MaxDelayMs}ms. Ctrl+C to stop." -ForegroundColor Cyan

try {
    while ($true) {
        $kind = $eventKinds[$rng.Next($eventKinds.Count)]

        switch ($kind) {
            "orders" {
                $type = @("created", "paid", "shipped", "cancelled")[$rng.Next(4)]
                $orderId = "ORD-{0:D5}" -f $rng.Next(1, 99999)
                $body = [ordered]@{
                    orderId  = $orderId
                    customer = $customers[$rng.Next($customers.Count)]
                    total    = [Math]::Round($rng.NextDouble() * 480 + 20, 2)
                    items    = $rng.Next(1, 6)
                } | ConvertTo-Json -Compress
                Invoke-Nats @("pub", "orders.$type", $body, "-H", "trace-id:$(New-Guid8)", "-H", "source:order-service")
                Write-Host "  orders.$type  $orderId" -ForegroundColor DarkGray
            }
            "logs" {
                $level = @("info", "info", "warn", "error")[$rng.Next(4)]
                $lines = $logLines[$level]
                $message = $lines[$rng.Next($lines.Count)]
                $source = $logSources[$rng.Next($logSources.Count)]
                Invoke-Nats @("pub", "logs.$level", "[$source] $message", "-H", "source:$source")
                Write-Host "  logs.$level  $message" -ForegroundColor DarkGray
            }
            "metrics" {
                $metric = @("cpu", "mem")[$rng.Next(2)]
                $value = [Math]::Round($rng.NextDouble() * 90 + 5, 1)
                Invoke-Nats @("pub", "metrics.$metric", "$value")
                Write-Host "  metrics.$metric  $value" -ForegroundColor DarkGray
            }
            "chat" {
                $user = $chatUsers[$rng.Next($chatUsers.Count)]
                $line = $chatLines[$rng.Next($chatLines.Count)]
                Invoke-Nats @("pub", "chat.general", $line, "-H", "user:$user")
                Write-Host "  chat.general  <$user> $line" -ForegroundColor DarkGray
            }
            "alerts" {
                $alert = @("disk", "latency")[$rng.Next(2)]
                $value = if ($alert -eq "disk") { $rng.Next(60, 100) } else { $rng.Next(50, 900) }
                $severity = if ($value -gt 85) { "critical" } else { "warning" }
                $body = [ordered]@{ alert = $alert; value = $value } | ConvertTo-Json -Compress
                Invoke-Nats @("pub", "alerts.$alert", $body, "-H", "severity:$severity")
                Write-Host "  alerts.$alert  $value ($severity)" -ForegroundColor DarkGray
            }
            "inventory" {
                $sku = $skus[$rng.Next($skus.Count)]
                $current = & $natsExe -s $Server kv get inventory $sku --raw | ConvertFrom-Json
                $current.quantity = [Math]::Max(0, $current.quantity - $rng.Next(1, 4))
                $body = $current | ConvertTo-Json -Compress
                Invoke-Nats @("kv", "put", "inventory", $sku, $body)
                Write-Host "  kv inventory/$sku  quantity -> $($current.quantity)" -ForegroundColor DarkGray
            }
            "session" {
                $key = "sess-$(New-Guid8)"
                $body = [ordered]@{
                    user      = $chatUsers[$rng.Next($chatUsers.Count)]
                    ip        = "10.0.{0}.{1}" -f $rng.Next(1, 255), $rng.Next(1, 255)
                    startedAt = (Get-Date).ToString("o")
                } | ConvertTo-Json -Compress
                Invoke-Nats @("kv", "put", "sessions", $key, $body)
                Write-Host "  kv sessions/$key" -ForegroundColor DarkGray
            }
            "sensor" {
                $bytes = New-Object byte[] (64 + $rng.Next(192))
                $rng.NextBytes($bytes)
                Publish-RawBytes -Subject "sensor.raw" -Bytes $bytes -Headers @("source:edge-node-1")
                Write-Host "  sensor.raw  $($bytes.Length) bytes" -ForegroundColor DarkGray
            }
        }

        Start-Sleep -Milliseconds $rng.Next($MinDelayMs, $MaxDelayMs)
    }
}
finally {
    Write-Host ""
    Write-Host "Stopped." -ForegroundColor Cyan
}
