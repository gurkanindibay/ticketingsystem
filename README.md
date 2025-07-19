# Ticketing System Microservices

A scalable ticketing system built with .NET 9, PostgreSQL, Redis, and RabbitMQ using microservices architecture.

## Architecture Overview

The system consists of three main microservices:

1. **Authentication Service** (Port 5001)
   - User registration and login
   - JWT token generation and validation
   - Refresh token management
   - Password hashing with BCrypt

2. **Events Service** (Port 5002)
   - Event creation and management
   - Event listing with location-based filtering
   - Redis caching for fast event retrieval
   - B-tree indexed queries on PostgreSQL

3. **Ticketing Service** (Port 5003)
   - Ticket purchasing with distributed locking
   - Transaction management
   - Concurrency control using RedLock.NET
   - Message queue integration with RabbitMQ

## Technology Stack

- **.NET 9** - Web API framework
- **PostgreSQL** - Primary database with B-tree indexing
- **Redis** - Caching and distributed locking
- **RabbitMQ** - Message queuing for event-driven architecture
- **Entity Framework Core** - ORM for PostgreSQL
- **JWT Bearer Authentication** - Stateless authentication
- **Docker & Docker Compose** - Containerization

## Key Features

### Authentication & Authorization System
- **ASP.NET Core Identity** - Complete user management system
- **JWT Bearer Tokens** - Stateless authentication with configurable expiration
- **Refresh Token System** - Secure token renewal with automatic rotation
- **Role-based Authorization** - Admin, Manager, and User roles
- **Password Security** - Built-in password validation and BCrypt hashing
- **Token Security** - HMACSHA256 signing with configurable secrets

### API Documentation
- **Swagger UI** integrated into all microservices
- Interactive API documentation with JWT Bearer authentication
- XML documentation comments for detailed endpoint descriptions
- Request/response examples and validation rules

### Security
- JWT token-based authentication
- BCrypt password hashing
- HMACSHA512 transaction ID generation
- Refresh token rotation

### Performance
- Redis caching for frequently accessed data
- PostgreSQL B-tree indexes for location-based queries
- Distributed locking for ticket purchases
- Event-driven messaging with RabbitMQ

### Scalability
- Microservices architecture
- Horizontal scaling support
- Database connection pooling
- Stateless service design

## Quick Start

### Prerequisites
- Docker and Docker Compose
- .NET 9 SDK (for development)

### Running with Docker Compose

1. Clone the repository
2. Navigate to the project root
3. Start all services:

```bash
docker-compose up -d
```

This will start:
- PostgreSQL on port 5432
- Redis on port 6379
- RabbitMQ on ports 5672 (AMQP) and 15672 (Management UI)
- Authentication Service on port 5001 with Swagger UI at http://localhost:5001/swagger
- Events Service on port 5002 with Swagger UI at http://localhost:5002/swagger
- Ticketing Service on port 5003 with Swagger UI at http://localhost:5003/swagger

**Note:** Docker services use HTTP by default to avoid certificate issues. For HTTPS in development, use the local development setup.

### Development Setup

#### Quick Start (In-Memory Database)
For immediate testing without setting up PostgreSQL:

1. Add the in-memory database package:
```bash
dotnet add src\TicketingSystem.Authentication\TicketingSystem.Authentication.csproj package Microsoft.EntityFrameworkCore.InMemory
```

2. The `appsettings.Development.json` is already configured to use in-memory database

3. Run the authentication service:
```bash
dotnet run --project src\TicketingSystem.Authentication
```

4. Access Swagger UI: http://localhost:5001/swagger

#### Full Setup (PostgreSQL + Redis + RabbitMQ)

1. Install .NET 9 SDK
2. Start infrastructure services:

```bash
# Start database services
docker-compose up -d postgres redis rabbitmq

# Wait for services to start
Start-Sleep 30

# Verify services are running
docker-compose ps
```

