using System.ComponentModel.DataAnnotations;
using Claims.Api.Enums;
using Claims.Api.Models;

namespace Claims.Api.DTOs;

/// <summary>
/// Payload a claimant sends to report an incident (first notice of loss).
/// [ApiController] rejects invalid payloads with HTTP 400 before the service is called.
/// </summary>
public class SubmitClaimRequest : IValidatableObject
{
    // Nullable so that a missing value fails [Required] instead of silently defaulting to Motor.
    [Required]
    public ClaimType? ClaimType { get; init; }

    [Required, StringLength(2000, MinimumLength = 10)]
    public string Description { get; init; } = string.Empty;

    [Required, StringLength(250, MinimumLength = 3)]
    public string IncidentLocation { get; init; } = string.Empty;

    [Required]
    public DateTimeOffset? IncidentDate { get; init; }

    [Range(typeof(decimal), "0.01", "100000000")]
    public decimal ClaimedAmount { get; init; }

    [Required]
    public string MarketCode { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Markets.IsSupported(MarketCode))
            yield return new ValidationResult(
                $"Market must be one of: {string.Join(", ", Markets.CurrencyByMarket.Keys)}.",
                [nameof(MarketCode)]);

        if (IncidentDate > DateTimeOffset.UtcNow)
            yield return new ValidationResult("Incident date cannot be in the future.", [nameof(IncidentDate)]);

        // SQLite does not enforce decimal scale, so money precision is enforced here.
        if (decimal.Round(ClaimedAmount, 2) != ClaimedAmount)
            yield return new ValidationResult("Claimed amount can have at most 2 decimal places.", [nameof(ClaimedAmount)]);
    }
}
