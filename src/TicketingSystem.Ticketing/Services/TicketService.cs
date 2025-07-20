using TicketingSystem.Shared.DTOs;
using TicketingSystem.Shared.Models;
using TicketingSystem.Shared.Utilities;
using TicketingSystem.Ticketing.Data;
using Microsoft.EntityFrameworkCore;
using RedLockNet;

namespace TicketingSystem.Ticketing.Services
{
    /// <summary>
    /// Ticket service implementation with Redis caching, PostgreSQL database, and RabbitMQ messaging
    /// Implements the business logic for ticket operations according to system requirements
    /// </summary>
    public class TicketService : ITicketService
    {
        private readonly IPaymentService _paymentService;
        private readonly IRedisService _redisService;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly TicketingDbContext _dbContext;
        private readonly IDistributedLockFactory _redLockFactory;
        private readonly ILogger<TicketService> _logger;

        public TicketService(
            IPaymentService paymentService,
            IRedisService redisService,
            IRabbitMQService rabbitMQService,
            TicketingDbContext dbContext,
            IDistributedLockFactory redLockFactory,
            ILogger<TicketService> logger)
        {
            _paymentService = paymentService;
            _redisService = redisService;
            _rabbitMQService = rabbitMQService;
            _dbContext = dbContext;
            _redLockFactory = redLockFactory;
            _logger = logger;
        }

