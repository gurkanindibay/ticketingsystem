using StackExchange.Redis;
using TicketingSystem.Shared.Models;
using Newtonsoft.Json;

namespace TicketingSystem.Ticketing.Services
{
    /// <summary>
    /// Interface for Redis caching operations
    /// </summary>
    public interface IRedisService
    {
        /// <summary>
        /// Get event from Redis cache
        /// </summary>
        Task<Event?> GetEventAsync(int eventId);

        /// <summary>
        /// Set event in Redis cache with TTL
        /// </summary>
        Task SetEventAsync(Event eventData, TimeSpan? expiry = null);

        /// <summary>
        /// Get event tickets from Redis cache
        /// </summary>
        Task<List<EventTicket>?> GetEventTicketsAsync(string userId);

        /// <summary>
        /// Set event tickets in Redis cache
        /// </summary>
        Task SetEventTicketsAsync(string userId, List<EventTicket> tickets, TimeSpan? expiry = null);

        /// <summary>
        /// Get event ticket transaction from Redis cache
        /// </summary>
        Task<EventTicketTransaction?> GetTransactionAsync(string transactionId);

        /// <summary>
        /// Set event ticket transaction in Redis cache
        /// </summary>
        Task SetTransactionAsync(EventTicketTransaction transaction, TimeSpan? expiry = null);

        /// <summary>
        /// Remove transaction from Redis cache
        /// </summary>
        Task RemoveTransactionAsync(string transactionId);

        /// <summary>
        /// Get current event capacity from Redis
        /// </summary>
        Task<int?> GetEventCapacityAsync(int eventId);

        /// <summary>
        /// Update event capacity in Redis with atomic operation
        /// </summary>
        Task<bool> UpdateEventCapacityAsync(int eventId, int newCapacity);

        /// <summary>
        /// Decrement event capacity atomically (for ticket purchases)
        /// </summary>
        Task<int> DecrementEventCapacityAsync(int eventId, int quantity);

        /// <summary>
        /// Increment event capacity atomically (for ticket cancellations)
        /// </summary>
        Task<int> IncrementEventCapacityAsync(int eventId, int quantity);

        /// <summary>
        /// Check if key exists in Redis
        /// </summary>
        Task<bool> KeyExistsAsync(string key);

        /// <summary>
        /// Remove key from Redis
        /// </summary>
        Task<bool> RemoveKeyAsync(string key);

        /// <summary>
        /// Set expiry on existing key
        /// </summary>
        Task<bool> SetExpiryAsync(string key, TimeSpan expiry);
    }
}
