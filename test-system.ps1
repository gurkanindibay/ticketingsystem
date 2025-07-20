# PowerShell Test Scripts for Ticketing System

# Test Authentication Flow
function Test-Authentication {
    Write-Host "🔐 Testing Authentication..." -ForegroundColor Yellow
    
    # Register user
    $registerBody = @{
        email = "testuser_$(Get-Date -Format 'yyyyMMddHHmmss')@test.com"
        password = "TestPassword123!"
        confirmPassword = "TestPassword123!"
    } | ConvertTo-Json
    
    try {
        $registerResponse = Invoke-RestMethod -Uri "http://localhost:5001/api/auth/register" -Method POST -Body $registerBody -ContentType "application/json"
        Write-Host "✅ Registration successful" -ForegroundColor Green
        
        # Login user  
        $loginBody = @{
            email = ($registerBody | ConvertFrom-Json).email
            password = "TestPassword123!"
        } | ConvertTo-Json
        
        $loginResponse = Invoke-RestMethod -Uri "http://localhost:5001/api/auth/login" -Method POST -Body $loginBody -ContentType "application/json"
        Write-Host "✅ Login successful" -ForegroundColor Green
        Write-Host "🔑 Token: $($loginResponse.token.Substring(0, 50))..." -ForegroundColor Cyan
        
        return $loginResponse.token
    }
    catch {
        Write-Host "❌ Authentication failed: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

# Test Events
function Test-Events($token) {
    Write-Host "🎫 Testing Events..." -ForegroundColor Yellow
    
    try {
        $headers = @{ Authorization = "Bearer $token" }
        $eventsResponse = Invoke-RestMethod -Uri "http://localhost:5002/api/events" -Method GET -Headers $headers
        Write-Host "✅ Events retrieved: $($eventsResponse.Count) events" -ForegroundColor Green
        return $eventsResponse
    }
    catch {
        Write-Host "❌ Events test failed: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

# Test Ticket Purchase
function Test-TicketPurchase($token) {
    Write-Host "🎟️ Testing Ticket Purchase..." -ForegroundColor Yellow
    
    $purchaseBody = @{
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
        $purchaseResponse = Invoke-RestMethod -Uri "http://localhost:5003/api/tickets/purchase" -Method POST -Body $purchaseBody -ContentType "application/json" -Headers $headers
        Write-Host "✅ Ticket purchase successful" -ForegroundColor Green
        Write-Host "🎫 Purchase details: $($purchaseResponse | ConvertTo-Json -Depth 2)" -ForegroundColor Cyan
        return $purchaseResponse
    }
    catch {
        Write-Host "❌ Ticket purchase failed: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.Exception.Response) {
            $errorDetails = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($errorDetails)
            $errorBody = $reader.ReadToEnd()
            Write-Host "Error details: $errorBody" -ForegroundColor Red
        }
        return $null
    }
}

# Test RabbitMQ Queue Stats
function Test-RabbitMQStats($token) {
    Write-Host "🐰 Testing RabbitMQ Queue Stats..." -ForegroundColor Yellow
    
    try {
        $headers = @{ Authorization = "Bearer $token" }
        $statsResponse = Invoke-RestMethod -Uri "http://localhost:5003/api/tickets/queue-stats" -Method GET -Headers $headers
        Write-Host "✅ Queue stats retrieved" -ForegroundColor Green
        Write-Host "📊 Stats: $($statsResponse | ConvertTo-Json -Depth 2)" -ForegroundColor Cyan
        return $statsResponse
    }
    catch {
        Write-Host "❌ Queue stats test failed: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

# Test Infrastructure
function Test-Infrastructure {
    Write-Host "🔧 Testing Infrastructure..." -ForegroundColor Yellow
    
    # Test service health endpoints
    $services = @(
        @{ Name = "Authentication"; Url = "http://localhost:5001/health" }
        @{ Name = "Ticketing & Events"; Url = "http://localhost:5002/health" }
        @{ Name = "Ticketing"; Url = "http://localhost:5003/health" }
    )
    
    foreach ($service in $services) {
        try {
            $response = Invoke-RestMethod -Uri $service.Url -Method GET -TimeoutSec 5
            Write-Host "✅ $($service.Name) service: Healthy" -ForegroundColor Green
        }
        catch {
            Write-Host "❌ $($service.Name) service: Not responding" -ForegroundColor Red
        }
    }
    
    # Test RabbitMQ Management API
    try {
        $rabbitCredentials = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("ticketinguser:ticketingpass123"))
        $rabbitHeaders = @{ Authorization = "Basic $rabbitCredentials" }
        $rabbitResponse = Invoke-RestMethod -Uri "http://localhost:15672/api/overview" -Headers $rabbitHeaders
        Write-Host "✅ RabbitMQ: Connected ($($rabbitResponse.rabbitmq_version))" -ForegroundColor Green
    }
    catch {
        Write-Host "❌ RabbitMQ: Not accessible" -ForegroundColor Red
    }
}

# Run Complete Test Suite
function Run-CompleteTests {
    Write-Host "🚀 Starting Complete Test Suite..." -ForegroundColor Magenta
    Write-Host "================================" -ForegroundColor Magenta
    
    # Test infrastructure
    Test-Infrastructure
    Write-Host ""
    
    # Test authentication and get token
    $token = Test-Authentication
    if (-not $token) {
        Write-Host "❌ Cannot continue without authentication" -ForegroundColor Red
        return
    }
    Write-Host ""
    
    # Test events
    $events = Test-Events $token
    Write-Host ""
    
    # Test ticket purchase
    $purchase = Test-TicketPurchase $token
    Write-Host ""
    
    # Test RabbitMQ stats
    $stats = Test-RabbitMQStats $token
    Write-Host ""
    
    Write-Host "✅ Complete test suite finished!" -ForegroundColor Green
    Write-Host "================================" -ForegroundColor Magenta
}

# Load Testing
function Run-LoadTest($token, $concurrentUsers = 5) {
    Write-Host "⚡ Running Load Test with $concurrentUsers concurrent users..." -ForegroundColor Yellow
    
    $jobs = @()
    
    for ($i = 1; $i -le $concurrentUsers; $i++) {
        $job = Start-Job -ScriptBlock {
            param($token, $userIndex)
            
            $purchaseBody = @{
                eventId = 1
                quantity = 1
                paymentRequest = @{
                    cardNumber = "4111111111111111"
                    expiryMonth = 12
                    expiryYear = 2025
                    cvv = "123"
                    cardHolderName = "Load Test User $userIndex"
                    amount = 50.00
                }
            } | ConvertTo-Json -Depth 3
            
            try {
                $headers = @{ Authorization = "Bearer $token" }
                $response = Invoke-RestMethod -Uri "http://localhost:5003/api/tickets/purchase" -Method POST -Body $purchaseBody -ContentType "application/json" -Headers $headers
                return @{ Success = $true; User = $userIndex }
            }
            catch {
                return @{ Success = $false; User = $userIndex; Error = $_.Exception.Message }
            }
        } -ArgumentList $token, $i
        
        $jobs += $job
    }
    
    Write-Host "Waiting for all purchases to complete..."
    $results = $jobs | Wait-Job | Receive-Job
    $jobs | Remove-Job
    
    $successful = ($results | Where-Object { $_.Success }).Count
    $failed = ($results | Where-Object { -not $_.Success }).Count
    
    Write-Host "✅ Load test completed: $successful successful, $failed failed" -ForegroundColor Green
}

# Menu System
function Show-Menu {
    Clear-Host
    Write-Host "=== Ticketing System Test Menu ===" -ForegroundColor Cyan
    Write-Host "1. Test Infrastructure"
    Write-Host "2. Test Authentication"  
    Write-Host "3. Test Events"
    Write-Host "4. Test Ticket Purchase"
    Write-Host "5. Test RabbitMQ Stats"
    Write-Host "6. Run Complete Test Suite"
    Write-Host "7. Run Load Test"
    Write-Host "8. Open RabbitMQ Management"
    Write-Host "9. Exit"
    Write-Host ""
}

# Main Menu Loop
do {
    Show-Menu
    $choice = Read-Host "Enter your choice (1-9)"
    
    switch ($choice) {
        "1" { Test-Infrastructure }
        "2" { $global:token = Test-Authentication }
        "3" { 
            if (-not $global:token) { $global:token = Test-Authentication }
            Test-Events $global:token 
        }
        "4" { 
            if (-not $global:token) { $global:token = Test-Authentication }
            Test-TicketPurchase $global:token 
        }
        "5" { 
            if (-not $global:token) { $global:token = Test-Authentication }
            Test-RabbitMQStats $global:token 
        }
        "6" { Run-CompleteTests }
        "7" { 
            if (-not $global:token) { $global:token = Test-Authentication }
            Run-LoadTest $global:token 
        }
        "8" { Start-Process "http://localhost:15672" }
        "9" { Write-Host "Goodbye!" -ForegroundColor Green; break }
        default { Write-Host "Invalid choice. Please try again." -ForegroundColor Red }
    }
    
    if ($choice -ne "9") {
        Write-Host ""
        Read-Host "Press Enter to continue"
    }
} while ($choice -ne "9")
