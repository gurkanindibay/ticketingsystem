# Test Rate Limiting Script
# This script tests the rate limiting functionality of the authentication API

Write-Host "🔬 Testing Rate Limiting on Authentication API" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green

$baseUrl = "http://localhost:5001/api/auth"
$headers = @{ "Content-Type" = "application/json" }

Write-Host "`n1. Testing Registration Rate Limiting (5 attempts per hour)" -ForegroundColor Yellow
Write-Host "Sending 8 registration requests..." -ForegroundColor Cyan

for ($i = 1; $i -le 8; $i++) {
    $body = @{
        email = "test$i@example.com"
        password = "TestPassword123!"
        firstName = "Test"
        lastName = "User$i"
    } | ConvertTo-Json

    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/register" -Method POST -Headers $headers -Body $body -ErrorAction Stop
        Write-Host "✅ Attempt $i`: SUCCESS - User registered" -ForegroundColor Green
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode
        if ($statusCode -eq 429) {
            Write-Host "⛔ Attempt $i`: RATE LIMITED (429) - Too many attempts!" -ForegroundColor Red
        }
        elseif ($statusCode -eq 409) {
            Write-Host "⚠️  Attempt $i`: CONFLICT (409) - User already exists" -ForegroundColor Yellow
        }
        else {
            Write-Host "❌ Attempt $i`: ERROR ($statusCode)" -ForegroundColor Red
        }
    }
    Start-Sleep -Milliseconds 500
}

Write-Host "`n2. Testing Login Rate Limiting (10 attempts per 15 minutes)" -ForegroundColor Yellow
Write-Host "Sending 12 login requests with wrong password..." -ForegroundColor Cyan

for ($i = 1; $i -le 12; $i++) {
    $body = @{
        email = "nonexistent@example.com"
        password = "wrongpassword"
    } | ConvertTo-Json

    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/login" -Method POST -Headers $headers -Body $body -ErrorAction Stop
        Write-Host "✅ Attempt $i`: SUCCESS (unexpected)" -ForegroundColor Green
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode
        if ($statusCode -eq 429) {
            Write-Host "⛔ Attempt $i`: RATE LIMITED (429) - Too many attempts!" -ForegroundColor Red
        }
        elseif ($statusCode -eq 401) {
            Write-Host "🔒 Attempt $i`: UNAUTHORIZED (401) - Invalid credentials" -ForegroundColor Gray
        }
        else {
            Write-Host "❌ Attempt $i`: ERROR ($statusCode)" -ForegroundColor Red
        }
    }
    Start-Sleep -Milliseconds 300
}

Write-Host "`n3. Testing Refresh Token Rate Limiting (20 attempts per 5 minutes)" -ForegroundColor Yellow
Write-Host "Sending 25 refresh requests with invalid token..." -ForegroundColor Cyan

for ($i = 1; $i -le 25; $i++) {
    $body = @{
        refreshToken = "invalid-refresh-token-$i"
        accessToken = "invalid-access-token"
    } | ConvertTo-Json

    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/refresh" -Method POST -Headers $headers -Body $body -ErrorAction Stop
        Write-Host "✅ Attempt $i`: SUCCESS (unexpected)" -ForegroundColor Green
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode
        if ($statusCode -eq 429) {
            Write-Host "⛔ Attempt $i`: RATE LIMITED (429) - Too many attempts!" -ForegroundColor Red
        }
        elseif ($statusCode -eq 400 -or $statusCode -eq 401) {
            Write-Host "🔒 Attempt $i`: INVALID TOKEN ($statusCode)" -ForegroundColor Gray
        }
        else {
            Write-Host "❌ Attempt $i`: ERROR ($statusCode)" -ForegroundColor Red
        }
    }
    Start-Sleep -Milliseconds 200
}

Write-Host "`n🎯 Rate Limiting Test Complete!" -ForegroundColor Green
Write-Host "Expected behavior:" -ForegroundColor Cyan
Write-Host "- Registration: First 5 succeed, rest get 429" -ForegroundColor White
Write-Host "- Login: First 10 get 401, rest get 429" -ForegroundColor White  
Write-Host "- Refresh: First 20 get 400/401, rest get 429" -ForegroundColor White
Write-Host "`nNote: Some registrations may show 409 (Conflict) if users already exist from previous runs." -ForegroundColor Gray
