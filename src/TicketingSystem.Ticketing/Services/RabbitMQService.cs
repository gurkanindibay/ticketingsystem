using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Newtonsoft.Json;
using TicketingSystem.Shared.Configuration;
using Microsoft.Extensions.Options;
using System.Text;
using System.Threading.Channels;

namespace TicketingSystem.Ticketing.Services
{
    /// <summary>
    /// RabbitMQ service implementation for asynchronous messaging
    /// Implements capacity updates and transaction messaging as per system requirements
    /// </summary>
    public class RabbitMQService : IRabbitMQService, IDisposable, IAsyncDisposable
    {
        private readonly RabbitMQSettings _settings;
        private readonly ILogger<RabbitMQService> _logger;
        private IConnection? _connection;
        private IChannel? _channel;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private bool _disposed = false;

        // Queue names as per system requirements
        private const string CAPACITY_UPDATE_QUEUE = "ticket.capacity.updates";
        private const string TRANSACTION_QUEUE = "ticket.transactions";
        private const string DEAD_LETTER_EXCHANGE = "ticket.dead.letter";
        private const string DEAD_LETTER_QUEUE = "ticket.dead.letter.queue";

        public RabbitMQService(IOptions<RabbitMQSettings> settings, ILogger<RabbitMQService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        /// <summary>
        /// Ensure RabbitMQ connection is established
        /// </summary>
        private async Task EnsureConnectionAsync()
        {
            if (_connection != null && _connection.IsOpen && _channel != null && _channel.IsOpen)
                return;

            await _connectionLock.WaitAsync();
            try
            {
                if (_connection != null && _connection.IsOpen && _channel != null && _channel.IsOpen)
                    return;

                await DisconnectAsync();

                _logger.LogInformation("Establishing RabbitMQ connection to {HostName}:{Port}", 
                    _settings.HostName, _settings.Port);

                var factory = new ConnectionFactory()
                {
                    HostName = _settings.HostName,
                    Port = _settings.Port,
                    UserName = _settings.Username,
                    Password = _settings.Password,
                    VirtualHost = _settings.VirtualHost,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                    RequestedHeartbeat = TimeSpan.FromSeconds(60)
                };

                _connection = await factory.CreateConnectionAsync();
                _channel = await _connection.CreateChannelAsync();

                // Set QoS for fair dispatch
                await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false);

                // Declare exchanges, queues, and bindings
                await DeclareInfrastructureAsync();

                _logger.LogInformation("RabbitMQ connection established successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to establish RabbitMQ connection");
                throw;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        /// <summary>
        /// Declare RabbitMQ infrastructure (exchanges, queues, bindings)
        /// </summary>
        private async Task DeclareInfrastructureAsync()
        {
            if (_channel == null) return;

            try
            {
                // Declare dead letter exchange
                await _channel.ExchangeDeclareAsync(
                    exchange: DEAD_LETTER_EXCHANGE,
                    type: ExchangeType.Direct,
                    durable: true,
                    autoDelete: false,
                    arguments: null
                );

                // Declare dead letter queue
                await _channel.QueueDeclareAsync(
                    queue: DEAD_LETTER_QUEUE,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null
                );

                // Bind dead letter queue to dead letter exchange
                await _channel.QueueBindAsync(
                    queue: DEAD_LETTER_QUEUE,
                    exchange: DEAD_LETTER_EXCHANGE,
                    routingKey: ""
                );

                // Arguments for main queues with dead letter routing
                var queueArguments = new Dictionary<string, object?>
                {
                    { "x-dead-letter-exchange", DEAD_LETTER_EXCHANGE },
                    { "x-message-ttl", 3600000 }, // 1 hour TTL
                    { "x-max-retries", 3 }
                };

                // Declare capacity update queue with durability and dead letter routing
                await _channel.QueueDeclareAsync(
                    queue: CAPACITY_UPDATE_QUEUE,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: queueArguments
                );

                // Declare transaction queue with durability and dead letter routing
                await _channel.QueueDeclareAsync(
                    queue: TRANSACTION_QUEUE,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: queueArguments
                );

                _logger.LogDebug("RabbitMQ infrastructure declared successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to declare RabbitMQ infrastructure");
                throw;
            }
        }

        /// <summary>
        /// Publish capacity update message asynchronously
        /// </summary>
        public async Task PublishCapacityUpdateAsync(CapacityUpdateMessage message)
        {
            await EnsureConnectionAsync();

            if (_channel == null)
            {
                _logger.LogError("RabbitMQ channel not available - capacity update message will be lost");
                throw new InvalidOperationException("RabbitMQ channel not available");
            }

            try
            {
                var messageBody = JsonConvert.SerializeObject(message, Formatting.None);
                var body = Encoding.UTF8.GetBytes(messageBody);

                var properties = new BasicProperties
                {
                    Persistent = true, // Make message persistent
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                    MessageId = Guid.NewGuid().ToString(),
                    Type = "CapacityUpdate",
                    ContentType = "application/json",
                    DeliveryMode = DeliveryModes.Persistent,
                    Priority = 5, // Medium priority
                    Headers = new Dictionary<string, object?>
                    {
                        { "eventId", message.EventId },
                        { "operation", message.Operation },
                        { "capacityChange", message.CapacityChange }
                    }
                };

                await _channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: CAPACITY_UPDATE_QUEUE,
                    mandatory: true, // Ensure message is routed
                    basicProperties: properties,
                    body: body
                );

                _logger.LogDebug("Published capacity update message for event {EventId}, change {Change}, transaction {TransactionId}",
                    message.EventId, message.CapacityChange, message.TransactionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish capacity update message for event {EventId}",
                    message.EventId);
                throw;
            }
        }

        /// <summary>
        /// Publish ticket transaction message asynchronously
        /// </summary>
        public async Task PublishTicketTransactionAsync(TicketTransactionMessage message)
        {
            await EnsureConnectionAsync();

            if (_channel == null)
            {
                _logger.LogError("RabbitMQ channel not available - transaction message will be lost");
                throw new InvalidOperationException("RabbitMQ channel not available");
            }

            try
            {
                var messageBody = JsonConvert.SerializeObject(message, Formatting.None);
                var body = Encoding.UTF8.GetBytes(messageBody);

                var properties = new BasicProperties
                {
                    Persistent = true, // Make message persistent
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                    MessageId = Guid.NewGuid().ToString(),
                    Type = "TicketTransaction",
                    ContentType = "application/json",
                    DeliveryMode = DeliveryModes.Persistent,
                    Priority = 7, // Higher priority for transactions
                    Headers = new Dictionary<string, object?>
                    {
                        { "transactionId", message.TransactionId },
                        { "eventId", message.EventId },
                        { "operation", message.Operation },
                        { "userId", message.UserId }
                    }
                };

                await _channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: TRANSACTION_QUEUE,
                    mandatory: true, // Ensure message is routed
                    basicProperties: properties,
                    body: body
                );

                _logger.LogDebug("Published transaction message for transaction {TransactionId}, event {EventId}",
                    message.TransactionId, message.EventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish transaction message for transaction {TransactionId}",
                    message.TransactionId);
                throw;
            }
        }

