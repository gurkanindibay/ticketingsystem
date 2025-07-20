using StackExchange.Redis;
using TicketingSystem.Shared.Models;
using Newtonsoft.Json;
using TicketingSystem.Shared.Configuration;
using Microsoft.Extensions.Options;

namespace TicketingSystem.Ticketing.Services
{
    /// <summary>
    /// Redis service implementation for caching ticket-related data
    /// Implements Redis operations as per system requirements with JSON storage
    /// </summary>
    public class RedisService : IRedisService, IDisposable
    {
        private readonly IDatabase _database;
        private readonly ConnectionMultiplexer _redis;
        private readonly RedisSettings _settings;
        private readonly ILogger<RedisService> _logger;
        private readonly string _keyPrefix;

        public RedisService(IOptions<RedisSettings> settings, ILogger<RedisService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
            _keyPrefix = _settings.KeyPrefix;

            try
            {
                var config = ConfigurationOptions.Parse(_settings.ConnectionString);
                config.ConnectRetry = 3;
                config.ConnectTimeout = 5000;
                config.SyncTimeout = 5000;

                _redis = ConnectionMultiplexer.Connect(config);
                _database = _redis.GetDatabase(_settings.Database);

                _logger.LogInformation("Redis connection established successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Redis at {ConnectionString}", _settings.ConnectionString);
                throw;
            }
        }

        /// <summary>
        /// Get event from Redis cache using key pattern: event:{id}
        /// </summary>
        public async Task<Event?> GetEventAsync(int eventId)
        {
            try
            {
                var key = $"{_keyPrefix}event:{eventId}";
                var value = await _database.StringGetAsync(key);
                
                if (!value.HasValue)
                {
                    _logger.LogDebug("Event {EventId} not found in Redis cache", eventId);
                    return null;
                }

                var eventData = JsonConvert.DeserializeObject<Event>(value);
                _logger.LogDebug("Event {EventId} retrieved from Redis cache", eventId);
                return eventData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting event {EventId} from Redis", eventId);
                return null;
            }
        }

