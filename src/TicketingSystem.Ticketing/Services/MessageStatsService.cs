using System.Collections.Concurrent;

namespace TicketingSystem.Ticketing.Services
{
    /// <summary>
    /// Service to track message processing statistics
    /// </summary>
    public interface IMessageStatsService
    {
        void RecordMessagePublished(string queueName, string messageType);
        void RecordMessageProcessed(string queueName, string messageType, bool success, TimeSpan processingTime);
        MessageStatsReport GetStats();
        void ResetStats();
    }

    public class MessageStatsService : IMessageStatsService
    {
        private readonly ConcurrentDictionary<string, QueueStats> _queueStats = new();
        private readonly object _lockObject = new();
        private DateTime _statsStartTime = DateTime.UtcNow;

        public void RecordMessagePublished(string queueName, string messageType)
        {
            var stats = _queueStats.GetOrAdd(queueName, _ => new QueueStats());
            Interlocked.Increment(ref stats.TotalPublished);
            
            stats.MessageTypes.AddOrUpdate(messageType, 
                new MessageTypeStats { Published = 1 },
                (key, existing) => 
                {
                    Interlocked.Increment(ref existing.Published);
                    return existing;
                });
        }

        public void RecordMessageProcessed(string queueName, string messageType, bool success, TimeSpan processingTime)
        {
            var stats = _queueStats.GetOrAdd(queueName, _ => new QueueStats());
            
            if (success)
            {
                Interlocked.Increment(ref stats.TotalProcessedSuccess);
            }
            else
            {
                Interlocked.Increment(ref stats.TotalProcessedFailed);
            }

            // Update processing time (simple moving average)
            lock (_lockObject)
            {
                stats.TotalProcessingTimeMs += processingTime.TotalMilliseconds;
                stats.ProcessedMessageCount++;
                stats.LastProcessedAt = DateTime.UtcNow;
            }

            stats.MessageTypes.AddOrUpdate(messageType,
                new MessageTypeStats { ProcessedSuccess = success ? 1 : 0, ProcessedFailed = success ? 0 : 1 },
                (key, existing) =>
                {
                    if (success)
                        Interlocked.Increment(ref existing.ProcessedSuccess);
                    else
                        Interlocked.Increment(ref existing.ProcessedFailed);
                    return existing;
                });
        }

        public MessageStatsReport GetStats()
        {
            var report = new MessageStatsReport
            {
                StatsStartTime = _statsStartTime,
                CurrentTime = DateTime.UtcNow,
                QueueStats = new Dictionary<string, QueueStatsSnapshot>()
            };

            foreach (var kvp in _queueStats)
            {
                var stats = kvp.Value;
                var snapshot = new QueueStatsSnapshot
                {
                    QueueName = kvp.Key,
                    TotalPublished = stats.TotalPublished,
                    TotalProcessedSuccess = stats.TotalProcessedSuccess,
                    TotalProcessedFailed = stats.TotalProcessedFailed,
                    AverageProcessingTimeMs = stats.ProcessedMessageCount > 0 
                        ? stats.TotalProcessingTimeMs / stats.ProcessedMessageCount 
                        : 0,
                    LastProcessedAt = stats.LastProcessedAt,
                    MessageTypes = stats.MessageTypes.ToDictionary(
                        mt => mt.Key,
                        mt => new MessageTypeStatsSnapshot
                        {
                            Published = mt.Value.Published,
                            ProcessedSuccess = mt.Value.ProcessedSuccess,
                            ProcessedFailed = mt.Value.ProcessedFailed
                        })
                };

                report.QueueStats[kvp.Key] = snapshot;
            }

            return report;
        }

        public void ResetStats()
        {
            _queueStats.Clear();
            _statsStartTime = DateTime.UtcNow;
        }
    }

    public class QueueStats
    {
        public long TotalPublished;
        public long TotalProcessedSuccess;
        public long TotalProcessedFailed;
        public double TotalProcessingTimeMs;
        public long ProcessedMessageCount;
        public DateTime? LastProcessedAt;
        public ConcurrentDictionary<string, MessageTypeStats> MessageTypes = new();
    }

    public class MessageTypeStats
    {
        public long Published;
        public long ProcessedSuccess;
        public long ProcessedFailed;
    }

    public class MessageStatsReport
    {
        public DateTime StatsStartTime { get; set; }
        public DateTime CurrentTime { get; set; }
        public TimeSpan TotalUptime => CurrentTime - StatsStartTime;
        public Dictionary<string, QueueStatsSnapshot> QueueStats { get; set; } = new();
        
        public long TotalPublishedAllQueues => QueueStats.Values.Sum(q => q.TotalPublished);
        public long TotalProcessedAllQueues => QueueStats.Values.Sum(q => q.TotalProcessedSuccess + q.TotalProcessedFailed);
        public long TotalSuccessAllQueues => QueueStats.Values.Sum(q => q.TotalProcessedSuccess);
        public long TotalFailedAllQueues => QueueStats.Values.Sum(q => q.TotalProcessedFailed);
        public double OverallSuccessRate => TotalProcessedAllQueues > 0 
            ? (double)TotalSuccessAllQueues / TotalProcessedAllQueues * 100 
            : 0;
    }

    public class QueueStatsSnapshot
    {
        public string QueueName { get; set; } = string.Empty;
        public long TotalPublished { get; set; }
        public long TotalProcessedSuccess { get; set; }
        public long TotalProcessedFailed { get; set; }
        public double AverageProcessingTimeMs { get; set; }
        public DateTime? LastProcessedAt { get; set; }
        public Dictionary<string, MessageTypeStatsSnapshot> MessageTypes { get; set; } = new();
        
        public long TotalProcessed => TotalProcessedSuccess + TotalProcessedFailed;
        public double SuccessRate => TotalProcessed > 0 ? (double)TotalProcessedSuccess / TotalProcessed * 100 : 0;
    }

    public class MessageTypeStatsSnapshot
    {
        public long Published { get; set; }
        public long ProcessedSuccess { get; set; }
        public long ProcessedFailed { get; set; }
        public long TotalProcessed => ProcessedSuccess + ProcessedFailed;
    }
}
