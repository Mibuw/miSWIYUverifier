# miSWIYUverifier

[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE) [![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/) [![swiyu Public Beta](https://img.shields.io/badge/swiyu-Public%20Beta-d52b1e.svg)](https://swiyu-admin-ch.github.io/)

A C#/.NET 10 **ASP.NET Core web app** (Minimal API with a browser UI) that verifies the
**Swiss E-ID (swiyu Public Beta) Beta-ID** credential via the
[swiyu-verifier](https://github.com/swiyu-admin-ch/swiyu-verifier) management REST API
(OpenID4VP / DCQL) — the Swiss sibling of
[miEUDIverifier](https://github.com/Mibuw/miEUDIverifier).

The web page shows a QR code. The user scans it with the **swiyu app** (iOS/Android),
confirms the data sharing of their **Beta-ID**, and the page displays the verified
identity data: given name, family name, date of birth, over-18, sex, nationality,
place of birth and portrait photo.

Status: **tested end-to-end** with the real swiyu app + Beta-ID (July 2026).

## Try it (live demo)

A public test instance is available at **https://miswiyuverifier.mitterbucher.com/** —
open it, scan the QR code with your swiyu app and confirm.

> **No guarantee of availability** — this endpoint may be offline at any time.
> To run your own instance, see the [Quick start](#quick-start) below.

- You need the **swiyu app** with a **Beta-ID** (free, self-issued — see step 1 below).
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
| `src/miSWIYUverifier.Core` | Reusable library: `VerifierApiService`, models, `QrCodeService`, DI extension |
| `src/miSWIYUverifier` | Minimal-API web host with single-page UI (port **5070**) |
| `src/miSWIYUverifier.Core.Tests` | xUnit tests |
| `docker/` | docker-compose for swiyu-verifier + PostgreSQL (local dev) and `docker-compose.vps.yml` (full stack for production); `docker/.env` (not in the repo!) holds DID + signing key |
| `Dockerfile` | Web-app image, built locally on the deployment host (never pushed to a registry) |
| `proxy/Caddyfile.example` | Reverse-proxy template (TLS on 443 → `/oid4vp/*` → localhost:8083); the real `proxy/Caddyfile` is not in the repo |
| `tools/` | DID toolbox JAR (download see below, not in the repo) |
| `didlog.jsonl` | Backup of the uploaded DID log — created during onboarding, not in the repo |
| `.didtoolbox/` | **Private keys** of the verifier DID (not in the repo — back them up externally!) |

> **No Docker image of this project is built or published** because the deployment
> configuration contains real organisation data (registered verifier DID, partner
> registration, signing key) — even though it is "only" the swiyu **Public Beta**.
> If you reuse this project, run the onboarding (step 2) with your own DID and keys;
> all real values stay local in `docker/.env`, `proxy/Caddyfile` and `.didtoolbox/`
> (excluded via `.gitignore`).

## Prerequisites

1. **.NET 10 SDK**
2. **Docker Desktop**
3. **Java 21+** (only for the DID toolbox during onboarding, e.g. `winget install EclipseAdoptium.Temurin.21.JRE`)
4. **swiyu app** on your smartphone (App Store / Play Store: "swiyu")
5. A **Beta-ID** in the swiyu app (step 1)
6. A **registered verifier DID** on the swiyu identifier registry (step 2)
7. A **public HTTPS URL** (step 3 — own domain or tunnel)

---

## Quick start

Five steps from zero to a running verifier — issue a Beta-ID (1), onboard your
verifier DID (2), expose the OID4VP endpoint via HTTPS (3), start the
swiyu-verifier container (4) and run the web app (5).

## Step 1: Issue a Beta-ID (once, 5 minutes)

Anyone can issue themselves a free Beta-ID (pseudo identity for the Public Beta):

1. Install and set up the swiyu app
2. Open https://www.bcs.admin.ch/bcs-web
3. Fill in the form (name, date of birth, … — freely chosen, not an official document)
4. Scan the displayed QR code with the swiyu app → the Beta-ID is in the wallet

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

## Configuration

`src/miSWIYUverifier/appsettings.json`, section `VerifierSettings`
(overridable via environment variables with the `SWIYU_` prefix, e.g.
`SWIYU_VerifierSettings__ManagementUrl`):

| Setting | Default | Description |
|---|---|---|
| `ManagementUrl` | `http://localhost:8083` | Management API of the swiyu-verifier |
| `PollIntervalSeconds` / `PollTimeoutSeconds` | 2 / 300 | Polling for the result |
| `VctValues` | `["betaid-sdjwt"]` | Accepted credential types |
| `AcceptedIssuerDids` | Beta-ID issuer | Trusted issuers (mandatory!) |
| `RequestedClaims` | name, birth date, age_over_18, sex, nationality, birth_place, portrait | Requested Beta-ID attributes |
| `JwtSecuredAuthorizationRequest` | `true` | Signed request object (JAR) |
| `ResponseMode` | `direct_post` | or `direct_post.jwt` |
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

## Troubleshooting & pitfalls

- **`"PresentationDefinition must be provided"`** → a 2.x image is running.
  `docker compose pull && docker compose up -d`; check the version via `/actuator/info`.
  The image in the compose file is pinned to 4.2.0 (see above).
- **Wallet aborts right when scanning with `invalid_request`** → a 3.x image is
  running, whose authorization request still uses `client_id_scheme` instead of an
  OID4VP 1.0 prefixed `client_id`. Upgrade to 4.x (see above), then `docker compose
  up -d` and click "New request" — an already generated QR code keeps the old
  request object.
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
