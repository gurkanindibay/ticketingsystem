using System.ComponentModel.DataAnnotations;

namespace TicketingSystem.Shared.DTOs
{
    /// <summary>
    /// Event DTO for API responses
    /// </summary>
    public class EventDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public TimeSpan Duration { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int Capacity { get; set; }
        public string Location { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public int AvailableTickets { get; set; }
    }

    /// <summary>
    /// Event search request DTO
    /// </summary>
    public class EventSearchRequest
    {
        public string? Location { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? EventType { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    /// <summary>
    /// Create event request DTO
    /// </summary>
    public class CreateEventRequest
    {
        [Required]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public TimeSpan Duration { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Capacity { get; set; }

        [Required]
        [StringLength(100)]
        public string Location { get; set; } = string.Empty;

        [StringLength(100)]
        public string EventType { get; set; } = string.Empty;
    }

    /// <summary>
    /// Event search response DTO
    /// </summary>
    public class EventSearchResponse
    {
        public List<EventDto> Events { get; set; } = new List<EventDto>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}
