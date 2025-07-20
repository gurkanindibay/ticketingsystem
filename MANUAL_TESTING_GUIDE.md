# Manual Testing Steps for Ticketing System

## Architecture Overview
The system has been consolidated from 3 services to 2 services:
- **Authentication Service** (Port 5001): User registration, login, JWT tokens
- **Ticketing & Events Service** (Port 5002): Event management, ticket operations, payment processing

This consolidation improves performance and reduces complexity while maintaining all functionality.

## Prerequisites
1. Docker is running with infrastructure services:
   ```
   docker-compose up -d postgres redis rabbitmq
   ```

2. Services are running on:
   - Authentication: http://localhost:5001
   - Ticketing & Events: http://localhost:5002

## Step-by-Step Manual Testing

### Step 1: Test Service Health
Open PowerShell/Command Prompt and run:

```bash
# Test Authentication Service
curl http://localhost:5001/health

# Test Ticketing & Events Service  
curl http://localhost:5002/health
```

**Expected Result**: Each should return a 200 OK response

### Step 2: Register a New User
```bash
curl -X POST http://localhost:5001/api/auth/register -H "Content-Type: application/json" -d "{\"userName\":\"testuser@example.com\", \"email\":\"testuser@example.com\",\"password\":\"TestPassword1453!\",\"confirmPassword\":\"TestPassword1453!\",\"firstName\":\"testuser\",\"lastName\":\"testuser\"}"
```

**Expected Result**: 200 OK with user registration confirmation

### Step 3: Login to Get JWT Token
```bash
curl -X POST http://localhost:5001/api/auth/login -H "Content-Type: application/json" -d "{\"email\":\"testuser@example.com\",\"password\":\"TestPassword1453!\"}"
```

**Expected Result**: 200 OK with JSON response containing `token` field
**Important**: Copy the token value for next steps!

### Step 4: Get Events List
Replace `YOUR_TOKEN_HERE` with the actual token from Step 3:

```bash
curl -X GET http://localhost:5002/api/events -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJuYW1laWQiOiIyMzdjZDA3Ni00ZDY4LTRkY2ItYTQ0YS03Y2Y1ZTEwM2MzYTQiLCJ1bmlxdWVfbmFtZSI6InRlc3R1c2VyQGV4YW1wbGUuY29tIiwiZW1haWwiOiJ0ZXN0dXNlckBleGFtcGxlLmNvbSIsImZpcnN0TmFtZSI6InRlc3R1c2VyIiwibGFzdE5hbWUiOiJ0ZXN0dXNlciIsImlzQWN0aXZlIjoiVHJ1ZSIsInJvbGUiOiJVc2VyIiwibmJmIjoxNzUzMDIyOTc5LCJleHAiOjE3NTMwMjM4NzksImlhdCI6MTc1MzAyMjk3OSwiaXNzIjoiVGlja2V0aW5nU3lzdGVtIiwiYXVkIjoiVGlja2V0aW5nU3lzdGVtLlVzZXJzIn0.4Hg6eRsWSBoqEAsTrz7lyiWYXtYExhhsWLrfpruQpJw"
```

**Expected Result**: JSON array of events

### Step 5: Test Event Management (New in Consolidated Service)
The Ticketing service now includes Events functionality. Test creating and managing events:

```bash
# Create a new event (Admin operation)
curl -X POST http://localhost:5002/api/events -H "Content-Type: application/json" -H "Authorization: Bearer YOUR_TOKEN_HERE" -d "{\"name\":\"Test Concert\",\"date\":\"2025-08-15T20:00:00\",\"duration\":\"02:30:00\",\"startTime\":\"20:00:00\",\"capacity\":1000,\"location\":\"New York\",\"eventType\":\"Concert\"}"

# Search events by location
curl -X GET "http://localhost:5002/api/events/search?location=New York" -H "Authorization: Bearer YOUR_TOKEN_HERE"

# Get specific event by ID
curl -X GET http://localhost:5002/api/events/1 -H "Authorization: Bearer YOUR_TOKEN_HERE"
```

**Expected Result**: Event creation, search, and retrieval work correctly

### Step 6: Purchase Tickets
Replace `YOUR_TOKEN_HERE` with your actual token:

```bash
curl -X POST http://localhost:5002/api/tickets/purchase -H "Content-Type: application/json" -H "Authorization: Bearer YOUR_TOKEN_HERE" -d "{\"eventId\":1,\"quantity\":2,\"paymentRequest\":{\"cardNumber\":\"4111111111111111\",\"expiryMonth\":12,\"expiryYear\":2025,\"cvv\":\"123\",\"cardHolderName\":\"Test User\",\"amount\":100.00}}"
```

**Expected Result**: 200 OK with purchase confirmation including transaction ID
**Key Point**: This tests the complete flow including payment validation and RabbitMQ messaging!

### Step 7: Check RabbitMQ Queue Statistics
```bash
curl -X GET http://localhost:5002/api/tickets/queue-stats -H "Authorization: Bearer YOUR_TOKEN_HERE"
```

**Expected Result**: JSON with queue statistics showing message counts

### Step 8: Monitor RabbitMQ Management Interface
1. Open browser to: http://localhost:15672
2. Login with:
   - Username: `ticketinguser`
   - Password: `ticketingpass123`
