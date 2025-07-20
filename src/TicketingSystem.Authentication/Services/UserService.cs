using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TicketingSystem.Authentication.Data;
using TicketingSystem.Shared.DTOs;
using TicketingSystem.Shared.Models;
using TicketingSystem.Shared.Utilities;

namespace TicketingSystem.Authentication.Services
{
    /// <summary>
    /// User service implementation for authentication operations
    /// </summary>
    public class UserService : IUserService
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IJwtService _jwtService;
        private readonly AuthDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserService> _logger;

        public UserService(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IJwtService jwtService,
            AuthDbContext context,
            IConfiguration configuration,
            ILogger<UserService> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtService = jwtService;
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Register a new user
        /// </summary>
        public async Task<ApiResponse<UserDto>> RegisterAsync(RegisterRequest request)
        {
            try
            {
                // Check if user already exists
                var existingUser = await _userManager.FindByEmailAsync(request.Email);
                if (existingUser != null)
                {
                    return ApiResponse<UserDto>.ErrorResponse("User with this email already exists", "DUPLICATE_EMAIL");
                }

                existingUser = await _userManager.FindByNameAsync(request.Username);
                if (existingUser != null)
                {
                    return ApiResponse<UserDto>.ErrorResponse("User with this username already exists", "DUPLICATE_USERNAME");
                }

                // Create new user
                var user = new User
                {
                    UserName = request.Username,
                    Email = request.Email,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                var result = await _userManager.CreateAsync(user, request.Password);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogWarning("Failed to create user {Email}: {Errors}", request.Email, errors);
                    return ApiResponse<UserDto>.ErrorResponse($"Failed to create user: {errors}", "CREATION_FAILED");
                }

                // Add default role
                await _userManager.AddToRoleAsync(user, "User");

                var userDto = new UserDto
                {
                    Id = user.Id,
                    Username = user.UserName!,
                    Email = user.Email!,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt
                };

                _logger.LogInformation("Successfully registered user {Email} with ID {UserId}", request.Email, user.Id);
                return ApiResponse<UserDto>.SuccessResponse(userDto, "User registered successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering user {Email}", request.Email);
                return ApiResponse<UserDto>.ErrorResponse("An error occurred during registration", "INTERNAL_ERROR");
            }
        }

        /// <summary>
        /// Register a new admin user (Development/Testing only)
        /// </summary>
        public async Task<ApiResponse<UserDto>> RegisterAdminAsync(RegisterRequest request)
        {
            try
            {
                _logger.LogInformation("Attempting to register admin user {Email}", request.Email);

                // Check if user already exists
                var existingUser = await _userManager.FindByEmailAsync(request.Email);
                if (existingUser != null)
                {
                    _logger.LogWarning("Admin registration failed - user already exists: {Email}", request.Email);
                    return ApiResponse<UserDto>.ErrorResponse("User with this email already exists", "USER_EXISTS");
                }

                // Create new admin user
                var user = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    UserName = request.Email,
                    Email = request.Email,
                    EmailConfirmed = true, // Auto-confirm for development
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, request.Password);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogWarning("Failed to create admin user {Email}: {Errors}", request.Email, errors);
                    return ApiResponse<UserDto>.ErrorResponse($"Failed to create admin user: {errors}", "CREATION_FAILED");
                }

                // Add admin role
                await _userManager.AddToRoleAsync(user, "Admin");

                var userDto = new UserDto
                {
                    Id = user.Id,
                    Username = user.UserName!,
                    Email = user.Email!,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt
                };

                _logger.LogInformation("Successfully registered admin user {Email} with ID {UserId}", request.Email, user.Id);
                return ApiResponse<UserDto>.SuccessResponse(userDto, "Admin user registered successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering admin user {Email}", request.Email);
                return ApiResponse<UserDto>.ErrorResponse("An error occurred during admin registration", "INTERNAL_ERROR");
            }
        }

        /// <summary>
        /// Authenticate user login
        /// </summary>
        public async Task<ApiResponse<AuthenticationResponse>> LoginAsync(LoginRequest request)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                {
                    _logger.LogWarning("Login attempt with non-existent email: {Email}", request.Email);
                    return ApiResponse<AuthenticationResponse>.ErrorResponse("Invalid email or password", "INVALID_CREDENTIALS");
                }

                if (!user.IsActive)
                {
                    _logger.LogWarning("Login attempt for inactive user: {Email}", request.Email);
                    return ApiResponse<AuthenticationResponse>.ErrorResponse("User account is inactive", "ACCOUNT_INACTIVE");
                }

                var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
                if (!result.Succeeded)
                {
                    _logger.LogWarning("Failed login attempt for user: {Email}", request.Email);
                    return ApiResponse<AuthenticationResponse>.ErrorResponse("Invalid email or password", "INVALID_CREDENTIALS");
                }

                // Get user roles
                var roles = await _userManager.GetRolesAsync(user);

                // Generate tokens
                var accessToken = _jwtService.GenerateAccessToken(user, roles);
                var refreshToken = _jwtService.GenerateRefreshToken();

                // Save refresh token
                var refreshTokenDays = int.Parse(_configuration["JwtSettings:RefreshTokenExpirationDays"] ?? "7");
                var refreshTokenEntity = new RefreshToken
                {
                    Token = refreshToken,
                    UserId = user.Id,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenDays),
                    IsRevoked = false
                };

                _context.RefreshTokens.Add(refreshTokenEntity);
                await _context.SaveChangesAsync();

                var response = new AuthenticationResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddHours(1), // Access token expires in 1 hour
                    User = new UserDto
                    {
                        Id = user.Id,
                        Username = user.UserName!,
                        Email = user.Email!,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        IsActive = user.IsActive,
                        CreatedAt = user.CreatedAt
                    }
                };

                _logger.LogInformation("Successful login for user {Email} with ID {UserId}", request.Email, user.Id);
                return ApiResponse<AuthenticationResponse>.SuccessResponse(response, "Login successful");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for user {Email}", request.Email);
                return ApiResponse<AuthenticationResponse>.ErrorResponse("An error occurred during login", "INTERNAL_ERROR");
            }
        }

        /// <summary>
        /// Refresh JWT token using refresh token
        /// </summary>
        public async Task<ApiResponse<AuthenticationResponse>> RefreshTokenAsync(RefreshTokenRequest request)
        {
            try
            {
                var principal = _jwtService.GetPrincipalFromExpiredToken(request.AccessToken);
                if (principal == null)
                {
                    return ApiResponse<AuthenticationResponse>.ErrorResponse("Invalid access token", "INVALID_TOKEN");
                }

                var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return ApiResponse<AuthenticationResponse>.ErrorResponse("Invalid token claims", "INVALID_TOKEN");
                }

                var refreshTokenEntity = await _context.RefreshTokens
                    .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken && rt.UserId == userId && !rt.IsRevoked);

                if (refreshTokenEntity == null || refreshTokenEntity.ExpiresAt <= DateTime.UtcNow)
                {
                    return ApiResponse<AuthenticationResponse>.ErrorResponse("Invalid or expired refresh token", "INVALID_REFRESH_TOKEN");
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null || !user.IsActive)
                {
                    return ApiResponse<AuthenticationResponse>.ErrorResponse("User not found or inactive", "USER_NOT_FOUND");
                }

                // Get user roles
                var roles = await _userManager.GetRolesAsync(user);

                // Generate new tokens
                var newAccessToken = _jwtService.GenerateAccessToken(user, roles);
                var newRefreshToken = _jwtService.GenerateRefreshToken();

                // Revoke old refresh token and create new one
                refreshTokenEntity.IsRevoked = true;
                refreshTokenEntity.RevokedAt = DateTime.UtcNow;

                var refreshTokenDays = int.Parse(_configuration["JwtSettings:RefreshTokenExpirationDays"] ?? "7");
                var newRefreshTokenEntity = new RefreshToken
                {
                    Token = newRefreshToken,
                    UserId = user.Id,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenDays),
                    IsRevoked = false
                };

                _context.RefreshTokens.Add(newRefreshTokenEntity);
                await _context.SaveChangesAsync();

                var response = new AuthenticationResponse
                {
                    AccessToken = newAccessToken,
                    RefreshToken = newRefreshToken,
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    User = new UserDto
                    {
                        Id = user.Id,
                        Username = user.UserName!,
                        Email = user.Email!,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        IsActive = user.IsActive,
                        CreatedAt = user.CreatedAt
                    }
                };

                _logger.LogInformation("Token refreshed for user {UserId}", userId);
                return ApiResponse<AuthenticationResponse>.SuccessResponse(response, "Token refreshed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return ApiResponse<AuthenticationResponse>.ErrorResponse("An error occurred during token refresh", "INTERNAL_ERROR");
            }
        }

        /// <summary>
        /// Logout user and revoke refresh token
        /// </summary>
        public async Task<ApiResponse<bool>> LogoutAsync(LogoutRequest request)
        {
            try
            {
                var refreshTokenEntity = await _context.RefreshTokens
                    .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken && !rt.IsRevoked);

                if (refreshTokenEntity != null)
                {
                    refreshTokenEntity.IsRevoked = true;
                    refreshTokenEntity.RevokedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("User logged out and refresh token revoked");
                return ApiResponse<bool>.SuccessResponse(true, "Logout successful");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return ApiResponse<bool>.ErrorResponse("An error occurred during logout", "INTERNAL_ERROR");
            }
        }

        /// <summary>
        /// Get user by ID
        /// </summary>
        public async Task<User?> GetUserByIdAsync(string userId)
        {
            return await _userManager.FindByIdAsync(userId);
        }
    }
}
