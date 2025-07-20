using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TicketingSystem.Ticketing.Data;

namespace TicketingSystem.Ticketing.Services
{
    /// <summary>
    /// Background service that starts RabbitMQ consumers for capacity updates and transaction processing
    /// </summary>
    public class RabbitMQBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RabbitMQBackgroundService> _logger;

        public RabbitMQBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<RabbitMQBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Starting RabbitMQ background consumers...");

                // Create a scope to get scoped services
                using var scope = _serviceProvider.CreateScope();
                var rabbitMQService = scope.ServiceProvider.GetRequiredService<IRabbitMQService>();

                // Start consumers for capacity updates and transaction processing
                await StartConsumersAsync(rabbitMQService, stoppingToken);

                _logger.LogInformation("RabbitMQ background consumers started successfully");

                // Keep the service running until cancellation is requested
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("RabbitMQ background service is stopping due to cancellation");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RabbitMQ background service");
                throw; // Re-throw to ensure the service fails properly
            }
        }

        private async Task StartConsumersAsync(IRabbitMQService rabbitMQService, CancellationToken cancellationToken)
        {
            try
            {
                // Start capacity update consumer
                _logger.LogInformation("Starting capacity update consumer...");
                await StartCapacityUpdateConsumerAsync(rabbitMQService, cancellationToken);

                // Start transaction consumer  
                _logger.LogInformation("Starting transaction consumer...");
                await StartTransactionConsumerAsync(rabbitMQService, cancellationToken);

                _logger.LogInformation("All RabbitMQ consumers started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start RabbitMQ consumers");
                throw;
            }
        }

        private async Task StartCapacityUpdateConsumerAsync(IRabbitMQService rabbitMQService, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting capacity update message consumer...");

                // Subscribe to capacity update messages
                await rabbitMQService.SubscribeToCapacityUpdatesAsync(async (message) =>
                {
                    _logger.LogInformation("Processing capacity update message: EventId={EventId}, Change={Change}, TransactionId={TransactionId}",
                        message.EventId, message.CapacityChange, message.TransactionId);

                    try
                    {
                        var processingStartTime = DateTime.UtcNow;
                        using var scope = _serviceProvider.CreateScope();
                        var ticketService = scope.ServiceProvider.GetRequiredService<ITicketService>();
                        var messageStatsService = scope.ServiceProvider.GetRequiredService<IMessageStatsService>();

                        // Process the capacity update in the database
                        await ProcessCapacityUpdateAsync(message, scope.ServiceProvider);

                        // Record successful processing
                        var processingTime = DateTime.UtcNow - processingStartTime;
                        messageStatsService.RecordMessageProcessed("ticket.capacity.updates", "CapacityUpdate", true, processingTime);

                        _logger.LogInformation("Successfully processed capacity update for Event {EventId}", message.EventId);
                    }
                    catch (Exception ex)
                    {
                        // Record failed processing
                        try
                        {
                            var processingTime = DateTime.UtcNow - DateTime.UtcNow; // Will be near zero for failed processing
                            using var scope = _serviceProvider.CreateScope();
                            var messageStatsService = scope.ServiceProvider.GetRequiredService<IMessageStatsService>();
                            messageStatsService.RecordMessageProcessed("ticket.capacity.updates", "CapacityUpdate", false, processingTime);
                        }
                        catch { /* Ignore stats recording errors */ }

                        _logger.LogError(ex, "Failed to process capacity update message for Event {EventId}", message.EventId);
                        throw; // This will trigger retry logic in RabbitMQ
                    }
                });

                _logger.LogInformation("Capacity update consumer started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start capacity update consumer");
                throw;
            }
        }

        private async Task StartTransactionConsumerAsync(IRabbitMQService rabbitMQService, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting transaction message consumer...");

                // Subscribe to transaction messages
                await rabbitMQService.SubscribeToTransactionMessagesAsync(async (message) =>
                {
                    _logger.LogInformation("Processing transaction message: TransactionId={TransactionId}, EventId={EventId}, Operation={Operation}",
                        message.TransactionId, message.EventId, message.Operation);

                    try
                    {
                        var processingStartTime = DateTime.UtcNow;
                        using var scope = _serviceProvider.CreateScope();
                        var messageStatsService = scope.ServiceProvider.GetRequiredService<IMessageStatsService>();

                        // Process the transaction message
                        await ProcessTransactionMessageAsync(message, scope.ServiceProvider);

                        // Record successful processing
                        var processingTime = DateTime.UtcNow - processingStartTime;
                        messageStatsService.RecordMessageProcessed("ticket.transactions", "TicketTransaction", true, processingTime);

                        _logger.LogInformation("Successfully processed transaction message {TransactionId}", message.TransactionId);
                    }
                    catch (Exception ex)
                    {
                        // Record failed processing
                        try
                        {
                            var processingTime = DateTime.UtcNow - DateTime.UtcNow; // Will be near zero for failed processing
                            using var scope = _serviceProvider.CreateScope();
                            var messageStatsService = scope.ServiceProvider.GetRequiredService<IMessageStatsService>();
                            messageStatsService.RecordMessageProcessed("ticket.transactions", "TicketTransaction", false, processingTime);
                        }
                        catch { /* Ignore stats recording errors */ }

                        _logger.LogError(ex, "Failed to process transaction message {TransactionId}", message.TransactionId);
                        throw; // This will trigger retry logic in RabbitMQ
                    }
                });

                _logger.LogInformation("Transaction consumer started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start transaction consumer");
                throw;
            }
        }

        private async Task ProcessCapacityUpdateAsync(CapacityUpdateMessage message, IServiceProvider serviceProvider)
        {
            var dbContext = serviceProvider.GetRequiredService<TicketingDbContext>();

            // Find the event and update its capacity
            var eventEntity = await dbContext.Events.FindAsync(message.EventId);
            if (eventEntity != null)
            {
                // Apply the capacity change (negative for purchases, positive for cancellations)
                eventEntity.Capacity += message.CapacityChange;
                eventEntity.UpdatedAt = DateTime.UtcNow;

                await dbContext.SaveChangesAsync();

                _logger.LogInformation("Updated Event {EventId} capacity by {Change}. New capacity: {NewCapacity}",
                    message.EventId, message.CapacityChange, eventEntity.Capacity);
            }
            else
            {
                _logger.LogWarning("Event {EventId} not found for capacity update", message.EventId);
            }
        }

        private async Task ProcessTransactionMessageAsync(TicketTransactionMessage message, IServiceProvider serviceProvider)
        {
            // For now, just log the transaction message
            // In a production system, this might update audit logs, send notifications, etc.
            _logger.LogInformation("Transaction audit: {TransactionId} for Event {EventId}, Amount: {Amount}, Status: {Status}",
                message.TransactionId, message.EventId, message.Amount, message.Status);

            // You could add additional processing here like:
            // - Updating analytics databases
            // - Sending notifications
            // - Triggering business workflows
            
            await Task.CompletedTask;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping RabbitMQ background service...");
            await base.StopAsync(cancellationToken);
            _logger.LogInformation("RabbitMQ background service stopped");
        }
    }
}
