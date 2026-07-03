namespace miSWIYUverifier.Configuration;

/// <summary>
/// Configuration settings for the swiyu-verifier management API connection.
/// </summary>
public class VerifierSettings
{
    public const string SectionName = "VerifierSettings";

    /// <summary>
    /// Base URL of the swiyu-verifier management interface
    /// (the Spring Boot service from ghcr.io/swiyu-admin-ch/swiyu-verifier).
    /// This URL must only be reachable from this server, never publicly.
    /// </summary>
    public string ManagementUrl { get; set; } = "http://localhost:8083";

    /// <summary>How often (in seconds) to poll for the verification result.</summary>
    public int PollIntervalSeconds { get; set; } = 2;

    /// <summary>
    /// Maximum time (in seconds) to wait for the verification result.
    /// Must be below the verifier's VERIFICATION_TTL_SEC (default 900 s) —
    /// expired verifications return 404.
    /// </summary>
    public int PollTimeoutSeconds { get; set; } = 300;

    /// <summary>DCQL format identifier. The swiyu Beta-ID is an SD-JWT VC ("dc+sd-jwt").</summary>
    public string SdJwtFormat { get; set; } = "dc+sd-jwt";

    /// <summary>
    /// Accepted verifiable-credential types (vct). Default is the swiyu Beta-ID.
    /// </summary>
    public List<string> VctValues { get; set; } = new() { "betaid-sdjwt" };

    /// <summary>
    /// DIDs of issuers whose credentials are trusted. Since swiyu-verifier v2.2.0
    /// this (or trust_anchors) is mandatory — an empty list means nothing is trusted.
    /// Default is the Beta-ID issuer of the swiyu Public Beta.
    /// </summary>
    public List<string> AcceptedIssuerDids { get; set; } = new()
    {
        "did:tdw:QmPEZPhDFR4nEYSFK5bMnvECqdpf1tPTPJuWs9QrMjCumw:identifier-reg.trust-infra.swiyu-int.admin.ch:api:v1:did:9a5559f0-b81c-4368-a170-e7b4ae424527",
    };

    /// <summary>
    /// Beta-ID claims requested from the wallet (flat SD-JWT claim names).
    /// Available: document_number, given_name, family_name, birth_date, age_over_16,
    /// age_over_18, age_over_65, age_birth_year, birth_place, place_of_origin, sex,
    /// nationality, portrait, issuance_date, expiry_date, ...
    /// </summary>
    public List<string> RequestedClaims { get; set; } = new()
    {
        "given_name",
        "family_name",
        "birth_date",
        "age_over_18",
        "sex",
        "nationality",
        "birth_place",
        "portrait",
    };

    /// <summary>Whether the authorization request is sent as a signed JWT (JAR).</summary>
    public bool JwtSecuredAuthorizationRequest { get; set; } = true;

    /// <summary>Wallet response mode. "direct_post" or "direct_post.jwt".</summary>
    public string ResponseMode { get; set; } = "direct_post";

    /// <summary>Require cryptographic holder binding of the presented credential.</summary>
    public bool RequireHolderBinding { get; set; } = true;

    // ── verification_purpose (shown to the user in the swiyu wallet) ─────────
    // Leave PurposeName empty to omit the verification_purpose block entirely.

    public string? PurposeScope { get; set; } = "ch.miswiyuverifier.demo";

    public string? PurposeName { get; set; } = "Identitätsprüfung";

    public string? PurposeDescription { get; set; } =
        "miSWIYUverifier Demo – Verifikation der Beta-ID";
}
