<#
.SYNOPSIS
    Provisions the "Acme order platform" demo scenario (streams, consumers, KV buckets, Object
    stores) on the demo nats-server started by demo/start-nats.ps1, so every lazynats tab has
    realistic, varied content the moment the app launches:

      - ORDERS stream (orders.>, unlimited limits, 2 consumers) contrasted with SYSTEM_LOGS
        (logs.>, bounded limits, no consumers - exercises the empty-hint drill-down state).
      - inventory KV bucket (6 seeded SKUs, no TTL) contrasted with sessions (starts empty,
        2-minute TTL - fed live by demo/generate-events.ps1).
      - product-images and reports Object stores, each seeded with a few small placeholder
        files.

    Safe to re-run against a freshly started (clean) container; `nats stream/kv/object add`
    fails loudly if the target already exists rather than silently reprovisioning, so re-run
    this only after demo/start-nats.ps1 has given you a clean slate.
#>
param(
    [string]$Server = "nats://localhost:4223"
)

$ErrorActionPreference = "Stop"

$natsExe = Join-Path $PSScriptRoot "..\.bin\nats.exe"
if (-not (Test-Path $natsExe)) {
    throw "nats CLI not found at $natsExe"
}

function Invoke-Nats {
    param([string[]]$CliArgs)
    & $natsExe "-s" $Server @CliArgs | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "nats $($CliArgs -join ' ') failed with exit code $LASTEXITCODE"
    }
}

