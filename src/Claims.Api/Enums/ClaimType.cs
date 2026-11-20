namespace Claims.Api.Enums;

/// <summary>
/// Specifies the type of insurance claim.
/// </summary>
public enum ClaimType
{
    /// <summary>
    /// Motor vehicle claim (e.g. accident, third-party damage, theft).
    /// </summary>
    Motor,

    /// <summary>
    /// Real estate or physical property claim (e.g. fire, water damage, natural hazard).
    /// </summary>
    Property
}
