namespace TicketingSystem.Shared.Configuration
{
    /// <summary>
    /// JWT configuration settings
    /// </summary>
    public class JwtSettings
    {
        public string SecretKey { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public int AccessTokenExpirationMinutes { get; set; } = 15;
        public int RefreshTokenExpirationDays { get; set; } = 7;
    }

    /// <summary>
    /// Database configuration settings
    /// </summary>
    public class DatabaseSettings
    {
        public string PostgreSQLConnectionString { get; set; } = string.Empty;
        public bool EnableSensitiveDataLogging { get; set; } = false;
        public int CommandTimeout { get; set; } = 30;
    }

    /// <summary>
    /// Redis configuration settings
    /// </summary>
    public class RedisSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
        public int Database { get; set; } = 0;
        public string KeyPrefix { get; set; } = "ticketing:";
        public int DefaultExpirationMinutes { get; set; } = 60;
    }

    /// <summary>
    /// RabbitMQ configuration settings
    /// </summary>
    public class RabbitMQSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string Username { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public string VirtualHost { get; set; } = "/";
        public string TicketPurchaseQueue { get; set; } = "ticket.purchase";
        public string EventUpdateQueue { get; set; } = "event.update";
    }

    /// <summary>
    /// Distributed lock configuration settings
    /// </summary>
    public class DistributedLockSettings
    {
        public string RedisConnectionString { get; set; } = string.Empty;
        public int DefaultLockTimeoutSeconds { get; set; } = 30;
        public int DefaultRetryDelayMs { get; set; } = 100;
        public int DefaultRetryCount { get; set; } = 3;
    }
}
