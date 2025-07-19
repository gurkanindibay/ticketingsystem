using TicketingSystem.Shared.DTOs;

namespace TicketingSystem.Authentication.Services
{
    /// <summary>
    /// Interface for authentication-related validation services
    /// </summary>
    public interface IAuthValidationService
    {
        /// <summary>
        /// Validates user registration request
        /// </summary>
        /// <param name="request">Registration request to validate</param>
        /// <returns>Validation result with success status and error messages</returns>
        ValidationResult ValidateRegistration(RegisterRequest request);

        /// <summary>
        /// Validates user login request
        /// </summary>
        /// <param name="request">Login request to validate</param>
        /// <returns>Validation result with success status and error messages</returns>
        ValidationResult ValidateLogin(LoginRequest request);

        /// <summary>
        /// Validates refresh token request
        /// </summary>
        /// <param name="request">Refresh token request to validate</param>
        /// <returns>Validation result with success status and error messages</returns>
        ValidationResult ValidateRefreshToken(RefreshTokenRequest request);

        /// <summary>
        /// Validates logout request
        /// </summary>
        /// <param name="request">Logout request to validate</param>
        /// <returns>Validation result with success status and error messages</returns>
        ValidationResult ValidateLogout(LogoutRequest request);

        /// <summary>
        /// Validates email format
        /// </summary>
        /// <param name="email">Email address to validate</param>
        /// <returns>True if email format is valid</returns>
        bool IsValidEmail(string email);

        /// <summary>
        /// Validates password strength
        /// </summary>
        /// <param name="password">Password to validate</param>
        /// <returns>Validation result with success status and error messages</returns>
        ValidationResult ValidatePassword(string password);
    }

    /// <summary>
    /// Represents the result of a validation operation
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();

        public static ValidationResult Success() => new() { IsValid = true };
        
        public static ValidationResult Failure(params string[] errors) => new() 
        { 
            IsValid = false, 
            Errors = errors.ToList() 
        };

        public static ValidationResult Failure(List<string> errors) => new() 
        { 
            IsValid = false, 
            Errors = errors 
        };
    }
}
