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
    public DbSet<MarketAnalysis> MarketAnalyses { get; set; }
    public DbSet<PaymentHistory> PaymentHistories { get; set; }
    public DbSet<DiscountCode> DiscountCodes { get; set; }  // ✅ İndirim kodları tablosu
    public DbSet<Subscription> Subscriptions { get; set; } // ✅ Paratika abonelik takibi

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ✅ Tüm DateTime alanlarını otomatik olarak UTC'ye dönüştüren konfigürasyon
        var dateTimeConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc));

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(dateTimeConverter);
                }
            }
        }

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
            entity.Property(e => e.Credits).HasColumnName("Credits").HasDefaultValue(0);
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

        // Configure MarketAnalysis entity
        modelBuilder.Entity<MarketAnalysis>(entity =>
        {
            entity.ToTable("MarketAnalyses");

            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.UserId).HasColumnName("UserId");
            entity.Property(e => e.HsCode).HasColumnName("HsCode").HasMaxLength(20).IsRequired();
            entity.Property(e => e.ProductName).HasColumnName("ProductName").HasMaxLength(255).IsRequired();
            entity.Property(e => e.TargetCountry).HasColumnName("TargetCountry").HasMaxLength(100).IsRequired();
            entity.Property(e => e.OriginCountry).HasColumnName("OriginCountry").HasMaxLength(100).HasDefaultValue("Türkiye");
            entity.Property(e => e.ReportContent).HasColumnName("ReportContent");
            entity.Property(e => e.IsSuccessful).HasColumnName("IsSuccessful").HasDefaultValue(true);
            entity.Property(e => e.ErrorMessage).HasColumnName("ErrorMessage").HasMaxLength(500);
            entity.Property(e => e.PdfDownloaded).HasColumnName("PdfDownloaded").HasDefaultValue(false);
            entity.Property(e => e.PdfDownloadedAt).HasColumnName("PdfDownloadedAt");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IpAddress).HasColumnName("IpAddress").HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasColumnName("UserAgent").HasMaxLength(500);
            entity.Property(e => e.ViewCount).HasColumnName("ViewCount").HasDefaultValue(1);
            entity.Property(e => e.IsFavorite).HasColumnName("IsFavorite").HasDefaultValue(false);
            entity.Property(e => e.Notes).HasColumnName("Notes").HasMaxLength(1000);

            // Foreign key relationship
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes for quick lookups
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.HsCode);
            entity.HasIndex(e => e.TargetCountry);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.HsCode, e.TargetCountry }); // Composite index
        });

        // Configure PaymentHistory entity
        modelBuilder.Entity<PaymentHistory>(entity =>
        {
            entity.ToTable("PaymentHistories");

            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.UserId).HasColumnName("UserId").IsRequired();
            entity.Property(e => e.OrderId).HasColumnName("OrderId").HasMaxLength(100).IsRequired();
            entity.Property(e => e.TransactionId).HasColumnName("TransactionId").HasMaxLength(100);
            entity.Property(e => e.ProductCode).HasColumnName("ProductCode").HasMaxLength(20).IsRequired();
            entity.Property(e => e.PackageName).HasColumnName("PackageName").HasMaxLength(50);
            entity.Property(e => e.Amount).HasColumnName("Amount").HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.Currency).HasColumnName("Currency").HasMaxLength(10).HasDefaultValue("USD");
            entity.Property(e => e.CreditsAdded).HasColumnName("CreditsAdded").IsRequired();
            entity.Property(e => e.Status).HasColumnName("Status").HasMaxLength(20).HasDefaultValue("PENDING");
            entity.Property(e => e.PaymentDate).HasColumnName("PaymentDate").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Installment).HasColumnName("Installment").HasDefaultValue(1);
            entity.Property(e => e.CardLastFour).HasColumnName("CardLastFour").HasMaxLength(4);
            entity.Property(e => e.ErrorMessage).HasColumnName("ErrorMessage").HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Foreign key relationship
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes for quick lookups
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.OrderId).IsUnique();
            entity.HasIndex(e => e.TransactionId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.PaymentDate);
        });

        // Configure Subscription entity
        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.ToTable("Subscriptions");

            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.UserId).HasColumnName("UserId").IsRequired();
            entity.Property(e => e.PlanCode).HasColumnName("PlanCode").HasMaxLength(64).IsRequired();
            entity.Property(e => e.PackageAlias).HasColumnName("PackageAlias").HasMaxLength(50).IsRequired();
            entity.Property(e => e.PackageName).HasColumnName("PackageName").HasMaxLength(50);
            entity.Property(e => e.Amount).HasColumnName("Amount").HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.Period).HasColumnName("Period").HasMaxLength(10).HasDefaultValue("MONTHLY");
            entity.Property(e => e.Status).HasColumnName("Status").HasMaxLength(20).HasDefaultValue("ACTIVE");
            entity.Property(e => e.StartDate).HasColumnName("StartDate").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.NextBillingDate).HasColumnName("NextBillingDate");
            entity.Property(e => e.CancelledAt).HasColumnName("CancelledAt");
            entity.Property(e => e.CancelReason).HasColumnName("CancelReason").HasMaxLength(500);
            entity.Property(e => e.CardToken).HasColumnName("CardToken").HasMaxLength(128);
            entity.Property(e => e.CardLastFour).HasColumnName("CardLastFour").HasMaxLength(4);
            entity.Property(e => e.CardBrand).HasColumnName("CardBrand").HasMaxLength(20);
            entity.Property(e => e.DiscountCode).HasColumnName("DiscountCode").HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt").HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Foreign key
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.PlanCode).IsUnique();
            entity.HasIndex(e => e.Status);
        });
    }
}