        /// <summary>
        /// Set event in Redis cache with TTL expiration for past events
        /// </summary>
        public async Task SetEventAsync(Event eventData, TimeSpan? expiry = null)
        {
            try
            {
                var key = $"{_keyPrefix}event:{eventData.Id}";
                var value = JsonConvert.SerializeObject(eventData);

                // Set TTL for events that are in the past (as per requirements)
                var effectiveExpiry = expiry;
                if (effectiveExpiry == null && eventData.Date < DateTime.UtcNow)
                {
                    effectiveExpiry = TimeSpan.FromHours(1); // Short TTL for past events
                }
                else if (effectiveExpiry == null)
                {
                    effectiveExpiry = TimeSpan.FromMinutes(_settings.DefaultExpirationMinutes);
                }

                await _database.StringSetAsync(key, value, effectiveExpiry);
                _logger.LogDebug("Event {EventId} stored in Redis cache with expiry {Expiry}", 
                    eventData.Id, effectiveExpiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting event {EventId} in Redis", eventData.Id);
            }
        }

        /// <summary>
        /// Get event tickets for a user from Redis cache
        /// </summary>
        public async Task<List<EventTicket>?> GetEventTicketsAsync(string userId)
        {
            try
            {
                var key = $"{_keyPrefix}event_tickets:{userId}";
                var value = await _database.StringGetAsync(key);

                if (!value.HasValue)
                {
                    _logger.LogDebug("Event tickets for user {UserId} not found in Redis cache", userId);
                    return null;
                }

                var tickets = JsonConvert.DeserializeObject<List<EventTicket>>(value);
                _logger.LogDebug("Event tickets for user {UserId} retrieved from Redis cache", userId);
                return tickets;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting event tickets for user {UserId} from Redis", userId);
                return null;
            }
        }

        /// <summary>
        /// Set event tickets in Redis cache with TTL expiration for past events
        /// </summary>
        public async Task SetEventTicketsAsync(string userId, List<EventTicket> tickets, TimeSpan? expiry = null)
        {
            try
            {
                var key = $"{_keyPrefix}event_tickets:{userId}";
                var value = JsonConvert.SerializeObject(tickets);

                // Set TTL based on the oldest event date
                var effectiveExpiry = expiry;
                if (effectiveExpiry == null && tickets.Any())
                {
                    var oldestEventDate = tickets.Min(t => t.EventDate);
                    if (oldestEventDate < DateTime.UtcNow)
                    {
                        effectiveExpiry = TimeSpan.FromHours(24); // Keep past event tickets for a day
                    }
                    else
                    {
                        effectiveExpiry = TimeSpan.FromMinutes(_settings.DefaultExpirationMinutes);
                    }
                }
                else if (effectiveExpiry == null)
                {
                    effectiveExpiry = TimeSpan.FromMinutes(_settings.DefaultExpirationMinutes);
                }

                await _database.StringSetAsync(key, value, effectiveExpiry);
                _logger.LogDebug("Event tickets for user {UserId} stored in Redis cache", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting event tickets for user {UserId} in Redis", userId);
            }
        }

        /// <summary>
        /// Get event ticket transaction from Redis cache
        /// </summary>
        public async Task<EventTicketTransaction?> GetTransactionAsync(string transactionId)
        {
            try
            {
                var key = $"{_keyPrefix}event_ticket_transactions:{transactionId}";
                var value = await _database.StringGetAsync(key);

                if (!value.HasValue)
                {
                    _logger.LogDebug("Transaction {TransactionId} not found in Redis cache", transactionId);
                    return null;
                }

                var transaction = JsonConvert.DeserializeObject<EventTicketTransaction>(value);
                _logger.LogDebug("Transaction {TransactionId} retrieved from Redis cache", transactionId);
                return transaction;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transaction {TransactionId} from Redis", transactionId);
                return null;
            }
        }

        /// <summary>
        /// Set event ticket transaction in Redis cache with TTL expiration for past events
        /// </summary>
        public async Task SetTransactionAsync(EventTicketTransaction transaction, TimeSpan? expiry = null)
        {
            try
            {
                var key = $"{_keyPrefix}event_ticket_transactions:{transaction.TransactionId}";
                var value = JsonConvert.SerializeObject(transaction);

                // Set TTL for transactions of past events
                var effectiveExpiry = expiry;
                if (effectiveExpiry == null && transaction.EventDate < DateTime.UtcNow)
                {
                    effectiveExpiry = TimeSpan.FromDays(1); // Keep past transactions for a day
                }
                else if (effectiveExpiry == null)
                {
                    effectiveExpiry = TimeSpan.FromMinutes(_settings.DefaultExpirationMinutes);
                }

                await _database.StringSetAsync(key, value, effectiveExpiry);
                _logger.LogDebug("Transaction {TransactionId} stored in Redis cache", transaction.TransactionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting transaction {TransactionId} in Redis", transaction.TransactionId);
            }
        }

        /// <summary>
        /// Remove transaction from Redis cache
        /// </summary>
        public async Task RemoveTransactionAsync(string transactionId)
        {
            try
            {
                var key = $"{_keyPrefix}event_ticket_transactions:{transactionId}";
                await _database.KeyDeleteAsync(key);
                _logger.LogDebug("Transaction {TransactionId} removed from Redis cache", transactionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing transaction {TransactionId} from Redis", transactionId);
            }
        }

        /// <summary>
        /// Get current event capacity from Redis
        /// </summary>
        public async Task<int?> GetEventCapacityAsync(int eventId)
        {
            try
            {
                var key = $"{_keyPrefix}event_capacity:{eventId}";
                var value = await _database.StringGetAsync(key);

                if (!value.HasValue)
                {
                    _logger.LogDebug("Event capacity for {EventId} not found in Redis cache", eventId);
                    return null;
                }

                if (int.TryParse(value, out var capacity))
                {
                    _logger.LogDebug("Event capacity for {EventId} retrieved from Redis: {Capacity}", eventId, capacity);
                    return capacity;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting event capacity for {EventId} from Redis", eventId);
                return null;
            }
        }

        /// <summary>
        /// Update event capacity in Redis with atomic operation
        /// </summary>
        public async Task<bool> UpdateEventCapacityAsync(int eventId, int newCapacity)
        {
            try
            {
                var key = $"{_keyPrefix}event_capacity:{eventId}";
                await _database.StringSetAsync(key, newCapacity.ToString());
                _logger.LogDebug("Event capacity for {EventId} updated to {Capacity}", eventId, newCapacity);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating event capacity for {EventId} in Redis", eventId);
                return false;
            }
        }

        /// <summary>
        /// Decrement event capacity atomically using Redis DECRBY command
        /// </summary>
        public async Task<int> DecrementEventCapacityAsync(int eventId, int quantity)
        {
            try
            {
                var key = $"{_keyPrefix}event_capacity:{eventId}";
                var result = await _database.StringDecrementAsync(key, quantity);
                _logger.LogDebug("Event capacity for {EventId} decremented by {Quantity}, new value: {NewCapacity}", 
                    eventId, quantity, result);
                return (int)result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrementing event capacity for {EventId} in Redis", eventId);
                throw;
            }
        }

        /// <summary>
        /// Increment event capacity atomically using Redis INCRBY command
        /// </summary>
        public async Task<int> IncrementEventCapacityAsync(int eventId, int quantity)
        {
            try
            {
                var key = $"{_keyPrefix}event_capacity:{eventId}";
                var result = await _database.StringIncrementAsync(key, quantity);
                _logger.LogDebug("Event capacity for {EventId} incremented by {Quantity}, new value: {NewCapacity}", 
                    eventId, quantity, result);
                return (int)result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing event capacity for {EventId} in Redis", eventId);
                throw;
            }
        }

        /// <summary>
        /// Check if key exists in Redis
        /// </summary>
        public async Task<bool> KeyExistsAsync(string key)
        {
            try
            {
                var fullKey = $"{_keyPrefix}{key}";
                return await _database.KeyExistsAsync(fullKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if key {Key} exists in Redis", key);
                return false;
            }
        }

        /// <summary>
        /// Remove key from Redis
        /// </summary>
        public async Task<bool> RemoveKeyAsync(string key)
        {
            try
            {
                var fullKey = $"{_keyPrefix}{key}";
                return await _database.KeyDeleteAsync(fullKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing key {Key} from Redis", key);
                return false;
            }
        }

        /// <summary>
        /// Set expiry on existing key
        /// </summary>
        public async Task<bool> SetExpiryAsync(string key, TimeSpan expiry)
        {
            try
            {
                var fullKey = $"{_keyPrefix}{key}";
                return await _database.KeyExpireAsync(fullKey, expiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting expiry on key {Key} in Redis", key);
                return false;
            }
        }

        /// <summary>
        /// Delete event from Redis cache
        /// </summary>
        public async Task<bool> DeleteEventAsync(int eventId)
        {
            try
            {
                var eventKey = $"{_keyPrefix}event:{eventId}";
                var capacityKey = $"{_keyPrefix}event_capacity:{eventId}";
                
                var tasks = new[]
                {
                    _database.KeyDeleteAsync(eventKey),
                    _database.KeyDeleteAsync(capacityKey)
                };

                var results = await Task.WhenAll(tasks);
                var deleted = results.Any(r => r);
                
                if (deleted)
                {
                    _logger.LogDebug("Event {EventId} and its capacity deleted from Redis", eventId);
                }
                
                return deleted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting event {EventId} from Redis", eventId);
                return false;
            }
        }

        /// <summary>
        /// Set event capacity in Redis
        /// </summary>
        public async Task SetEventCapacityAsync(int eventId, int capacity)
        {
            try
            {
                var key = $"{_keyPrefix}event_capacity:{eventId}";
                await _database.StringSetAsync(key, capacity.ToString());
                _logger.LogDebug("Event capacity for {EventId} set to {Capacity}", eventId, capacity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting event capacity for {EventId} in Redis", eventId);
                throw;
            }
        }

        public void Dispose()
        {
            _redis?.Dispose();
        }
    }
}
