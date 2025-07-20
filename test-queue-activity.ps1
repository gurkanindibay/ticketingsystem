#!/usr/bin/env pwsh
# Test to observe queue activity during ticket purchases

Write-Host "=== Queue Activity Monitor ===" -ForegroundColor Green

# Get auth token first
Write-Host "`nGetting authentication token..." -ForegroundColor Yellow
$authResponse = curl.exe -s -X POST http://localhost:5001/api/auth/login -H "Content-Type: application/json" -d '{\"email\": \"john.doe@example.com\", \"password\": \"SecurePass123!\"}' | ConvertFrom-Json

if (-not $authResponse.success) {
    Write-Host "Authentication failed: $($authResponse.message)" -ForegroundColor Red
    exit 1
}

$token = $authResponse.data.token
Write-Host "✅ Authenticated successfully" -ForegroundColor Green

Write-Host "`n🔍 Starting queue monitoring with rapid purchases..." -ForegroundColor Cyan
Write-Host "This will make multiple purchases quickly to potentially catch messages in-flight`n" -ForegroundColor Gray

# Function to get queue stats
function Get-QueueStats {
    try {
        $response = curl.exe -s http://localhost:5002/api/tickets/admin/queue-status | ConvertFrom-Json
        if ($response.success) {
            return $response.data
        }
    } catch {
        return $null
    }
}

# Function to make a purchase
function Make-Purchase($index) {
    $purchaseData = @{
        eventId = 1
        quantity = 1
        paymentMethod = "credit_card"
        paymentDetails = @{
            amount = 25.00
            paymentMethod = "credit_card"
            cardNumber = "4111111111111111"
            expiryMonth = "12"
            expiryYear = "2025"
            cvv = "123"
            cardholderName = "Test User $index"
        }
    } | ConvertTo-Json -Depth 3

    try {
        $response = curl.exe -s -X POST http://localhost:5002/api/tickets/purchase -H "Content-Type: application/json" -H "Authorization: Bearer $token" -d $purchaseData | ConvertFrom-Json
        return $response.success
    } catch {
        return $false
    }
}

# Monitor before purchases
Write-Host "📊 Queue stats BEFORE purchases:" -ForegroundColor Yellow
$beforeStats = Get-QueueStats
if ($beforeStats) {
    Write-Host "  Capacity Queue: $($beforeStats.capacity_queue_messages) messages, $($beforeStats.capacity_queue_consumers) consumers" -ForegroundColor Gray
    Write-Host "  Transaction Queue: $($beforeStats.transaction_queue_messages) messages, $($beforeStats.transaction_queue_consumers) consumers" -ForegroundColor Gray
}

# Make rapid purchases and check queue stats
Write-Host "`n🚀 Making 3 rapid purchases..." -ForegroundColor Yellow

for ($i = 1; $i -le 3; $i++) {
    Write-Host "  Purchase $i..." -NoNewline -ForegroundColor White
    
    # Get stats immediately before purchase
    $statsBefore = Get-QueueStats
    
    # Make purchase
    $success = Make-Purchase $i
    
    # Get stats immediately after purchase (might catch messages in-flight)
    $statsAfter = Get-QueueStats
    
    if ($success) {
        Write-Host " ✅" -ForegroundColor Green
        if ($statsAfter) {
            Write-Host "    Capacity: $($statsAfter.capacity_queue_messages) | Transaction: $($statsAfter.transaction_queue_messages)" -ForegroundColor Cyan
        }
    } else {
        Write-Host " ❌" -ForegroundColor Red
    }
    
    # Small delay between purchases
    Start-Sleep -Milliseconds 100
}

# Final stats
Write-Host "`n📊 Queue stats AFTER purchases:" -ForegroundColor Yellow
$afterStats = Get-QueueStats
if ($afterStats) {
    Write-Host "  Capacity Queue: $($afterStats.capacity_queue_messages) messages, $($afterStats.capacity_queue_consumers) consumers" -ForegroundColor Gray
    Write-Host "  Transaction Queue: $($afterStats.transaction_queue_messages) messages, $($afterStats.transaction_queue_consumers) consumers" -ForegroundColor Gray
    Write-Host "  Dead Letter Queue: $($afterStats.dead_letter_queue_messages) messages" -ForegroundColor Gray
}

Write-Host "`n💡 Analysis:" -ForegroundColor Cyan
Write-Host "• If you see 0 messages throughout = Perfect! Processing is faster than publishing" -ForegroundColor Green
Write-Host "• If you briefly see >0 messages = Normal! Shows messages in-flight during processing" -ForegroundColor Blue
Write-Host "• Consistent >0 messages = Would indicate a processing bottleneck" -ForegroundColor Yellow

Write-Host "`n=== Queue Activity Monitor Complete ===" -ForegroundColor Green
