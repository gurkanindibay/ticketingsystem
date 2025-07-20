using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TicketingSystem.Shared.DTOs;
using TicketingSystem.Shared.Utilities;
using TicketingSystem.Ticketing.Services;

namespace TicketingSystem.Ticketing.Controllers
{
    /// <summary>
    /// Events controller for managing events and event listings
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class EventsController : ControllerBase
    {
        private readonly IEventService _eventService;
        private readonly ILogger<EventsController> _logger;

        public EventsController(IEventService eventService, ILogger<EventsController> logger)
        {
            _eventService = eventService;
            _logger = logger;
        }

        /// <summary>
        /// Search and list events with filtering options
        /// </summary>
        /// <param name="request">Event search criteria</param>
        /// <returns>Paginated list of events matching the search criteria</returns>
        /// <response code="200">Events retrieved successfully</response>
        /// <response code="400">Invalid search parameters</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<EventSearchResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        public async Task<ActionResult<ApiResponse<EventSearchResponse>>> SearchEvents([FromQuery] EventSearchRequest request)
        {
            try
            {
                _logger.LogInformation("Searching events with criteria: {Request}", System.Text.Json.JsonSerializer.Serialize(request));
                
                var response = await _eventService.SearchEventsAsync(request);
                
                _logger.LogInformation("Found {Count} events", response.TotalCount);
                return Ok(ApiResponse<EventSearchResponse>.SuccessResponse(response, "Events retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching events");
                return BadRequest(ApiResponse<EventSearchResponse>.ErrorResponse("Error retrieving events"));
            }
        }

        /// <summary>
        /// Get event details by ID
        /// </summary>
        /// <param name="id">Event ID</param>
        /// <returns>Event details</returns>
        /// <response code="200">Event found</response>
        /// <response code="404">Event not found</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<EventDto>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 404)]
        public async Task<ActionResult<ApiResponse<EventDto>>> GetEvent(int id)
        {
            try
            {
                _logger.LogInformation("Getting event with ID: {EventId}", id);
                
                var eventDto = await _eventService.GetEventByIdAsync(id);
                
                if (eventDto == null)
                {
                    _logger.LogWarning("Event not found: {EventId}", id);
                    return NotFound(ApiResponse<EventDto>.ErrorResponse("Event not found"));
                }

                return Ok(ApiResponse<EventDto>.SuccessResponse(eventDto, "Event retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting event {EventId}", id);
                return BadRequest(ApiResponse<EventDto>.ErrorResponse("Error retrieving event"));
            }
        }

        /// <summary>
        /// Create a new event (Admin only)
        /// </summary>
        /// <param name="request">Event creation details</param>
        /// <returns>Created event details</returns>
        /// <response code="201">Event created successfully</response>
        /// <response code="400">Invalid event data</response>
        /// <response code="401">Unauthorized - Admin access required</response>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ApiResponse<EventDto>), 201)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 401)]
        public async Task<ActionResult<ApiResponse<EventDto>>> CreateEvent([FromBody] CreateEventRequest request)
        {
            try
            {
                _logger.LogInformation("Creating new event: {EventName}", request.Name);
                
                var eventDto = await _eventService.CreateEventAsync(request);
                
                _logger.LogInformation("Event created successfully: {EventId}", eventDto.Id);
                return CreatedAtAction(nameof(GetEvent), new { id = eventDto.Id },
                    ApiResponse<EventDto>.SuccessResponse(eventDto, "Event created successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating event: {EventName}", request.Name);
                return BadRequest(ApiResponse<EventDto>.ErrorResponse("Error creating event"));
            }
        }

        /// <summary>
        /// Update an existing event (Admin only)
        /// </summary>
        /// <param name="id">Event ID</param>
        /// <param name="request">Updated event details</param>
        /// <returns>Updated event details</returns>
        /// <response code="200">Event updated successfully</response>
        /// <response code="400">Invalid event data</response>
        /// <response code="401">Unauthorized - Admin access required</response>
        /// <response code="404">Event not found</response>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ApiResponse<EventDto>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 401)]
        [ProducesResponseType(typeof(ApiResponse<string>), 404)]
        public async Task<ActionResult<ApiResponse<EventDto>>> UpdateEvent(int id, [FromBody] CreateEventRequest request)
        {
            try
            {
                _logger.LogInformation("Updating event: {EventId}", id);
                
                var eventDto = await _eventService.UpdateEventAsync(id, request);
                
                if (eventDto == null)
                {
                    _logger.LogWarning("Event not found for update: {EventId}", id);
                    return NotFound(ApiResponse<EventDto>.ErrorResponse("Event not found"));
                }

                _logger.LogInformation("Event updated successfully: {EventId}", id);
                return Ok(ApiResponse<EventDto>.SuccessResponse(eventDto, "Event updated successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating event: {EventId}", id);
                return BadRequest(ApiResponse<EventDto>.ErrorResponse("Error updating event"));
            }
        }

        /// <summary>
        /// Delete an event (Admin only)
        /// </summary>
        /// <param name="id">Event ID</param>
        /// <returns>Deletion confirmation</returns>
        /// <response code="200">Event deleted successfully</response>
        /// <response code="401">Unauthorized - Admin access required</response>
        /// <response code="404">Event not found</response>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ApiResponse), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 401)]
        [ProducesResponseType(typeof(ApiResponse<string>), 404)]
        public async Task<ActionResult<ApiResponse>> DeleteEvent(int id)
        {
            try
            {
                _logger.LogInformation("Deleting event: {EventId}", id);
                
                var success = await _eventService.DeleteEventAsync(id);
                
                if (!success)
                {
                    _logger.LogWarning("Event not found for deletion: {EventId}", id);
                    return NotFound(ApiResponse.ErrorResponse("Event not found"));
                }

                _logger.LogInformation("Event deleted successfully: {EventId}", id);
                return Ok(ApiResponse.SuccessResponse("Event deleted successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting event: {EventId}", id);
                return BadRequest(ApiResponse.ErrorResponse("Error deleting event"));
            }
        }
    }
}
