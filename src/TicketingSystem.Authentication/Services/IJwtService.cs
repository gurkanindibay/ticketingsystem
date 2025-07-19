using TicketingSystem.Shared.Models;

namespace TicketingSystem.Authentication.Services
{
    /// <summary>
    /// Interface for JWT token operations
    /// </summary>
    public interface IJwtService
    {
        /// <summary>
        /// Generate JWT access token for the user
        /// </summary>
        /// <param name="user">User to generate token for</param>
        /// <param name="roles">User roles</param>
        /// <returns>JWT access token</returns>
        string GenerateAccessToken(User user, IList<string> roles);

        /// <summary>
        /// Generate refresh token
        /// </summary>
        /// <returns>Refresh token string</returns>
        string GenerateRefreshToken();

        /// <summary>
        /// Get principal from expired token (for refresh token validation)
        /// </summary>
        /// <param name="token">Expired JWT token</param>
        /// <returns>ClaimsPrincipal if valid, null otherwise</returns>
        System.Security.Claims.ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);

        /// <summary>
        /// Generate HMACSHA512 transaction ID
        /// </summary>
        /// <param name="data">Data to hash</param>
        /// <returns>Transaction ID</returns>
        string GenerateTransactionId(string data);
    }
}
