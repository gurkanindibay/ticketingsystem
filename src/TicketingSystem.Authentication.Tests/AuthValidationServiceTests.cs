using Microsoft.Extensions.Logging;
using Moq;
using TicketingSystem.Authentication.Services;
using TicketingSystem.Shared.DTOs;
using Xunit;

namespace TicketingSystem.Authentication.Tests
{
    public class AuthValidationServiceTests
    {
        private readonly AuthValidationService _validationService;
        private readonly Mock<ILogger<AuthValidationService>> _mockLogger;

        public AuthValidationServiceTests()
        {
            _mockLogger = new Mock<ILogger<AuthValidationService>>();
            _validationService = new AuthValidationService(_mockLogger.Object);
        }

        [Fact]
        public void ValidateRegistration_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = "test@example.com",
                Password = "SecureP@ssW0rd!",  // No sequential characters
                Username = "testuser",
                FirstName = "Test",
                LastName = "User"
            };

            // Act
            var result = _validationService.ValidateRegistration(request);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ValidateRegistration_InvalidEmail_ReturnsError()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = "invalid-email",
                Password = "SecureP@ss456!",
                Username = "testuser",
                FirstName = "Test",
                LastName = "User"
            };

            // Act
            var result = _validationService.ValidateRegistration(request);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("Invalid email format", result.Errors);
        }

        [Fact]
        public void ValidateRegistration_WeakPassword_ReturnsError()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = "test@example.com",
                Password = "weak",
                Username = "testuser",
                FirstName = "Test",
                LastName = "User"
            };

            // Act
            var result = _validationService.ValidateRegistration(request);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("Password must be at least 8 characters long", result.Errors);
        }

        [Fact]
        public void ValidateRegistration_CommonPassword_ReturnsError()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = "test@example.com",
                Password = "password123",
                Username = "testuser",
                FirstName = "Test",
                LastName = "User"
            };

            // Act
            var result = _validationService.ValidateRegistration(request);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("Password is too common. Please choose a more secure password", result.Errors);
        }

        [Fact]
        public void ValidateLogin_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var request = new LoginRequest
            {
                Email = "test@example.com",
                Password = "SecureP@ss456!"
            };

            // Act
            var result = _validationService.ValidateLogin(request);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ValidateLogin_InvalidEmail_ReturnsError()
        {
            // Arrange
            var request = new LoginRequest
            {
                Email = "invalid-email",
                Password = "SecureP@ss456!"
            };

            // Act
            var result = _validationService.ValidateLogin(request);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("Invalid email format", result.Errors);
        }

        [Fact]
        public void ValidateRefreshToken_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var request = new RefreshTokenRequest
            {
                AccessToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c",
                RefreshToken = "valid-refresh-token-12345-abcdef"
            };

            // Act
            var result = _validationService.ValidateRefreshToken(request);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ValidateRefreshToken_EmptyToken_ReturnsError()
        {
            // Arrange
            var request = new RefreshTokenRequest
            {
                RefreshToken = ""
            };

            // Act
            var result = _validationService.ValidateRefreshToken(request);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("Refresh token is required", result.Errors);
        }

        [Fact]
        public void ValidateLogout_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var request = new LogoutRequest
            {
                RefreshToken = "valid-refresh-token-12345"
            };

            // Act
            var result = _validationService.ValidateLogout(request);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }
    }
}
