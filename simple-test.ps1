# Simple Test Script for Ticketing System
Write-Host "=== Ticketing System Manual Tests ===" -ForegroundColor Cyan

# Test 1: Infrastructure Health Check
Write-Host "`n🔧 Testing Infrastructure..." -ForegroundColor Yellow

$services = @("http://localhost:5001", "http://localhost:5002")
foreach ($service in $services) {
    try {
        $response = Invoke-WebRequest -Uri "$service/health" -Method GET -TimeoutSec 5 -UseBasicParsing
        Write-Host "✅ Service $service : OK" -ForegroundColor Green
    }
    catch {
        Write-Host "❌ Service $service : Failed" -ForegroundColor Red
    }
}

# Test 2: Register and Login
Write-Host "`n🔐 Testing Authentication..." -ForegroundColor Yellow

$email = "testuser_$(Get-Date -Format 'yyyyMMddHHmmss')@test.com"
$registerData = @{
    email = $email
    password = "TestPassword123!"
    confirmPassword = "TestPassword123!"
} | ConvertTo-Json

try {
    # Register
    Invoke-RestMethod -Uri "http://localhost:5001/api/auth/register" -Method POST -Body $registerData -ContentType "application/json" | Out-Null
    Write-Host "✅ Registration: Success" -ForegroundColor Green
    
    # Login
    $loginData = @{
        email = $email
        password = "TestPassword123!"
    } | ConvertTo-Json
    
    $loginResponse = Invoke-RestMethod -Uri "http://localhost:5001/api/auth/login" -Method POST -Body $loginData -ContentType "application/json"
    $token = $loginResponse.token
    Write-Host "✅ Login: Success" -ForegroundColor Green
    Write-Host "🔑 Token: $($token.Substring(0, 30))..." -ForegroundColor Cyan
}
catch {
    Write-Host "❌ Authentication failed: $($_.Exception.Message)" -ForegroundColor Red
    exit
}

# Test 3: Get Events
Write-Host "`n🎫 Testing Events..." -ForegroundColor Yellow
try {
    $headers = @{ Authorization = "Bearer $token" }
    $events = Invoke-RestMethod -Uri "http://localhost:5002/api/events" -Method GET -Headers $headers
    Write-Host "✅ Events: Retrieved $($events.Count) events" -ForegroundColor Green
}
catch {
    Write-Host "❌ Events failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 4: Purchase Tickets
Write-Host "`n🎟️ Testing Ticket Purchase..." -ForegroundColor Yellow

$purchaseData = @{
    eventId = 1
    quantity = 2
    paymentRequest = @{
        cardNumber = "4111111111111111"
        expiryMonth = 12
        expiryYear = 2025
        cvv = "123"
        cardHolderName = "Test User"
        amount = 100.00
    }
} | ConvertTo-Json -Depth 3

try {
    $headers = @{ Authorization = "Bearer $token" }
    $purchase = Invoke-RestMethod -Uri "http://localhost:5002/api/tickets/purchase" -Method POST -Body $purchaseData -ContentType "application/json" -Headers $headers
    Write-Host "✅ Ticket Purchase: Success" -ForegroundColor Green
    Write-Host "🎫 Purchase ID: $($purchase.transactionId)" -ForegroundColor Cyan
}
catch {
    Write-Host "❌ Ticket purchase failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $errorStream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($errorStream)
        $errorDetails = $reader.ReadToEnd()
        Write-Host "Error details: $errorDetails" -ForegroundColor Red
    }
}

# Test 5: RabbitMQ Queue Stats
Write-Host "`n🐰 Testing RabbitMQ..." -ForegroundColor Yellow
try {
    $headers = @{ Authorization = "Bearer $token" }
    $stats = Invoke-RestMethod -Uri "http://localhost:5002/api/tickets/queue-stats" -Method GET -Headers $headers
    Write-Host "✅ RabbitMQ Stats: Retrieved" -ForegroundColor Green
    Write-Host "📊 Capacity Queue Messages: $($stats.capacity_queue_messages)" -ForegroundColor Cyan
    Write-Host "📊 Transaction Queue Messages: $($stats.transaction_queue_messages)" -ForegroundColor Cyan
    Write-Host "📊 Dead Letter Queue Messages: $($stats.dead_letter_queue_messages)" -ForegroundColor Cyan
}
catch {
    Write-Host "❌ RabbitMQ stats failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n✅ All tests completed!" -ForegroundColor Green
Write-Host "🌐 Open RabbitMQ Management: http://localhost:15672 (user: ticketinguser, pass: ticketingpass123)" -ForegroundColor Cyan

Read-Host "`nPress Enter to exit"
