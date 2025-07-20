# Ticketing System Manual Testing Guide

## 1. Prerequisites
Ensure Docker is running and infrastructure services are started:
```bash
docker-compose up -d postgres redis rabbitmq
```

## 2. Service URLs
- Authentication: http://localhost:5001
- Events: http://localhost:5002  
- Ticketing: http://localhost:5003
- RabbitMQ Management: http://localhost:15672 (user: ticketinguser, pass: ticketingpass123)

## 3. Manual HTTP Tests

### Test 1: Health Check
```bash
curl http://localhost:5001/health
curl http://localhost:5002/health  
curl http://localhost:5003/health
```

### Test 2: Register User
```bash
curl -X POST http://localhost:5001/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "testuser@example.com",
    "password": "TestPassword123!",
    "confirmPassword": "TestPassword123!"
  }'
```

### Test 3: Login User
```bash
curl -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "testuser@example.com", 
    "password": "TestPassword123!"
  }'
```

### Test 4: Get Events (save the token from login)
```bash
curl -X GET http://localhost:5002/api/events \
  -H "Authorization: Bearer YOUR_TOKEN_HERE"
```

### Test 5: Purchase Tickets
```bash
curl -X POST http://localhost:5003/api/tickets/purchase \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN_HERE" \
  -d '{
    "eventId": 1,
    "quantity": 2,
    "paymentRequest": {
      "cardNumber": "4111111111111111",
      "expiryMonth": 12,
      "expiryYear": 2025,
      "cvv": "123", 
      "cardHolderName": "Test User",
      "amount": 100.00
    }
  }'
```

### Test 6: Check RabbitMQ Queue Stats
```bash
curl -X GET http://localhost:5003/api/tickets/queue-stats \
  -H "Authorization: Bearer YOUR_TOKEN_HERE"
```

## 4. RabbitMQ Management Interface
1. Open http://localhost:15672
2. Login with: ticketinguser / ticketingpass123
3. Check Queues tab to see:
   - ticket.capacity.updates
   - ticket.transactions  
   - ticket.dead.letter.queue

## 5. Manual Test Runner
Run the comprehensive test suite:
```bash
cd src/TicketingSystem.ManualTests
dotnet run
```

## 6. Test Scenarios

### Successful Purchase Flow
1. Register user
2. Login to get token
3. List events
4. Purchase tickets with valid card
5. Verify RabbitMQ messages
6. Check database updates

### Error Scenarios
1. Invalid payment card (fails Luhn check)
2. Insufficient event capacity
3. Invalid authentication token
4. RabbitMQ connection failure
5. Database connection failure

### Load Testing
1. Multiple concurrent purchases
2. High-frequency API calls
3. RabbitMQ message throughput
4. Database connection pooling

## 7. Monitoring Points
- Service logs for errors
- RabbitMQ queue lengths
- Database connection status
- Redis cache hit rates
- Payment validation results