3. Update database configuration (set `"UseInMemoryDatabase": false` in `appsettings.Development.json`)

4. Restore NuGet packages:

```bash
dotnet restore
```

5. Build the solution:

```bash
dotnet build
```

6. Set up HTTPS development certificates (optional):

**Option A: Using PowerShell Script**
```powershell
# If execution policy allows
.\setup-https.ps1

# If you get execution policy error, use bypass
PowerShell -ExecutionPolicy Bypass -File .\setup-https.ps1
```

**Option B: Using Batch File**
```batch
# Windows batch file (no execution policy issues)
.\setup-https.bat
```

**Option C: Manual Commands**
```bash
# Clean existing certificates
dotnet dev-certs https --clean

# Create and trust new certificate
dotnet dev-certs https --trust

# Verify certificate
dotnet dev-certs https --check --trust
```

7. Run individual services:

```bash
# Authentication Service (HTTP - Recommended for development)
dotnet run --project src/TicketingSystem.Authentication

# Events Service (HTTP)
dotnet run --project src/TicketingSystem.Events

# Ticketing Service (HTTP)
dotnet run --project src/TicketingSystem.Ticketing
```

**For HTTPS (if certificate setup successful):**
```bash
# Use the HTTPS profile
dotnet run --project src/TicketingSystem.Authentication --launch-profile Development-HTTPS
```

## API Endpoints

### Authentication Service (Port 5001)

- **Swagger UI:** http://localhost:5001/swagger
- `POST /api/auth/register` - User registration
- `POST /api/auth/login` - User login
- `POST /api/auth/refresh` - Refresh access token
- `POST /api/auth/logout` - Logout user
- `GET /health` - Health check endpoint

### Events Service (Port 5002)

- **Swagger UI:** http://localhost:5002/swagger
- `GET /api/events` - List events with filtering
- `GET /api/events/{id}` - Get event details
- `POST /api/events` - Create new event (authenticated)
- `PUT /api/events/{id}` - Update event (authenticated)
- `DELETE /api/events/{id}` - Delete event (authenticated)
- `GET /health` - Health check endpoint

### Ticketing Service (Port 5003)

- **Swagger UI:** http://localhost:5003/swagger
- `POST /api/tickets/purchase` - Purchase tickets (authenticated)
- `GET /api/tickets/user` - Get user's tickets (authenticated)
- `GET /api/tickets/{transactionId}` - Get ticket details (authenticated)
- `DELETE /api/tickets/{transactionId}` - Cancel ticket (authenticated)
- `GET /health` - Health check endpoint

## Data Models

### Core Entities

- **User** - Authentication and user management
- **Event** - Event details with location indexing
- **EventTicket** - Purchased tickets
- **EventTicketTransaction** - Transaction history
- **RefreshToken** - JWT refresh token management

### Database Indexes

- B-tree index on `Event.Location` for location-based queries
- Composite indexes on `(Location, Date)` for optimized filtering
- Unique indexes on transaction IDs and tokens

## Authentication & Authorization Setup

### Overview
The Authentication microservice uses **ASP.NET Core Identity** combined with **JWT Bearer tokens** to provide a robust, scalable authentication system. The implementation includes:

- **User Registration & Login** with email/password
- **JWT Access Tokens** (15-minute expiration by default)
- **Refresh Tokens** (7-day expiration by default) 
- **Role-based Authorization** (Admin, Manager, User)
- **Automatic Role Seeding** on application startup
- **Token Security** with HMACSHA256 signing

### Features

#### 🔐 **ASP.NET Core Identity Integration**
- Built-in user management with robust password policies
- Email uniqueness validation
- Account lockout protection (5 failed attempts = 5-minute lockout)
- Extensible user model with custom properties

#### 🎟️ **JWT Token System**
- **Access Tokens**: Short-lived (15 minutes) for API access
- **Refresh Tokens**: Long-lived (7 days) for token renewal
- **Automatic Rotation**: New refresh token issued on each refresh
- **Secure Revocation**: Tokens can be revoked and tracked

