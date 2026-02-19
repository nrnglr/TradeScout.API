using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradeScout.API.Models;

/// <summary>
/// Business entity representing a scraped business from Google Maps
/// </summary>
[Table("Businesses")]
public class Business
{
    [Key]
    [Column("Id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [Column("UserId")]
    public int UserId { get; set; }

    [Column("ScrapingJobId")]
    public int? ScrapingJobId { get; set; }

    [Required]
    [Column("BusinessName")]
    [MaxLength(300)]
    public string BusinessName { get; set; } = string.Empty;

    [Column("Address")]
    [MaxLength(500)]
    public string? Address { get; set; }

    [Column("Phone")]
    [MaxLength(50)]
    public string? Phone { get; set; }

    [Column("Mobile")]
    [MaxLength(50)]
    public string? Mobile { get; set; }

    [Column("Email")]
    [MaxLength(255)]
    public string? Email { get; set; }

    [Column("Website")]
    [MaxLength(500)]
    public string? Website { get; set; }

    [Column("SocialMedia")]
    [MaxLength(500)]
    public string? SocialMedia { get; set; }

    [Column("Comments")]
    [MaxLength(2000)]
    public string? Comments { get; set; }

    [Column("Rating")]
    public decimal? Rating { get; set; }

    [Column("ReviewCount")]
    public int? ReviewCount { get; set; }

    [Column("WorkingHours")]
    [MaxLength(1000)]
    public string? WorkingHours { get; set; }

    [Column("Category")]
    [MaxLength(200)]
    public string? Category { get; set; }

    [Column("City")]
    [MaxLength(100)]
    public string? City { get; set; }

    [Column("Country")]
    [MaxLength(100)]
    public string? Country { get; set; }

    [Column("Language")]
    [MaxLength(50)]
    public string? Language { get; set; }

    [Required]
    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("UserId")]
    public virtual User? User { get; set; }

    [ForeignKey("ScrapingJobId")]
    public virtual ScrapingJob? ScrapingJob { get; set; }
}
