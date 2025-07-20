using System.ComponentModel.DataAnnotations;

namespace TicketingSystem.Shared.Models
{
    /// <summary>
    /// Event tickets model for both PostgreSQL and Redis storage
    /// </summary>
    public class EventTicket
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public int EventId { get; set; }

        [Required]
        public DateTime EventDate { get; set; }

        [Required]
        [MaxLength(128)]
        public string TransactionId { get; set; } = string.Empty; // HMACSHA512 generated

        public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Event Event { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}
