using System.ComponentModel.DataAnnotations;

namespace TicketingSystem.Shared.DTOs
{
    /// <summary>
    /// Ticket purchase request DTO
    /// </summary>
    public class PurchaseTicketRequest
    {
        [Required]
        public int EventId { get; set; }

        [Required]
        public DateTime EventDate { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; } = 1;

        [Required]
        public PaymentRequest PaymentDetails { get; set; } = new PaymentRequest();
    }

    /// <summary>
    /// Ticket purchase response DTO
    /// </summary>
    public class PurchaseTicketResponse
    {
        public string TransactionId { get; set; } = string.Empty;
        public List<TicketDto> Tickets { get; set; } = new List<TicketDto>();
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public PaymentResponse? PaymentDetails { get; set; }
    }

    /// <summary>
    /// Ticket DTO
    /// </summary>
    public class TicketDto
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int EventId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public string TransactionId { get; set; } = string.Empty;
        public DateTime PurchasedAt { get; set; }
        public string Location { get; set; } = string.Empty;
    }

    /// <summary>
    /// User tickets request DTO
    /// </summary>
    public class UserTicketsRequest
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    /// <summary>
    /// User tickets response DTO
    /// </summary>
    public class UserTicketsResponse
    {
        public List<TicketDto> Tickets { get; set; } = new List<TicketDto>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}
