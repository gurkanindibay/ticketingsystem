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

        // Navigation properties - only Authentication service entities
        public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        
        // Note: EventTickets and EventTicketTransactions are in Ticketing service
        // Use UserId string property for cross-service references
    }
}