        /// <summary>
        /// Purchase tickets for an event with payment processing, RedLock concurrency control, and Redis/PostgreSQL updates
        /// </summary>
        public async Task<PurchaseTicketResponse> PurchaseTicketsAsync(PurchaseTicketRequest request, string userId)
        {
            var lockKey = SecurityHelper.GenerateTicketLockKey(request.EventId, request.EventDate);
            
            using (var redLock = await _redLockFactory.CreateLockAsync(lockKey, TimeSpan.FromSeconds(30)))
            {
                if (!redLock.IsAcquired)
                {
                    _logger.LogWarning("Failed to acquire Redis lock for event {EventId}", request.EventId);
                    return new PurchaseTicketResponse
                    {
                        Status = "Failed",
                        TransactionId = string.Empty
                    };
                }

                try
                {
                    _logger.LogInformation("Starting ticket purchase for user {UserId}, event {EventId}, quantity {Quantity}",
                        userId, request.EventId, request.Quantity);

                    // Step 1: Check event availability (Redis cache first, then database)
                    var (isAvailable, pricePerTicket, availableTickets) = await CheckEventAvailabilityAsync(request.EventId, request.Quantity);
                    
                    if (!isAvailable)
                    {
                        _logger.LogWarning("Event {EventId} not available for {Quantity} tickets. Available: {Available}",
                            request.EventId, request.Quantity, availableTickets);
                        
                        return new PurchaseTicketResponse
                        {
                            Status = "Failed",
                            TransactionId = string.Empty
                        };
                    }

                    // Step 2: Calculate total amount and validate payment request
                    var totalAmount = pricePerTicket * request.Quantity;
                    request.PaymentDetails.Amount = totalAmount;
                    request.PaymentDetails.Description = $"Tickets for Event {request.EventId} - Quantity: {request.Quantity}";

                    // Step 3: Generate transaction ID using HMACSHA512 as per requirements
                    var transactionId = SecurityHelper.GenerateTransactionId(
                        userId.GetHashCode(), 
                        request.EventId, 
                        DateTime.UtcNow);

                    // Step 4: Create pending transaction (synchronous Redis/PostgreSQL update)
                    var transaction = new EventTicketTransaction
                    {
                        EventId = request.EventId,
                        UserId = userId,
                        TransactionId = transactionId,
                        EventDate = request.EventDate,
                        Status = TransactionStatus.Pending,
                        Amount = totalAmount,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    // Store in database
                    _dbContext.EventTicketTransactions.Add(transaction);
                    await _dbContext.SaveChangesAsync();

                    // Store in Redis cache
                    await _redisService.SetTransactionAsync(transaction);

                    // Step 5: Process payment
                    var paymentResponse = await _paymentService.ProcessPaymentAsync(request.PaymentDetails);

                    if (!paymentResponse.IsSuccess)
                    {
                        _logger.LogWarning("Payment failed for transaction {TransactionId}: {ErrorMessage}",
                            transactionId, paymentResponse.ErrorMessage);

                        // Update transaction status to failed (synchronous update)
                        transaction.Status = TransactionStatus.Failed;
                        transaction.UpdatedAt = DateTime.UtcNow;
                        await _dbContext.SaveChangesAsync();
                        await _redisService.SetTransactionAsync(transaction);

                        return new PurchaseTicketResponse
                        {
                            TransactionId = transactionId,
                            Status = "Payment Failed",
                            TotalAmount = totalAmount,
                            PaymentDetails = paymentResponse
                        };
                    }

                    // Step 6: Create tickets and update transaction (synchronous Redis/PostgreSQL update)
                    var tickets = await CreateTicketsAsync(request, userId, transactionId, pricePerTicket);
                    
                    // Update transaction status to completed (synchronous update)
                    transaction.Status = TransactionStatus.Completed;
                    transaction.UpdatedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                    await _redisService.SetTransactionAsync(transaction);

                    // Step 7: Update event capacity synchronously in Redis, asynchronously in PostgreSQL via RabbitMQ
                    await _redisService.DecrementEventCapacityAsync(request.EventId, request.Quantity);
                    
                    // Publish capacity update message to RabbitMQ for PostgreSQL update
                    await _rabbitMQService.PublishCapacityUpdateAsync(new CapacityUpdateMessage
                    {
                        EventId = request.EventId,
                        CapacityChange = -request.Quantity,
                        TransactionId = transactionId,
                        Operation = "purchase"
                    });

                    _logger.LogInformation("Ticket purchase completed successfully for transaction {TransactionId}",
                        transactionId);

                    return new PurchaseTicketResponse
                    {
                        TransactionId = transactionId,
                        Tickets = tickets,
                        TotalAmount = totalAmount,
                        Status = "Completed",
                        PaymentDetails = paymentResponse
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error purchasing tickets for user {UserId}, event {EventId}",
                        userId, request.EventId);
                    
                    return new PurchaseTicketResponse
                    {
                        Status = "Error",
                        TransactionId = string.Empty
                    };
                }
            }
        }

        /// <summary>
        /// Cancel a ticket purchase and process refund with database and Redis updates
        /// </summary>
        public async Task<bool> CancelTicketAsync(string transactionId, string userId)
        {
            try
            {
                _logger.LogInformation("Cancelling ticket for transaction {TransactionId}, user {UserId}",
                    transactionId, userId);

                // Find transaction in database first, then check Redis cache
                var transaction = await _dbContext.EventTicketTransactions
                    .FirstOrDefaultAsync(t => t.TransactionId == transactionId && t.UserId == userId);

                if (transaction == null)
                {
                    // Try Redis cache
                    transaction = await _redisService.GetTransactionAsync(transactionId);
                    if (transaction == null || transaction.UserId != userId)
                    {
                        _logger.LogWarning("Transaction {TransactionId} not found for user {UserId}",
                            transactionId, userId);
                        return false;
                    }
                }

                // Check if cancellation is allowed (e.g., event not too close)
                if (transaction.EventDate <= DateTime.UtcNow.AddHours(24))
                {
                    _logger.LogWarning("Cannot cancel ticket for transaction {TransactionId} - event too close",
                        transactionId);
                    return false;
                }

                // Process refund
                var refundRequest = new RefundRequest
                {
                    PaymentId = transactionId, // In a real scenario, we'd have the payment ID stored
                    Amount = transaction.Amount,
                    Reason = "Ticket cancellation"
                };

                var refundResponse = await _paymentService.ProcessRefundAsync(refundRequest);

                if (refundResponse.IsSuccess)
                {
                    // Update transaction status (synchronous update)
                    transaction.Status = TransactionStatus.Cancelled;
                    transaction.UpdatedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                    await _redisService.SetTransactionAsync(transaction);

                    // Remove tickets from database and Redis
                    var tickets = await _dbContext.EventTickets
                        .Where(t => t.TransactionId == transactionId)
                        .ToListAsync();

                    var ticketCount = tickets.Count;
                    _dbContext.EventTickets.RemoveRange(tickets);
                    await _dbContext.SaveChangesAsync();

                    // Update user tickets cache in Redis
                    await InvalidateUserTicketsCache(userId);

                    // Update event capacity (increase available tickets)
                    await _redisService.IncrementEventCapacityAsync(transaction.EventId, ticketCount);
                    
                    // Publish capacity update message to RabbitMQ
                    await _rabbitMQService.PublishCapacityUpdateAsync(new CapacityUpdateMessage
                    {
                        EventId = transaction.EventId,
                        CapacityChange = ticketCount,
                        TransactionId = transactionId,
                        Operation = "cancel"
                    });

                    _logger.LogInformation("Ticket cancelled successfully for transaction {TransactionId}",
                        transactionId);
                    return true;
                }

                _logger.LogWarning("Refund failed for transaction {TransactionId}: {ErrorMessage}",
                    transactionId, refundResponse.ErrorMessage);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling ticket for transaction {TransactionId}",
                    transactionId);
                return false;
            }
        }

