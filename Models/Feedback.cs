using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradeScout.API.Models;

/// <summary>
/// Feedback entity for user feedback and complaints
/// </summary>
[Table("Feedbacks")]
public class Feedback
{
    [Key]
    [Column("Id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [Column("FullName")]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [Column("Email")]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Column("Phone")]
    [MaxLength(20)]
    public string? Phone { get; set; }

    [Required]
    [Column("Subject")]
    [MaxLength(255)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [Column("Message")]
    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;

    [Column("FeedbackType")]
    [MaxLength(50)]
    public string? FeedbackType { get; set; } // "complaint", "suggestion", "question", "other"

    [Required]
    [Column("IsRead")]
    public bool IsRead { get; set; } = false;

    [Required]
    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("RepliedAt")]
    public DateTime? RepliedAt { get; set; }

    [Column("Reply")]
    [MaxLength(2000)]
    public string? Reply { get; set; }

    [Column("Status")]
    [MaxLength(50)]
    public string Status { get; set; } = "pending"; // "pending", "in-review", "resolved"
}
