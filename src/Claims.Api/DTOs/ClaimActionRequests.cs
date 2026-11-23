using System.ComponentModel.DataAnnotations;

namespace Claims.Api.DTOs;

/// <summary>
/// Every state-changing request carries the claim Version the client last saw.
/// If the claim changed since then, the API returns 409 instead of overwriting someone else's work.
/// Nullable + [Required] so a missing version is a 400, not a silent 0.
/// </summary>
public abstract class VersionedRequest
{
    [Required]
    public int? Version { get; init; }
}

public class PickUpClaimRequest : VersionedRequest;

public class SettleClaimRequest : VersionedRequest;

public class SetReserveRequest : VersionedRequest, IValidatableObject
{
    [Range(typeof(decimal), "0.01", "100000000")]
    public decimal Amount { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        MoneyValidation.Validate(Amount, nameof(Amount));
}

public class RequestInformationRequest : VersionedRequest
{
    [Required, StringLength(1000, MinimumLength = 5)]
    public string Question { get; init; } = string.Empty;
}

public class RespondToInformationRequest : VersionedRequest
{
    [Required, StringLength(2000, MinimumLength = 1)]
    public string Response { get; init; } = string.Empty;
}

public class ApproveClaimRequest : VersionedRequest, IValidatableObject
{
    [Range(typeof(decimal), "0.01", "100000000")]
    public decimal ApprovedAmount { get; init; }

    [Required, StringLength(1000, MinimumLength = 5)]
    public string Reason { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        MoneyValidation.Validate(ApprovedAmount, nameof(ApprovedAmount));
}

public class RejectClaimRequest : VersionedRequest
{
    [Required, StringLength(1000, MinimumLength = 5)]
    public string Reason { get; init; } = string.Empty;
}

public static class MoneyValidation
{
    /// <summary>SQLite does not enforce decimal scale, so money precision is enforced in the API.</summary>
    public static IEnumerable<ValidationResult> Validate(decimal amount, string memberName)
    {
        if (decimal.Round(amount, 2) != amount)
            yield return new ValidationResult($"{memberName} can have at most 2 decimal places.", [memberName]);
    }
}
