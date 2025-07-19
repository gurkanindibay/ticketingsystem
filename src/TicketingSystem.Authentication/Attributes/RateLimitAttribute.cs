using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TicketingSystem.Authentication.Services;
using TicketingSystem.Shared.Utilities;

namespace TicketingSystem.Authentication.Attributes
{
    /// <summary>
    /// Rate limiting attribute for action methods
    /// </summary>
    public class RateLimitAttribute : ActionFilterAttribute
    {
        private readonly string _action;
        private readonly int _maxAttempts;
        private readonly int _windowMinutes;
        private readonly bool _useCustomLimits;

        /// <summary>
        /// Initialize rate limiting attribute with default configuration
        /// </summary>
        /// <param name="action">Action type for rate limiting (register, login, etc.)</param>
        public RateLimitAttribute(string action)
        {
            _action = action;
            _useCustomLimits = false;
        }

        /// <summary>
        /// Initialize rate limiting attribute with custom limits
        /// </summary>
        /// <param name="action">Action type for rate limiting</param>
        /// <param name="maxAttempts">Maximum attempts allowed</param>
        /// <param name="windowMinutes">Time window in minutes</param>
        public RateLimitAttribute(string action, int maxAttempts, int windowMinutes)
        {
            _action = action;
            _maxAttempts = maxAttempts;
            _windowMinutes = windowMinutes;
            _useCustomLimits = true;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var rateLimitService = context.HttpContext.RequestServices.GetRequiredService<IRateLimitService>();
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<RateLimitAttribute>>();

            // Get client IP address
            var clientIp = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            try
            {
                // Check if rate limit is exceeded
                bool isExceeded;
                if (_useCustomLimits)
                {
                    // Use custom rate limits if provided
                    isExceeded = await rateLimitService.IsRateLimitExceededAsync(clientIp, _action, _maxAttempts, _windowMinutes);
                }
                else
                {
                    // Use default configured rate limits
                    isExceeded = await rateLimitService.IsRateLimitExceededAsync(clientIp, _action);
                }

                if (isExceeded)
                {
                    logger.LogWarning("Rate limit exceeded for action: {Action}, IP: {ClientIp}", _action, clientIp);
                    
                    var response = ApiResponse<object>.ErrorResponse(
                        $"Too many {_action} attempts. Please try again later.", 
                        "RATE_LIMIT_EXCEEDED");

                    context.Result = new ObjectResult(response)
                    {
                        StatusCode = 429
                    };
                    return;
                }

                // Record this attempt
                if (_useCustomLimits)
                {
                    await rateLimitService.RecordAttemptAsync(clientIp, _action, _windowMinutes);
                }
                else
                {
                    await rateLimitService.RecordAttemptAsync(clientIp, _action);
                }

                // Continue to the action
                await next();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in rate limiting for action: {Action}, IP: {ClientIp}", _action, clientIp);
                // If rate limiting fails, continue with the request (fail open)
                await next();
            }
        }
    }
}
