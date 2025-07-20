#!/usr/bin/env pwsh
# Test queue status endpoint

Write-Host "=== Testing Queue Status Endpoint ===" -ForegroundColor Green

try {
    # Get queue status
    Write-Host "`nTesting queue status endpoint..." -ForegroundColor Yellow
    $queueResponse = curl.exe -s http://localhost:5002/api/tickets/admin/queue-status | ConvertFrom-Json
    
    if ($queueResponse.success) {
        Write-Host "Queue Status Retrieved Successfully!" -ForegroundColor Green
        
        # Display queue information
        $queues = $queueResponse.data.queues
        if ($queues) {
            Write-Host "`nQueue Statistics:" -ForegroundColor Cyan
            foreach ($queueName in $queues.PSObject.Properties.Name) {
                $queue = $queues.$queueName
                Write-Host "  ${queueName}:" -ForegroundColor White
                Write-Host "    Messages: $($queue.messageCount)" -ForegroundColor Gray
                Write-Host "    Consumers: $($queue.consumerCount)" -ForegroundColor Gray
            }
        } else {
            Write-Host "`nLegacy Queue Statistics:" -ForegroundColor Cyan
            Write-Host "  Capacity Queue Messages: $($queueResponse.data.capacity_queue_messages)" -ForegroundColor Gray
            Write-Host "  Capacity Queue Consumers: $($queueResponse.data.capacity_queue_consumers)" -ForegroundColor Gray
            Write-Host "  Transaction Queue Messages: $($queueResponse.data.transaction_queue_messages)" -ForegroundColor Gray
            Write-Host "  Transaction Queue Consumers: $($queueResponse.data.transaction_queue_consumers)" -ForegroundColor Gray
            Write-Host "  Dead Letter Queue Messages: $($queueResponse.data.dead_letter_queue_messages)" -ForegroundColor Gray
        }
        
        # Display connection status
        $connection = $queueResponse.data.connection
        if ($connection) {
            Write-Host "`nConnection Status:" -ForegroundColor Cyan
            Write-Host "  Connection Open: $($connection.isOpen)" -ForegroundColor Gray
            Write-Host "  Channel Number: $($connection.channelNumber)" -ForegroundColor Gray
        } else {
            Write-Host "`nConnection Status:" -ForegroundColor Cyan
            Write-Host "  Connection Open: $($queueResponse.data.connection_open)" -ForegroundColor Gray
            Write-Host "  Channel Open: $($queueResponse.data.channel_open)" -ForegroundColor Gray
        }
        
        Write-Host "`nTimestamp: $($queueResponse.data.timestamp)" -ForegroundColor Gray
        
    } else {
        Write-Host "Failed to get queue status: $($queueResponse.message)" -ForegroundColor Red
    }
    
} catch {
    Write-Host "Error testing queue status: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== Queue Status Test Complete ===" -ForegroundColor Green