        /// <summary>
        /// Subscribe to capacity update messages for processing
        /// </summary>
        public async Task SubscribeToCapacityUpdatesAsync(Func<CapacityUpdateMessage, Task> messageHandler)
        {
            await EnsureConnectionAsync();

            if (_channel == null)
            {
                _logger.LogError("RabbitMQ channel not available - cannot subscribe to capacity updates");
                throw new InvalidOperationException("RabbitMQ channel not available");
            }

            try
            {
                var consumer = new AsyncEventingBasicConsumer(_channel);
                
                consumer.ReceivedAsync += async (model, ea) =>
                {
                    var deliveryTag = ea.DeliveryTag;
                    var retryCount = 0;
                    
                    try
                    {
                        // Check retry count from headers
                        if (ea.BasicProperties.Headers?.TryGetValue("x-retry-count", out var retryObj) == true)
                        {
                            retryCount = Convert.ToInt32(retryObj);
                        }

                        var body = ea.Body.ToArray();
                        var messageJson = Encoding.UTF8.GetString(body);
                        var message = JsonConvert.DeserializeObject<CapacityUpdateMessage>(messageJson);

                        if (message != null)
                        {
                            await messageHandler(message);
                            
                            // Acknowledge message after successful processing
                            await _channel.BasicAckAsync(deliveryTag: deliveryTag, multiple: false);
                            
                            _logger.LogDebug("Processed capacity update message for event {EventId}",
                                message.EventId);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to deserialize capacity update message");
                            await _channel.BasicNackAsync(deliveryTag: deliveryTag, multiple: false, requeue: false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing capacity update message (retry {RetryCount})", retryCount);
                        
                        if (retryCount < 3)
                        {
                            // Republish with retry count
                            await RetryMessageAsync(ea, retryCount + 1, CAPACITY_UPDATE_QUEUE);
                            await _channel.BasicAckAsync(deliveryTag: deliveryTag, multiple: false);
                        }
                        else
                        {
                            // Max retries exceeded - send to dead letter queue
                            _logger.LogError("Max retries exceeded for capacity update message - sending to dead letter queue");
                            await _channel.BasicNackAsync(deliveryTag: deliveryTag, multiple: false, requeue: false);
                        }
                    }
                };

                await _channel.BasicConsumeAsync(
                    queue: CAPACITY_UPDATE_QUEUE,
                    autoAck: false, // Manual acknowledgment for reliability
                    consumer: consumer
                );

                _logger.LogInformation("Subscribed to capacity update messages");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to capacity update messages");
                throw;
            }
        }

        /// <summary>
        /// Subscribe to transaction messages for processing
        /// </summary>
        public async Task SubscribeToTransactionMessagesAsync(Func<TicketTransactionMessage, Task> messageHandler)
        {
            await EnsureConnectionAsync();

            if (_channel == null)
            {
                _logger.LogError("RabbitMQ channel not available - cannot subscribe to transaction messages");
                throw new InvalidOperationException("RabbitMQ channel not available");
            }

            try
            {
                var consumer = new AsyncEventingBasicConsumer(_channel);
                
                consumer.ReceivedAsync += async (model, ea) =>
                {
                    var deliveryTag = ea.DeliveryTag;
                    var retryCount = 0;
                    
                    try
                    {
                        // Check retry count from headers
                        if (ea.BasicProperties.Headers?.TryGetValue("x-retry-count", out var retryObj) == true)
                        {
                            retryCount = Convert.ToInt32(retryObj);
                        }

                        var body = ea.Body.ToArray();
                        var messageJson = Encoding.UTF8.GetString(body);
                        var message = JsonConvert.DeserializeObject<TicketTransactionMessage>(messageJson);

                        if (message != null)
                        {
                            await messageHandler(message);
                            
                            // Acknowledge message after successful processing
                            await _channel.BasicAckAsync(deliveryTag: deliveryTag, multiple: false);
                            
                            _logger.LogDebug("Processed transaction message for transaction {TransactionId}",
                                message.TransactionId);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to deserialize transaction message");
                            await _channel.BasicNackAsync(deliveryTag: deliveryTag, multiple: false, requeue: false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing transaction message (retry {RetryCount})", retryCount);
                        
                        if (retryCount < 3)
                        {
                            // Republish with retry count
                            await RetryMessageAsync(ea, retryCount + 1, TRANSACTION_QUEUE);
                            await _channel.BasicAckAsync(deliveryTag: deliveryTag, multiple: false);
                        }
                        else
                        {
                            // Max retries exceeded - send to dead letter queue
                            _logger.LogError("Max retries exceeded for transaction message - sending to dead letter queue");
                            await _channel.BasicNackAsync(deliveryTag: deliveryTag, multiple: false, requeue: false);
                        }
                    }
                };

                await _channel.BasicConsumeAsync(
                    queue: TRANSACTION_QUEUE,
                    autoAck: false, // Manual acknowledgment for reliability
                    consumer: consumer
                );

                _logger.LogInformation("Subscribed to transaction messages");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to transaction messages");
                throw;
            }
        }

        /// <summary>
        /// Retry a failed message with exponential backoff
        /// </summary>
        private async Task RetryMessageAsync(BasicDeliverEventArgs originalMessage, int retryCount, string queue)
        {
            if (_channel == null) return;

            try
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount)); // Exponential backoff
                
                var properties = new BasicProperties
                {
                    Persistent = true,
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                    MessageId = originalMessage.BasicProperties.MessageId,
                    Type = originalMessage.BasicProperties.Type,
                    ContentType = "application/json",
                    DeliveryMode = DeliveryModes.Persistent,
                    Headers = new Dictionary<string, object?>(originalMessage.BasicProperties.Headers ?? new Dictionary<string, object?>())
                    {
                        ["x-retry-count"] = retryCount,
                        ["x-original-queue"] = queue,
                        ["x-retry-timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    }
                };

                // Use delayed retry queue for exponential backoff
                var delayedQueue = $"{queue}.retry.{delay.TotalSeconds}s";
                
                await _channel.QueueDeclareAsync(
                    queue: delayedQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: new Dictionary<string, object?>
                    {
                        { "x-message-ttl", (int)delay.TotalMilliseconds },
                        { "x-dead-letter-exchange", "" },
                        { "x-dead-letter-routing-key", queue }
                    }
                );

                await _channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: delayedQueue,
                    mandatory: false,
                    basicProperties: properties,
                    body: originalMessage.Body
                );

                _logger.LogDebug("Message scheduled for retry {RetryCount} in {Delay} seconds", retryCount, delay.TotalSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to schedule message retry");
            }
        }

        /// <summary>
        /// Get queue statistics
        /// </summary>
        public async Task<Dictionary<string, object>> GetQueueStatsAsync()
        {
            await EnsureConnectionAsync();
            
            var stats = new Dictionary<string, object>();
            
            if (_channel == null)
            {
                stats["error"] = "Channel not available";
                return stats;
            }

            try
            {
                var capacityQueueInfo = await _channel.QueueDeclarePassiveAsync(CAPACITY_UPDATE_QUEUE);
                var transactionQueueInfo = await _channel.QueueDeclarePassiveAsync(TRANSACTION_QUEUE);
                var deadLetterQueueInfo = await _channel.QueueDeclarePassiveAsync(DEAD_LETTER_QUEUE);

                stats["capacity_queue_messages"] = capacityQueueInfo.MessageCount;
                stats["capacity_queue_consumers"] = capacityQueueInfo.ConsumerCount;
                stats["transaction_queue_messages"] = transactionQueueInfo.MessageCount;
                stats["transaction_queue_consumers"] = transactionQueueInfo.ConsumerCount;
                stats["dead_letter_queue_messages"] = deadLetterQueueInfo.MessageCount;
                stats["connection_open"] = _connection?.IsOpen ?? false;
                stats["channel_open"] = _channel?.IsOpen ?? false;
                
                _logger.LogDebug("Retrieved queue statistics: {Stats}", JsonConvert.SerializeObject(stats));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve queue statistics");
                stats["error"] = ex.Message;
            }
            
            return stats;
        }

        /// <summary>
        /// Disconnect from RabbitMQ
        /// </summary>
        private async Task DisconnectAsync()
        {
            try
            {
                if (_channel != null)
                {
                    await _channel.CloseAsync();
                    await _channel.DisposeAsync();
                    _channel = null;
                }

                if (_connection != null)
                {
                    await _connection.CloseAsync();
                    await _connection.DisposeAsync();
                    _connection = null;
                }

                _logger.LogDebug("RabbitMQ connection closed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting from RabbitMQ");
            }
        }

        /// <summary>
        /// Dispose of RabbitMQ resources synchronously
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            
            DisposeAsync().AsTask().Wait();
            _connectionLock.Dispose();
            _disposed = true;
        }

        /// <summary>
        /// Dispose of RabbitMQ resources asynchronously
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            
            await DisconnectAsync();
            _connectionLock.Dispose();
            _disposed = true;
        }
    }
}
