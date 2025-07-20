using Microsoft.AspNetCore.Mvc;
using TicketingSystem.Shared.DTOs;
using TicketingSystem.Shared.Utilities;
using TicketingSystem.Ticketing.Services;

namespace TicketingSystem.Ticketing.Controllers
{
    /// <summary>
    /// Tickets controller for ticket purchases, cancellations, and user ticket management
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class TicketsController : ControllerBase
    {
        private readonly ITicketService _ticketService;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly IMessageStatsService _messageStatsService;
        private readonly ILogger<TicketsController> _logger;

        public TicketsController(ITicketService ticketService, IRabbitMQService rabbitMQService, IMessageStatsService messageStatsService, ILogger<TicketsController> logger)
        {
            _ticketService = ticketService;
            _rabbitMQService = rabbitMQService;
            _messageStatsService = messageStatsService;
            _logger = logger;
        }
        /// <summary>
        /// Purchase tickets for an event (Requires authentication)
        /// </summary>
        /// <param name="request">Ticket purchase details including payment information</param>
        /// <returns>Purchase confirmation with ticket details and payment status</returns>
        /// <response code="200">Tickets purchased successfully</response>
        /// <response code="400">Invalid purchase request or insufficient tickets</response>
        /// <response code="401">Authentication required</response>
        /// <response code="404">Event not found</response>
        /// <response code="409">Tickets sold out or capacity exceeded</response>
        /// <response code="422">Payment processing failed</response>
        [HttpPost("purchase")]
        [ProducesResponseType(typeof(ApiResponse<PurchaseTicketResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 401)]
        [ProducesResponseType(typeof(ApiResponse<string>), 404)]
        [ProducesResponseType(typeof(ApiResponse<string>), 409)]
        [ProducesResponseType(typeof(ApiResponse<string>), 422)]
        public async Task<ActionResult<ApiResponse<PurchaseTicketResponse>>> PurchaseTickets([FromBody] PurchaseTicketRequest request)
        {
            try
            {
                _logger.LogInformation("Ticket purchase request received for event {EventId}, quantity {Quantity}",
                    request.EventId, request.Quantity);

                // TODO: Get user ID from authentication context when implemented
                var userId = "user_1"; // Mock user ID for now

                // Validate request
                if (request.EventId <= 0 || request.Quantity <= 0 || request.Quantity > 10)
                {
                    return BadRequest(ApiResponse<PurchaseTicketResponse>.ErrorResponse(
                        "Invalid request. Event ID must be positive and quantity must be between 1 and 10."));
                }

                // Check event availability first
                var (isAvailable, pricePerTicket, availableTickets) = await _ticketService.CheckEventAvailabilityAsync(request.EventId, request.Quantity);
                
                if (!isAvailable)
                {
                    if (availableTickets == 0)
                    {
                        return Conflict(ApiResponse<PurchaseTicketResponse>.ErrorResponse(
                            "Event is sold out or no longer available."));
                    }
                    else if (availableTickets < request.Quantity)
                    {
                        return Conflict(ApiResponse<PurchaseTicketResponse>.ErrorResponse(
                            $"Only {availableTickets} tickets available. Requested: {request.Quantity}"));
                    }
                    else
                    {
                        return NotFound(ApiResponse<PurchaseTicketResponse>.ErrorResponse(
                            "Event not found or no longer available."));
                    }
                }

                // Process ticket purchase
                var result = await _ticketService.PurchaseTicketsAsync(request, userId);

                return result.Status switch
                {
                    "Completed" => Ok(ApiResponse<PurchaseTicketResponse>.SuccessResponse(result, 
                        "Tickets purchased successfully")),
                    "Payment Failed" => UnprocessableEntity(ApiResponse<PurchaseTicketResponse>.ErrorResponse(
                        "Payment processing failed. Please check your payment details and try again.")),
                    "Failed" => Conflict(ApiResponse<PurchaseTicketResponse>.ErrorResponse(
                        "Ticket purchase failed due to availability")),
                    _ => StatusCode(500, ApiResponse<PurchaseTicketResponse>.ErrorResponse(
                        "An error occurred while processing your purchase"))
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ticket purchase for event {EventId}", request.EventId);
                return StatusCode(500, ApiResponse<PurchaseTicketResponse>.ErrorResponse(
                    "An unexpected error occurred while processing your purchase"));
            }
        }

        /// <summary>
        /// Get user's purchased tickets (Requires authentication)
        /// </summary>
        /// <param name="request">Pagination and filtering options</param>
        /// <returns>User's tickets with pagination</returns>
        /// <response code="200">Tickets retrieved successfully</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="401">Authentication required</response>
        [HttpGet("user")]
        [ProducesResponseType(typeof(ApiResponse<UserTicketsResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 401)]
        public async Task<ActionResult<ApiResponse<UserTicketsResponse>>> GetUserTickets([FromQuery] UserTicketsRequest request)
        {
            try
            {
                // TODO: Get user ID from authentication context when implemented
                var userId = "user_1"; // Mock user ID for now

                // Validate pagination parameters
                if (request.PageNumber < 1 || request.PageSize < 1 || request.PageSize > 100)
                {
                    return BadRequest(ApiResponse<UserTicketsResponse>.ErrorResponse(
                        "Invalid pagination parameters. Page number must be >= 1 and page size between 1 and 100."));
                }

                var result = await _ticketService.GetUserTicketsAsync(request, userId);
                return Ok(ApiResponse<UserTicketsResponse>.SuccessResponse(result, "Tickets retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user tickets");
                return StatusCode(500, ApiResponse<UserTicketsResponse>.ErrorResponse(
                    "An error occurred while retrieving your tickets"));
            }
        }

        /// <summary>
        /// Get ticket details by transaction ID (Requires authentication)
        /// </summary>
        /// <param name="transactionId">Transaction ID</param>
        /// <returns>Ticket details</returns>
        /// <response code="200">Ticket found</response>
        /// <response code="401">Authentication required</response>
        /// <response code="404">Ticket not found</response>
        [HttpGet("{transactionId}")]
        [ProducesResponseType(typeof(ApiResponse<PurchaseTicketResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 401)]
        [ProducesResponseType(typeof(ApiResponse<string>), 404)]
        public async Task<ActionResult<ApiResponse<PurchaseTicketResponse>>> GetTicketByTransaction(string transactionId)
        {
            try
            {
                // TODO: Get user ID from authentication context when implemented
                var userId = "user_1"; // Mock user ID for now

                if (string.IsNullOrWhiteSpace(transactionId))
                {
                    return BadRequest(ApiResponse<PurchaseTicketResponse>.ErrorResponse(
                        "Transaction ID is required"));
                }

                var result = await _ticketService.GetTicketByTransactionAsync(transactionId, userId);
                
                if (result == null)
                {
                    return NotFound(ApiResponse<PurchaseTicketResponse>.ErrorResponse(
                        "Ticket not found or you don't have permission to view it"));
                }

                return Ok(ApiResponse<PurchaseTicketResponse>.SuccessResponse(result, 
                    "Ticket details retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving ticket by transaction {TransactionId}", transactionId);
                return StatusCode(500, ApiResponse<PurchaseTicketResponse>.ErrorResponse(
                    "An error occurred while retrieving ticket details"));
            }
        }

        /// <summary>
        /// Cancel a ticket (Requires authentication)
        /// </summary>
        /// <param name="transactionId">Transaction ID of the ticket to cancel</param>
        /// <returns>Cancellation confirmation</returns>
        /// <response code="200">Ticket cancelled successfully</response>
        /// <response code="400">Ticket cannot be cancelled (e.g., event too close)</response>
        /// <response code="401">Authentication required</response>
        /// <response code="404">Ticket not found</response>
        [HttpDelete("{transactionId}")]
        [ProducesResponseType(typeof(ApiResponse), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 401)]
        [ProducesResponseType(typeof(ApiResponse<string>), 404)]
        public async Task<ActionResult<ApiResponse>> CancelTicket(string transactionId)
        {
            try
            {
                // TODO: Get user ID from authentication context when implemented
                var userId = "user_1"; // Mock user ID for now

                if (string.IsNullOrWhiteSpace(transactionId))
                {
                    return BadRequest(ApiResponse.ErrorResponse("Transaction ID is required"));
                }

                var result = await _ticketService.CancelTicketAsync(transactionId, userId);

                if (!result)
                {
                    return NotFound(ApiResponse.ErrorResponse(
                        "Ticket not found, cancellation not allowed, or you don't have permission to cancel this ticket"));
                }

                return Ok(ApiResponse.SuccessResponse("Ticket cancelled successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling ticket {TransactionId}", transactionId);
                return StatusCode(500, ApiResponse.ErrorResponse(
                    "An error occurred while cancelling the ticket"));
            }
        }

        /// <summary>
        /// Get RabbitMQ queue statistics for monitoring
        /// </summary>
        /// <returns>Queue status information</returns>
        [HttpGet("admin/queue-status")]
        public async Task<ActionResult<ApiResponse<object>>> GetQueueStatus()
        {
            try
            {
                var queueInfo = await _rabbitMQService.GetQueueStatsAsync();
                return Ok(new ApiResponse<Dictionary<string, object>> 
                { 
                    Success = true, 
                    Data = queueInfo, 
                    Message = "Queue status retrieved" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving queue status");
                return StatusCode(500, ApiResponse.ErrorResponse("Error retrieving queue status"));
            }
        }
    }
}
