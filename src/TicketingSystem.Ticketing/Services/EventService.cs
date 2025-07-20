using Microsoft.EntityFrameworkCore;
using TicketingSystem.Shared.Data;
using TicketingSystem.Shared.DTOs;
using TicketingSystem.Shared.Models;

namespace TicketingSystem.Ticketing.Services
{
    /// <summary>
    /// Service implementation for event management with Redis caching and database operations
    /// </summary>
    public class EventService : IEventService
    {
        private readonly TicketingDbContext _context;
        private readonly IRedisService _redisService;
        private readonly ILogger<EventService> _logger;

        public EventService(TicketingDbContext context, IRedisService redisService, ILogger<EventService> logger)
        {
            _context = context;
            _redisService = redisService;
            _logger = logger;
        }

        /// <summary>
        /// Search events with filtering, sorting, and caching
        /// </summary>
        public async Task<EventSearchResponse> SearchEventsAsync(EventSearchRequest request)
        {
            try
            {
                // Build query with filters
                var query = _context.Events.AsQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(request.Location))
                {
                    query = query.Where(e => e.Location.Contains(request.Location));
                }

                if (!string.IsNullOrEmpty(request.EventName))
                {
                    query = query.Where(e => e.Name.Contains(request.EventName));
                }

                if (!string.IsNullOrEmpty(request.EventType))
                {
                    query = query.Where(e => e.EventType == request.EventType);
                }

                if (request.DateFrom.HasValue)
                {
                    query = query.Where(e => e.Date >= request.DateFrom.Value);
                }

                if (request.DateTo.HasValue)
                {
                    query = query.Where(e => e.Date <= request.DateTo.Value);
                }

                // Apply sorting
                query = request.SortBy?.ToLower() switch
                {
                    "name" => request.SortDescending ? query.OrderByDescending(e => e.Name) : query.OrderBy(e => e.Name),
                    "location" => request.SortDescending ? query.OrderByDescending(e => e.Location) : query.OrderBy(e => e.Location),
                    "capacity" => request.SortDescending ? query.OrderByDescending(e => e.Capacity) : query.OrderBy(e => e.Capacity),
                    _ => request.SortDescending ? query.OrderByDescending(e => e.Date) : query.OrderBy(e => e.Date)
                };

                // Get total count for pagination
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

                // Apply pagination
                var events = await query
                    .Skip((request.PageNumber - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToListAsync();

                // Convert to DTOs and calculate available tickets
                var eventDtos = new List<EventDto>();
                foreach (var eventEntity in events)
                {
                    var availableTickets = await GetAvailableTicketsAsync(eventEntity.Id);
                    eventDtos.Add(MapToDto(eventEntity, availableTickets));
                }

                var response = new EventSearchResponse
                {
                    Events = eventDtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize,
                    TotalPages = totalPages
                };

                _logger.LogInformation("Event search completed: {TotalCount} events found", totalCount);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching events");
                throw;
            }
        }

        /// <summary>
        /// Get event by ID with Redis caching
        /// </summary>
        public async Task<EventDto?> GetEventByIdAsync(int eventId)
        {
            try
            {
                // Try to get from Redis cache first
                var cachedEvent = await _redisService.GetEventAsync(eventId);
                if (cachedEvent != null)
                {
                    var availableTickets = await GetAvailableTicketsAsync(eventId);
                    _logger.LogDebug("Event {EventId} retrieved from cache", eventId);
                    return MapToDto(cachedEvent, availableTickets);
                }

                // Get from database
                var eventEntity = await _context.Events.FindAsync(eventId);
                if (eventEntity == null)
                {
                    return null;
                }

                // Cache the event in Redis
                await _redisService.SetEventAsync(eventEntity);

                var availableTicketsFromDb = await GetAvailableTicketsAsync(eventId);
                _logger.LogDebug("Event {EventId} retrieved from database and cached", eventId);
                
                return MapToDto(eventEntity, availableTicketsFromDb);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting event {EventId}", eventId);
                throw;
            }
        }

        /// <summary>
        /// Create a new event
        /// </summary>
        public async Task<EventDto> CreateEventAsync(CreateEventRequest request)
        {
            try
            {
                var eventEntity = new Event
                {
                    Name = request.Name,
                    Date = request.Date,
                    Duration = request.Duration,
                    StartTime = request.StartTime,
                    EndTime = request.StartTime.Add(request.Duration),
                    Capacity = request.Capacity,
                    Location = request.Location,
                    EventType = request.EventType
                };

                _context.Events.Add(eventEntity);
                await _context.SaveChangesAsync();

                // Cache the new event
                await _redisService.SetEventAsync(eventEntity);

                _logger.LogInformation("Event created: {EventId} - {EventName}", eventEntity.Id, eventEntity.Name);
                return MapToDto(eventEntity, request.Capacity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating event: {EventName}", request.Name);
                throw;
            }
        }

        /// <summary>
        /// Update an existing event
        /// </summary>
        public async Task<EventDto?> UpdateEventAsync(int eventId, CreateEventRequest request)
        {
            try
            {
                var eventEntity = await _context.Events.FindAsync(eventId);
                if (eventEntity == null)
                {
                    return null;
                }

                eventEntity.Name = request.Name;
                eventEntity.Date = request.Date;
                eventEntity.Duration = request.Duration;
                eventEntity.StartTime = request.StartTime;
                eventEntity.EndTime = request.StartTime.Add(request.Duration);
                eventEntity.Capacity = request.Capacity;
                eventEntity.Location = request.Location;
                eventEntity.EventType = request.EventType;

                await _context.SaveChangesAsync();

                // Update cache
                await _redisService.SetEventAsync(eventEntity);

                var availableTickets = await GetAvailableTicketsAsync(eventId);
                
                _logger.LogInformation("Event updated: {EventId} - {EventName}", eventId, eventEntity.Name);
                return MapToDto(eventEntity, availableTickets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating event: {EventId}", eventId);
                throw;
            }
        }

        /// <summary>
        /// Delete an event
        /// </summary>
        public async Task<bool> DeleteEventAsync(int eventId)
        {
            try
            {
                var eventEntity = await _context.Events.FindAsync(eventId);
                if (eventEntity == null)
                {
                    return false;
                }

                // Check if there are any tickets sold for this event
                var ticketCount = await _context.EventTickets.CountAsync(t => t.EventId == eventId);
                if (ticketCount > 0)
                {
                    throw new InvalidOperationException("Cannot delete event with existing ticket sales");
                }

                _context.Events.Remove(eventEntity);
                await _context.SaveChangesAsync();

                // Remove from cache
                await _redisService.DeleteEventAsync(eventId);

                _logger.LogInformation("Event deleted: {EventId} - {EventName}", eventId, eventEntity.Name);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting event: {EventId}", eventId);
                throw;
            }
        }

        /// <summary>
        /// Get available tickets for an event
        /// </summary>
        public async Task<int> GetAvailableTicketsAsync(int eventId)
        {
            try
            {
                // Try to get from Redis cache first for better performance
                var cachedCapacity = await _redisService.GetEventCapacityAsync(eventId);
                if (cachedCapacity.HasValue)
                {
                    return cachedCapacity.Value;
                }

                // Calculate from database
                var eventEntity = await _context.Events.FindAsync(eventId);
                if (eventEntity == null)
                {
                    return 0;
                }

                var soldTickets = await _context.EventTickets.CountAsync(t => t.EventId == eventId);
                var availableTickets = eventEntity.Capacity - soldTickets;

                // Cache the result
                await _redisService.SetEventCapacityAsync(eventId, availableTickets);

                return Math.Max(0, availableTickets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available tickets for event {EventId}", eventId);
                throw;
            }
        }

        /// <summary>
        /// Update event capacity after ticket operations
        /// </summary>
        public async Task UpdateEventCapacityAsync(int eventId, int capacityChange)
        {
            try
            {
                // Update Redis cache
                await _redisService.UpdateEventCapacityAsync(eventId, capacityChange);
                
                _logger.LogDebug("Event capacity updated: {EventId}, change: {CapacityChange}", eventId, capacityChange);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating event capacity: {EventId}, change: {CapacityChange}", eventId, capacityChange);
                throw;
            }
        }

        /// <summary>
        /// Map Event entity to EventDto
        /// </summary>
        private static EventDto MapToDto(Event eventEntity, int availableTickets)
        {
            return new EventDto
            {
                Id = eventEntity.Id,
                Name = eventEntity.Name,
                Date = eventEntity.Date,
                Duration = eventEntity.Duration,
                StartTime = eventEntity.StartTime,
                EndTime = eventEntity.EndTime,
                Capacity = eventEntity.Capacity,
                Location = eventEntity.Location,
                EventType = eventEntity.EventType,
                AvailableTickets = availableTickets
            };
        }
    }
}
