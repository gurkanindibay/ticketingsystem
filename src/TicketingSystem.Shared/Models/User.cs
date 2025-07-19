using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace TicketingSystem.Shared.Models
{
    /// <summary>
    /// User model for authentication and ticket purchases - inherits from IdentityUser
    /// </summary>
    public class User : IdentityUser
    {
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual ICollection<EventTicket> EventTickets { get; set; } = new List<EventTicket>();
        public virtual ICollection<EventTicketTransaction> EventTicketTransactions { get; set; } = new List<EventTicketTransaction>();
        public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    }
}
