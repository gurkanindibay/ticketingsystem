using Microsoft.EntityFrameworkCore;
using TicketingSystem.Shared.Models;

namespace TicketingSystem.Ticketing.Data
{
    /// <summary>
    /// Database context for Ticketing microservice - handles events and ticket-related data
    /// </summary>
    public class TicketingDbContext : DbContext
    {
        public TicketingDbContext(DbContextOptions<TicketingDbContext> options) : base(options)
        {
        }

        public DbSet<Event> Events { get; set; }
        public DbSet<EventTicket> EventTickets { get; set; }
        public DbSet<EventTicketTransaction> EventTicketTransactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Event configurations with B-tree index for location-based queries
            modelBuilder.Entity<Event>(entity =>
            {
                entity.HasIndex(e => e.Location);
                entity.HasIndex(e => e.Date);
                entity.HasIndex(e => new { e.Location, e.Date });
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.Location).IsRequired();
            });

            // EventTicket configurations
            modelBuilder.Entity<EventTicket>(entity =>
            {
                entity.HasIndex(e => e.TransactionId).IsUnique();
                entity.HasIndex(e => new { e.UserId, e.EventId });
                entity.Property(e => e.TransactionId).IsRequired();
                entity.Property(e => e.UserId).IsRequired(); // String reference to User ID from Auth service
                
                entity.HasOne(et => et.Event)
                    .WithMany(e => e.EventTickets)
                    .HasForeignKey(et => et.EventId)
                    .OnDelete(DeleteBehavior.Cascade);

                // No foreign key constraint to User - UserId is a string reference to Auth service
                entity.Ignore(et => et.User);
            });

            // EventTicketTransaction configurations
            modelBuilder.Entity<EventTicketTransaction>(entity =>
            {
                entity.HasIndex(e => e.TransactionId).IsUnique();
                entity.HasIndex(e => new { e.UserId, e.EventId });
                entity.HasIndex(e => e.Status);
                entity.Property(e => e.TransactionId).IsRequired();
                entity.Property(e => e.Amount).HasPrecision(18, 2);
                entity.Property(e => e.UserId).IsRequired(); // String reference to User ID from Auth service
                
                entity.HasOne(ett => ett.Event)
                    .WithMany(e => e.EventTicketTransactions)
                    .HasForeignKey(ett => ett.EventId)
                    .OnDelete(DeleteBehavior.Cascade);

                // No foreign key constraint to User - UserId is a string reference to Auth service
                entity.Ignore(ett => ett.User);
            });
        }
    }
}
