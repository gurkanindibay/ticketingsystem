@echo off
echo Setting up HTTPS development environment for TicketingSystem...
echo.

echo Cleaning existing development certificates...
dotnet dev-certs https --clean

echo Creating new development certificate...
dotnet dev-certs https --trust

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ✅ Development certificate created and trusted successfully!
    echo.
    echo 📋 Available endpoints after starting services:
    echo HTTP URLs (Recommended for development):
    echo   • Authentication Service: http://localhost:5001/swagger
    echo   • Events Service:         http://localhost:5002/swagger
    echo   • Ticketing Service:      http://localhost:5003/swagger
    echo.
    echo HTTPS URLs (After certificate setup):
    echo   • Authentication Service: https://localhost:7001/swagger
    echo   • Events Service:         https://localhost:7002/swagger
    echo   • Ticketing Service:      https://localhost:7003/swagger
    echo.
    echo 🚀 How to run services:
    echo HTTP (Default Profile):
    echo   dotnet run --project src\TicketingSystem.Authentication
    echo   dotnet run --project src\TicketingSystem.Events
    echo   dotnet run --project src\TicketingSystem.Ticketing
    echo.
    echo HTTPS:
    echo   dotnet run --project src\TicketingSystem.Authentication --launch-profile Development-HTTPS
    echo   dotnet run --project src\TicketingSystem.Events --launch-profile Development-HTTPS
    echo   dotnet run --project src\TicketingSystem.Ticketing --launch-profile Development-HTTPS
) else (
    echo ❌ Failed to create development certificate.
    echo You can still use HTTP endpoints for development.
    echo.
    echo 📋 HTTP endpoints (Always available):
    echo   • Authentication Service: http://localhost:5001/swagger
    echo   • Events Service:         http://localhost:5002/swagger
    echo   • Ticketing Service:      http://localhost:5003/swagger
)

echo.
echo 💡 Tips:
echo   • Use HTTP endpoints for easier development and testing
echo   • HTTPS redirection is disabled in Development mode
echo   • Run 'dotnet dev-certs https --check' to verify certificate status
echo   • If you still have HTTPS issues, stick to HTTP endpoints

pause
