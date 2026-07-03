using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using miSWIYUverifier.Configuration;
using miSWIYUverifier.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace miSWIYUverifier.Services;

public class VerifierApiService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        // Don't escape characters like '+' (e.g. the "dc+sd-jwt" format id) into \uXXXX;
        // this is a server-to-server JSON body, so relaxed escaping keeps it human-readable.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // Technical SD-JWT / status-list claims that are no identity attributes.
    private static readonly HashSet<string> TechnicalClaims = new(StringComparer.OrdinalIgnoreCase)
    {
        "vct", "vct#integrity", "vct_metadata_uri", "vct_metadata_uri#integrity",
        "iss", "iat", "exp", "nbf", "sub", "aud", "jti",
        "cnf", "status", "nonce", "_sd", "_sd_alg",
    };

    private readonly HttpClient _http;
    private readonly VerifierSettings _settings;
    private readonly ILogger<VerifierApiService> _logger;

    public VerifierApiService(
        HttpClient http,
        IOptions<VerifierSettings> settings,
        ILogger<VerifierApiService> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
    }

    // ── Step 1: Create Verification ──────────────────────────────────────────

    public async Task<VerificationResponse> CreateVerificationAsync(
        CancellationToken ct = default)
    {
        var request = BuildCreateRequest();

        _logger.LogInformation("Creating verification at {Url}",
            _settings.ManagementUrl + "/management/api/verifications");

        var response = await _http.PostAsJsonAsync(
            "management/api/verifications", request, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                "Failed to create verification: " + response.StatusCode + "\n" + body);
        }

        var result = await response.Content
            .ReadFromJsonAsync<VerificationResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Empty response from verifier endpoint.");

        if (string.IsNullOrWhiteSpace(result.VerificationDeepLink))
            throw new InvalidOperationException(
                "Verifier response contains no verification_deeplink.");

        _logger.LogInformation("Verification created. ID: {Id}", result.Id);
        return result;
    }

    /// <summary>
    /// Builds the DCQL request for the Beta-ID from the configured claims.
    /// </summary>
    public CreateVerificationRequest BuildCreateRequest()
    {
        var claims = _settings.RequestedClaims
            .Select(name => new DcqlClaim { Path = new List<string> { name } })
            .ToList();

        var request = new CreateVerificationRequest
        {
            AcceptedIssuerDids            = _settings.AcceptedIssuerDids,
            JwtSecuredAuthorizationRequest = _settings.JwtSecuredAuthorizationRequest,
            ResponseMode                  = _settings.ResponseMode,
            DcqlQuery = new DcqlQuery
            {
                Credentials = new List<DcqlCredential>
                {
                    new DcqlCredential
                    {
                        Id     = Guid.NewGuid().ToString(),
                        Format = _settings.SdJwtFormat,
                        Meta   = new DcqlCredentialMeta { VctValues = _settings.VctValues },
                        Claims = claims,
                        RequireCryptographicHolderBinding = _settings.RequireHolderBinding,
                    },
                },
            },
        };

        if (!string.IsNullOrWhiteSpace(_settings.PurposeName))
        {
            request.VerificationPurpose = new VerificationPurpose
            {
                Scope       = _settings.PurposeScope,
                PurposeName = new Dictionary<string, string>
                {
                    ["default"] = _settings.PurposeName,
                },
                PurposeDescription = string.IsNullOrWhiteSpace(_settings.PurposeDescription)
                    ? null
                    : new Dictionary<string, string>
                    {
                        ["default"] = _settings.PurposeDescription,
                    },
            };
        }

        return request;
    }

    // ── Step 2: Poll for the Verification Result ─────────────────────────────

    public async Task<VerificationResponse> WaitForVerificationAsync(
        string verificationId,
        Action<string>? onRawResponse = null,
        CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow.AddSeconds(_settings.PollTimeoutSeconds);
        var interval = TimeSpan.FromSeconds(_settings.PollIntervalSeconds);
        var url      = "management/api/verifications/" + Uri.EscapeDataString(verificationId);

        _logger.LogInformation("Polling for verification result (timeout: {T}s)",
            _settings.PollTimeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var response = await _http.GetAsync(url, ct);

            if (response.IsSuccessStatusCode)
            {
                var rawJson = await response.Content.ReadAsStringAsync(ct);
                onRawResponse?.Invoke(rawJson);

                var verification = JsonSerializer.Deserialize<VerificationResponse>(
                    rawJson, JsonOptions);
                if (verification is null) { await Task.Delay(interval, ct); continue; }

                if (verification.IsSuccess)
                {
                    _logger.LogInformation("Verification succeeded.");
                    return verification;
                }

                if (verification.IsFailed)
                    throw new InvalidOperationException(
                        DescribeError(verification.WalletResponse));
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Expired verifications (VERIFICATION_TTL_SEC) are deleted and return 404.
                throw new TimeoutException(
                    "Verification expired on the verifier (TTL reached).");
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Poll {Status}: {Body}", response.StatusCode, body);
            }

            await Task.Delay(interval, ct);
        }

        throw new TimeoutException(
            "No wallet response within " + _settings.PollTimeoutSeconds + "s.");
    }

    private static string DescribeError(WalletResponseData? walletResponse)
    {
        var code        = walletResponse?.ErrorCode;
        var description = walletResponse?.ErrorDescription;

        var message = code switch
        {
            "client_rejected"          => "Die Anfrage wurde in der Wallet abgelehnt.",
            "credential_expired"       => "Das vorgelegte Credential ist abgelaufen.",
            "credential_revoked"       => "Das vorgelegte Credential wurde widerrufen.",
            "credential_suspended"     => "Das vorgelegte Credential ist suspendiert.",
            "issuer_not_accepted"      => "Der Aussteller des Credentials wird nicht akzeptiert.",
            "holder_binding_mismatch"  => "Holder-Binding-Prüfung fehlgeschlagen.",
            "jwt_expired"              => "Die Wallet-Antwort ist abgelaufen (JWT expired).",
            null                       => "Verifikation fehlgeschlagen.",
            _                          => "Verifikation fehlgeschlagen: " + code,
        };

        return string.IsNullOrWhiteSpace(description)
            ? message
            : message + " (" + description + ")";
    }

    // ── Step 3: Extract Identity Data ─────────────────────────────────────────

    /// <summary>
    /// Extrahiert die Identitätsdaten aus <c>wallet_response.credential_subject_data</c>.
    /// Der swiyu-verifier hat das SD-JWT bereits validiert und liefert die offengelegten
    /// Claims — es ist kein lokales Token-Parsing nötig. Seit v3 sind die Claims nach
    /// DCQL-Credential-ID gruppiert: <c>{ "&lt;credential-id&gt;": [ { claims… } ] }</c>;
    /// ältere Versionen lieferten ein flaches Objekt. Beides wird unterstützt.
    /// </summary>
    public IdentityData ExtractIdentityData(VerificationResponse verification)
    {
        var identity = new IdentityData();

        var data = verification.WalletResponse?.CredentialSubjectData;
        if (data is null || data.Value.ValueKind != JsonValueKind.Object)
            return identity;

        identity.CredentialFormat = _settings.SdJwtFormat;

        foreach (var claim in data.Value.EnumerateObject())
            ApplyClaim(claim.Name, claim.Value, identity);

        return identity;
    }

    private static void ApplyClaim(string name, JsonElement value, IdentityData identity)
    {
        if (TechnicalClaims.Contains(name)) return;

        switch (name)
        {
            case "given_name":  identity.GivenName  = GetStringValue(value)?.Trim(); break;
            case "family_name": identity.FamilyName = GetStringValue(value); break;
            // Beta-ID uses "birth_date"; accept the OIDC-style "birthdate" as an alias too.
            case "birth_date":
            case "birthdate":   identity.BirthDate  = GetStringValue(value); break;
            case "age_over_18": identity.AgeOver18  = GetStringValue(value); break;
            case "sex":         identity.Sex        = GetStringValue(value); break;
            case "nationality": identity.Nationality = GetStringValue(value); break;
            case "birth_place": identity.BirthPlace = GetStringValue(value); break;
            case "portrait":    identity.Portrait   = NormalizePortrait(GetStringValue(value)); break;
            default:
                // Container statt Claim (z.B. DCQL-Credential-ID → [ { claims… } ]):
                // rekursiv in Objekte und Objekt-Arrays absteigen.
                if (value.ValueKind == JsonValueKind.Object)
                {
                    foreach (var claim in value.EnumerateObject())
                        ApplyClaim(claim.Name, claim.Value, identity);
                    break;
                }
                if (value.ValueKind == JsonValueKind.Array &&
                    value.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Object))
                {
                    foreach (var item in value.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object) continue;
                        foreach (var claim in item.EnumerateObject())
                            ApplyClaim(claim.Name, claim.Value, identity);
                    }
                    break;
                }
                var v = GetStringValue(value);
                if (v != null)
                    identity.AdditionalClaims[name] = v;
                break;
        }
    }

    /// <summary>
    /// Normalizes the portrait claim to a data URI usable in an &lt;img&gt; tag.
    /// The Beta-ID delivers either a full data URI or plain base64 image bytes.
    /// </summary>
    private static string? NormalizePortrait(string? portrait)
    {
        if (string.IsNullOrWhiteSpace(portrait)) return null;
        if (portrait.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return portrait;
        return "data:image/jpeg;base64," + portrait;
    }

    private static string? GetStringValue(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.String: return el.GetString();
            case JsonValueKind.Number: return el.GetRawText();
            case JsonValueKind.True:   return "true";
            case JsonValueKind.False:  return "false";
            case JsonValueKind.Array:
                var items = el.EnumerateArray()
                    .Select(GetStringValue)
                    .Where(s => !string.IsNullOrWhiteSpace(s));
                var joined = string.Join(", ", items);
                return joined.Length > 0 ? joined : null;
            default:                   return null;
        }
    }
}
