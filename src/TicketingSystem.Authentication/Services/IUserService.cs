using TicketingSystem.Shared.DTOs;
using TicketingSystem.Shared.Models;
using TicketingSystem.Shared.Utilities;

namespace TicketingSystem.Authentication.Services
{
    /// <summary>
    /// Interface for user authentication operations
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// Register a new user
        /// </summary>
        /// <param name="request">Registration request</param>
        /// <returns>API response with user data</returns>
        Task<ApiResponse<UserDto>> RegisterAsync(RegisterRequest request);

        /// <summary>
        /// Authenticate user login
        /// </summary>
        /// <param name="request">Login request</param>
        /// <returns>API response with authentication result</returns>
        Task<ApiResponse<AuthenticationResponse>> LoginAsync(LoginRequest request);

        /// <summary>
        /// Refresh JWT token using refresh token
        /// </summary>
        /// <param name="request">Refresh token request</param>
        /// <returns>API response with new tokens</returns>
        Task<ApiResponse<AuthenticationResponse>> RefreshTokenAsync(RefreshTokenRequest request);

        /// <summary>
        /// Logout user and revoke refresh token
        /// </summary>
        /// <param name="request">Logout request</param>
        /// <returns>API response</returns>
        Task<ApiResponse<bool>> LogoutAsync(LogoutRequest request);

        /// <summary>
        /// Get user by ID
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>User or null if not found</returns>
        Task<User?> GetUserByIdAsync(string userId);
    }
}
