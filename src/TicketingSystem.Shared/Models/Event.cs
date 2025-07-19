using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TicketingSystem.Shared.Models
{
    /// <summary>
    /// Event model for both PostgreSQL and Redis storage
    /// </summary>
    public class Event
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public DateTime Date { get; set; }

        public TimeSpan Duration { get; set; }

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }

        [Required]
        public int Capacity { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("location")] // B-tree index in PostgreSQL
        public string Location { get; set; } = string.Empty; // City only

        [MaxLength(100)]
        public string EventType { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<EventTicket> EventTickets { get; set; } = new List<EventTicket>();
        public virtual ICollection<EventTicketTransaction> EventTicketTransactions { get; set; } = new List<EventTicketTransaction>();
    }
}
