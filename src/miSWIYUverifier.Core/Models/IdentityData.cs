namespace miSWIYUverifier.Models;

/// <summary>The identity data extracted from the verified Beta-ID presentation.</summary>
public class IdentityData
{
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
    public string? BirthDate { get; set; }

    /// <summary>"true" / "false" as disclosed by the wallet.</summary>
    public string? AgeOver18 { get; set; }

    /// <summary>Sex as issued in the Beta-ID (ISO/IEC 5218: 1 = male, 2 = female).</summary>
    public string? Sex { get; set; }

    public string? Nationality { get; set; }
    public string? BirthPlace { get; set; }

    /// <summary>Portrait photo as a data URI (data:image/...;base64,...), if disclosed.</summary>
    public string? Portrait { get; set; }

    /// <summary>Source credential format (the Beta-ID is "dc+sd-jwt").</summary>
    public string? CredentialFormat { get; set; }

    /// <summary>Additional disclosed claims beyond the mapped properties.</summary>
    public Dictionary<string, string> AdditionalClaims { get; set; } = new();

    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(FamilyName) &&
        !string.IsNullOrWhiteSpace(GivenName) &&
        !string.IsNullOrWhiteSpace(BirthDate);
}