3. Go to "Queues" tab
4. You should see queues:
   - `ticket.capacity.updates`
   - `ticket.transactions`
   - `ticket.dead.letter.queue`

**Expected Result**: Queues exist and may have messages from the ticket purchase

## Test Payment Validation

### Valid Cards (Should Succeed)
```bash
# Visa
curl -X POST http://localhost:5002/api/tickets/purchase -H "Content-Type: application/json" -H "Authorization: Bearer YOUR_TOKEN_HERE" -d "{\"eventId\":1,\"quantity\":1,\"paymentRequest\":{\"cardNumber\":\"4111111111111111\",\"expiryMonth\":12,\"expiryYear\":2025,\"cvv\":\"123\",\"cardHolderName\":\"Test User\",\"amount\":50.00}}"

# MasterCard  
curl -X POST http://localhost:5002/api/tickets/purchase -H "Content-Type: application/json" -H "Authorization: Bearer YOUR_TOKEN_HERE" -d "{\"eventId\":1,\"quantity\":1,\"paymentRequest\":{\"cardNumber\":\"5555555555554444\",\"expiryMonth\":12,\"expiryYear\":2025,\"cvv\":\"123\",\"cardHolderName\":\"Test User\",\"amount\":50.00}}"
```

### Invalid Cards (Should Fail)
```bash
# Invalid Luhn check
curl -X POST http://localhost:5002/api/tickets/purchase -H "Content-Type: application/json" -H "Authorization: Bearer YOUR_TOKEN_HERE" -d "{\"eventId\":1,\"quantity\":1,\"paymentRequest\":{\"cardNumber\":\"1234567890123456\",\"expiryMonth\":12,\"expiryYear\":2025,\"cvv\":\"123\",\"cardHolderName\":\"Test User\",\"amount\":50.00}}"

# Declined card
curl -X POST http://localhost:5002/api/tickets/purchase -H "Content-Type: application/json" -H "Authorization: Bearer YOUR_TOKEN_HERE" -d "{\"eventId\":1,\"quantity\":1,\"paymentRequest\":{\"cardNumber\":\"4000000000000002\",\"expiryMonth\":12,\"expiryYear\":2025,\"cvv\":\"123\",\"cardHolderName\":\"Test User\",\"amount\":50.00}}"
```

## What to Verify

### 1. Database Operations
- User registration creates record in database
- Event listings come from database
- Ticket purchases create records in event_tickets table
- Transaction records created in event_ticket_transactions table

### 2. Redis Caching
- Event data is cached in Redis
- Capacity updates are reflected in cache
- Cache hit rates are high

### 3. RabbitMQ Messaging
- Capacity update messages published after purchases
- Transaction audit messages published
- Dead letter queue handles failed messages
- Queue statistics show message flow

### 4. Payment Processing
- Luhn algorithm validates card numbers
- Different card types handled correctly
- Declined transactions properly rejected
- Transaction IDs generated with HMACSHA512

### 5. Concurrency Control
- RedLock prevents overselling
- Multiple simultaneous purchases handled correctly
- Database consistency maintained

### 6. Consolidated Service Benefits
- Single service handles both events and tickets
- Shared Redis caching for related data
- Unified logging and error handling
- Better performance due to no inter-service calls

## Automated Testing Scripts

For easier testing, use the provided PowerShell scripts:

```powershell
# Comprehensive system test
.\test-system.ps1

# Simple validation test
.\simple-test.ps1

# Manual interactive test runner
.\test-system.bat
```

These scripts automatically test all the manual steps above and provide detailed output.

## Troubleshooting

### Services Not Responding
1. Check if services are running: `netstat -an | findstr ":500"`
2. Check service logs for errors
3. Verify database connection strings
4. Ensure only 2 services are running (Authentication on 5001, Ticketing+Events on 5002)

### Redis Configuration Error ("configuration is empty")
If you see `System.ArgumentException: is empty (Parameter 'configuration')`:

**Root Cause:** Service started before DI configuration was properly set up.

**Solution:**
1. **Stop the Ticketing service** (Ctrl+C in the terminal)
2. **Ensure Redis is running**: `docker-compose up -d redis`
3. **Restart the service**: `dotnet run --project src\TicketingSystem.Ticketing`
4. **Test connectivity**: `curl http://localhost:5002/health`

**Quick diagnostic:** Run `.\quick-redis-check.ps1` to check Redis and service status.

### Authentication Issues
1. Verify user was created in database
2. Check JWT token format
3. Ensure token is not expired (tokens expire in 15 minutes)
4. **Get a fresh token if expired:**
   ```bash
   curl -X POST http://localhost:5001/api/auth/login -H "Content-Type: application/json" -d "{\"email\":\"testuser@example.com\",\"password\":\"TestPassword1453!\"}"
   ```

### Payment Failures
1. Verify card number passes Luhn check
2. Check payment service logs
3. Ensure amount is positive

### RabbitMQ Issues
1. Check RabbitMQ is running: `docker ps`
2. Verify connection settings
3. Check queue declarations in RabbitMQ management

### Database Issues
1. Check PostgreSQL is running
2. Verify connection string
3. Check if tables exist
4. Review EF Core migrations
