using System.Text.Json;
using FluentAssertions;
using miSWIYUverifier.Models;
using Xunit;

namespace miSWIYUverifier.Core.Tests.Models;

public class VerificationResponseTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void Deserializes_CreateResponse_With_DeepLink()
    {
        const string json = """
            {
                "id": "7f2e9c1a-0b3d-4e5f-8a6b-9c0d1e2f3a4b",
                "request_nonce": "aIxs7p648grTy9IOQLfF1JIeSpHH2Cia",
                "state": "PENDING",
                "verification_url": "https://verifier.example.com/oid4vp/api/request-object/7f2e9c1a-0b3d-4e5f-8a6b-9c0d1e2f3a4b",
                "verification_deeplink": "swiyu-verify://?client_id=did%3Atdw%3Aexample&request_uri=https%3A%2F%2Fverifier.example.com%2Foid4vp%2Fapi%2Frequest-object%2F7f2e9c1a"
            }
            """;

        var response = JsonSerializer.Deserialize<VerificationResponse>(json, JsonOptions)!;

        response.Id.Should().Be("7f2e9c1a-0b3d-4e5f-8a6b-9c0d1e2f3a4b");
        response.RequestNonce.Should().Be("aIxs7p648grTy9IOQLfF1JIeSpHH2Cia");
        response.IsPending.Should().BeTrue();
        response.IsSuccess.Should().BeFalse();
        response.IsFailed.Should().BeFalse();
        response.VerificationDeepLink.Should().StartWith("swiyu-verify://?client_id=");
        response.WalletResponse.Should().BeNull();
    }

    [Fact]
    public void Deserializes_Success_With_CredentialSubjectData()
    {
        const string json = """
            {
                "id": "7f2e9c1a-0b3d-4e5f-8a6b-9c0d1e2f3a4b",
                "state": "SUCCESS",
                "wallet_response": {
                    "credential_subject_data": {
                        "given_name": "Maria",
                        "family_name": "Muster",
                        "birth_date": "1990-05-17",
                        "vct": "betaid-sdjwt"
                    }
                }
            }
            """;

        var response = JsonSerializer.Deserialize<VerificationResponse>(json, JsonOptions)!;

        response.IsSuccess.Should().BeTrue();
        response.WalletResponse.Should().NotBeNull();
        var data = response.WalletResponse!.CredentialSubjectData!.Value;
        data.GetProperty("given_name").GetString().Should().Be("Maria");
        data.GetProperty("vct").GetString().Should().Be("betaid-sdjwt");
    }

    [Fact]
    public void Deserializes_Failed_With_ErrorCode()
    {
        const string json = """
            {
                "id": "7f2e9c1a-0b3d-4e5f-8a6b-9c0d1e2f3a4b",
                "state": "FAILED",
                "wallet_response": {
                    "error_code": "client_rejected",
                    "error_description": "Verification was rejected by the holder"
                }
            }
            """;

        var response = JsonSerializer.Deserialize<VerificationResponse>(json, JsonOptions)!;

        response.IsFailed.Should().BeTrue();
        response.WalletResponse!.ErrorCode.Should().Be("client_rejected");
        response.WalletResponse.ErrorDescription.Should().Contain("rejected");
    }
}
