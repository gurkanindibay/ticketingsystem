using System.Security.Cryptography;
using System.Text;

namespace TicketingSystem.Shared.Utilities
{
    /// <summary>
    /// Utility class for generating transaction IDs and other security tokens
    /// </summary>
    public static class SecurityHelper
    {
        /// <summary>
        /// Generates a secure transaction ID using HMACSHA512
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="eventId">Event ID</param>
        /// <param name="timestamp">Timestamp</param>
        /// <returns>Base64 encoded transaction ID</returns>
        public static string GenerateTransactionId(int userId, int eventId, DateTime timestamp)
        {
            var data = $"{userId}:{eventId}:{timestamp:yyyy-MM-ddTHH:mm:ss.fffZ}:{Guid.NewGuid()}";
            var key = GenerateRandomKey();
            
            using var hmac = new HMACSHA512(key);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hash).Replace("/", "_").Replace("+", "-");
        }

        /// <summary>
        /// Generates a secure refresh token
        /// </summary>
        /// <returns>Base64 encoded refresh token</returns>
        public static string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber).Replace("/", "_").Replace("+", "-");
        }

        /// <summary>
        /// Generates a random key for HMAC
        /// </summary>
        /// <returns>Random byte array</returns>
        private static byte[] GenerateRandomKey()
        {
            var key = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(key);
            return key;
        }

        /// <summary>
        /// Generates a Redis lock key for ticket purchases
        /// </summary>
        /// <param name="eventId">Event ID</param>
        /// <param name="eventDate">Event Date</param>
        /// <returns>Redis lock key</returns>
        public static string GenerateTicketLockKey(int eventId, DateTime eventDate)
        {
            return $"ticket_lock:{eventId}:{eventDate:yyyy-MM-dd}";
        }

        /// <summary>
        /// Generates a Redis cache key for events
        /// </summary>
        /// <param name="location">Event location</param>
        /// <param name="date">Event date (optional)</param>
        /// <returns>Redis cache key</returns>
        public static string GenerateEventCacheKey(string location, DateTime? date = null)
        {
            var dateStr = date?.ToString("yyyy-MM-dd") ?? "all";
            return $"events:{location.ToLower()}:{dateStr}";
        }
    }
}
