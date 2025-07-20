using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using TicketingSystem.Shared.Models;

namespace TicketingSystem.Authentication.Data
{
    /// <summary>
    /// Database context for Authentication microservice - handles Identity and user-related data
    /// </summary>
    public class AuthDbContext : IdentityDbContext<User>
    {
        public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
        {
        }

        public DbSet<RefreshToken> RefreshTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configurations
            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(e => e.FirstName).HasMaxLength(100);
                entity.Property(e => e.LastName).HasMaxLength(100);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
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
