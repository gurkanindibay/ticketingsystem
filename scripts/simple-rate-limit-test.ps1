# Simple Rate Limiting Test
# Tests the built-in .NET rate limiting functionality

Write-Host "Testing Built-in .NET Rate Limiting" -ForegroundColor Green
Write-Host "====================================" -ForegroundColor Green

$baseUrl = "http://localhost:5001/api/auth"

Write-Host ""
Write-Host "Testing Registration Endpoint (5 attempts allowed per hour):" -ForegroundColor Yellow

for ($i = 1; $i -le 8; $i++) {
    $body = @{
        email = "test$i@ratelimit.com"
        password = "TestPassword123!"
        firstName = "Test"
        lastName = "User"
    } | ConvertTo-Json
    
    try {
        Invoke-RestMethod -Uri "$baseUrl/register" -Method POST -ContentType "application/json" -Body $body | Out-Null
        Write-Host "Attempt $i : SUCCESS - Registration allowed" -ForegroundColor Green
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        
        switch ($statusCode) {
            429 { 
                Write-Host "Attempt $i : RATE LIMITED (429) - Working correctly!" -ForegroundColor Red
                break
            }
            409 { 
                Write-Host "Attempt $i : CONFLICT (409) - User exists" -ForegroundColor Yellow
                break
            }
            400 { 
                Write-Host "Attempt $i : BAD REQUEST (400) - Validation error" -ForegroundColor Orange
                break
            }
            default { 
                Write-Host "Attempt $i : ERROR ($statusCode)" -ForegroundColor Red
                break
            }
        }
    }
    
    Start-Sleep -Milliseconds 500
}

Write-Host ""
Write-Host "Testing Login Endpoint (10 attempts allowed per 15 minutes):" -ForegroundColor Yellow

for ($i = 1; $i -le 12; $i++) {
    $body = @{
        email = "fake@user.com"
        password = "wrongpassword"
    } | ConvertTo-Json
    
    try {
        Invoke-RestMethod -Uri "$baseUrl/login" -Method POST -ContentType "application/json" -Body $body | Out-Null
        Write-Host "Attempt $i : SUCCESS (unexpected)" -ForegroundColor Green
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        
        switch ($statusCode) {
            429 { 
                Write-Host "Attempt $i : RATE LIMITED (429) - Working correctly!" -ForegroundColor Red
                break
            }
            401 { 
                Write-Host "Attempt $i : UNAUTHORIZED (401) - Invalid credentials" -ForegroundColor Gray
                break
            }
            400 { 
                Write-Host "Attempt $i : BAD REQUEST (400) - Validation error" -ForegroundColor Orange
                break
            }
            default { 
                Write-Host "Attempt $i : ERROR ($statusCode)" -ForegroundColor Red
                break
            }
        }
    }
    
    Start-Sleep -Milliseconds 300
}

Write-Host ""
Write-Host "Rate Limiting Test Complete!" -ForegroundColor Green
Write-Host "Expected behavior:" -ForegroundColor Cyan
Write-Host "- Registration: First few succeed, then 429 after limit" -ForegroundColor White
Write-Host "- Login: 401 for wrong credentials, then 429 after rate limit" -ForegroundColor White
