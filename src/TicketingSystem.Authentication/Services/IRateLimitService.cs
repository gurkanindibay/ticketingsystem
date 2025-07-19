namespace TicketingSystem.Authentication.Services
{
    /// <summary>
    /// Rate limiting service interface for preventing abuse
    /// </summary>
    public interface IRateLimitService
    {
        /// <summary>
        /// Check if the rate limit is exceeded for a given key and action
        /// </summary>
        /// <param name="key">Unique identifier (IP address, user ID, etc.)</param>
        /// <param name="action">Action type (register, login, etc.)</param>
        /// <returns>True if rate limit is exceeded</returns>
        Task<bool> IsRateLimitExceededAsync(string key, string action);

        /// <summary>
        /// Check if the rate limit is exceeded with custom limits
        /// </summary>
        /// <param name="key">Unique identifier</param>
        /// <param name="action">Action type</param>
        /// <param name="maxAttempts">Maximum attempts allowed</param>
        /// <param name="windowMinutes">Time window in minutes</param>
        /// <returns>True if rate limit is exceeded</returns>
        Task<bool> IsRateLimitExceededAsync(string key, string action, int maxAttempts, int windowMinutes);

        /// <summary>
        /// Record an attempt for rate limiting
        /// </summary>
        /// <param name="key">Unique identifier</param>
        /// <param name="action">Action type</param>
        Task RecordAttemptAsync(string key, string action);

        /// <summary>
        /// Record an attempt for rate limiting with custom window
        /// </summary>
        /// <param name="key">Unique identifier</param>
        /// <param name="action">Action type</param>
        /// <param name="windowMinutes">Time window in minutes</param>
        Task RecordAttemptAsync(string key, string action, int windowMinutes);

        /// <summary>
        /// Clear rate limit data for a key and action
        /// </summary>
        /// <param name="key">Unique identifier</param>
        /// <param name="action">Action type</param>
        Task ClearRateLimitAsync(string key, string action);
    }
}
