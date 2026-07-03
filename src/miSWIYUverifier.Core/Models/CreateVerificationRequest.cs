using System.Text.Json.Serialization;

namespace miSWIYUverifier.Models;

/// <summary>
/// Request body for POST /management/api/verifications on the swiyu-verifier.
/// Since swiyu-verifier v3.0.0 only DCQL (OID4VP 1.0) is supported —
/// DIF Presentation Exchange has been removed.
/// </summary>
public class CreateVerificationRequest
{
    /// <summary>
    /// Issuer DIDs whose credentials are accepted. Mandatory (alternatively
    /// trust_anchors) since v2.2.0 — an empty list trusts nothing.
    /// </summary>
    [JsonPropertyName("accepted_issuer_dids")]
    public List<string> AcceptedIssuerDids { get; set; } = new();

    [JsonPropertyName("jwt_secured_authorization_request")]
    public bool JwtSecuredAuthorizationRequest { get; set; } = true;

    /// <summary>"direct_post" or "direct_post.jwt".</summary>
    [JsonPropertyName("response_mode")]
    public string ResponseMode { get; set; } = "direct_post";

    /// <summary>Purpose of the verification, displayed to the user in the wallet.</summary>
    [JsonPropertyName("verification_purpose")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public VerificationPurpose? VerificationPurpose { get; set; }

    [JsonPropertyName("dcql_query")]
    public DcqlQuery DcqlQuery { get; set; } = new();
}

/// <summary>Localized purpose information shown in the swiyu wallet.</summary>
public class VerificationPurpose
{
    [JsonPropertyName("scope")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Scope { get; set; }

    /// <summary>Localized name, e.g. { "default": "Identity check" }.</summary>
    [JsonPropertyName("purpose_name")]
    public Dictionary<string, string> PurposeName { get; set; } = new();

    [JsonPropertyName("purpose_description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? PurposeDescription { get; set; }
}

// ─── DCQL (Digital Credentials Query Language, OID4VP 1.0) ───────────────────

public class DcqlQuery
{
    [JsonPropertyName("credentials")]
    public List<DcqlCredential> Credentials { get; set; } = new();
}

public class DcqlCredential
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Credential format, e.g. "dc+sd-jwt" for the Beta-ID.</summary>
    [JsonPropertyName("format")]
    public string Format { get; set; } = "dc+sd-jwt";

    [JsonPropertyName("meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DcqlCredentialMeta? Meta { get; set; }

    [JsonPropertyName("claims")]
    public List<DcqlClaim> Claims { get; set; } = new();

    [JsonPropertyName("require_cryptographic_holder_binding")]
    public bool RequireCryptographicHolderBinding { get; set; } = true;
}

public class DcqlCredentialMeta
{
    /// <summary>Accepted credential types; the Beta-ID uses "betaid-sdjwt".</summary>
    [JsonPropertyName("vct_values")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? VctValues { get; set; }
}

public class DcqlClaim
{
    /// <summary>Claim path; flat SD-JWT claims use a single segment, e.g. ["given_name"].</summary>
    [JsonPropertyName("path")]
    public List<string> Path { get; set; } = new();
}
