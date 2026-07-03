using System.Net;
using System.Text.Json;
using FluentAssertions;
using miSWIYUverifier.Configuration;
using miSWIYUverifier.Models;
using miSWIYUverifier.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace miSWIYUverifier.Core.Tests.Services;

public class VerifierApiServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static VerifierApiService CreateService(
        MockHttpMessageHandler handler, VerifierSettings? settings = null)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8083/"),
        };
        return new VerifierApiService(
            http,
            Options.Create(settings ?? new VerifierSettings()),
            NullLogger<VerifierApiService>.Instance);
    }

    // ── CreateVerificationAsync ───────────────────────────────────────────────

    [Fact]
    public async Task CreateVerification_Posts_Dcql_Request_And_Parses_Response()
    {
        var handler = new MockHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """
            {
                "id": "abc-123",
                "state": "PENDING",
                "verification_deeplink": "swiyu-verify://?client_id=did%3Atdw%3Ax&request_uri=https%3A%2F%2Fv.example.com%2Foid4vp%2Fapi%2Frequest-object%2Fabc-123"
            }
            """);

        var service = CreateService(handler);
        var result  = await service.CreateVerificationAsync();

        result.Id.Should().Be("abc-123");
        result.VerificationDeepLink.Should().StartWith("swiyu-verify://");

        handler.Requests.Should().HaveCount(1);
        var (method, url, body) = handler.Requests[0];
        method.Should().Be(HttpMethod.Post);
        url.Should().Be("http://localhost:8083/management/api/verifications");

        using var doc  = JsonDocument.Parse(body!);
        var root = doc.RootElement;

        // DCQL query with the Beta-ID defaults
        var credential = root.GetProperty("dcql_query")
            .GetProperty("credentials")[0];
        credential.GetProperty("format").GetString().Should().Be("dc+sd-jwt");
        credential.GetProperty("meta").GetProperty("vct_values")[0]
            .GetString().Should().Be("betaid-sdjwt");
        credential.GetProperty("require_cryptographic_holder_binding")
            .GetBoolean().Should().BeTrue();

        var claimNames = credential.GetProperty("claims").EnumerateArray()
            .Select(c => c.GetProperty("path")[0].GetString())
            .ToList();
        claimNames.Should().Contain(new[] { "given_name", "family_name", "birth_date", "portrait" });

        // Trust: without accepted_issuer_dids nothing would be accepted (since v2.2.0)
        root.GetProperty("accepted_issuer_dids").GetArrayLength().Should().BeGreaterThan(0);
        root.GetProperty("jwt_secured_authorization_request").GetBoolean().Should().BeTrue();
        root.GetProperty("response_mode").GetString().Should().Be("direct_post");
        root.GetProperty("verification_purpose").GetProperty("purpose_name")
            .GetProperty("default").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateVerification_Omits_Purpose_When_Not_Configured()
    {
        var handler = new MockHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """
            { "id": "abc", "state": "PENDING", "verification_deeplink": "swiyu-verify://?x=1" }
            """);

        var settings = new VerifierSettings { PurposeName = null };
        var service  = CreateService(handler, settings);
        await service.CreateVerificationAsync();

        using var doc = JsonDocument.Parse(handler.Requests[0].Body!);
        doc.RootElement.TryGetProperty("verification_purpose", out _).Should().BeFalse();
    }

    [Fact]
    public async Task CreateVerification_Throws_When_DeepLink_Missing()
    {
        var handler = new MockHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """{ "id": "abc", "state": "PENDING" }""");

        var service = CreateService(handler);
        var act     = () => service.CreateVerificationAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*verification_deeplink*");
    }

    [Fact]
    public async Task CreateVerification_Throws_On_Http_Error()
    {
        var handler = new MockHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.BadRequest, """{ "detail": "dcql_query is required" }""");

        var service = CreateService(handler);
        var act     = () => service.CreateVerificationAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*BadRequest*");
    }

    // ── WaitForVerificationAsync ──────────────────────────────────────────────

    [Fact]
    public async Task WaitForVerification_Returns_On_Success_After_Pending()
    {
        var handler = new MockHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """{ "id": "abc", "state": "PENDING" }""");
        handler.Enqueue(HttpStatusCode.OK, """
            {
                "id": "abc",
                "state": "SUCCESS",
                "wallet_response": {
                    "credential_subject_data": { "given_name": "Maria", "family_name": "Muster" }
                }
            }
            """);

        var settings = new VerifierSettings { PollIntervalSeconds = 0, PollTimeoutSeconds = 10 };
        var service  = CreateService(handler, settings);

        var result = await service.WaitForVerificationAsync("abc");

        result.IsSuccess.Should().BeTrue();
        handler.Requests.Should().HaveCount(2);
        handler.Requests[0].Url.Should()
            .Be("http://localhost:8083/management/api/verifications/abc");
    }

    [Fact]
    public async Task WaitForVerification_Throws_On_Client_Rejected()
    {
        var handler = new MockHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """
            {
                "id": "abc",
                "state": "FAILED",
                "wallet_response": { "error_code": "client_rejected" }
            }
            """);

        var settings = new VerifierSettings { PollIntervalSeconds = 0, PollTimeoutSeconds = 10 };
        var service  = CreateService(handler, settings);

        var act = () => service.WaitForVerificationAsync("abc");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*abgelehnt*");
    }

    [Fact]
    public async Task WaitForVerification_Throws_TimeoutException_On_404()
    {
        var handler = new MockHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.NotFound, "{}");

        var settings = new VerifierSettings { PollIntervalSeconds = 0, PollTimeoutSeconds = 10 };
        var service  = CreateService(handler, settings);

        var act = () => service.WaitForVerificationAsync("abc");

        await act.Should().ThrowAsync<TimeoutException>()
            .WithMessage("*TTL*");
    }

    // ── ExtractIdentityData ───────────────────────────────────────────────────

    private static VerificationResponse ParseVerification(string json) =>
        JsonSerializer.Deserialize<VerificationResponse>(json, JsonOptions)!;

    [Fact]
    public void ExtractIdentityData_Maps_All_Known_Claims()
    {
        var verification = ParseVerification("""
            {
                "id": "abc",
                "state": "SUCCESS",
                "wallet_response": {
                    "credential_subject_data": {
                        "given_name": "Maria ",
                        "family_name": "Muster",
                        "birth_date": "1990-05-17",
                        "age_over_18": true,
                        "sex": "2",
                        "nationality": "CH",
                        "birth_place": "Bern",
                        "portrait": "data:image/jpeg;base64,AAAA",
                        "document_number": "BETA-123",
                        "vct": "betaid-sdjwt",
                        "iss": "did:tdw:issuer",
                        "cnf": { "kty": "EC" },
                        "status": { "status_list": {} }
                    }
                }
            }
            """);

        var service  = CreateService(new MockHttpMessageHandler());
        var identity = service.ExtractIdentityData(verification);

        identity.GivenName.Should().Be("Maria");
        identity.FamilyName.Should().Be("Muster");
        identity.BirthDate.Should().Be("1990-05-17");
        identity.AgeOver18.Should().Be("true");
        identity.Sex.Should().Be("2");
        identity.Nationality.Should().Be("CH");
        identity.BirthPlace.Should().Be("Bern");
        identity.Portrait.Should().Be("data:image/jpeg;base64,AAAA");
        identity.CredentialFormat.Should().Be("dc+sd-jwt");
        identity.IsComplete.Should().BeTrue();

        // Extra disclosed claim lands in AdditionalClaims ...
        identity.AdditionalClaims.Should().ContainKey("document_number");
        // ... technical SD-JWT claims are filtered out.
        identity.AdditionalClaims.Keys.Should().NotContain(new[] { "vct", "iss", "cnf", "status" });
    }

    [Fact]
    public void ExtractIdentityData_Handles_V3_Structure_Grouped_By_CredentialId()
    {
        // Real swiyu-verifier 3.0.3 shape: claims are wrapped in
        // { "<dcql-credential-id>": [ { ...claims... } ] }
        var verification = ParseVerification("""
            {
                "id": "abc",
                "state": "SUCCESS",
                "wallet_response": {
                    "credential_subject_data": {
                        "06bf3864-98f5-49f4-9da6-4e94abc0019b": [
                            {
                                "given_name": "Marco Elio",
                                "family_name": "Prumaz",
                                "birth_date": "1988-06-19",
                                "age_over_18": "true",
                                "sex": "1",
                                "nationality": "CH, FR",
                                "birth_place": "Vallorbe",
                                "vct": "betaid-sdjwt",
                                "vct_metadata_uri": "https://bcs.admin.ch/bcs-web/metadata/x",
                                "vct_metadata_uri#integrity": "sha256-xyz",
                                "iss": "did:tdw:issuer",
                                "cnf": { "jwk": { "kty": "EC" } },
                                "iat": "2026-06-23T00:00:00.000+00:00",
                                "status": { "status_list": { "idx": 88218 } }
                            }
                        ]
                    }
                }
            }
            """);

        var service  = CreateService(new MockHttpMessageHandler());
        var identity = service.ExtractIdentityData(verification);

        identity.GivenName.Should().Be("Marco Elio");
        identity.FamilyName.Should().Be("Prumaz");
        identity.BirthDate.Should().Be("1988-06-19");
        identity.AgeOver18.Should().Be("true");
        identity.Sex.Should().Be("1");
        identity.Nationality.Should().Be("CH, FR");
        identity.BirthPlace.Should().Be("Vallorbe");
        identity.IsComplete.Should().BeTrue();
        identity.AdditionalClaims.Keys.Should().NotContain(new[]
            { "vct", "vct_metadata_uri", "vct_metadata_uri#integrity", "iss", "cnf", "iat", "status" });
    }

    [Fact]
    public void ExtractIdentityData_Wraps_Plain_Base64_Portrait_As_DataUri()
    {
        var verification = ParseVerification("""
            {
                "id": "abc",
                "state": "SUCCESS",
                "wallet_response": {
                    "credential_subject_data": { "portrait": "/9j/4AAQSkZJRg==" }
                }
            }
            """);

        var service  = CreateService(new MockHttpMessageHandler());
        var identity = service.ExtractIdentityData(verification);

        identity.Portrait.Should().Be("data:image/jpeg;base64,/9j/4AAQSkZJRg==");
    }

    [Fact]
    public void ExtractIdentityData_Joins_Array_Claims()
    {
        var verification = ParseVerification("""
            {
                "id": "abc",
                "state": "SUCCESS",
                "wallet_response": {
                    "credential_subject_data": { "nationality": ["CH", "IT"] }
                }
            }
            """);

        var service  = CreateService(new MockHttpMessageHandler());
        var identity = service.ExtractIdentityData(verification);

        identity.Nationality.Should().Be("CH, IT");
    }

    [Fact]
    public void ExtractIdentityData_Returns_Empty_When_No_Wallet_Response()
    {
        var verification = ParseVerification("""{ "id": "abc", "state": "PENDING" }""");

        var service  = CreateService(new MockHttpMessageHandler());
        var identity = service.ExtractIdentityData(verification);

        identity.IsComplete.Should().BeFalse();
        identity.GivenName.Should().BeNull();
    }
}
