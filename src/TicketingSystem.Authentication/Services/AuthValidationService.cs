using System.Net.Mail;
using System.Text.RegularExpressions;
using TicketingSystem.Shared.DTOs;

namespace TicketingSystem.Authentication.Services
{
    /// <summary>
    /// Service for authentication-related validation logic
    /// </summary>
    public class AuthValidationService : IAuthValidationService
    {
        private readonly ILogger<AuthValidationService> _logger;

        public AuthValidationService(ILogger<AuthValidationService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Validates user registration request
        /// </summary>
        public ValidationResult ValidateRegistration(RegisterRequest request)
        {
            var errors = new List<string>();

            try
            {
                // Check required fields
                if (string.IsNullOrWhiteSpace(request.Email))
                {
                    errors.Add("Email is required");
                }

                if (string.IsNullOrWhiteSpace(request.Password))
                {
                    errors.Add("Password is required");
                }

                if (string.IsNullOrWhiteSpace(request.FirstName))
                {
                    errors.Add("First name is required");
                }

                if (string.IsNullOrWhiteSpace(request.LastName))
                {
                    errors.Add("Last name is required");
                }

                // Validate email format
                if (!string.IsNullOrWhiteSpace(request.Email) && !IsValidEmail(request.Email))
                {
                    errors.Add("Invalid email format");
                }

                // Validate password strength
                if (!string.IsNullOrWhiteSpace(request.Password))
                {
                    var passwordValidation = ValidatePassword(request.Password);
                    if (!passwordValidation.IsValid)
                    {
                        errors.AddRange(passwordValidation.Errors);
                    }
                }

                // Validate name lengths
                if (!string.IsNullOrWhiteSpace(request.FirstName) && request.FirstName.Length > 50)
                {
                    errors.Add("First name must not exceed 50 characters");
                }

                if (!string.IsNullOrWhiteSpace(request.LastName) && request.LastName.Length > 50)
                {
                    errors.Add("Last name must not exceed 50 characters");
                }

                // Check for potentially malicious input
                if (ContainsMaliciousContent(request.Email) || 
                    ContainsMaliciousContent(request.FirstName) || 
                    ContainsMaliciousContent(request.LastName))
                {
                    errors.Add("Invalid characters detected in input");
                }

                return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating registration request");
                return ValidationResult.Failure("Validation error occurred");
            }
        }

        /// <summary>
        /// Validates user login request
        /// </summary>
        public ValidationResult ValidateLogin(LoginRequest request)
        {
            var errors = new List<string>();

            try
            {
                // Check required fields
                if (string.IsNullOrWhiteSpace(request.Email))
                {
                    errors.Add("Email is required");
                }

                if (string.IsNullOrWhiteSpace(request.Password))
                {
                    errors.Add("Password is required");
                }

                // Validate email format
                if (!string.IsNullOrWhiteSpace(request.Email) && !IsValidEmail(request.Email))
                {
                    errors.Add("Invalid email format");
                }

                // Check for potentially malicious input
                if (ContainsMaliciousContent(request.Email))
                {
                    errors.Add("Invalid characters detected in email");
                }

                return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating login request");
                return ValidationResult.Failure("Validation error occurred");
            }
        }

        /// <summary>
        /// Validates refresh token request
        /// </summary>
        public ValidationResult ValidateRefreshToken(RefreshTokenRequest request)
        {
            var errors = new List<string>();

            try
            {
                // Check required fields
                if (string.IsNullOrWhiteSpace(request.RefreshToken))
                {
                    errors.Add("Refresh token is required");
                }

                if (string.IsNullOrWhiteSpace(request.AccessToken))
                {
                    errors.Add("Access token is required");
                }

                // Validate token format (basic checks)
                if (!string.IsNullOrWhiteSpace(request.RefreshToken) && request.RefreshToken.Length < 10)
                {
                    errors.Add("Invalid refresh token format");
                }

                if (!string.IsNullOrWhiteSpace(request.AccessToken) && !request.AccessToken.Contains('.'))
                {
                    errors.Add("Invalid access token format");
                }

                return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating refresh token request");
                return ValidationResult.Failure("Validation error occurred");
            }
        }

        /// <summary>
        /// Validates logout request
        /// </summary>
        public ValidationResult ValidateLogout(LogoutRequest request)
        {
            var errors = new List<string>();

            try
            {
                // Check required fields
                if (string.IsNullOrWhiteSpace(request.RefreshToken))
                {
                    errors.Add("Refresh token is required");
                }

                // Validate token format (basic checks)
                if (!string.IsNullOrWhiteSpace(request.RefreshToken) && request.RefreshToken.Length < 10)
                {
                    errors.Add("Invalid refresh token format");
                }

                return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating logout request");
                return ValidationResult.Failure("Validation error occurred");
            }
        }

        /// <summary>
        /// Validates email format using multiple approaches
        /// </summary>
        public bool IsValidEmail(string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                    return false;

                // Trim whitespace
                email = email.Trim();

                // Check length
                if (email.Length > 254) // RFC 5321 limit
                    return false;

                // Use MailAddress for validation
                var mailAddress = new MailAddress(email);
                
                // Additional checks
                if (mailAddress.Address != email)
                    return false;

                // Check for common issues
                if (email.Contains("..") || // Double dots
                    email.StartsWith(".") || // Leading dot
                    email.EndsWith(".") ||   // Trailing dot
                    email.Contains(" "))     // Spaces
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates password strength according to security requirements
        /// </summary>
        public ValidationResult ValidatePassword(string password)
        {
            var errors = new List<string>();

            try
            {
                if (string.IsNullOrWhiteSpace(password))
                {
                    errors.Add("Password is required");
                    return ValidationResult.Failure(errors);
                }

                // Length requirements
                if (password.Length < 8)
                {
                    errors.Add("Password must be at least 8 characters long");
                }

                if (password.Length > 128)
                {
                    errors.Add("Password must not exceed 128 characters");
                }

                // Character requirements
                if (!password.Any(char.IsUpper))
                {
                    errors.Add("Password must contain at least one uppercase letter");
                }

                if (!password.Any(char.IsLower))
                {
                    errors.Add("Password must contain at least one lowercase letter");
                }

                if (!password.Any(char.IsDigit))
                {
                    errors.Add("Password must contain at least one number");
                }

                if (!password.Any(c => "!@#$%^&*()_+-=[]{}|;:,.<>?".Contains(c)))
                {
                    errors.Add("Password must contain at least one special character");
                }

                // Common password checks
                if (IsCommonPassword(password))
                {
                    errors.Add("Password is too common. Please choose a more secure password");
                }

                // Sequential or repeated character checks
                if (HasSequentialCharacters(password))
                {
                    errors.Add("Password should not contain sequential characters (e.g., 123, abc)");
                }

                if (HasRepeatedCharacters(password))
                {
                    errors.Add("Password should not contain too many repeated characters");
                }

                return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating password");
                return ValidationResult.Failure("Password validation error occurred");
            }
        }

        /// <summary>
        /// Checks for potentially malicious content
        /// </summary>
        private static bool ContainsMaliciousContent(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            // Check for common injection patterns
            var maliciousPatterns = new[]
            {
                @"<script[^>]*>",
                @"javascript:",
                @"vbscript:",
                @"on\w+\s*=",
                @"eval\s*\(",
                @"expression\s*\(",
                @"--",
                @"\/\*",
                @"\*\/",
                @"xp_",
                @"sp_"
            };

            return maliciousPatterns.Any(pattern => 
                Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase));
        }

        /// <summary>
        /// Checks if password is in list of common passwords
        /// </summary>
        private static bool IsCommonPassword(string password)
        {
            var commonPasswords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "password", "123456", "password123", "admin", "qwerty",
                "letmein", "welcome", "monkey", "dragon", "master",
                "123456789", "1234567890", "password1", "123123",
                "abc123", "Password1", "iloveyou", "1234567", "123321"
            };

            return commonPasswords.Contains(password);
        }

        /// <summary>
        /// Checks for sequential characters (123, abc, etc.)
        /// </summary>
        private static bool HasSequentialCharacters(string password)
        {
            for (int i = 0; i < password.Length - 2; i++)
            {
                var char1 = password[i];
                var char2 = password[i + 1];
                var char3 = password[i + 2];

                // Check for ascending sequence
                if (char2 == char1 + 1 && char3 == char2 + 1)
                    return true;

                // Check for descending sequence
                if (char2 == char1 - 1 && char3 == char2 - 1)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks for too many repeated characters
        /// </summary>
        private static bool HasRepeatedCharacters(string password)
        {
            // Check for more than 2 consecutive repeated characters
            for (int i = 0; i < password.Length - 2; i++)
            {
                if (password[i] == password[i + 1] && password[i + 1] == password[i + 2])
                    return true;
            }

            return false;
        }
    }
}
