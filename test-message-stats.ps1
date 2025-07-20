#!/usr/bin/env pwsh
# Test message statistics endpoint

Write-Host "=== Message Statistics Monitor ===" -ForegroundColor Green

try {
    # Get initial statistics
    Write-Host "`nGetting initial message statistics..." -ForegroundColor Yellow
    $initialStats = curl.exe -s http://localhost:5002/api/tickets/admin/message-stats | ConvertFrom-Json
    
    if ($initialStats.success) {
        Write-Host "Initial Statistics Retrieved:" -ForegroundColor Green
        $data = $initialStats.data
        Write-Host "  Total Published (All Queues): $($data.totalPublishedAllQueues)" -ForegroundColor Cyan
        Write-Host "  Total Processed (All Queues): $($data.totalProcessedAllQueues)" -ForegroundColor Cyan
        Write-Host "  Success Rate: $($data.overallSuccessRate.ToString('F2'))%" -ForegroundColor Cyan
        Write-Host "  Uptime: $($data.totalUptime)" -ForegroundColor Gray
        
        if ($data.queueStats -and $data.queueStats.PSObject.Properties.Count -gt 0) {
            Write-Host "`nPer-Queue Statistics:" -ForegroundColor Yellow
            foreach ($queueName in $data.queueStats.PSObject.Properties.Name) {
                $queue = $data.queueStats.$queueName
                Write-Host "  $queueName:" -ForegroundColor White
                Write-Host "    Published: $($queue.totalPublished)" -ForegroundColor Gray
                Write-Host "    Processed: $($queue.totalProcessed) (Success: $($queue.totalProcessedSuccess), Failed: $($queue.totalProcessedFailed))" -ForegroundColor Gray
                Write-Host "    Success Rate: $($queue.successRate.ToString('F2'))%" -ForegroundColor Gray
                Write-Host "    Avg Processing Time: $($queue.averageProcessingTimeMs.ToString('F2'))ms" -ForegroundColor Gray
                if ($queue.lastProcessedAt) {
                    Write-Host "    Last Processed: $($queue.lastProcessedAt)" -ForegroundColor Gray
                }
            }
        }
    } else {
        Write-Host "Failed to get initial statistics: $($initialStats.message)" -ForegroundColor Red
    }
    
    # Get authentication token
    Write-Host "`nGetting authentication token..." -ForegroundColor Yellow
    $authResponse = curl.exe -s -X POST http://localhost:5001/api/auth/login -H "Content-Type: application/json" -d '{"email": "john.doe@example.com", "password": "SecurePass123!"}' | ConvertFrom-Json
    
    if (-not $authResponse.success) {
        Write-Host "Authentication failed: $($authResponse.message)" -ForegroundColor Red
        exit 1
    }
    
    $token = $authResponse.data.token
    Write-Host "Authentication successful!" -ForegroundColor Green
    
    # Make a few ticket purchases to generate statistics
    Write-Host "`nMaking 3 ticket purchases to generate message statistics..." -ForegroundColor Yellow
    
    for ($i = 1; $i -le 3; $i++) {
        Write-Host "  Purchase $i..." -NoNewline -ForegroundColor White
        
        $purchaseData = '{"eventId": 1, "quantity": 1, "paymentMethod": "credit_card", "paymentDetails": {"amount": 35.00, "paymentMethod": "credit_card", "cardNumber": "4111111111111111", "expiryMonth": "12", "expiryYear": "2025", "cvv": "123", "cardholderName": "Stats Test User"}}'
        
        $response = curl.exe -s -X POST http://localhost:5002/api/tickets/purchase -H "Content-Type: application/json" -H "Authorization: Bearer $token" -d $purchaseData | ConvertFrom-Json
        
        if ($response.success) {
            Write-Host " Success" -ForegroundColor Green
        } else {
            Write-Host " Failed: $($response.message)" -ForegroundColor Red
        }
        
        Start-Sleep -Milliseconds 500  # Small delay between purchases
    }
    
    # Wait a moment for processing
    Write-Host "`nWaiting for message processing..." -ForegroundColor Gray
    Start-Sleep -Seconds 2
    
    # Get updated statistics
    Write-Host "`nGetting updated message statistics..." -ForegroundColor Yellow
    $updatedStats = curl.exe -s http://localhost:5002/api/tickets/admin/message-stats | ConvertFrom-Json
    
    if ($updatedStats.success) {
        Write-Host "`nUpdated Statistics:" -ForegroundColor Green
        $data = $updatedStats.data
        Write-Host "  Total Published: $($data.totalPublishedAllQueues)" -ForegroundColor Cyan
        Write-Host "  Total Processed: $($data.totalProcessedAllQueues)" -ForegroundColor Cyan
        Write-Host "  Success Rate: $($data.overallSuccessRate.ToString('F2'))%" -ForegroundColor Cyan
        
        if ($data.queueStats -and $data.queueStats.PSObject.Properties.Count -gt 0) {
            Write-Host "`nDetailed Queue Statistics:" -ForegroundColor Yellow
            foreach ($queueName in $data.queueStats.PSObject.Properties.Name) {
                $queue = $data.queueStats.$queueName
                Write-Host "  $queueName:" -ForegroundColor White
                Write-Host "    Published: $($queue.totalPublished)" -ForegroundColor Gray
                Write-Host "    Processed Successfully: $($queue.totalProcessedSuccess)" -ForegroundColor Green
                Write-Host "    Processing Failed: $($queue.totalProcessedFailed)" -ForegroundColor Red
                Write-Host "    Success Rate: $($queue.successRate.ToString('F2'))%" -ForegroundColor Cyan
                Write-Host "    Average Processing Time: $($queue.averageProcessingTimeMs.ToString('F2'))ms" -ForegroundColor Gray
                if ($queue.lastProcessedAt) {
                    Write-Host "    Last Processed: $($queue.lastProcessedAt)" -ForegroundColor Gray
                }
                
                if ($queue.messageTypes -and $queue.messageTypes.PSObject.Properties.Count -gt 0) {
                    Write-Host "    Message Types:" -ForegroundColor Yellow
                    foreach ($typeName in $queue.messageTypes.PSObject.Properties.Name) {
                        $type = $queue.messageTypes.$typeName
                        Write-Host "      $typeName - Published: $($type.published), Processed: $($type.totalProcessed)" -ForegroundColor Gray
                    }
                }
            }
        }
        
        Write-Host "`nComparison with Initial Stats:" -ForegroundColor Magenta
        $publishedDiff = $data.totalPublishedAllQueues - $initialStats.data.totalPublishedAllQueues
        $processedDiff = $data.totalProcessedAllQueues - $initialStats.data.totalProcessedAllQueues
        Write-Host "  New Messages Published: +$publishedDiff" -ForegroundColor Cyan
        Write-Host "  New Messages Processed: +$processedDiff" -ForegroundColor Cyan
        
    } else {
        Write-Host "Failed to get updated statistics: $($updatedStats.message)" -ForegroundColor Red
    }
    
} catch {
    Write-Host "Error during statistics test: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== Message Statistics Test Complete ===" -ForegroundColor Green
Write-Host "You can also view these stats at: http://localhost:5002/api/tickets/admin/message-stats" -ForegroundColor Gray
