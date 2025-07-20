#!/usr/bin/env pwsh

# Simple ticket purchase test script for Windows PowerShell
# This script tests the ticket purchase functionality with proper JSON handling

Write-Host "Testing Ticket Purchase..." -ForegroundColor Green

# Replace this with a fresh token from your login
$token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJuYW1laWQiOiJjYjE4YjMzZi1kODk4LTQ0OWItYjE0OC0xZTViZWQzYzA5MTYiLCJ1bmlxdWVfbmFtZSI6InRlc3R1c2VyQGV4YW1wbGUuY29tIiwiZW1haWwiOiJ0ZXN0dXNlckBleGFtcGxlLmNvbSIsImZpcnN0TmFtZSI6InRlc3R1c2VyIiwibGFzdE5hbWUiOiJ0ZXN0dXNlciIsImlzQWN0aXZlIjoiVHJ1ZSIsInJvbGUiOiJVc2VyIiwibmJmIjoxNzUzMDI4NTc4LCJleHAiOjE3NTMwMjk0NzgsImlhdCI6MTc1MzAyODU3OCwiaXNzIjoiVGlja2V0aW5nU3lzdGVtIiwiYXVkIjoiVGlja2V0aW5nU3lzdGVtLlVzZXJzIn0.NosrHhnoDleSp70M7pDv-OXao2PtbrFMBcB2_S8wpns"

$headers = @{
    'Content-Type' = 'application/json'
    'Authorization' = "Bearer $token"
}

$purchaseRequest = @{
    eventId = 1
    eventDate = "2025-08-15T20:00:00Z"
    quantity = 1
    paymentDetails = @{
        paymentMethod = "credit_card"
        cardNumber = "4111111111111111"
        expiryMonth = "12"
        expiryYear = "2025"
        cvv = "123"
        cardHolderName = "Test User"
        amount = 50.00
        currency = "USD"
        description = "Test ticket purchase from PowerShell script"
    }
} | ConvertTo-Json -Depth 3

Write-Host "Request Body:" -ForegroundColor Yellow
Write-Host $purchaseRequest

Write-Host "`nSending request to http://localhost:5002/api/tickets/purchase..." -ForegroundColor Yellow

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5002/api/tickets/purchase" -Method Post -Headers $headers -Body $purchaseRequest
    
    Write-Host "`nSUCCESS!" -ForegroundColor Green
    Write-Host "Response:" -ForegroundColor Green
    Write-Host ($response | ConvertTo-Json -Depth 5)
    
} catch {
    Write-Host "`nERROR!" -ForegroundColor Red
    Write-Host "Status: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
    
    if ($_.ErrorDetails) {
        Write-Host "Details: $($_.ErrorDetails.Message)" -ForegroundColor Red
    } else {
        Write-Host "Message: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    # Try to get more details from the response stream
    try {
        $errorStream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($errorStream)
        $errorContent = $reader.ReadToEnd()
        Write-Host "Full Error Response: $errorContent" -ForegroundColor Red
    } catch {
        Write-Host "Could not read error response details" -ForegroundColor Red
    }
}

Write-Host "`nTest completed." -ForegroundColor Green