# `nats stream add`/`consumer add` fall back to interactive prompts for any option not covered
# by a CLI flag, which fails outright under a non-interactive PowerShell invocation ("cannot
# prompt for user input without a terminal"). Driving them from a full --config JSON file (the
# raw JetStream API config) avoids that entirely - kv/object add don't have this problem.
function Write-JsonConfig {
    param([hashtable]$Config)
    $path = [System.IO.Path]::GetTempFileName()
    ($Config | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $path -NoNewline
    return $path
}

$tempFiles = @()
$assetsDir = $null

try {
    Write-Host "== Streams ==" -ForegroundColor Cyan

    $ordersConfig = Write-JsonConfig @{
        name                 = "ORDERS"
        subjects             = @("orders.>")
        retention            = "limits"
        max_consumers        = -1
        max_msgs_per_subject = -1
        max_msgs             = -1
        max_bytes            = -1
        max_age              = 86400000000000    # 24h, in ns - unlimited otherwise
        max_msg_size         = -1
        storage              = "file"
        discard              = "old"
        num_replicas         = 1
        duplicate_window     = 120000000000      # 2m, in ns
    }
    $tempFiles += $ordersConfig
    Invoke-Nats @("stream", "add", "--config", $ordersConfig)
    Write-Host "  ORDERS (orders.>, unlimited limits)" -ForegroundColor DarkGray

    $logsConfig = Write-JsonConfig @{
        name                 = "SYSTEM_LOGS"
        subjects             = @("logs.>")
        retention            = "limits"
        max_consumers        = -1
        max_msgs_per_subject = -1
        max_msgs             = -1
        max_bytes            = 5242880            # 5MB - bounded, contrasts with ORDERS
        max_age              = 3600000000000      # 1h, in ns
        max_msg_size         = -1
        storage              = "file"
        discard              = "old"
        num_replicas         = 1
        duplicate_window     = 120000000000
    }
    $tempFiles += $logsConfig
    Invoke-Nats @("stream", "add", "--config", $logsConfig)
    Write-Host "  SYSTEM_LOGS (logs.>, bounded limits)" -ForegroundColor DarkGray

    Write-Host "== Consumers (ORDERS only - SYSTEM_LOGS is left with none) ==" -ForegroundColor Cyan

    $fulfillmentConfig = Write-JsonConfig @{
        durable_name    = "fulfillment"
        description     = "Order fulfillment worker"
        filter_subject  = "orders.created"
        ack_policy      = "explicit"
        deliver_policy  = "all"
        replay_policy   = "instant"
        max_deliver     = -1
        ack_wait        = 30000000000
        max_ack_pending = 1000
        headers_only    = $false
    }
    $tempFiles += $fulfillmentConfig
    Invoke-Nats @("consumer", "add", "ORDERS", "--config", $fulfillmentConfig)

    $notificationsConfig = Write-JsonConfig @{
        durable_name    = "notifications"
        description     = "Order lifecycle notification dispatcher"
        filter_subject  = "orders.>"
        ack_policy      = "explicit"
        deliver_policy  = "all"
        replay_policy   = "instant"
        max_deliver     = -1
        ack_wait        = 30000000000
        max_ack_pending = 1000
        headers_only    = $false
    }
    $tempFiles += $notificationsConfig
    Invoke-Nats @("consumer", "add", "ORDERS", "--config", $notificationsConfig)
    Write-Host "  fulfillment (orders.created), notifications (orders.>)" -ForegroundColor DarkGray

    Write-Host "== KV buckets ==" -ForegroundColor Cyan

    Invoke-Nats @("kv", "add", "inventory", "--history", "5", "--storage", "file", "--replicas", "1")
    Invoke-Nats @("kv", "add", "sessions", "--history", "1", "--ttl", "2m", "--storage", "file", "--replicas", "1")
    Write-Host "  inventory (no TTL), sessions (2m TTL, starts empty)" -ForegroundColor DarkGray

    $skus = @(
        @{ Key = "sku-1001"; Name = "Wireless Mouse"; Quantity = 42; Warehouse = "EU-WEST" }
        @{ Key = "sku-1002"; Name = "Mechanical Keyboard"; Quantity = 17; Warehouse = "EU-WEST" }
        @{ Key = "sku-1003"; Name = "USB-C Hub"; Quantity = 63; Warehouse = "US-EAST" }
        @{ Key = "sku-1004"; Name = "27in Monitor"; Quantity = 9; Warehouse = "US-EAST" }
        @{ Key = "sku-1005"; Name = "Laptop Stand"; Quantity = 31; Warehouse = "EU-WEST" }
        @{ Key = "sku-1006"; Name = "Webcam 1080p"; Quantity = 24; Warehouse = "APAC" }
    )
    foreach ($sku in $skus) {
        $value = [ordered]@{
            sku       = $sku.Key.ToUpper()
            name      = $sku.Name
            quantity  = $sku.Quantity
            warehouse = $sku.Warehouse
        } | ConvertTo-Json -Compress
        # Pass the value as a positional arg, not via stdin - piping a string to a native exe
        # through PowerShell appends a trailing CRLF, corrupting the stored JSON's byte length.
        Invoke-Nats @("kv", "put", "inventory", $sku.Key, $value)
    }
    Write-Host "  seeded $($skus.Count) SKUs into inventory" -ForegroundColor DarkGray

    Write-Host "== Object stores ==" -ForegroundColor Cyan

    Invoke-Nats @("object", "add", "product-images", "--storage", "file", "--replicas", "1")
    Invoke-Nats @("object", "add", "reports", "--storage", "file", "--replicas", "1")

    $assetsDir = Join-Path ([System.IO.Path]::GetTempPath()) "lazynats-demo-assets"
    New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null

    $images = @(
        @{ File = "sku-1001.png"; Description = "Product photo - SKU-1001 Wireless Mouse" }
        @{ File = "sku-1002.png"; Description = "Product photo - SKU-1002 Mechanical Keyboard" }
        @{ File = "sku-1003.png"; Description = "Product photo - SKU-1003 USB-C Hub" }
        @{ File = "sku-1004.png"; Description = "Product photo - SKU-1004 27in Monitor" }
    )
    $rng = [System.Random]::new()
    foreach ($image in $images) {
        $path = Join-Path $assetsDir $image.File
        $bytes = New-Object byte[] (2048 + $rng.Next(4096))
        $rng.NextBytes($bytes)
        [System.IO.File]::WriteAllBytes($path, $bytes)
        Invoke-Nats @("object", "put", "product-images", $path, "--name", $image.File, "--description", $image.Description)
    }

    $today = Get-Date -Format "yyyy-MM-dd"
    $reports = @(
        @{
            File        = "daily-summary-$today.csv"
            Description = "Daily order summary"
            Content     = "date,orders,revenue`n$today,128,15420.50`n"
        }
        @{
            File        = "inventory-audit-$today.txt"
            Description = "Inventory audit notes"
            Content     = "Inventory audit for $today`nAll SKUs within expected variance.`n"
        }
    )
    foreach ($report in $reports) {
        $path = Join-Path $assetsDir $report.File
        Set-Content -LiteralPath $path -Value $report.Content -NoNewline
        Invoke-Nats @("object", "put", "reports", $path, "--name", $report.File, "--description", $report.Description)
    }
    Write-Host "  product-images (4 objects), reports (2 objects)" -ForegroundColor DarkGray

    Write-Host ""
    Write-Host "Demo data seeded on $Server." -ForegroundColor Green
    Write-Host "Next: dotnet run --project src/lazynats -- -s $Server" -ForegroundColor Cyan
}
finally {
    foreach ($file in $tempFiles) {
        Remove-Item -LiteralPath $file -Force -ErrorAction SilentlyContinue
    }
    if ($assetsDir -and (Test-Path $assetsDir)) {
        Remove-Item -LiteralPath $assetsDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
