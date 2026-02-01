using Microsoft.EntityFrameworkCore;
using TradeScout.API.Models;

namespace TradeScout.API.Data;

/// <summary>
/// Application database context for TradeScout
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User entity to map to existing table
        modelBuilder.Entity<User>(entity =>
        {
            // Map to existing table name
            entity.ToTable("Users");

            // Configure Email as unique
            entity.HasIndex(e => e.Email)
                .IsUnique()
                .HasDatabaseName("Users_Email_key");

            // Explicit column mappings (PostgreSQL kullanıyor)
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.FullName).HasColumnName("FullName").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Email).HasColumnName("Email").HasMaxLength(150).IsRequired();
            entity.Property(e => e.PasswordHash).HasColumnName("PasswordHash").IsRequired();
            entity.Property(e => e.CompanyName).HasColumnName("CompanyName").HasMaxLength(150);
            entity.Property(e => e.Credits).HasColumnName("Credits").HasDefaultValue(5);
            entity.Property(e => e.PackageType).HasColumnName("PackageType").HasMaxLength(50).HasDefaultValue("Free");
            entity.Property(e => e.Role).HasColumnName("Role").HasMaxLength(20).HasDefaultValue("User");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.LastLogin).HasColumnName("LastLogin");
        });
    }
}
