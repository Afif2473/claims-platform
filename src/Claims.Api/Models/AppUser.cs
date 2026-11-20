using Claims.Api.Enums;

namespace Claims.Api.Models;

/// <summary>
/// Represents a user of the Claims Management System.
/// </summary>
public class AppUser
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties: claims where this user is the claimant
    public ICollection<Claim> SubmittedClaims { get; set; } = new List<Claim>();

    // Navigation properties: claims assigned to this user as claims officer
    public ICollection<Claim> AssignedClaims { get; set; } = new List<Claim>();
}
