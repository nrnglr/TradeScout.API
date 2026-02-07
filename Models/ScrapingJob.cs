using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradeScout.API.Models;

/// <summary>
/// Scraping job entity for tracking data collection tasks
/// </summary>
[Table("ScrapingJobs")]
public class ScrapingJob
{
    [Key]
    [Column("Id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [Column("UserId")]
    public int UserId { get; set; }

    [Required]
    [Column("Category")]
    [MaxLength(200)]
    public string Category { get; set; } = string.Empty;

    [Required]
    [Column("City")]
    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [Column("Country")]
    [MaxLength(100)]
    public string? Country { get; set; }

    [Column("Language")]
    [MaxLength(50)]
    public string Language { get; set; } = "tr";

    [Required]
    [Column("Status")]
    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed, Failed

    [Column("TotalResults")]
    public int TotalResults { get; set; } = 0;

    [Column("CreditsUsed")]
    public int CreditsUsed { get; set; } = 0;

    [Column("ErrorMessage")]
    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }

    [Column("StartedAt")]
    public DateTime? StartedAt { get; set; }

    [Column("CompletedAt")]
    public DateTime? CompletedAt { get; set; }

    [Required]
    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey("UserId")]
    public virtual User? User { get; set; }
}
