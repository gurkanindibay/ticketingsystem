using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using TicketingSystem.Authentication.Services;
using TicketingSystem.Shared.DTOs;
using TicketingSystem.Shared.Utilities;

namespace TicketingSystem.Authentication.Controllers
{
    /// <summary>
    /// Authentication controller for user registration, login, and token management
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IAuthValidationService _validationService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IUserService userService, IAuthValidationService validationService, ILogger<AuthController> logger)
        {
            _userService = userService;
            _validationService = validationService;
            _logger = logger;
        }

        /// <summary>
        /// Register a new user
        /// </summary>
        /// <param name="request">User registration details</param>
        /// <returns>User registration response</returns>
        /// <response code="200">Registration successful</response>
        /// <response code="400">Invalid registration data</response>
        /// <response code="409">User already exists</response>
        /// <response code="429">Too many registration attempts</response>
        [HttpPost("register")]
        [EnableRateLimiting("register")] // 5 attempts per hour (from config)
        [ProducesResponseType(typeof(ApiResponse<UserDto>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 409)]
        [ProducesResponseType(typeof(ApiResponse<string>), 429)]
        public async Task<ActionResult<ApiResponse<UserDto>>> Register([FromBody] RegisterRequest request)
        {
            // Security: Log registration attempt with IP address
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            _logger.LogInformation("Registration attempt from IP: {ClientIp}, Email: {Email}", clientIp, request.Email);

            // Validate request using validation service
            var validationResult = _validationService.ValidateRegistration(request);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Registration validation failed for email: {Email}, IP: {ClientIp}, Errors: {Errors}", 
                    request.Email, clientIp, string.Join(", ", validationResult.Errors));
                return BadRequest(ApiResponse<UserDto>.ErrorResponse("Registration validation failed", validationResult.Errors));
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(ApiResponse<UserDto>.ErrorResponse("Invalid registration data", errors));
            }

            var result = await _userService.RegisterAsync(request);
            
            if (!result.Success)
            {
                // Security: Log failed registration attempts
                _logger.LogWarning("Registration failed for email: {Email}, IP: {ClientIp}, Errors: {Errors}", 
                    request.Email, clientIp, string.Join(", ", result.Errors));
                
                return result.Errors.Any(e => e.Contains("already exists")) 
                    ? Conflict(result) 
                    : BadRequest(result);
            }

            // Security: Log successful registration (without sensitive data)
            _logger.LogInformation("User registered successfully: {Email}, IP: {ClientIp}", request.Email, clientIp);
            return Ok(result);
        }

        /// <summary>
        /// Authenticate a user and return JWT tokens
        /// </summary>
        /// <param name="request">User login credentials</param>
        /// <returns>Authentication response with JWT tokens</returns>
        /// <response code="200">Login successful</response>
        /// <response code="400">Invalid credentials</response>
        /// <response code="401">Authentication failed</response>
        /// <response code="429">Too many login attempts</response>
        [HttpPost("login")]
        [EnableRateLimiting("login")] // 10 attempts per 15 minutes (from config)
        [ProducesResponseType(typeof(ApiResponse<AuthenticationResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 401)]
        [ProducesResponseType(typeof(ApiResponse<string>), 429)]
        public async Task<ActionResult<ApiResponse<AuthenticationResponse>>> Login([FromBody] LoginRequest request)
        {
            // Security: Log login attempt with IP address
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            _logger.LogInformation("Login attempt from IP: {ClientIp}, Email: {Email}", clientIp, request.Email);

            // Validate request using validation service
            var validationResult = _validationService.ValidateLogin(request);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Login validation failed for email: {Email}, IP: {ClientIp}, Errors: {Errors}", 
                    request.Email, clientIp, string.Join(", ", validationResult.Errors));
                return BadRequest(ApiResponse<AuthenticationResponse>.ErrorResponse("Login validation failed", validationResult.Errors));
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(ApiResponse<AuthenticationResponse>.ErrorResponse("Invalid login data", errors));
            }

            var result = await _userService.LoginAsync(request);
            
            if (!result.Success)
            {
                // Security: Log failed login attempts (potential brute force)
                _logger.LogWarning("Login failed for email: {Email}, IP: {ClientIp}, Errors: {Errors}", 
                    request.Email, clientIp, string.Join(", ", result.Errors));
                
                return result.Errors.Any(e => e.Contains("INVALID_CREDENTIALS")) 
                    ? Unauthorized(result) 
                    : BadRequest(result);
            }

            // Security: Log successful login (without sensitive data)
            _logger.LogInformation("User logged in successfully: {Email}, IP: {ClientIp}", request.Email, clientIp);
            return Ok(result);
        }

        /// <summary>
        /// Refresh access token using refresh token
        /// </summary>
        /// <param name="request">Refresh token request</param>
        /// <returns>New authentication response with fresh JWT tokens</returns>
        /// <response code="200">Token refresh successful</response>
        /// <response code="400">Invalid refresh token</response>
        /// <response code="401">Refresh token expired or revoked</response>
        /// <response code="429">Too many refresh attempts</response>
        [HttpPost("refresh")]
        [EnableRateLimiting("refresh")] // 20 attempts per 5 minutes (from config)
        [ProducesResponseType(typeof(ApiResponse<AuthenticationResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 401)]
        [ProducesResponseType(typeof(ApiResponse<string>), 429)]
        public async Task<ActionResult<ApiResponse<AuthenticationResponse>>> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            // Validate request using validation service
            var validationResult = _validationService.ValidateRefreshToken(request);
            if (!validationResult.IsValid)
            {
                return BadRequest(ApiResponse<AuthenticationResponse>.ErrorResponse("Refresh token validation failed", validationResult.Errors));
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(ApiResponse<AuthenticationResponse>.ErrorResponse("Invalid refresh token data", errors));
            }

            var result = await _userService.RefreshTokenAsync(request);
            
            if (!result.Success)
            {
                return result.Errors.Any(e => e.Contains("INVALID_REFRESH_TOKEN") || e.Contains("INVALID_TOKEN")) 
                    ? Unauthorized(result) 
                    : BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Logout user and revoke refresh token
        /// </summary>
        /// <param name="request">Refresh token to revoke</param>
        /// <returns>Logout confirmation</returns>
        /// <response code="200">Logout successful</response>
        /// <response code="400">Invalid refresh token</response>
        [HttpPost("logout")]
        [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        public async Task<ActionResult<ApiResponse<bool>>> Logout([FromBody] LogoutRequest request)
        {
            // Validate request using validation service
            var validationResult = _validationService.ValidateLogout(request);
            if (!validationResult.IsValid)
            {
                return BadRequest(ApiResponse<bool>.ErrorResponse("Logout validation failed", validationResult.Errors));
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(ApiResponse<bool>.ErrorResponse("Invalid logout data", errors));
            }

            var result = await _userService.LogoutAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// Register a new admin user (Development/Testing only)
        /// </summary>
        /// <param name="request">Admin user registration details</param>
        /// <returns>Admin user registration response</returns>
        /// <response code="200">Admin registration successful</response>
        /// <response code="400">Invalid registration data</response>
        /// <response code="409">User already exists</response>
        [HttpPost("register-admin")]
        [ProducesResponseType(typeof(ApiResponse<UserDto>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 409)]
        public async Task<ActionResult<ApiResponse<UserDto>>> RegisterAdmin([FromBody] RegisterRequest request)
        {
            // Security: Log admin registration attempt with IP address
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            _logger.LogInformation("Admin registration attempt from IP: {ClientIp}, Email: {Email}", clientIp, request.Email);

            // Validate request using validation service
            var validationResult = _validationService.ValidateRegistration(request);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Admin registration validation failed for email: {Email}, IP: {ClientIp}, Errors: {Errors}", 
                    request.Email, clientIp, string.Join(", ", validationResult.Errors));
                return BadRequest(ApiResponse<UserDto>.ErrorResponse("Admin registration validation failed", validationResult.Errors));
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(ApiResponse<UserDto>.ErrorResponse("Invalid admin registration data", errors));
            }

            var result = await _userService.RegisterAdminAsync(request);
            
            if (!result.Success)
            {
                // Security: Log failed admin registration attempts
                _logger.LogWarning("Admin registration failed for email: {Email}, IP: {ClientIp}, Errors: {Errors}", 
                    request.Email, clientIp, string.Join(", ", result.Errors));
                
                return result.Errors.Any(e => e.Contains("already exists")) 
                    ? Conflict(result) 
                    : BadRequest(result);
            }

            // Security: Log successful admin registration (without sensitive data)
            _logger.LogInformation("Admin user registered successfully: {Email}, IP: {ClientIp}", request.Email, clientIp);
            return Ok(result);
        }

        /// <summary>
        /// Get current user profile information
        /// </summary>
        /// <returns>Current user profile</returns>
        /// <response code="200">User profile retrieved successfully</response>
        /// <response code="401">User not authenticated</response>
        /// <response code="404">User not found</response>
        [HttpGet("profile")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<UserDto>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 401)]
        [ProducesResponseType(typeof(ApiResponse<string>), 404)]
        public async Task<ActionResult<ApiResponse<UserDto>>> GetProfile()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(ApiResponse<UserDto>.ErrorResponse("User not authenticated", "NOT_AUTHENTICATED"));
            }

            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound(ApiResponse<UserDto>.ErrorResponse("User not found", "USER_NOT_FOUND"));
            }

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

            return Ok(ApiResponse<UserDto>.SuccessResponse(userDto, "User profile retrieved successfully"));
        }
    }
}