        /// <summary>
        /// Get user's purchased tickets with pagination (Redis cache first, then database)
        /// </summary>
        public async Task<UserTicketsResponse> GetUserTicketsAsync(UserTicketsRequest request, string userId)
        {
            try
            {
                // Try Redis cache first
                var cachedTickets = await _redisService.GetEventTicketsAsync(userId);
                
                List<EventTicket> userTickets;
                if (cachedTickets != null)
                {
                    userTickets = cachedTickets;
                    _logger.LogDebug("User tickets retrieved from Redis cache for user {UserId}", userId);
                }
                else
                {
                    // Fallback to database
                    userTickets = await _dbContext.EventTickets
                        .Where(t => t.UserId == userId)
                        .Include(t => t.Event)
                        .OrderByDescending(t => t.PurchasedAt)
                        .ToListAsync();

                    // Cache the results in Redis
                    await _redisService.SetEventTicketsAsync(userId, userTickets);
                    _logger.LogDebug("User tickets retrieved from database and cached for user {UserId}", userId);
                }

                // Apply date filtering
                var filteredTickets = userTickets.AsQueryable();
                
                if (request.FromDate.HasValue)
                {
                    filteredTickets = filteredTickets.Where(t => t.EventDate >= request.FromDate.Value);
                }
                
                if (request.ToDate.HasValue)
                {
                    filteredTickets = filteredTickets.Where(t => t.EventDate <= request.ToDate.Value);
                }

                var totalCount = filteredTickets.Count();
                
                // Apply pagination
                var pagedTickets = filteredTickets
                    .OrderByDescending(t => t.PurchasedAt)
                    .Skip((request.PageNumber - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToList();

                var ticketDtos = new List<TicketDto>();
                foreach (var t in pagedTickets)
                {
                    var eventName = t.Event?.Name ?? await GetEventNameAsync(t.EventId);
                    var location = t.Event?.Location ?? await GetEventLocationAsync(t.EventId);
                    
                    ticketDtos.Add(new TicketDto
                    {
                        Id = t.Id,
                        UserId = t.UserId,
                        EventId = t.EventId,
                        EventName = eventName,
                        EventDate = t.EventDate,
                        TransactionId = t.TransactionId,
                        PurchasedAt = t.PurchasedAt,
                        Location = location
                    });
                }

                return new UserTicketsResponse
                {
                    Tickets = ticketDtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / request.PageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tickets for user {UserId}", userId);
                return new UserTicketsResponse();
            }
        }

        /// <summary>
        /// Get ticket details by transaction ID (Redis cache first, then database)
        /// </summary>
        public async Task<PurchaseTicketResponse?> GetTicketByTransactionAsync(string transactionId, string userId)
        {
            try
            {
                // Try Redis cache first
                var transaction = await _redisService.GetTransactionAsync(transactionId);
                
                if (transaction == null)
                {
                    // Fallback to database
                    transaction = await _dbContext.EventTicketTransactions
                        .FirstOrDefaultAsync(t => t.TransactionId == transactionId);
                    
                    if (transaction != null)
                    {
                        // Cache the result
                        await _redisService.SetTransactionAsync(transaction);
                    }
                }

                if (transaction == null || transaction.UserId != userId)
                {
                    return null;
                }

                // Get tickets for this transaction
                var tickets = await _dbContext.EventTickets
                    .Where(t => t.TransactionId == transactionId)
                    .Include(t => t.Event)
                    .ToListAsync();

                var ticketDtos = tickets.Select(t => new TicketDto
                {
                    Id = t.Id,
                    UserId = t.UserId,
                    EventId = t.EventId,
                    EventName = t.Event?.Name ?? "Unknown Event",
                    EventDate = t.EventDate,
                    TransactionId = t.TransactionId,
                    PurchasedAt = t.PurchasedAt,
                    Location = t.Event?.Location ?? "Unknown Location"
                }).ToList();

                return new PurchaseTicketResponse
                {
                    TransactionId = transactionId,
                    Tickets = ticketDtos,
                    TotalAmount = transaction.Amount,
                    Status = transaction.Status.ToString()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting ticket by transaction {TransactionId}", transactionId);
                return null;
            }
        }

        /// <summary>
        /// Check event availability using Redis cache first, then database
        /// </summary>
        public async Task<(bool isAvailable, decimal pricePerTicket, int availableTickets)> CheckEventAvailabilityAsync(int eventId, int quantity)
        {
            try
            {
                // Try Redis cache first for event data
                var eventData = await _redisService.GetEventAsync(eventId);
                
                if (eventData == null)
                {
                    // Fallback to database
                    eventData = await _dbContext.Events.FirstOrDefaultAsync(e => e.Id == eventId);
                    
                    if (eventData == null)
                    {
                        return (false, 0, 0);
                    }

                    // Cache the event data
                    await _redisService.SetEventAsync(eventData);
                }

                // Check if event is still valid
                if (eventData.Date <= DateTime.UtcNow)
                {
                    return (false, 0, 0);
                }

                // Get current available capacity from Redis
                var availableCapacity = await _redisService.GetEventCapacityAsync(eventId);
                
                if (availableCapacity == null)
                {
                    // Calculate and set initial capacity in Redis
                    var soldTicketsCount = await _dbContext.EventTickets
                        .CountAsync(t => t.EventId == eventId);
                    
                    availableCapacity = eventData.Capacity - soldTicketsCount;
                    await _redisService.UpdateEventCapacityAsync(eventId, availableCapacity.Value);
                }

                var isAvailable = availableCapacity >= quantity;
                var pricePerTicket = GetEventPrice(eventId, eventData.EventType);

                return (isAvailable, pricePerTicket, availableCapacity.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking availability for event {EventId}", eventId);
                return (false, 0, 0);
            }
        }

        /// <summary>
        /// Create tickets in database and cache them in Redis
        /// </summary>
        private async Task<List<TicketDto>> CreateTicketsAsync(PurchaseTicketRequest request, string userId, string transactionId, decimal pricePerTicket)
        {
            var tickets = new List<TicketDto>();
            var eventTickets = new List<EventTicket>();
            
            for (int i = 0; i < request.Quantity; i++)
            {
                var eventTicket = new EventTicket
                {
                    UserId = userId,
                    EventId = request.EventId,
                    EventDate = request.EventDate,
                    TransactionId = transactionId,
                    PurchasedAt = DateTime.UtcNow
                };

                eventTickets.Add(eventTicket);
            }

            // Save to database
            _dbContext.EventTickets.AddRange(eventTickets);
            await _dbContext.SaveChangesAsync();

            // Convert to DTOs
            foreach (var eventTicket in eventTickets)
            {
                tickets.Add(new TicketDto
                {
                    Id = eventTicket.Id,
                    UserId = userId,
                    EventId = request.EventId,
                    EventName = await GetEventNameAsync(request.EventId),
                    EventDate = request.EventDate,
                    TransactionId = transactionId,
                    PurchasedAt = DateTime.UtcNow,
                    Location = await GetEventLocationAsync(request.EventId)
                });
            }

            // Invalidate user tickets cache to force refresh
            await InvalidateUserTicketsCache(userId);

            return tickets;
        }

        /// <summary>
        /// Get event name (with caching)
        /// </summary>
        private async Task<string> GetEventNameAsync(int eventId)
        {
            var eventData = await _redisService.GetEventAsync(eventId);
            if (eventData != null)
            {
                return eventData.Name;
            }

            eventData = await _dbContext.Events.FirstOrDefaultAsync(e => e.Id == eventId);
            if (eventData != null)
            {
                await _redisService.SetEventAsync(eventData);
                return eventData.Name;
            }

            return "Unknown Event";
        }

        /// <summary>
        /// Get event location (with caching)
        /// </summary>
        private async Task<string> GetEventLocationAsync(int eventId)
        {
            var eventData = await _redisService.GetEventAsync(eventId);
            if (eventData != null)
            {
                return eventData.Location;
            }

            eventData = await _dbContext.Events.FirstOrDefaultAsync(e => e.Id == eventId);
            if (eventData != null)
            {
                await _redisService.SetEventAsync(eventData);
                return eventData.Location;
            }

            return "Unknown Location";
        }

        /// <summary>
        /// Get event pricing based on event type
        /// </summary>
        private static decimal GetEventPrice(int eventId, string eventType)
        {
            // Mock pricing logic based on event type and ID
            return eventType?.ToLower() switch
            {
                "concert" => 50.00m,
                "conference" => 150.00m,
                "sports" => 75.00m,
                "theater" => 40.00m,
                _ => 25.00m // Default price
            };
        }

        /// <summary>
        /// Invalidate user tickets cache in Redis
        /// </summary>
        private async Task InvalidateUserTicketsCache(string userId)
        {
            try
            {
                await _redisService.RemoveKeyAsync($"event_tickets:{userId}");
                _logger.LogDebug("Invalidated user tickets cache for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating user tickets cache for user {UserId}", userId);
            }
        }
    }
}
