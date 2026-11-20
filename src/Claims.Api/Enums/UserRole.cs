namespace Claims.Api.Enums;

/// <summary>
/// Defines the role of a user within the Claims system.
/// </summary>
public enum UserRole
{
    /// <summary>
    /// Policyholder or individual filing and tracking an insurance claim.
    /// </summary>
    Claimant,

    /// <summary>
    /// Claims adjuster/officer reviewing, assessing, and managing claims.
    /// </summary>
    ClaimsOfficer,

    /// <summary>
    /// Claims team manager viewing operational workload and outstanding liability exposure.
    /// </summary>
    Manager
}
