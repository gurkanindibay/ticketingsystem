# Ticketing System Troubleshooting Script
Write-Host "=== Ticketing System Troubleshooting ===" -ForegroundColor Cyan

# Function to check if a port is in use
function Test-Port {
    param($Port)
    try {
        $connection = New-Object System.Net.Sockets.TcpClient("localhost", $Port)
        $connection.Close()
        return $true
    }
    catch {
        return $false
    }
}

# Function to kill processes by port
function Stop-ProcessByPort {
    param($Port)
    try {
        $processes = netstat -ano | findstr ":$Port"
        if ($processes) {
            Write-Host "Found processes on port $Port" -ForegroundColor Yellow
            foreach ($line in $processes) {
                if ($line -match "\s+(\d+)$") {
                    $processId = $matches[1]
                    Write-Host "Stopping process $processId..." -ForegroundColor Yellow
                    Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
                }
            }
        }
    }
    catch {
        Write-Host "Error stopping processes on port $Port" -ForegroundColor Red
    }
}

Write-Host "`n🔍 Checking service ports..." -ForegroundColor Yellow

# Check port 5001 (Authentication)
if (Test-Port 5001) {
    Write-Host "✅ Port 5001 (Authentication): In use" -ForegroundColor Green
} else {
    Write-Host "❌ Port 5001 (Authentication): Not in use" -ForegroundColor Red
}

# Check port 5002 (Ticketing & Events)
if (Test-Port 5002) {
    Write-Host "✅ Port 5002 (Ticketing & Events): In use" -ForegroundColor Green
} else {
    Write-Host "❌ Port 5002 (Ticketing & Events): Not in use" -ForegroundColor Red
}

# Check if old port 5003 is still in use
if (Test-Port 5003) {
    Write-Host "⚠️  Port 5003: Still in use (should be free after consolidation)" -ForegroundColor Yellow
    $stopOld = Read-Host "Stop process on old port 5003? (y/n)"
    if ($stopOld -eq "y") {
        Stop-ProcessByPort 5003
    }
}

Write-Host "`n🐳 Checking Docker containers..." -ForegroundColor Yellow
try {
    $containers = docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
    Write-Host $containers -ForegroundColor Gray
    
    # Check specific services
    $redisRunning = docker ps --filter "name=redis" --format "{{.Names}}" 2>$null
    $postgresRunning = docker ps --filter "name=postgres" --format "{{.Names}}" 2>$null
    $rabbitmqRunning = docker ps --filter "name=rabbitmq" --format "{{.Names}}" 2>$null
    
    if ($redisRunning) {
        Write-Host "✅ Redis container: Running" -ForegroundColor Green
    } else {
        Write-Host "❌ Redis container: Not running" -ForegroundColor Red
        Write-Host "   Start with: docker-compose up -d redis" -ForegroundColor Gray
    }
    
    if ($postgresRunning) {
        Write-Host "✅ PostgreSQL container: Running" -ForegroundColor Green
    } else {
        Write-Host "❌ PostgreSQL container: Not running" -ForegroundColor Red
        Write-Host "   Start with: docker-compose up -d postgres" -ForegroundColor Gray
    }
    
    if ($rabbitmqRunning) {
        Write-Host "✅ RabbitMQ container: Running" -ForegroundColor Green
    } else {
        Write-Host "❌ RabbitMQ container: Not running" -ForegroundColor Red
        Write-Host "   Start with: docker-compose up -d rabbitmq" -ForegroundColor Gray
    }
}
catch {
    Write-Host "❌ Docker not available or not running" -ForegroundColor Red
    Write-Host "   Install Docker Desktop and ensure it's running" -ForegroundColor Gray
}

Write-Host "`n🔧 Package Version Check..." -ForegroundColor Yellow
Write-Host "Entity Framework Core version should be 9.0.x" -ForegroundColor Gray
Write-Host "Npgsql.EntityFrameworkCore.PostgreSQL should be 9.0.1" -ForegroundColor Gray

$needsClean = Read-Host "`nDo you need to clean and rebuild projects? (y/n)"
if ($needsClean -eq "y") {
    Write-Host "`n🧹 Cleaning projects..." -ForegroundColor Yellow
    
    # Stop any running processes first
    Write-Host "Stopping services on ports 5001-5003..." -ForegroundColor Yellow
    Stop-ProcessByPort 5001
    Stop-ProcessByPort 5002
    Stop-ProcessByPort 5003
    
    Start-Sleep -Seconds 3
    
    # Clean and build
    Write-Host "Cleaning solution..." -ForegroundColor Yellow
    dotnet clean
    
    Write-Host "Building solution..." -ForegroundColor Yellow
    dotnet build
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Build successful!" -ForegroundColor Green
    } else {
        Write-Host "❌ Build failed. Check the output above for errors." -ForegroundColor Red
        Write-Host "Common issues:" -ForegroundColor Yellow
        Write-Host "  • Package version mismatches (see package version check above)" -ForegroundColor Gray
        Write-Host "  • Services still running (ports still locked)" -ForegroundColor Gray
        Write-Host "  • Missing dependencies" -ForegroundColor Gray
    }
}

Write-Host "`n📋 Quick Start Commands:" -ForegroundColor Cyan
Write-Host "Start Authentication:    dotnet run --project src\TicketingSystem.Authentication" -ForegroundColor Gray
Write-Host "Start Ticketing & Events: dotnet run --project src\TicketingSystem.Ticketing" -ForegroundColor Gray
Write-Host "Run automated tests:     .\simple-test.ps1" -ForegroundColor Gray

Write-Host "`n🌐 Service URLs:" -ForegroundColor Cyan
Write-Host "Authentication:          http://localhost:5001/swagger" -ForegroundColor Gray
Write-Host "Ticketing & Events:      http://localhost:5002/swagger" -ForegroundColor Gray
Write-Host "RabbitMQ Management:     http://localhost:15672" -ForegroundColor Gray

Read-Host "`nPress Enter to exit"
