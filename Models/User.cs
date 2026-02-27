using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradeScout.API.Models;

/// <summary>
/// User type enum
/// </summary>
public enum UserType
{
    Manufacturer,
    Wholesaler,
    Researcher
}

/// <summary>
/// User entity representing a TradeScout user
/// </summary>
[Table("Users")]
public class User
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

    [Required]
    [Column("PasswordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("CompanyName")]
    [MaxLength(150)]
    public string? CompanyName { get; set; }

    [Column("Address")]
    [MaxLength(255)]
    public string? Address { get; set; }

    [Column("City")]
    [MaxLength(100)]
    public string? City { get; set; }

    [Column("Country")]
    [MaxLength(100)]
    public string? Country { get; set; }

    [Column("Phone")]
    [MaxLength(20)]
    public string? Phone { get; set; }

    [Column("Website")]
    [MaxLength(255)]
    public string? Website { get; set; }

    [Column("UserType")]
    public UserType? UserType { get; set; }

    [Required]
    [Column("Credits")]
    public int Credits { get; set; } = 5;

    [Required]
    [Column("MaxResultsPerSearch")]
    public int MaxResultsPerSearch { get; set; } = 200; // Varsayılan 200 firma

    [Required]
    [Column("PackageType")]
    [MaxLength(50)]
    public string PackageType { get; set; } = "Free";

    [Required]
    [Column("Role")]
    [MaxLength(20)]
    public string Role { get; set; } = "User";

    [Required]
    [Column("IsActive")]
    public bool IsActive { get; set; } = true;

    [Required]
    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("LastLogin")]
    public DateTime? LastLogin { get; set; }
}
