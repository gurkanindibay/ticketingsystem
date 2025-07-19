using Microsoft.Extensions.Caching.Memory;

namespace TicketingSystem.Authentication.Services
{
    /// <summary>
    /// In-memory rate limiting service implementation
    /// For production, consider using Redis for distributed scenarios
    /// </summary>
    public class InMemoryRateLimitService : IRateLimitService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<InMemoryRateLimitService> _logger;
        private readonly IConfiguration _configuration;

        // Rate limit configurations
        private readonly Dictionary<string, RateLimitConfig> _rateLimits;

        public InMemoryRateLimitService(IMemoryCache cache, ILogger<InMemoryRateLimitService> logger, IConfiguration configuration)
        {
            _cache = cache;
            _logger = logger;
            _configuration = configuration;

            // Configure rate limits for different actions
            _rateLimits = new Dictionary<string, RateLimitConfig>
            {
                ["register"] = new RateLimitConfig
                {
                    MaxAttempts = int.Parse(_configuration["RateLimiting:Registration:MaxAttempts"] ?? "5"),
                    WindowMinutes = int.Parse(_configuration["RateLimiting:Registration:WindowMinutes"] ?? "60")
                },
                ["login"] = new RateLimitConfig
                {
                    MaxAttempts = int.Parse(_configuration["RateLimiting:Login:MaxAttempts"] ?? "10"),
                    WindowMinutes = int.Parse(_configuration["RateLimiting:Login:WindowMinutes"] ?? "15")
                },
                ["refresh"] = new RateLimitConfig
                {
                    MaxAttempts = int.Parse(_configuration["RateLimiting:Refresh:MaxAttempts"] ?? "20"),
                    WindowMinutes = int.Parse(_configuration["RateLimiting:Refresh:WindowMinutes"] ?? "5")
                }
            };
        }

        public async Task<bool> IsRateLimitExceededAsync(string key, string action)
        {
            try
            {
                if (!_rateLimits.TryGetValue(action, out var config))
                {
                    // No rate limit configured for this action
                    return false;
                }

                return await IsRateLimitExceededAsync(key, action, config.MaxAttempts, config.WindowMinutes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking rate limit for key: {Key}, action: {Action}", key, action);
                // In case of error, allow the request (fail open)
                return false;
            }
        }

        public async Task<bool> IsRateLimitExceededAsync(string key, string action, int maxAttempts, int windowMinutes)
        {
            try
            {
                var cacheKey = GetCacheKey(key, action);
                var attempts = _cache.Get<List<DateTime>>(cacheKey) ?? new List<DateTime>();

                // Remove expired attempts
                var cutoff = DateTime.UtcNow.AddMinutes(-windowMinutes);
                attempts = attempts.Where(attempt => attempt > cutoff).ToList();

                // Check if limit exceeded
                var isExceeded = attempts.Count >= maxAttempts;

                if (isExceeded)
                {
                    _logger.LogWarning("Rate limit exceeded for key: {Key}, action: {Action}, attempts: {Attempts}, limit: {Limit}",
                        key, action, attempts.Count, maxAttempts);
                }

                return await Task.FromResult(isExceeded);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking rate limit for key: {Key}, action: {Action}", key, action);
                // In case of error, allow the request (fail open)
                return false;
            }
        }

        public async Task RecordAttemptAsync(string key, string action)
        {
            try
            {
                if (!_rateLimits.TryGetValue(action, out var config))
                {
                    return;
                }

                await RecordAttemptAsync(key, action, config.WindowMinutes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording rate limit attempt for key: {Key}, action: {Action}", key, action);
            }
        }

        public async Task RecordAttemptAsync(string key, string action, int windowMinutes)
        {
            try
            {
                var cacheKey = GetCacheKey(key, action);
                var attempts = _cache.Get<List<DateTime>>(cacheKey) ?? new List<DateTime>();

                // Add current attempt
                attempts.Add(DateTime.UtcNow);

                // Remove expired attempts
                var cutoff = DateTime.UtcNow.AddMinutes(-windowMinutes);
                attempts = attempts.Where(attempt => attempt > cutoff).ToList();

                // Store updated attempts with expiration
                var expiration = TimeSpan.FromMinutes(windowMinutes);
                _cache.Set(cacheKey, attempts, expiration);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording rate limit attempt for key: {Key}, action: {Action}", key, action);
            }
        }

        public async Task ClearRateLimitAsync(string key, string action)
        {
            try
            {
                var cacheKey = GetCacheKey(key, action);
                _cache.Remove(cacheKey);
                
                _logger.LogInformation("Rate limit cleared for key: {Key}, action: {Action}", key, action);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing rate limit for key: {Key}, action: {Action}", key, action);
            }
        }

        private static string GetCacheKey(string key, string action)
        {
            return $"rate_limit:{action}:{key}";
        }

        private class RateLimitConfig
        {
            public int MaxAttempts { get; set; }
            public int WindowMinutes { get; set; }
        }
    }
}
