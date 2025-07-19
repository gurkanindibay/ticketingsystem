using Microsoft.EntityFrameworkCore;
using TicketingSystem.Shared.Models;

namespace TicketingSystem.Shared.Data
{
    /// <summary>
    /// Main database context for PostgreSQL
    /// </summary>
    public class TicketingDbContext : DbContext
    {
        public TicketingDbContext(DbContextOptions<TicketingDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<EventTicket> EventTickets { get; set; }
        public DbSet<EventTicketTransaction> EventTicketTransactions { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configurations
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.Username).IsUnique();
                entity.Property(e => e.Email).IsRequired();
                entity.Property(e => e.Username).IsRequired();
                entity.Property(e => e.PasswordHash).IsRequired();
            });

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
                
                entity.HasOne(et => et.Event)
                    .WithMany(e => e.EventTickets)
                    .HasForeignKey(et => et.EventId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(et => et.User)
                    .WithMany(u => u.EventTickets)
                    .HasForeignKey(et => et.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // EventTicketTransaction configurations
            modelBuilder.Entity<EventTicketTransaction>(entity =>
            {
                entity.HasIndex(e => e.TransactionId).IsUnique();
                entity.HasIndex(e => new { e.UserId, e.EventId });
                entity.HasIndex(e => e.Status);
                entity.Property(e => e.TransactionId).IsRequired();
                entity.Property(e => e.Amount).HasPrecision(18, 2);
                
                entity.HasOne(ett => ett.Event)
                    .WithMany(e => e.EventTicketTransactions)
                    .HasForeignKey(ett => ett.EventId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ett => ett.User)
                    .WithMany(u => u.EventTicketTransactions)
                    .HasForeignKey(ett => ett.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // RefreshToken configurations
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasIndex(e => e.Token).IsUnique();
                entity.HasIndex(e => new { e.UserId, e.IsRevoked });
                entity.Property(e => e.Token).IsRequired();
                
                entity.HasOne(rt => rt.User)
                    .WithMany(u => u.RefreshTokens)
                    .HasForeignKey(rt => rt.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
