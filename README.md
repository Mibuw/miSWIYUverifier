# miSWIYUverifier

[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE) [![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/) [![swiyu Public Beta](https://img.shields.io/badge/swiyu-Public%20Beta-d52b1e.svg)](https://swiyu-admin-ch.github.io/)

A C#/.NET 10 **ASP.NET Core web app** (Minimal API with a browser UI) that verifies the
**Swiss E-ID (swiyu Public Beta) Beta-ID** credential via the
[swiyu-verifier](https://github.com/swiyu-admin-ch/swiyu-verifier) management REST API
(OpenID4VP / DCQL) — the Swiss sibling of
[miEUDIverifier](https://github.com/Mibuw/miEUDIverifier).

The web page shows a QR code. The user scans it with the **swiyu Sandbox Wallet**
(iOS/Android),
confirms the data sharing of their **Beta-ID**, and the page displays the verified
identity data: given name, family name, date of birth, over-18, sex, nationality,
place of birth and portrait photo.

Status: **tested end-to-end on 10 September 2026** — scan, consent, presentation and
display of the verified data including the portrait, against the swiyu **Sandbox
Wallet** (iOS 1.18.0) and a Beta-ID issued the same day, with swiyu-verifier 4.2.0
speaking OID4VP 1.0. Previous full pass: 3 July 2026.

The swiyu sandbox moves fast and breaks verifiers without warning; if this stops
working, [What broke and when](#what-broke-and-when) lists every incompatibility hit
so far, and how each was diagnosed.

> ### ⚠️ You need the **swiyu Sandbox Wallet**, not the regular swiyu app
>
> On **4 September 2026** the Public Beta was renamed **Sandbox**, and the regular
> **swiyu Wallet became production-only**
> ([announcement](https://www.eid.admin.ch/en/20260903-public-beta-becomes-sandbox)).
> This project runs against the sandbox infrastructure
> (`*.trust-infra.swiyu-int.admin.ch`), so the normal swiyu app can no longer be used
> with it — it finds no matching credential and answers `access_denied`.
>
> - **iOS:** [swiyu Sandbox Wallet](https://apps.apple.com/us/app/swiyu-sandbox-wallet/id6771296857)
>   — direct link only, it is **not searchable** in the App Store (iOS 17+)
> - **Android:** APK from the
>   [wallet releases](https://github.com/swiyu-admin-ch/eidch-android-wallet/releases)
>   — not on Google Play
>
> The Sandbox Wallet starts empty: issue a fresh Beta-ID inside it (step 1). Nothing
> changes on the verifier side — the sandbox URLs, DIDs and the issuer stay the same.

## Try it (live demo)

A public test instance is available at **https://miswiyuverifier.mitterbucher.com/** —
open it, scan the QR code with your swiyu Sandbox Wallet and confirm.

> **No guarantee of availability** — this endpoint may be offline at any time.
> To run your own instance, see the [Quick start](#quick-start) below.

- You need the **swiyu Sandbox Wallet** (see the note above — *not* the regular swiyu
  app) with a **Beta-ID** (free, self-issued — see step 1 below).
- Every visitor gets **their own verification session** (own QR code); any number of
  verifications can run in parallel. "New request" starts a fresh verification anytime.
- Note: the Beta-ID contains **made-up pseudo data only** (not an official document).

## Flow (Cross-Device)

```
                                    ┌─ host machine ───────────────────────────────┐
   swiyu app ──HTTPS:443──► router  │ Caddy (TLS, /oid4vp/* only)                  │
   (smartphone)                     │   └─► swiyu-verifier :8083 ◄── PostgreSQL    │
        ▲                           │         ▲ management API (local only!)       │
        │ scans QR                  │         │                                    │
   Browser ◄──────:5070──────────── │ miSWIYUverifier (ASP.NET)                    │
                                    └──────────────────────────────────────────────┘
```

- The ASP.NET frontend (`src/miSWIYUverifier`) creates a verification via the
  **management API** of the swiyu-verifier (DCQL query on the Beta-ID) and renders the
  returned `verification_deeplink` as a QR code.
- The swiyu app fetches the signed request object from the **publicly reachable HTTPS**
  `EXTERNAL_URL` and posts the presentation back to it. TLS is terminated by **Caddy**,
  which forwards **only** `/oid4vp/*` to the container — the management API stays
  unreachable from outside.
- The frontend polls `GET /management/api/verifications/{id}` until `state = SUCCESS`
  and displays the data from `wallet_response.credential_subject_data`.
- All cryptographic validation (SD-JWT, signatures, holder binding, revocation) is done
  by the swiyu-verifier service — the core library stays slim.

## Project structure

| Path | Content |
|---|---|
| `src/miSWIYUverifier.Core` | Reusable library: `VerifierApiService`, models, `QrCodeService`, DI extension — published as the **`miSWIYUverifier`** NuGet package |
| `src/miSWIYUverifier` | Minimal-API web host with single-page UI (port **5070**) |
| `src/miSWIYUverifier.Core.Tests` | xUnit tests |
| `docker/` | `docker-compose.yml` (swiyu-verifier + PostgreSQL for local dev), `docker-compose.vps.yml` (full stack, built from source) and `docker-compose.published.yml` (full stack from published images, for reuse); `docker/.env` (not in the repo!) holds DID + signing key |
| `Dockerfile` | Web-app image — built locally for the VPS deployment, published to **`ghcr.io/mibuw/miswiyuverifier`** on a version tag |
| `.github/workflows/release.yml` | On a `v*` tag: pushes the container image to ghcr.io and packs the NuGet package |
| `proxy/Caddyfile.example` | Reverse-proxy template (TLS on 443 → `/oid4vp/*` → localhost:8083); the real `proxy/Caddyfile` is not in the repo |
| `tools/` | DID toolbox JAR (download see below, not in the repo) |
| `didlog.jsonl` | Backup of the uploaded DID log — created during onboarding, not in the repo |
| `.didtoolbox/` | **Private keys** of the verifier DID (not in the repo — back them up externally!) |

> **Credentials never enter the build.** The registered verifier DID, the partner
> registration and the signing key live in `docker/.env`, `proxy/Caddyfile` and
> `.didtoolbox/` — all excluded via `.gitignore`, and all excluded from the image via
> `.dockerignore`. They belong to the **swiyu-verifier** service and reach it as
> environment variables at run time; the web-app image only ever learns the management
> URL. That is why the image can be published — see
> [Reusing this project](#reusing-this-project). If you reuse it, run the onboarding
> (step 2) with your own DID and keys: request objects are signed with that private
> key, so someone else's DID is of no use to you.

## Prerequisites

1. **.NET 10 SDK**
2. **Docker Desktop**
3. **Java 21+** (only for the DID toolbox during onboarding, e.g. `winget install EclipseAdoptium.Temurin.21.JRE`)
4. **swiyu Sandbox Wallet** on your smartphone — iOS via
   [direct App Store link](https://apps.apple.com/us/app/swiyu-sandbox-wallet/id6771296857),
   Android as an APK from the
   [releases](https://github.com/swiyu-admin-ch/eidch-android-wallet/releases).
   The regular "swiyu" app from the stores is production-only and will **not** work.
5. A **Beta-ID** in the Sandbox Wallet (step 1)
6. A **registered verifier DID** on the swiyu identifier registry (step 2)
7. A **public HTTPS URL** (step 3 — own domain or tunnel)

---

## Quick start

Five steps from zero to a running verifier — issue a Beta-ID (1), onboard your
verifier DID (2), expose the OID4VP endpoint via HTTPS (3), start the
swiyu-verifier container (4) and run the web app (5).

## Step 1: Issue a Beta-ID (once, 5 minutes)

Anyone can issue themselves a free Beta-ID (pseudo identity for the Public Beta):

1. Install and set up the **swiyu Sandbox Wallet** (see the note at the top)
2. Open https://www.bcs.admin.ch/bcs-web
3. Fill in the form (name, date of birth, … — freely chosen, not an official document)
4. Scan the displayed QR code with the Sandbox Wallet → the Beta-ID is in the wallet

> **Beta-IDs get revoked.** They are Public-Beta test credentials, not documents with
> a long life. A Beta-ID that worked weeks ago can show up as revoked without any
> notice in the wallet — the wallet still presents it, and the verifier is the one
> that rejects it (`credential_revoked`, see Troubleshooting). If verification starts
> failing at the very last step, issue a fresh Beta-ID here and delete the old one
> from the wallet.

## Step 2: Verifier onboarding (once)

The verifier needs its own DID on the identifier registry of the Public Beta.
Reference: [cookbook "Onboarding base and trust registry"](https://swiyu-admin-ch.github.io/cookbooks/onboarding-base-and-trust-registry/)

**Important:** there is no "DID function" inside the ePortal itself — it is only the
authenticated entry point. The DID is created locally with the DID toolbox.

### 2a. Accounts & tokens

1. **ePortal** (https://eportal.admin.ch, AGOV or CH login): search for the service
   **"swiyu Trust Infrastructure"** and open it → leads to the swiyu portal.
2. **swiyu portal**: register a business partner (name + contact e-mail).
   Result: the `SWIYU_PARTNER_ID` (visible in the dashboard).
3. **API self-service portal** (https://selfservice.api.admin.ch/api-selfservice):
   subscribe to the **`swiyucorebusiness_identifier`** API. Result: `CUSTOMER_KEY`,
   `CUSTOMER_SECRET`, `BOOTSTRAP_REFRESH_TOKEN` and an `ACCESS_TOKEN`
   (**valid for ~24 h only** — renew via the refresh token afterwards).
   The second API (`swiyucorebusiness_status`) is only needed by issuers.

### 2b. Reserve a DID slot

```powershell
# returns id (= IDENTIFIER_REGISTRY_ID) and identifierRegistryUrl
Invoke-RestMethod -Method Post `
  -Uri "https://identifier-reg-api.trust-infra.swiyu-int.admin.ch/api/v1/identifier/business-entities/<SWIYU_PARTNER_ID>/identifier-entries" `
  -Headers @{ Authorization = "Bearer <ACCESS_TOKEN>" }
```

### 2c. Create the DID (locally)

Download the DID toolbox (Maven Central — the `latest` GitHub release has no assets!):

```powershell
Invoke-WebRequest "https://repo1.maven.org/maven2/ch/admin/swiyu/didtoolbox/2.2.1/didtoolbox-2.2.1-jar-with-dependencies.jar" -OutFile tools\didtoolbox.jar
java -jar tools\didtoolbox.jar create --identifier-registry-url "<identifierRegistryUrl from 2b>" | Out-File didlog.jsonl -Encoding utf8NoBOM
```

The toolbox creates three key pairs in `.didtoolbox/` (incl. `auth-key-01` =
EC P-256 key, PEM) and prints the DID log. The DID has the form
`did:webvh:<scid>:identifier-reg.trust-infra.swiyu-int.admin.ch:api:v1:did:<IDENTIFIER_REGISTRY_ID>`
(method `did:webvh`, the successor of `did:tdw`).

### 2d. Upload the DID log

```powershell
Invoke-RestMethod -Method Put `
  -Uri "https://identifier-reg-api.trust-infra.swiyu-int.admin.ch/api/v1/identifier/business-entities/<SWIYU_PARTNER_ID>/identifier-entries/<IDENTIFIER_REGISTRY_ID>" `
  -Headers @{ Authorization = "Bearer <ACCESS_TOKEN>" } `
  -ContentType "application/jsonl+json" -InFile didlog.jsonl
```

Success check (public, no token): `GET <identifierRegistryUrl>/did.jsonl` → HTTP 200.
(Note: `GET` on the management entry itself returns 404 — that is normal.)

### 2e. Note the values for `docker/.env`

- `VERIFIER_DID` — the DID from the DID log (`state.id`)
- `DID_VERIFICATION_METHOD` — DID + key fragment: `<DID>#auth-key-01`
- `SIGNING_KEY` — content of `.didtoolbox/auth-key-01` (PEM)

> Back up `.didtoolbox/` externally — it is the only place holding the private keys!

## Step 3: Public HTTPS URL

The swiyu app requires HTTPS with a **publicly trusted certificate**
(Let's Encrypt is fine, self-signed is rejected).

### Option A: Own domain + Caddy (this setup)

Prerequisites: DNS A record pointing to your public IP, router port forwarding
**TCP 443** to this machine, certificate + key as PEM files.

1. Install Caddy: `winget install CaddyServer.Caddy`
2. Copy `proxy/Caddyfile.example` to `proxy/Caddyfile` and replace the
   `<PLACEHOLDERS>` (hostname + certificate paths); the real file stays local
   via `.gitignore`. Peculiarities of this setup:
   - `auto_https disable_redirects` — Caddy opens no port-80 listener
     (another service already ran there; port 80 is not forwarded anyway)
   - Only `handle /oid4vp/*` is proxied, everything else → 404
     (protects the management API!)
3. Run:

   ```powershell
   caddy run --config proxy\Caddyfile
   ```

   > Caddy then runs only until logout/reboot — **no autostart configured**.
   > Start it manually after each reboot (or register it as a scheduled
   > task/service if needed).

4. Test from outside: `https://<domain>/oid4vp/api/openid-client-metadata.json`
   must return the verifier metadata; `/management/...` must return 404.

**Certificate renewal:** after renewal (Let's Encrypt, 90 days) replace the
PEM files at the configured path and restart Caddy.

### Option B: Tunnel (development only)

```powershell
cloudflared tunnel --url http://localhost:8083   # or: ngrok http 8083
```

Use the displayed URL as `EXTERNAL_URL`. Caution: the URL changes on every tunnel
start (→ adjust `.env`, restart the container, "New request"); additionally the
management API becomes public too — acceptable only for Public-Beta development.

## Step 4: Start the swiyu-verifier service

```powershell
cd docker
Copy-Item .env.example .env
# edit .env: EXTERNAL_URL, VERIFIER_DID, DID_VERIFICATION_METHOD, SIGNING_KEY
docker compose up -d
```

This yields the containers `miSWIYUverifier-verifier` and `miSWIYUverifier-postgres`,
both with `restart: unless-stopped` (they survive reboots as long as Docker Desktop
starts at sign-in: Settings → General → "Start Docker Desktop when you sign in").

Checks:
- `GET http://localhost:8083/actuator/health` → `UP`
- Swagger UI: http://localhost:8083/swagger-ui/index.html
- `GET http://localhost:8083/actuator/info` → must show **version 4.x**!

> **The image version is deliberately pinned to `4.2.0`.** Two moving targets make
> the pin necessary:
> - Up to **2.x** only the old `presentation_definition` format was understood;
>   DCQL requests were rejected with `"PresentationDefinition must be provided"`.
>   Since v3.0.0 only DCQL is supported.
> - **3.x** still emitted a draft-era authorization request with
>   `"client_id_scheme": "did"` and an unprefixed `client_id`. The swiyu wallet
>   meanwhile enforces **OID4VP 1.0**, where `client_id_scheme` no longer exists
>   and the scheme is a prefix of the `client_id`
>   (`decentralized_identifier:did:webvh:…`). Scanning the QR code against a 3.x
>   verifier fails immediately with **`invalid_request`**. Fixed in v4.0.0 via the
>   `client_id_prefix` property (default `decentralized_identifier`).

> The management API (port 8083) must **never** be publicly reachable —
> it is unprotected by default and hands out identity data.

## Step 5: Start miSWIYUverifier

```powershell
dotnet run --project src/miSWIYUverifier
```

The browser opens http://localhost:5070 with the QR code. Scan it with the swiyu
app and confirm the data sharing — the page displays the verified data.

> Port **5070**, not 5060: Chrome blocks 5060 as an "unsafe port"
> (SIP port, `ERR_UNSAFE_PORT`).

## Production deployment (VPS)

The live demo runs on a Linux VPS where a central **Caddy container** terminates TLS
for several projects. `docker/docker-compose.vps.yml` runs the full stack (web app
built from source + swiyu-verifier + PostgreSQL) **without publishing any host
ports** — the services that Caddy must reach join the external Caddy docker
network instead and are addressed by service name:

```bash
git clone https://github.com/Mibuw/miSWIYUverifier /opt/miswiyuverifier
# create docker/.env (EXTERNAL_URL=https://<your-domain>, DID, signing key)
cd /opt/miswiyuverifier/docker
docker compose -f docker-compose.vps.yml up -d --build
```

Caddy site block (TLS via Let's Encrypt is automatic once DNS points at the host):

```
<your-domain> {
    handle /oid4vp/*   { reverse_proxy miswiyu-verifier:8080 }
    handle /api/debug* { respond 404 }
    handle             { reverse_proxy miswiyu-webapp:5070 }
}
```

The `/api/debug*` block at the proxy matters: behind a reverse proxy the app sees
the proxy's IP instead of the caller's, so the app's built-in localhost check is
complemented by blocking the route at the edge. The management API is never
published at all — it is only reachable inside the docker network.

## Reusing this project

Two artefacts are published so you do not have to clone and build this repo:

| Artefact | What it is |
|---|---|
| [`miSWIYUverifier`](https://www.nuget.org/packages/miSWIYUverifier) | .NET library — build the DCQL query, create a verification, render the QR code, poll, flatten the claims |
| `ghcr.io/mibuw/miswiyuverifier` | The ready web app (QR page + REST API) as a container |

**Neither carries credentials.** The verifier DID and signing key belong to the
swiyu-verifier service and reach it as environment variables at run time; this image
only ever learns the management URL, and `.dockerignore` keeps `docker/`, `proxy/` and
`.didtoolbox/` out of the build context entirely.

**What you have to bring yourself**, because it cannot be shared:

1. **Your own verifier DID and signing key** (step 2). Request objects are signed with
   that private key, so someone else's DID is useless to you.
2. **A public HTTPS endpoint with a publicly trusted certificate** (step 3). The wallet
   fetches the request object from it and rejects self-signed certificates.
3. **The swiyu Sandbox Wallet** and a Beta-ID (step 1, and the note at the top).

### Run the whole stack from published images

```bash
# docker/.env holds EXTERNAL_URL, VERIFIER_DID, DID_VERIFICATION_METHOD, SIGNING_KEY
cd docker
docker compose -f docker-compose.published.yml up -d
```

That starts the web app, the swiyu-verifier and PostgreSQL, publishes the UI on
`:5070` and binds the management API to loopback only. Settings can be overridden with
the `SWIYU_` prefix and `__` for nesting, e.g.
`SWIYU_VerifierSettings__PurposeName`. Lists are the exception — an environment
variable can replace an entry but cannot shorten a list, so to change which claims are
requested, mount your own file over `/app/appsettings.json`.

### Use the library in your own app

```csharp
builder.Services.AddMiSWIYUverifier(builder.Configuration);

var verifier     = app.Services.GetRequiredService<VerifierApiService>();
var verification = await verifier.CreateVerificationAsync();
var qrPng        = QrCodeService.GeneratePng(verification.VerificationDeepLink!);

var result   = await verifier.WaitForVerificationAsync(verification.Id);
var identity = verifier.ExtractIdentityData(result);
```

The library does no cryptography of its own — SD-JWT parsing, issuer signatures,
holder binding and status list checks are all done by the swiyu-verifier service, so
you always run it alongside.

### Cutting a release

```bash
git tag v1.0.0 && git push origin v1.0.0
```

`.github/workflows/release.yml` then builds and pushes the image to ghcr.io (tagged
`1.0.0`, `1.0`, `1` and `latest`), packs the NuGet package, keeps it as a build
artefact and publishes it to nuget.org.

Publishing uses **[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)**,
so **no API key is stored anywhere**. The workflow requests a GitHub OIDC token,
`NuGet/login@v1` exchanges it at nuget.org for a key that is short-lived (about an
hour) and single-use, and nuget.org authorises the push by matching the token's claims
against the trusted publishing policy registered for the package. That is why the job
declares `id-token: write` — without it the token request fails silently and the login
step never gets a usable key.

Two things have to line up, neither of them secret:

1. On nuget.org, the package's **trusted publishing policy** must name this repository
   *and* the workflow file `release.yml`.
2. Your nuget.org account name must be available to the workflow:

   ```bash
   gh variable set NUGET_USER --repo Mibuw/miSWIYUverifier
   ```

Without `NUGET_USER` both steps are skipped, so a fork still builds the package and
keeps it as an artefact without publishing anything.

Publishing is the one irreversible step here: a version on nuget.org can be unlisted,
but never replaced or deleted. The container image has no such constraint — the next
tag simply overwrites `latest`.

## Configuration

`src/miSWIYUverifier/appsettings.json`, section `VerifierSettings`
(overridable via environment variables with the `SWIYU_` prefix, e.g.
`SWIYU_VerifierSettings__ManagementUrl`):

| Setting | Default | Description |
|---|---|---|
| `ManagementUrl` | `http://localhost:8083` | Management API of the swiyu-verifier |
| `PollIntervalSeconds` / `PollTimeoutSeconds` | 2 / 300 | Polling for the result |
| `VctValues` | `["betaid-sdjwt", "urn:vct:ch.admin.bcs.betaid"]` | Accepted credential types — keep **both**, the Beta-ID vct is mid-migration |
| `AcceptedIssuerDids` | Beta-ID issuer | Trusted issuers (mandatory!) |
| `RequestedClaims` | name, birth date, age_over_18, sex, nationality, birth_place, portrait | Requested Beta-ID attributes |
| `JwtSecuredAuthorizationRequest` | `true` | Signed request object (JAR) |
| `ResponseMode` | `direct_post.jwt` | Do not change: the swiyu wallet only accepts `direct_post.jwt` and `dc_api.jwt` |
| `PurposeName` / `PurposeDescription` / `PurposeScope` | demo texts | Purpose shown in the wallet (empty = omit) |

Available Beta-ID claims: `document_number`, `given_name`, `family_name`,
`birth_date`, `age_over_16`, `age_over_18`, `age_over_65`, `age_birth_year`,
`birth_place`, `place_of_origin`, `sex`, `nationality`, `portrait`,
`issuance_date`, `expiry_date`, and more.

## Web endpoints

### Demo UI

| Route | Description |
|---|---|
| `GET /` | Single-page UI (DE/EN via Accept-Language). The page creates its own session per visitor via the REST API and polls its status. |
| `GET /api/debug/{id}` | Session details incl. raw management-API response — reachable **from localhost only** |

### REST API (session based, any number of parallel verifications)

For integration into your own applications; sessions expire after 30 minutes
(the verifier deletes verifications after `VERIFICATION_TTL_SEC` = 900 s anyway).

| Route | Description |
|---|---|
| `POST /api/verification` | Start a new verification → `{ id, deepLink, qrCodeUrl, qrBase64 }` |
| `GET /api/verification/{id}/qrcode` | QR code as `image/png` (directly usable as `<img src>`) |
| `GET /api/verification/{id}/status` | `{ id, status, error }` — status: `waiting` \| `complete` \| `partial` \| `error` |
| `GET /api/verification/{id}/data` | Verified identity data: `200` + `{ id, status, data }` when done, `202` while `waiting`, `502` on error |
| `DELETE /api/verification/{id}` | Discard the session (`204`) |

Typical client flow:

```powershell
# 1. start a verification
$v = Invoke-RestMethod -Method Post http://localhost:5070/api/verification

# 2. show the QR code (e.g. <img src="http://localhost:5070$($v.qrCodeUrl)">)

# 3. poll the status until != waiting
Invoke-RestMethod "http://localhost:5070/api/verification/$($v.id)/status"

# 4. fetch the data
Invoke-RestMethod "http://localhost:5070/api/verification/$($v.id)/data"
# → { "id": "...", "status": "complete",
#     "data": { "givenName": "...", "familyName": "...", "birthDate": "...",
#               "ageOver18": "true", "sex": "1", "nationality": "CH",
#               "birthPlace": "...", "portrait": "data:image/jpeg;base64,...",
#               "additionalClaims": { } } }
```

> The REST API is unauthenticated and intended for internal-network use —
> do not expose port 5070 publicly for production use, or put your own
> auth layer in front of it.

## What broke and when

Everything in this table was a *silent* breaking change: nothing on this side was
touched, the demo simply stopped working. They are recorded here because the symptoms
are misleading — three of the five report an error that names the wrong culprit.

| Date | Symptom | Actual cause | Fix |
|---|---|---|---|
| 2026-08-31 | `invalid_request` on QR scan, wallet went silent | `ResponseMode` was plain `direct_post`; the wallet's `ResponseMode` enum only accepts `direct_post.jwt` and `dc_api.jwt` and decodes it non-optionally, so the whole request object failed to decode | `direct_post.jwt` |
| 2026-08-31 | same `invalid_request` | verifier 3.0.3 still sent the draft-era `client_id_scheme` plus an unprefixed `client_id`; OID4VP 1.0 makes the scheme a prefix | pin verifier **4.2.0** (`client_id_prefix`) |
| 2026-08-31 | same `invalid_request` | client metadata declared `jwt_vp` under the draft key `vp_formats`, while the query asked for `dc+sd-jwt` | `vp_formats_supported` with `dc+sd-jwt` |
| 2026-09-04 | wallet reports "no matching credential", verifier logs a clean `access_denied` | the Public Beta became the **Sandbox** and the regular swiyu Wallet turned production-only | use the **Sandbox Wallet** (see the note at the top) |
| 2026-09-10 | same "no matching credential" | the Beta-ID `vct` is migrating from `betaid-sdjwt` to `urn:vct:ch.admin.bcs.betaid`; a freshly issued credential carries the new value | list **both** in `VctValues` |
| 2026-09-10 | `credential_revoked` — "the presented credential was revoked" | **nothing was revoked.** The BCS status list token has no `exp`, which verifier 4.x requires by default; the failed precondition is reported as a revocation | `VERIFICATION_EXPIRY_MUST_BE_PRESENT: "false"` |

Two lessons that would have saved most of the time spent:

- **When pinning a new verifier major version, read `migration-guides/vX-to-vY.md`, not
  just the release notes.** The missing-`exp` setting is documented there, with BCS
  named as the affected issuer. Missing it turned a working demo into a "your
  credential is revoked" false alarm that cost several rounds of re-issuing Beta-IDs.
- **Read the wallet's source before theorising about it.** Both wallets are open
  source and the validation is a few dozen readable lines;
  `gh search code --owner swiyu-admin-ch <term>` found in one step what guessing from
  release notes had not. The `direct_post` and `vct` causes were both found that way.

The swiyu roadmap post
[`_posts/2026-05-19-roadmap-swiss-profiles.md`](https://github.com/swiyu-admin-ch/swiyu-admin-ch.github.io/blob/main/_posts/2026-05-19-roadmap-swiss-profiles.md)
announces these migration steps in advance and is worth watching. As of September 2026
its remaining item is **signed metadata** enforcement in both wallets — expect that to
be the next thing to break.

## Troubleshooting & pitfalls

- **`"PresentationDefinition must be provided"`** → a 2.x image is running.
  `docker compose pull && docker compose up -d`; check the version via `/actuator/info`.
  The image in the compose file is pinned to 4.2.0 (see above).
- **Wallet aborts right when scanning with `invalid_request`** → the authorization
  request is not OID4VP 1.0 compliant. Three causes, all fixed in this repo:
  1. `ResponseMode` is plain **`direct_post`**. The wallet's `ResponseMode` enum
     (`RequestObject.swift` in `swiyu-admin-ch/eidch-ios-wallet`) only has cases for
     `direct_post.jwt` and `dc_api.jwt`, and it decodes the field non-optionally —
     so `direct_post` makes the whole request object fail to decode before any of
     its contents are looked at. Use `direct_post.jwt` (encrypted response); the
     verifier then adds `jwks` and `encrypted_response_enc_values_supported` to the
     client metadata by itself, and decrypts the response before the web app sees it.
     Check this cause first: it is our own setting, so it survives verifier upgrades
     and looks identical to the two below.
  2. A **3.x image** is running, whose request still uses `client_id_scheme` instead
     of a prefixed `client_id`. Upgrade to 4.x (see above).
  3. The **client metadata** still uses the draft-era `vp_formats` key, or declares
     a format other than the one being requested. OID4VP 1.0 renamed the key to
     `vp_formats_supported`, and for the Beta-ID it must declare `dc+sd-jwt`
     (with `sd-jwt_alg_values` / `kb-jwt_alg_values`) — not `jwt_vp`. Check what is
     actually published: `https://<domain>/oid4vp/api/openid-client-metadata.json`.

  After any of these: `docker compose up -d` and click "New request" — an already
  generated QR code keeps the old request object. Note that `docker compose up -d`
  does **not** notice changes to an inline `configs:` block; for metadata changes use
  `docker compose up -d --force-recreate <verifier-service>`, otherwise the old
  metadata is served on silently.
- **Wallet says "no matching ID" / `access_denied` with no description** → the wallet
  answered properly but had nothing to present. Two causes, in order of likelihood:
  1. **The wrong wallet app.** Since 4 September 2026 the regular swiyu Wallet is
     production-only; this project needs the **swiyu Sandbox Wallet** (see the note at
     the top of this README). This is the one to check first — the server side looks
     completely healthy, the log shows `Successfully processed verification
     presentation`, and the only trace is `"error_code": "access_denied"` with a null
     description.
  2. **The `vct` does not match.** The Beta-ID is migrating its vct from
     `betaid-sdjwt` to `urn:vct:ch.admin.bcs.betaid`, so `VctValues` must list
     **both** — a credential issued before the switch carries the old value, one
     issued after it the new one. A freshly issued Beta-ID against a query that only
     knows `betaid-sdjwt` matches nothing.
  3. **A claim is missing from the credential.** DCQL requires *every* requested claim
     to be present; one missing claim makes the whole credential non-matching. The
     Beta-ID form lets fields be left empty, so a hastily issued Beta-ID may lack e.g.
     `portrait` or `birth_place`. To isolate it, cut `RequestedClaims` down to
     `given_name` and add entries back until the match breaks — if even `given_name`
     alone does not match, the cause is `vct` or format, not the claims.
- **`credential_revoked` / "Credential is not valid"** → **do not trust this message.**
  It does not mean the credential is revoked. `TokenStatusListVerifier.verifyStatus`
  returns `(valid=false, status=empty)` whenever its *preconditions* fail, and
  `SdJwtVpTokenVerifier` maps that to `CREDENTIAL_REVOKED` — the same error a real
  revocation produces. The comment in the swiyu source says as much: *"Something
  wrong with the status list or revoked"*.

  The usual cause here is the **missing `exp` on the BCS status list token**.
  swiyu-verifier 4.x requires an expiry by default; the BCS token has only `iss`,
  `sub`, `iat` and `status_list`. Both compose files therefore set
  `VERIFICATION_EXPIRY_MUST_BE_PRESENT: "false"`, which the
  [v3-to-v4 migration guide](https://github.com/swiyu-admin-ch/swiyu-verifier/blob/main/migration-guides/v3.x-to-v4.x.md)
  recommends for BCS by name. If that setting is lost, every Beta-ID starts
  reporting as revoked.

  To tell a real revocation from this, fetch the status list yourself — it is public
  and needs no credential:

  ```bash
  # the URI appears in the verifier log at DEBUG:
  #   LOGGING_LEVEL_CH_ADMIN_BJ_SWIYU_VERIFIER_SERVICE_STATUSLIST: DEBUG
  curl -s https://status-reg.trust-infra.swiyu-int.admin.ch/api/v1/statuslist/<id>.jwt
  ```

  Decode the payload: no `exp` present means the precondition above is the cause.
  A genuine revocation would additionally require the credential's own index to be
  set in the (zlib-compressed, base64url) `status_list.lst` bitmap.

  Only if the token *does* carry an `exp` and the bit really is set is the credential
  actually revoked — then issue a fresh Beta-ID (step 1) and delete the old one.
- **Verification SUCCESS but no data shown** → since v3, `credential_subject_data`
  is **grouped by DCQL credential id**:
  `{ "<credential-id>": [ { …claims… } ] }` instead of flat. The extraction in
  `VerifierApiService.ExtractIdentityData` descends recursively and supports both
  shapes. For diagnosis: `GET /api/debug/{id}` → `lastRawResponse`.
- **Browser shows `ERR_UNSAFE_PORT`** → Chrome blocks port 5060 (among others);
  that is why the app runs on 5070.
- **`Failed to create verification`** → is the container running?
  `docker ps`, if needed `docker compose -f docker/docker-compose.yml up -d`.
- **Wallet reports an error on scan** → is `EXTERNAL_URL` reachable from the phone?
  Is Caddy running (port 443)? Test from outside:
  `https://<domain>/oid4vp/api/openid-client-metadata.json`.
  After changing `EXTERNAL_URL`, restart the container and click "New request".
- **`issuer_not_accepted`** → the Beta-ID issuer DID in `AcceptedIssuerDids` does
  not match. Check the current value in the
  [Beta-ID cookbook](https://swiyu-admin-ch.github.io/cookbooks/how-to-use-beta-id/).
- **404 while polling / QR expired** → verifications expire after
  `VERIFICATION_TTL_SEC` (900 s); the app's polling timeout is 300 s.
  Click "New request".
- **Caddy fails to start: `listening on :80 … access denied`** → port 80 is in use
  elsewhere; that is why `auto_https disable_redirects` is set in the Caddyfile.
- **Not reachable after a Windows reboot** → start Caddy manually (no autostart
  configured); Docker Desktop must start at sign-in.
- **Access token expired (registry API)** → only relevant for onboarding/DID
  updates; renew via `BOOTSTRAP_REFRESH_TOKEN` or the API self-service portal.
  Normal operation needs no token.

### Diagnosing a wallet-side rejection

The wallet shows one generic message (`invalid_request`) for a long list of causes and
sends nothing back when it rejects a request object, so guessing is expensive. These
three steps localise the fault quickly, in this order.

**1. Ask the verifier what it recorded.** The management API holds the wallet's own
error code — this is what a "revoked", "expired" or "not accepted" case looks like from
the server side. It is not published through the reverse proxy, so query it from inside
the docker network:

```bash
docker run --rm --network <project>_internal curlimages/curl -s http://<verifier-service>:8080/management/api/verifications/<id>
# → {"state":"FAILED","wallet_response":{"error_code":"credential_revoked", …}}
```

Nothing recorded at all means the wallet never sent a response — the request object was
rejected locally, and step 2 applies.

**2. Find out whether the wallet even fetched the request object.** Enable the access
log on the reverse-proxy site block temporarily (in Caddy: add `log` inside the site
block, then `caddy reload`) and scan once. The wallet identifies itself as
**`swiyuWallet`**:

```
GET 200 /oid4vp/api/request-object/<id>    UA: swiyuWallet
```

A hit followed by silence means the request object was fetched and discarded during
validation — the fault is in its *content*. No hit at all means the deep link, DNS or
TLS is the problem, and the request object is irrelevant.

**3. Read the wallet's own rules.** Both wallets are open source and the validation is
short and readable — it is faster than inferring wallet behaviour from verifier release
notes, which is how the `direct_post` cause above stayed hidden through two upgrades:

```bash
REPO=swiyu-admin-ch/eidch-ios-wallet
SRC=Modules/Features/BITOpenID/Sources/BITOpenID/Domain
gh api "repos/$REPO/contents/$SRC/Validators/RequestObjectValidator.swift" --jq .content | base64 -d
```

`RequestObjectValidator.swift` lists every rejection condition, and
`Domain/Models/Presentation/RequestObject.swift` defines which fields are decoded
non-optionally and which enum values are accepted — a value outside those enums fails
the whole decode before any content is inspected. The Android wallet lives in
[`eidch-android-wallet`](https://github.com/swiyu-admin-ch/eidch-android-wallet); on
Android, `adb logcat | grep -i swiyu` prints the rejection reason directly.

Useful self-checks that need no wallet at all:

```bash
# request object: header, claims and Content-Type (must be application/oauth-authz-req+jwt)
curl -sD - https://<domain>/oid4vp/api/request-object/<id>
# published client metadata
curl -s https://<domain>/oid4vp/api/openid-client-metadata.json | jq
```

## Operations: what runs where?

| Component | Start | Survives reboot? |
|---|---|---|
| Caddy (443) | `caddy run --config proxy\Caddyfile` | ❌ start manually |
| Containers (8083, 5434) | `docker compose up -d` in `docker/` | ✅ (`unless-stopped`, if Docker Desktop autostarts) |
| Web app (5070) | `dotnet run --project src/miSWIYUverifier` | ❌ start manually |

## References

- swiyu-verifier: https://github.com/swiyu-admin-ch/swiyu-verifier
  (`documentation/verification_process.md`, `openapi.yaml`, `sample.compose.yml`)
- Cookbooks: https://swiyu-admin-ch.github.io/
- Beta Credential Service (Beta-ID): https://www.bcs.admin.ch/bcs-web
- DID toolbox: https://github.com/swiyu-admin-ch/didtoolbox-java
- ePortal: https://eportal.admin.ch · API self-service: https://selfservice.api.admin.ch/api-selfservice
- Sibling project for the EU: [miEUDIverifier](https://github.com/Mibuw/miEUDIverifier)

## License

[MIT](LICENSE) — © 2026 Wolfgang Mitterbucher