#### 👤 **Role-Based Access Control**
- **Admin**: Full system access
- **Manager**: Event management capabilities
- **User**: Standard user operations
- Roles are automatically seeded on application startup

### Configuration

The authentication system is configured through `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=ticketingdb;Username=ticketinguser;Password=ticketingpass123;"
  },
  "JwtSettings": {
    "SecretKey": "YourSuperSecretKeyThatIsAtLeast32CharactersLong12345",
    "Issuer": "TicketingSystem",
    "Audience": "TicketingSystem.Users",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  }
}
```

**Important Security Notes:**
- ⚠️ **Change the `SecretKey`** in production to a cryptographically secure random string
- 🔒 **Use environment variables** for production secrets
- 📝 **Secret must be at least 32 characters** for HMACSHA256

### API Endpoints

#### Authentication Endpoints

| Method | Endpoint | Description | Auth Required |
|--------|----------|-------------|---------------|
| `POST` | `/api/auth/register` | Register new user | ❌ |
| `POST` | `/api/auth/login` | User login | ❌ |
| `POST` | `/api/auth/refresh` | Refresh access token | ❌ |
| `POST` | `/api/auth/logout` | Logout (revoke refresh token) | ❌ |
| `GET` | `/api/auth/profile` | Get current user profile | ✅ |

#### Registration Request
```json
{
  "username": "johndoe",
  "email": "john@example.com",
  "password": "SecurePass123!",
  "firstName": "John",
  "lastName": "Doe"
}
```

#### Login Request
```json
{
  "email": "john@example.com",
  "password": "SecurePass123!"
}
```

#### Authentication Response
```json
{
  "success": true,
  "message": "Login successful",
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "7a8b9c0d1e2f3g4h5i6j7k8l9m0n1o2p...",
    "expiresAt": "2025-07-19T16:30:00Z",
    "user": {
      "id": "usr_123456789",
      "username": "johndoe",
      "email": "john@example.com",
      "firstName": "John",
      "lastName": "Doe",
      "isActive": true,
      "createdAt": "2025-07-19T15:00:00Z"
    }
  }
}
```

### Using Authentication in API Calls

#### 1. Register/Login to get tokens
```bash
# Register a new user
curl -X POST "http://localhost:5001/api/auth/register" \
  -H "Content-Type: application/json" \
  -d '{
    "username": "johndoe",
    "email": "john@example.com", 
    "password": "SecurePass123!",
    "firstName": "John",
    "lastName": "Doe"
  }'

# Login to get tokens
curl -X POST "http://localhost:5001/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "john@example.com",
    "password": "SecurePass123!"
  }'
```

#### 2. Use access token for protected endpoints
```bash
# Get user profile (requires authentication)
curl -X GET "http://localhost:5001/api/auth/profile" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"

# Call other microservices (requires authentication)
curl -X GET "http://localhost:5002/api/events" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"

curl -X POST "http://localhost:5003/api/tickets/purchase" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "eventId": 1, "quantity": 2 }'
```

#### 3. Refresh tokens when expired
```bash
# Refresh access token
curl -X POST "http://localhost:5001/api/auth/refresh" \
  -H "Content-Type: application/json" \
  -d '{
    "accessToken": "EXPIRED_ACCESS_TOKEN",
    "refreshToken": "YOUR_REFRESH_TOKEN"
  }'
```

#### 4. Logout to revoke refresh token
```bash
# Logout (revokes refresh token)
curl -X POST "http://localhost:5001/api/auth/logout" \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "YOUR_REFRESH_TOKEN"
  }'
```

### Swagger UI Integration

The authentication system is fully integrated with Swagger UI:

1. **Access Swagger**: http://localhost:5001/swagger
2. **Authorize**: Click the "Authorize" button (🔒)
3. **Login Flow**:
   - First register a user via `/api/auth/register`
   - Then login via `/api/auth/login` 
   - Copy the `accessToken` from the response
