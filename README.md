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

1. Install .NET 9 SDK
2. Restore NuGet packages:

```bash
dotnet restore
```

3. Build the solution:

```bash
dotnet build
```

4. Set up HTTPS development certificates (optional):

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

5. Run individual services:

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
