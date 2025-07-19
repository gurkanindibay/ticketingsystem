# Test the validation service integration
Write-Host "Testing validation service integration..." -ForegroundColor Green

# Start the authentication service in background
Start-Process powershell -ArgumentList "-Command `"cd c:\source\Tryouts\TicketingSystem\src\TicketingSystem.Authentication; dotnet run`"" -WindowStyle Hidden

# Wait for service to start
Write-Host "Waiting for service to start..." -ForegroundColor Yellow
Start-Sleep -Seconds 10

try {
    # Test 1: Valid registration
    Write-Host "`nTest 1: Valid Registration" -ForegroundColor Cyan
    $validRequest = @{
        email = "test@example.com"
        password = "SecurePass123!"
        fullName = "Test User"
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "https://localhost:7001/api/auth/register" -Method POST -Body $validRequest -ContentType "application/json" -SkipCertificateCheck
    Write-Host "Valid registration: SUCCESS" -ForegroundColor Green
    
    # Test 2: Invalid email format
    Write-Host "`nTest 2: Invalid Email Format" -ForegroundColor Cyan
    $invalidEmailRequest = @{
        email = "invalid-email"
        password = "SecurePass123!"
        fullName = "Test User"
    } | ConvertTo-Json
    
    try {
        $response2 = Invoke-RestMethod -Uri "https://localhost:7001/api/auth/register" -Method POST -Body $invalidEmailRequest -ContentType "application/json" -SkipCertificateCheck
        Write-Host "Invalid email test: FAILED (should have been rejected)" -ForegroundColor Red
    } catch {
        Write-Host "Invalid email test: SUCCESS (correctly rejected)" -ForegroundColor Green
    }
    
    # Test 3: Weak password
    Write-Host "`nTest 3: Weak Password" -ForegroundColor Cyan
    $weakPasswordRequest = @{
        email = "test2@example.com"
        password = "weak"
        fullName = "Test User2"
    } | ConvertTo-Json
    
    try {
        $response3 = Invoke-RestMethod -Uri "https://localhost:7001/api/auth/register" -Method POST -Body $weakPasswordRequest -ContentType "application/json" -SkipCertificateCheck
        Write-Host "Weak password test: FAILED (should have been rejected)" -ForegroundColor Red
    } catch {
        Write-Host "Weak password test: SUCCESS (correctly rejected)" -ForegroundColor Green
    }
    
    # Test 4: Common password
    Write-Host "`nTest 4: Common Password" -ForegroundColor Cyan
    $commonPasswordRequest = @{
        email = "test3@example.com"
        password = "password123"
        fullName = "Test User3"
    } | ConvertTo-Json
    
    try {
        $response4 = Invoke-RestMethod -Uri "https://localhost:7001/api/auth/register" -Method POST -Body $commonPasswordRequest -ContentType "application/json" -SkipCertificateCheck
        Write-Host "Common password test: FAILED (should have been rejected)" -ForegroundColor Red
    } catch {
        Write-Host "Common password test: SUCCESS (correctly rejected)" -ForegroundColor Green
    }
    
    Write-Host "`nValidation service integration tests completed!" -ForegroundColor Green
    
} catch {
    Write-Host "Error testing validation service: $($_.Exception.Message)" -ForegroundColor Red
} finally {
    # Stop the service
    Write-Host "`nStopping authentication service..." -ForegroundColor Yellow
    Get-Process -Name "TicketingSystem.Authentication" -ErrorAction SilentlyContinue | Stop-Process -Force
    Write-Host "Service stopped." -ForegroundColor Green
}
