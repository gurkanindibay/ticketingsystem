# Ticketing Service Requirements

This document outlines the requirements for a Ticketmaster-like ticketing service, designed for 1M daily users and 10,000 concurrent ticket purchases. It uses .NET Core, Redis, PostgreSQL, RabbitMQ, and Kubernetes, with a focus on scalability, security, and low-latency searches. These requirements support interview preparation for Software Architect and Engineering Manager roles.

## Functional Requirements
- **Operations:**
  - **List event:** Users can list events by location (city), event name (partial match), event type, and date, with results sorted by date.
  - **Buy ticket:** Users purchase tickets for events, requiring authentication.
  - **Cancel ticket:** Users cancel purchased tickets, requiring authentication.
  - **List bought tickets:** Users view their purchased tickets, requiring authentication.
- **Scope Exclusions:** Admin operations (e.g., event creation) and reservation systems (e.g., seat locking) are excluded for simplicity.

## Nonfunctional Requirements
- **Scalability and Concurrency:** Handle 1M daily users, with 10,000 concurrent ticket purchases (90% views, 10% sales: 9000 view operations, 1000 ticket sales).
- **Microservices Architecture:** Organize services by functional areas; ticketing, authentication for modularity.
- **Search Latency:** All event listing searches (by location, name, type, date) return in <500ms, even during peak load.
- **High Availability:** Maintain 99.9% uptime for uninterrupted ticket purchases.
- **Secure Transactions:** Use HTTPS for encrypted data transfer, HMACSHA512 for transaction ID generation, and require user authentication.
- **Auto-Scaling:** Trigger scaling based on request volume or CPU usage to handle peak loads (e.g., ticket sale launches).
- **Concurrency Control:** Prevent race conditions during ticket purchases to avoid overselling, using Redlock on Redis for capacity updates.
- **Backups:** Daily backups of ticket and order data in PostgreSQL for disaster recovery.

## System Components
- **Front-End:** Single-page application (SPA) using Angular or React, consuming REST APIs from .NET Core backend.
- **Backend:** .NET Core microservices for event listing, ticket purchases, cancellations, and user ticket queries.
- **Cache:** Redis for caching `event`, `event_tickets`, and `event_ticket_transactions` data, stored as JSON objects (e.g., `event:<id>` with fields for name, location, date, type) for multi-dimensional queries.
- **Database:** Single PostgreSQL server, with high Redis cache hit rates (>90%) to minimize load.
- **Message Queue:** RabbitMQ for asynchronous capacity updates.
- **Deployment:** Kubernetes with Minikube, horizontally scaling all components except PostgreSQL.

## Data Models
- **event (PostgreSQL and Redis):**
  - Fields: `name`, `date`, `duration`, `start_time`, `end_time`, `capacity`, `location` (city only, with B-tree index in PostgreSQL).
  - Expiration: Records expire in Redis when `date` is in the past (using TTL).
- **event_tickets (PostgreSQL and Redis):**
  - Fields: `user_id`, `event_id`, `event_date`.
  - Expiration: Records expire in Redis when `event_date` is in the past.
- **event_ticket_transactions (PostgreSQL and Redis):**
  - Fields: `event_id`, `user_id`, `transaction_id`, `event_date`, `status` (e.g., pending, completed, failed).
  - Expiration: Records expire in Redis when `event_date` is in the past.

## Authentication Mechanism
- **Identity Server Microservice:** Manages user authentication (login with credentials) and authorization (user roles for ticket operations).
- **JWT Tokens:** Used for stateless authentication, validated by .NET Core microservices using a shared secret for internal communication and a public key for SPA clients.
- **Token Expiration:** JWT tokens expire in 1 hour, with refresh tokens issued by the identity server (stored in PostgreSQL, 7-day lifespan, revocable on logout/security events) to prevent frequent re-logins.

## Concurrency and Data Consistency
- **Redlock:** Used on Redis to lock event capacity updates during ticket purchases, preventing overselling.
- **Synchronous Updates:** Delete, insert, and update operations for `List event`, `Buy ticket`, `Cancel ticket`, and `List bought tickets` are stored synchronously in Redis and PostgreSQL.
- **Asynchronous Capacity Updates:**
  - When updating `capacity` in Redis, add a record to `event_ticket_transactions`.
  - Publish update to RabbitMQ queue (dedicated for ticket transactions).
  - Consumer transactionally decreases `capacity` in PostgreSQL and deletes the `event_ticket_transactions` record.
- **Redis Persistence:** Uses RDB snapshots every 60 seconds for performance and durability.
- **Monitoring:** Monitor RabbitMQ queue size with Prometheus using the `rabbitmq_prometheus` plugin for metrics collection.

## Notes for GitHub Copilot
- Use these requirements to generate .NET Core controllers, EF Core models, Redis JSON queries, RabbitMQ producers/consumers, and Kubernetes YAMLs.
- Always attempt manual coding first, then use Copilot for boilerplate (e.g., “Create .NET Core controller for ticket purchase”) or optimization (e.g., “Optimize C# hash table for HMACSHA512”).
- Prompt Copilot to include HMACSHA512 for transaction IDs and Redlock for concurrency.
- Ensure synchronous Redis/PostgreSQL updates for non-capacity operations and asynchronous RabbitMQ for capacity updates.