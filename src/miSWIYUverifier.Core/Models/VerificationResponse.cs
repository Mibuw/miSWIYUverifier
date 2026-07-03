using System.Text.Json;
using System.Text.Json.Serialization;

namespace miSWIYUverifier.Models;

/// <summary>
/// Management response of the swiyu-verifier, returned by
/// POST /management/api/verifications and GET /management/api/verifications/{id}.
/// </summary>
public class VerificationResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("request_nonce")]
    public string? RequestNonce { get; set; }

    /// <summary>Verification state: "PENDING" | "SUCCESS" | "FAILED".</summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = "PENDING";

    /// <summary>Public OID4VP request-object URL fetched by the wallet.</summary>
    [JsonPropertyName("verification_url")]
    public string? VerificationUrl { get; set; }

    /// <summary>
    /// Complete wallet deep link (e.g. swiyu-verify://?client_id=...&amp;request_uri=...),
    /// ready to be rendered as a QR code.
    /// </summary>
    [JsonPropertyName("verification_deeplink")]
    public string? VerificationDeepLink { get; set; }

    /// <summary>Wallet result — subject data on SUCCESS, error details on FAILED.</summary>
    [JsonPropertyName("wallet_response")]
    public WalletResponseData? WalletResponse { get; set; }

    // ── Helpers ──────────────────────────────────────────────────────────────

    public bool IsPending => string.Equals(State, "PENDING", StringComparison.OrdinalIgnoreCase);
    public bool IsSuccess => string.Equals(State, "SUCCESS", StringComparison.OrdinalIgnoreCase);
    public bool IsFailed  => string.Equals(State, "FAILED",  StringComparison.OrdinalIgnoreCase);
}

/// <summary>Wallet response payload inside the management response.</summary>
public class WalletResponseData
{
    /// <summary>
    /// Error code on FAILED, e.g. "client_rejected" (user declined in the wallet),
    /// "credential_expired", "credential_revoked", "issuer_not_accepted", ...
    /// </summary>
    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }

    /// <summary>
    /// The disclosed and verified credential attributes as a flat object,
    /// e.g. { "given_name": "...", "family_name": "...", "vct": "betaid-sdjwt", ... }.
    /// </summary>
    [JsonPropertyName("credential_subject_data")]
    public JsonElement? CredentialSubjectData { get; set; }
}
