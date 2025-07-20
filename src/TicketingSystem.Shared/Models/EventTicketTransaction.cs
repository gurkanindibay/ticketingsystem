using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TicketingSystem.Shared.Models
{
    /// <summary>
    /// Transaction status enumeration
    /// </summary>
    public enum TransactionStatus
    {
        Pending,
        Completed,
        Failed,
        Cancelled
    }

    /// <summary>
    /// Event ticket transactions model for both PostgreSQL and Redis storage
    /// </summary>
    public class EventTicketTransaction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int EventId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)]
        public string TransactionId { get; set; } = string.Empty; // HMACSHA512 generated

        [Required]
        public DateTime EventDate { get; set; }

        [Required]
        public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

        public decimal Amount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Event Event { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}
