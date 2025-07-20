# PowerShell script to set up HTTPS development environment for TicketingSystem

Write-Host "Setting up HTTPS development environment for TicketingSystem..." -ForegroundColor Green

# Step 1: Clean existing development certificates
Write-Host "Cleaning existing development certificates..." -ForegroundColor Yellow
dotnet dev-certs https --clean

# Step 2: Create new development certificate
Write-Host "Creating new development certificate..." -ForegroundColor Yellow
dotnet dev-certs https --trust

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Development certificate created and trusted successfully!" -ForegroundColor Green
    
    Write-Host "`n📋 Available endpoints after starting services:" -ForegroundColor Cyan
    Write-Host "HTTP URLs (Recommended for development):" -ForegroundColor White
    Write-Host "  • Authentication Service: http://localhost:5001/swagger" -ForegroundColor Gray
    Write-Host "  • Ticketing & Events:     http://localhost:5002/swagger" -ForegroundColor Gray
    
    Write-Host "`nHTTPS URLs (After certificate setup):" -ForegroundColor White
    Write-Host "  • Authentication Service: https://localhost:7001/swagger" -ForegroundColor Gray
    Write-Host "  • Ticketing & Events:     https://localhost:7002/swagger" -ForegroundColor Gray
    
    Write-Host "`n🚀 How to run services:" -ForegroundColor Cyan
    Write-Host "HTTP (Default Profile):" -ForegroundColor White
    Write-Host "  dotnet run --project src\TicketingSystem.Authentication" -ForegroundColor Gray
    Write-Host "  dotnet run --project src\TicketingSystem.Ticketing" -ForegroundColor Gray
    
    Write-Host "`nHTTPS:" -ForegroundColor White
    Write-Host "  dotnet run --project src\TicketingSystem.Authentication --launch-profile Development-HTTPS" -ForegroundColor Gray
    Write-Host "  dotnet run --project src\TicketingSystem.Ticketing --launch-profile Development-HTTPS" -ForegroundColor Gray
    
} else {
    Write-Host "❌ Failed to create development certificate." -ForegroundColor Red
    Write-Host "You can still use HTTP endpoints for development." -ForegroundColor Yellow
    
    Write-Host "`n📋 HTTP endpoints (Always available):" -ForegroundColor Cyan
    Write-Host "  • Authentication Service: http://localhost:5001/swagger" -ForegroundColor Gray
    Write-Host "  • Ticketing & Events:     http://localhost:5002/swagger" -ForegroundColor Gray
}

Write-Host "`n💡 Tips:" -ForegroundColor Cyan
Write-Host "  • Use HTTP endpoints for easier development and testing" -ForegroundColor Gray
Write-Host "  • HTTPS redirection is disabled in Development mode" -ForegroundColor Gray
Write-Host "  • Run 'dotnet dev-certs https --check' to verify certificate status" -ForegroundColor Gray
Write-Host "  • If you still have HTTPS issues, stick to HTTP endpoints" -ForegroundColor Gray
