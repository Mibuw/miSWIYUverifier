# miSWIYUverifier.Core

Client library for verifying **Swiss E-ID (swiyu) Beta-ID** credentials from .NET.

It drives the management API of the
[swiyu-verifier](https://github.com/swiyu-admin-ch/swiyu-verifier) service: it builds
the DCQL query, creates a verification, renders the wallet deep link as a QR code,
polls for the result and flattens the presented claims into a plain object.

**All cryptography — SD-JWT parsing, issuer signatures, holder binding, status list
checks — is done by the swiyu-verifier service, not by this library.** You therefore
always run it alongside that service (plus its PostgreSQL), and you need your own
verifier DID and signing key from the swiyu identifier registry.

## Install

```bash
dotnet add package miSWIYUverifier.Core
```

## Use

```csharp
builder.Services.AddMiSWIYUverifier(builder.Configuration);

var verifier     = app.Services.GetRequiredService<VerifierApiService>();
var verification = await verifier.CreateVerificationAsync();

// Show this to the user — the wallet scans it
var qrPng = QrCodeService.GeneratePng(verification.VerificationDeepLink!);

var result   = await verifier.WaitForVerificationAsync(verification.Id);
var identity = verifier.ExtractIdentityData(result);
// identity.GivenName, identity.FamilyName, identity.BirthDate, identity.Portrait, ...
```

## Configure

```json
{
  "VerifierSettings": {
    "ManagementUrl": "http://localhost:8083",
    "VctValues": [ "betaid-sdjwt", "urn:vct:ch.admin.bcs.betaid" ],
    "AcceptedIssuerDids": [ "did:tdw:…" ],
    "RequestedClaims": [ "given_name", "family_name", "birth_date", "portrait" ],
    "ResponseMode": "direct_post.jwt"
  }
}
```

Only `ManagementUrl` is normally worth setting; every list falls back to values that
match the current swiyu Beta-ID. Two of the defaults matter and should not be
weakened: `ResponseMode` must stay `direct_post.jwt` (the wallet accepts nothing
else), and `VctValues` must keep **both** vct values while the Beta-ID migrates from
`betaid-sdjwt` to `urn:vct:ch.admin.bcs.betaid`.

The swiyu sandbox introduces breaking changes without notice. The
[project README](https://github.com/Mibuw/miSWIYUverifier#what-broke-and-when) keeps a
dated table of every incompatibility hit so far and how each was diagnosed — worth
reading before debugging a wallet that suddenly refuses to play along.

## License

MIT
