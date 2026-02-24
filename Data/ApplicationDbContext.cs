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
    public DbSet<Business> Businesses { get; set; }
    public DbSet<ScrapingJob> ScrapingJobs { get; set; }
    public DbSet<Feedback> Feedbacks { get; set; }

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
            entity.Property(e => e.Address).HasColumnName("Address").HasMaxLength(255);
            entity.Property(e => e.City).HasColumnName("City").HasMaxLength(100);
            entity.Property(e => e.Country).HasColumnName("Country").HasMaxLength(100);
            entity.Property(e => e.Phone).HasColumnName("Phone").HasMaxLength(20);
            entity.Property(e => e.Website).HasColumnName("Website").HasMaxLength(255);
            entity.Property(e => e.UserType).HasColumnName("UserType");
            entity.Property(e => e.Credits).HasColumnName("Credits").HasDefaultValue(5);
            entity.Property(e => e.PackageType).HasColumnName("PackageType").HasMaxLength(50).HasDefaultValue("Free");
            entity.Property(e => e.Role).HasColumnName("Role").HasMaxLength(20).HasDefaultValue("User");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.LastLogin).HasColumnName("LastLogin");
        });

        // Configure Feedback entity
        modelBuilder.Entity<Feedback>(entity =>
        {
            entity.ToTable("Feedbacks");

            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.FullName).HasColumnName("FullName").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Email).HasColumnName("Email").HasMaxLength(150).IsRequired();
            entity.Property(e => e.Phone).HasColumnName("Phone").HasMaxLength(20);
            entity.Property(e => e.Subject).HasColumnName("Subject").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Message).HasColumnName("Message").HasMaxLength(2000).IsRequired();
            entity.Property(e => e.FeedbackType).HasColumnName("FeedbackType").HasMaxLength(50);
            entity.Property(e => e.IsRead).HasColumnName("IsRead").HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.RepliedAt).HasColumnName("RepliedAt");
            entity.Property(e => e.Reply).HasColumnName("Reply").HasMaxLength(2000);
            entity.Property(e => e.Status).HasColumnName("Status").HasMaxLength(50).HasDefaultValue("pending");

            // Create index on email for quick lookups
            entity.HasIndex(e => e.Email);
        });
    }
}
