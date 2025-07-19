using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        private readonly ILogger<AuthController> _logger;

        public AuthController(IUserService userService, ILogger<AuthController> logger)
        {
            _userService = userService;
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
        [HttpPost("register")]
        [ProducesResponseType(typeof(ApiResponse<UserDto>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 409)]
        public async Task<ActionResult<ApiResponse<UserDto>>> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(ApiResponse<UserDto>.ErrorResponse("Invalid registration data", errors));
            }

            var result = await _userService.RegisterAsync(request);
            
            if (!result.Success)
            {
                return result.Errors.Any(e => e.Contains("already exists")) 
                    ? Conflict(result) 
                    : BadRequest(result);
            }

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
        [HttpPost("login")]
        [ProducesResponseType(typeof(ApiResponse<AuthenticationResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 401)]
        public async Task<ActionResult<ApiResponse<AuthenticationResponse>>> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(ApiResponse<AuthenticationResponse>.ErrorResponse("Invalid login data", errors));
            }

            var result = await _userService.LoginAsync(request);
            
            if (!result.Success)
            {
                return result.Errors.Any(e => e.Contains("INVALID_CREDENTIALS")) 
                    ? Unauthorized(result) 
                    : BadRequest(result);
            }

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
        [HttpPost("refresh")]
        [ProducesResponseType(typeof(ApiResponse<AuthenticationResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 401)]
        public async Task<ActionResult<ApiResponse<AuthenticationResponse>>> RefreshToken([FromBody] RefreshTokenRequest request)
        {
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
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(ApiResponse<bool>.ErrorResponse("Invalid logout data", errors));
            }

            var result = await _userService.LogoutAsync(request);
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
