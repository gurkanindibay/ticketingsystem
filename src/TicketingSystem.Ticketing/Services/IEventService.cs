using TicketingSystem.Shared.DTOs;

namespace TicketingSystem.Ticketing.Services
{
    /// <summary>
    /// Service interface for event management operations
    /// </summary>
    public interface IEventService
    {
        /// <summary>
        /// Search events based on criteria with caching
        /// </summary>
        Task<EventSearchResponse> SearchEventsAsync(EventSearchRequest request);
        
        /// <summary>
        /// Get event by ID with Redis caching
        /// </summary>
        Task<EventDto?> GetEventByIdAsync(int eventId);
        
        /// <summary>
        /// Create a new event (Admin only)
        /// </summary>
        Task<EventDto> CreateEventAsync(CreateEventRequest request);
        
        /// <summary>
        /// Update an existing event (Admin only)
        /// </summary>
        Task<EventDto?> UpdateEventAsync(int eventId, CreateEventRequest request);
        
        /// <summary>
        /// Delete an event (Admin only)
        /// </summary>
        Task<bool> DeleteEventAsync(int eventId);
        
        /// <summary>
        /// Get available tickets for an event
        /// </summary>
        Task<int> GetAvailableTicketsAsync(int eventId);
        
        /// <summary>
        /// Update event capacity after ticket purchase/refund
        /// </summary>
        Task UpdateEventCapacityAsync(int eventId, int capacityChange);
    }
}
