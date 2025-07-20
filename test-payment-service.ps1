# Test Payment Service PowerShell Script
# This script demonstrates the new buy ticket operation with mock payment service

Write-Host "=== Ticketing System - Payment Service Test ===" -ForegroundColor Green
Write-Host ""

# Configuration
$baseUrl = "http://localhost:5003"
$headers = @{
    "Content-Type" = "application/json"
}

# Test payload for successful payment
$successfulPayment = @{
    eventId = 1
    eventDate = "2025-08-20T20:00:00Z"
    quantity = 2
    paymentDetails = @{
        paymentMethod = "credit_card"
        cardNumber = "4111111111111111"
        expiryMonth = "12"
        expiryYear = "2026"
        cvv = "123"
        cardHolderName = "John Doe"
        currency = "USD"
        description = "Rock Concert 2025 Tickets"
    }
} | ConvertTo-Json -Depth 3

# Test payload for failed payment
$failedPayment = @{
    eventId = 1
    eventDate = "2025-08-20T20:00:00Z"
    quantity = 1
    paymentDetails = @{
        paymentMethod = "credit_card"
        cardNumber = "4000000000000002"
        expiryMonth = "12"
        expiryYear = "2026"
        cvv = "123"
        cardHolderName = "Jane Smith"
        currency = "USD"
        description = "Rock Concert 2025 Tickets"
    }
} | ConvertTo-Json -Depth 3

Write-Host "Test Payloads Created:" -ForegroundColor Yellow
Write-Host "1. Successful Payment (Card: 4111111111111111)"
Write-Host "2. Failed Payment (Card: 4000000000000002)"
Write-Host ""

Write-Host "To test manually, start the Ticketing service with:" -ForegroundColor Cyan
Write-Host "cd src\TicketingSystem.Ticketing"
Write-Host "dotnet run"
Write-Host ""

Write-Host "Then use these test commands:" -ForegroundColor Cyan
Write-Host ""

Write-Host "# Health Check:" -ForegroundColor Magenta
Write-Host "curl http://localhost:5003/health"
Write-Host ""

Write-Host "# Successful Payment Test:" -ForegroundColor Magenta
Write-Host "curl -X POST http://localhost:5003/api/tickets/purchase \"
Write-Host "  -H 'Content-Type: application/json' \"
Write-Host "  -d '$successfulPayment'"
Write-Host ""

Write-Host "# Failed Payment Test:" -ForegroundColor Magenta
Write-Host "curl -X POST http://localhost:5003/api/tickets/purchase \"
Write-Host "  -H 'Content-Type: application/json' \"
Write-Host "  -d '$failedPayment'"
Write-Host ""

Write-Host "# Get User Tickets:" -ForegroundColor Magenta
Write-Host "curl 'http://localhost:5003/api/tickets/user?pageNumber=1&pageSize=10'"
Write-Host ""

Write-Host "Access Swagger UI at: http://localhost:5003/swagger" -ForegroundColor Green
Write-Host ""

Write-Host "=== Test Card Numbers ===" -ForegroundColor Yellow
Write-Host "4111111111111111 - Successful payment"
Write-Host "4242424242424242 - Successful payment"
Write-Host "4000000000000002 - Payment declined"
Write-Host "4000000000000119 - Processing status"
Write-Host "4000000000000127 - Insufficient funds"
