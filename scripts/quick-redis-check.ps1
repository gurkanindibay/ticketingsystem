# Quick Redis and Service Diagnostic Script
Write-Host "=== Quick Diagnostic for Redis Configuration Issue ===" -ForegroundColor Cyan

# Check if Redis is running
Write-Host "`n🔍 Checking Redis connectivity..." -ForegroundColor Yellow
try {
    $result = Test-NetConnection -ComputerName localhost -Port 6379 -WarningAction SilentlyContinue
    if ($result.TcpTestSucceeded) {
        Write-Host "✅ Redis port 6379: Accessible" -ForegroundColor Green
    } else {
        Write-Host "❌ Redis port 6379: Not accessible" -ForegroundColor Red
        Write-Host "   Solution: Start Redis with 'docker-compose up -d redis'" -ForegroundColor Gray
    }
}
catch {
    Write-Host "❌ Cannot test Redis connectivity" -ForegroundColor Red
}

# Check if Ticketing service is running
Write-Host "`n🔍 Checking Ticketing service..." -ForegroundColor Yellow
try {
    $result = Test-NetConnection -ComputerName localhost -Port 5002 -WarningAction SilentlyContinue
    if ($result.TcpTestSucceeded) {
        Write-Host "✅ Ticketing service port 5002: Running" -ForegroundColor Green
        Write-Host "   ⚠️  Service needs restart after configuration changes" -ForegroundColor Yellow
    } else {
        Write-Host "❌ Ticketing service port 5002: Not running" -ForegroundColor Red
    }
}
catch {
    Write-Host "❌ Cannot test service connectivity" -ForegroundColor Red
}

# Show Docker status
Write-Host "`n🐳 Docker status..." -ForegroundColor Yellow
try {
    $redisContainer = docker ps --filter "name=redis" --format "{{.Names}}\t{{.Status}}" 2>$null
    if ($redisContainer) {
        Write-Host "✅ Redis container: $redisContainer" -ForegroundColor Green
    } else {
        Write-Host "❌ Redis container: Not running" -ForegroundColor Red
        $startInfra = Read-Host "Start infrastructure services? (y/n)"
        if ($startInfra -eq "y") {
            Write-Host "Starting infrastructure..." -ForegroundColor Yellow
            docker-compose up -d postgres redis rabbitmq
        }
    }
}
catch {
    Write-Host "❌ Docker not available" -ForegroundColor Red
}

Write-Host "`n🔧 Solution Steps:" -ForegroundColor Cyan
Write-Host "1. Ensure Redis is running: docker-compose up -d redis" -ForegroundColor Gray
Write-Host "2. Stop Ticketing service: Ctrl+C in the terminal" -ForegroundColor Gray
Write-Host "3. Restart service: dotnet run --project src\TicketingSystem.Ticketing" -ForegroundColor Gray
Write-Host "4. Test: curl http://localhost:5002/health" -ForegroundColor Gray

Read-Host "`nPress Enter to exit"