4. **Set Bearer Token**: Enter `Bearer YOUR_ACCESS_TOKEN` in the authorization dialog
5. **Test Protected Endpoints**: You can now call `/api/auth/profile` and other protected endpoints

### Implementation Details

#### Password Policy
```csharp
options.Password.RequireDigit = true;                    // Must contain numbers
options.Password.RequireLowercase = true;               // Must contain lowercase
options.Password.RequireUppercase = true;               // Must contain uppercase  
options.Password.RequireNonAlphanumeric = false;        // Special chars optional
options.Password.RequiredLength = 8;                    // Minimum 8 characters
options.Password.RequiredUniqueChars = 1;               // At least 1 unique char
```

#### JWT Claims Structure
```json
{
  "nameid": "usr_123456789",           // User ID
  "unique_name": "johndoe",            // Username
  "email": "john@example.com",         // Email
  "firstName": "John",                 // First name
  "lastName": "Doe",                   // Last name
  "isActive": "true",                  // Account status
  "role": ["User"],                    // User roles
  "iss": "TicketingSystem",           // Issuer
  "aud": "TicketingSystem.Users",     // Audience
  "exp": 1737302400,                   // Expiration timestamp
  "iat": 1737301500                    // Issued at timestamp
}
```

#### Database Schema
```sql
-- ASP.NET Core Identity tables are automatically created:
-- AspNetUsers (extends with FirstName, LastName, IsActive, CreatedAt, UpdatedAt)
-- AspNetRoles (Admin, Manager, User)
-- AspNetUserRoles (user-role relationships)

-- Custom refresh token table:
-- RefreshTokens (Token, UserId, ExpiresAt, IsRevoked, etc.)

-- Updated related tables to use string UserId (Identity compatibility):
-- EventTickets (UserId: string, EventId: int, TransactionId: string)
-- EventTicketTransactions (UserId: string, EventId: int, TransactionId: string)
```

**Important Note:** All user-related foreign keys have been updated from `int` to `string` to maintain compatibility with ASP.NET Core Identity's string-based user IDs.

### Development vs Production

#### Development Setup
- HTTP endpoints enabled for easier testing
- Relaxed HTTPS requirements (`RequireHttpsMetadata = false`)
- Database automatically created and seeded
- Default roles seeded on startup

#### Production Recommendations
- ✅ Use HTTPS only (`RequireHttpsMetadata = true`)
- ✅ Store JWT secret in environment variables or Azure Key Vault
- ✅ Use production-grade PostgreSQL instance
- ✅ Enable logging and monitoring
- ✅ Implement rate limiting on authentication endpoints
- ✅ Use Redis for refresh token storage (optional)

### Troubleshooting

#### Common Issues

1. **"Unauthorized" on protected endpoints**
   - Ensure you're passing the Bearer token correctly: `Authorization: Bearer YOUR_TOKEN`
   - Check if the token has expired (15-minute default)
   - Verify the token was obtained from login/refresh

2. **"Invalid token" errors**
   - Make sure the JWT secret matches between token creation and validation
   - Check if the token format is correct (should be three base64 parts separated by dots)

3. **Password validation errors**
   - Ensure password meets policy requirements (8+ chars, upper, lower, digit)
   - Check for common passwords or dictionary words

4. **Database connection issues**
   - Verify PostgreSQL is running and connection string is correct
   - Check if database and user exist with proper permissions

5. **Database connection issues**
   - **Quick Fix**: Use in-memory database for testing by setting `"UseInMemoryDatabase": true` in `appsettings.Development.json`
   - **Production Setup**: Start PostgreSQL with `docker-compose up -d postgres redis rabbitmq`
   - Verify PostgreSQL is running and connection string is correct
   - Check if database and user exist with proper permissions

6. **Foreign key type mismatch errors**
   - This has been resolved: All `UserId` fields are now `string` type to match ASP.NET Core Identity
   - If you encounter similar issues, ensure all related models use `string UserId`

