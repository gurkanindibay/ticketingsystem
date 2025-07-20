@echo off
echo === Ticketing System Manual Tests ===

REM Test 1: Health Checks
echo.
echo Testing Infrastructure...
curl -s http://localhost:5001/health && echo Authentication Service: OK || echo Authentication Service: FAILED
curl -s http://localhost:5002/health && echo Events Service: OK || echo Events Service: FAILED  
curl -s http://localhost:5003/health && echo Ticketing Service: OK || echo Ticketing Service: FAILED

REM Test 2: Register User
echo.
echo Testing User Registration...
curl -X POST http://localhost:5001/api/auth/register ^
  -H "Content-Type: application/json" ^
  -d "{\"email\":\"testuser@example.com\",\"password\":\"TestPassword123!\",\"confirmPassword\":\"TestPassword123!\"}" ^
  -o register_response.json

if exist register_response.json (
    echo Registration: SUCCESS
) else (
    echo Registration: FAILED
)

REM Test 3: Login User  
echo.
echo Testing User Login...
curl -X POST http://localhost:5001/api/auth/login ^
  -H "Content-Type: application/json" ^
  -d "{\"email\":\"testuser@example.com\",\"password\":\"TestPassword123!\"}" ^
  -o login_response.json

if exist login_response.json (
    echo Login: SUCCESS
    echo Check login_response.json for the JWT token
) else (
    echo Login: FAILED
)

REM Test 4: Instructions for manual testing
echo.
echo ========================================
echo MANUAL TESTING INSTRUCTIONS:
echo ========================================
echo.
echo 1. Copy the token from login_response.json
echo.
echo 2. Test Events (replace YOUR_TOKEN):
echo curl -X GET http://localhost:5002/api/events ^
echo   -H "Authorization: Bearer YOUR_TOKEN"
echo.
echo 3. Test Ticket Purchase (replace YOUR_TOKEN):
echo curl -X POST http://localhost:5003/api/tickets/purchase ^
echo   -H "Content-Type: application/json" ^
echo   -H "Authorization: Bearer YOUR_TOKEN" ^
echo   -d "{\"eventId\":1,\"quantity\":2,\"paymentRequest\":{\"cardNumber\":\"4111111111111111\",\"expiryMonth\":12,\"expiryYear\":2025,\"cvv\":\"123\",\"cardHolderName\":\"Test User\",\"amount\":100.00}}"
echo.
echo 4. Test RabbitMQ Stats (replace YOUR_TOKEN):
echo curl -X GET http://localhost:5003/api/tickets/queue-stats ^
echo   -H "Authorization: Bearer YOUR_TOKEN"
echo.
echo 5. Open RabbitMQ Management Interface:
echo http://localhost:15672
echo Username: ticketinguser
echo Password: ticketingpass123
echo.
echo ========================================

REM Cleanup
if exist register_response.json del register_response.json
if exist login_response.json (
    echo Token is saved in login_response.json
) else (
    echo No login response file created
)

pause
