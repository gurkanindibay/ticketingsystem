using RabbitMQ.Client;
using Newtonsoft.Json;
using TicketingSystem.Shared.Configuration;
using Microsoft.Extensions.Options;
using System.Text;

namespace TicketingSystem.Ticketing.Services
{
    /// <summary>
    /// Interface for RabbitMQ messaging operations
    /// </summary>
    public interface IRabbitMQService
    {
        /// <summary>
        /// Publish capacity update message for asynchronous PostgreSQL update
        /// </summary>
        Task PublishCapacityUpdateAsync(CapacityUpdateMessage message);

        /// <summary>
        /// Publish ticket transaction message
        /// </summary>
        Task PublishTicketTransactionAsync(TicketTransactionMessage message);

        /// <summary>
        /// Subscribe to capacity update messages for processing
        /// </summary>
        Task SubscribeToCapacityUpdatesAsync(Func<CapacityUpdateMessage, Task> messageHandler);

        /// <summary>
        /// Subscribe to transaction messages for processing
        /// </summary>
        Task SubscribeToTransactionMessagesAsync(Func<TicketTransactionMessage, Task> messageHandler);

        /// <summary>
        /// Get queue statistics for monitoring
        /// </summary>
        Task<Dictionary<string, object>> GetQueueStatsAsync();
    }

    /// <summary>
    /// Capacity update message for RabbitMQ
    /// </summary>
    public class CapacityUpdateMessage
    {
        public int EventId { get; set; }
        public int CapacityChange { get; set; } // Positive for increases, negative for decreases
        public string TransactionId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Operation { get; set; } = string.Empty; // "purchase", "cancel"
    }

    /// <summary>
    /// Ticket transaction message for RabbitMQ
    /// </summary>
    public class TicketTransactionMessage
    {
        public string TransactionId { get; set; } = string.Empty;
        public int EventId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Operation { get; set; } = string.Empty; // "create", "complete", "cancel"
    }
}