#### Debugging Tips

```bash
# Check if authentication service is running
curl http://localhost:5001/health

# Test registration without Swagger
curl -X POST "http://localhost:5001/api/auth/register" \
  -H "Content-Type: application/json" \
  -d '{"username":"test","email":"test@test.com","password":"Test123!","firstName":"Test","lastName":"User"}'

# Test login  
curl -X POST "http://localhost:5001/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"test@test.com","password":"Test123!"}'

# Verify JWT token structure (decode at jwt.io)
echo "YOUR_JWT_TOKEN" | cut -d'.' -f2 | base64 -d
```

## Configuration

### Environment Variables

Each service can be configured using environment variables:

```bash
# Database
ConnectionStrings__DefaultConnection=Host=localhost;Database=ticketingdb;Username=user;Password=pass;

# JWT Settings
JwtSettings__SecretKey=YourSecretKey
JwtSettings__Issuer=TicketingSystem
JwtSettings__Audience=TicketingSystem.Users

# Redis
RedisSettings__ConnectionString=localhost:6379

# RabbitMQ (Ticketing Service only)
RabbitMQSettings__HostName=localhost
RabbitMQSettings__Username=guest
RabbitMQSettings__Password=guest
```

## Project Structure

```
TicketingSystem/
├── src/
│   ├── TicketingSystem.Authentication/    # Authentication microservice
│   ├── TicketingSystem.Events/            # Events microservice
│   ├── TicketingSystem.Ticketing/         # Ticketing microservice
│   └── TicketingSystem.Shared/            # Shared models, DTOs, and utilities
├── docker-compose.yml                     # Docker Compose configuration
├── TicketingSystem.sln                   # Solution file
└── README.md                             # This file
```

## Monitoring and Health Checks

- **Swagger UI:** Available on each service at `/swagger`
  - Authentication: http://localhost:5001/swagger
  - Events: http://localhost:5002/swagger  
  - Ticketing: http://localhost:5003/swagger
- **Health Check Endpoints:** Available on each service at `/health`
- **RabbitMQ Management UI:** http://localhost:15672 (guest/guest)

## Troubleshooting

### PowerShell Execution Policy Issues

If you encounter PowerShell execution policy errors:

**Quick Fix:**
```powershell
# Use execution policy bypass for single script
PowerShell -ExecutionPolicy Bypass -File .\setup-https.ps1
```

**Alternative Solutions:**
```batch
# Use the batch file instead
.\setup-https.bat

# Or run commands manually
dotnet dev-certs https --clean
dotnet dev-certs https --trust
```

**Permanent Fix (Optional):**
```powershell
# Set execution policy for current user (requires admin)
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### HTTPS Certificate Issues

If you're experiencing HTTPS errors, follow these steps:

**Option 1: Use HTTP (Recommended for Development)**
```bash
# Simply run the services - they default to HTTP in development
dotnet run --project src/TicketingSystem.Authentication
# Access via: http://localhost:5001/swagger
```

**Option 2: Fix HTTPS Certificates**
```powershell
# Run the setup script
.\setup-https.ps1

# Or manually:
dotnet dev-certs https --clean
dotnet dev-certs https --trust

# Then use HTTPS profile
dotnet run --project src/TicketingSystem.Authentication --launch-profile Development-HTTPS
# Access via: https://localhost:7001/swagger
```

**Option 3: Check Certificate Status**
```bash
# Check if development certificate exists and is trusted
dotnet dev-certs https --check --trust
```

### Common Solutions
- **ERR_CERT_AUTHORITY_INVALID**: Run `dotnet dev-certs https --trust`
- **Connection refused**: Make sure you're using the correct port and protocol
- **Certificate errors in browser**: Clear browser cache and restart browser
- **Still having issues**: Use HTTP endpoints instead - they work perfectly for development

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Submit a pull request

## License

This project is licensed under the MIT License.
