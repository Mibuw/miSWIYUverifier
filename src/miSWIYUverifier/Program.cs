using System.Diagnostics;
using miSWIYUverifier.Configuration;
using miSWIYUverifier.Services;
using miSWIYUverifier.WebServer;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// ── 1. WebApplication aufbauen ────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables("SWIYU_");
builder.Logging
    .ClearProviders()
    .AddConsole()
    .SetMinimumLevel(LogLevel.Warning)
    .AddFilter("miSWIYUverifier", LogLevel.Information);

var settings = builder.Configuration
    .GetSection(VerifierSettings.SectionName)
    .Get<VerifierSettings>() ?? new VerifierSettings();

builder.Services.Configure<VerifierSettings>(
    builder.Configuration.GetSection(VerifierSettings.SectionName));

builder.Services.AddHttpClient<VerifierApiService>(client =>
{
    client.BaseAddress = new Uri(settings.ManagementUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();
var verifier = app.Services.GetRequiredService<VerifierApiService>();

Console.WriteLine("\n  miSWIYUverifier – Starte ...");
Console.WriteLine($"  Management-API  : {settings.ManagementUrl}");

// ── 2. Session-Verwaltung ─────────────────────────────────────────────────────
// Jede Verification ist eine eigene Session (UI und REST-API nutzen denselben
// Store) — beliebig viele Besucher können parallel verifizieren.

var sessions = new VerificationSessionStore();

void StartSessionPolling(VerificationSession session)
{
    _ = Task.Run(async () =>
    {
        try
        {
            var result = await verifier.WaitForVerificationAsync(
                session.Id,
                onRawResponse: raw => session.LastRawResponse = raw,
                ct: app.Lifetime.ApplicationStopping);

            var identity = verifier.ExtractIdentityData(result);
            session.Identity = identity;
            session.Status   = identity.IsComplete ? "complete" : "partial";

            Console.WriteLine($"  [{session.Id[..8]}] Identitaet empfangen: {identity.GivenName} {identity.FamilyName}");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            session.Status       = "error";
            session.ErrorMessage = ex.Message;
            Console.WriteLine($"  [{session.Id[..8]}] Fehler: {ex.Message}");
        }
    });
}

async Task<VerificationSession> CreateSessionAsync(CancellationToken ct)
{
    var verification = await verifier.CreateVerificationAsync(ct);

    var session = new VerificationSession
    {
        Id       = verification.Id,
        DeepLink = verification.VerificationDeepLink!,
        QrPng    = QrCodeService.GeneratePng(verification.VerificationDeepLink!),
    };
    sessions.Add(session);
    StartSessionPolling(session);

    Console.WriteLine($"  [{session.Id[..8]}] Neue Verification");
    return session;
}

// ── 3. Web-Routen ─────────────────────────────────────────────────────────────

// UI: statische Seite; holt sich ihre Session selbst über die REST-API
app.MapGet("/", (HttpRequest request) =>
    Results.Content(
        HtmlPage.Render(request.Headers.AcceptLanguage.ToString()),
        "text/html; charset=utf-8"));

// REST-API (session-basiert):
// POST /api/verification              → neue Verification (id, deepLink, QR)
// GET  /api/verification/{id}/qrcode  → QR-Code als PNG
// GET  /api/verification/{id}/status  → waiting | complete | partial | error
// GET  /api/verification/{id}/data    → verifizierte Identitätsdaten
// DELETE /api/verification/{id}       → Session verwerfen

app.MapPost("/api/verification", async () =>
{
    try
    {
        var session = await CreateSessionAsync(app.Lifetime.ApplicationStopping);
        return Results.Json(new
        {
            id        = session.Id,
            deepLink  = session.DeepLink,
            qrCodeUrl = "/api/verification/" + session.Id + "/qrcode",
            qrBase64  = Convert.ToBase64String(session.QrPng),
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: 502);
    }
});

app.MapGet("/api/verification/{id}/qrcode", (string id) =>
    sessions.Get(id) is { } session
        ? Results.File(session.QrPng, "image/png")
        : Results.NotFound());

app.MapGet("/api/verification/{id}/status", (string id) =>
    sessions.Get(id) is { } session
        ? Results.Json(new { id = session.Id, status = session.Status, error = session.ErrorMessage })
        : Results.NotFound());

app.MapGet("/api/verification/{id}/data", (string id) =>
{
    var session = sessions.Get(id);
    if (session is null) return Results.NotFound();

    return session.Status switch
    {
        // 202: Wallet-Antwort steht noch aus — später erneut abfragen
        "waiting" => Results.Json(new { id = session.Id, status = session.Status },
                                  statusCode: 202),
        "error"   => Results.Json(new { id = session.Id, status = session.Status, error = session.ErrorMessage },
                                  statusCode: 502),
        _         => Results.Json(new { id = session.Id, status = session.Status, data = session.Identity }),
    };
});

app.MapDelete("/api/verification/{id}", (string id) =>
    sessions.Remove(id) ? Results.NoContent() : Results.NotFound());

// Debug-Endpunkt zeigt Identitaetsdaten + Roh-Response → nur von localhost erlaubt
// (die Seite selbst ist ueber Port-Forwarding oeffentlich erreichbar)
app.MapGet("/api/debug/{id}", (string id, HttpContext ctx) =>
{
    var remote = ctx.Connection.RemoteIpAddress;
    if (remote is null || !System.Net.IPAddress.IsLoopback(remote))
        return Results.NotFound();

    var session = sessions.Get(id);
    if (session is null) return Results.NotFound();

    return Results.Json(new
    {
        id              = session.Id,
        status          = session.Status,
        deepLink        = session.DeepLink,
        error           = session.ErrorMessage,
        lastRawResponse = session.LastRawResponse,
        identity        = session.Identity,
    });
});

// ── 4. Starten ────────────────────────────────────────────────────────────────
// Kestrel liest Endpoints + Zertifikat direkt aus appsettings.json
// (Kestrel:Endpoints übersteuert app.Urls – kein app.Urls.Add() nötig)

// LAN-IP ermitteln
var lanIp = System.Net.NetworkInformation.NetworkInterface
    .GetAllNetworkInterfaces()
    .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
             && n.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
    .SelectMany(n => n.GetIPProperties().UnicastAddresses)
    .Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
    .Select(a => a.Address.ToString())
    .FirstOrDefault();

// Fallback-Port 5070 — NICHT 5060: den blockiert Chrome als "unsafe port" (SIP)
var httpUrl  = builder.Configuration["Kestrel:Endpoints:Http:Url"]  ?? "http://0.0.0.0:5070";
var httpsUrl = builder.Configuration["Kestrel:Endpoints:Https:Url"];

var localHttp  = httpUrl.Replace("0.0.0.0",  "localhost");
var localHttps = httpsUrl?.Replace("0.0.0.0", "localhost");

Console.WriteLine($"  HTTP       : {localHttp}");
if (localHttps != null) Console.WriteLine($"  HTTPS      : {localHttps}");
if (lanIp != null)
{
    Console.WriteLine($"  LAN HTTP   : {httpUrl.Replace("0.0.0.0",  lanIp)}");
    if (httpsUrl != null)
        Console.WriteLine($"  LAN HTTPS  : {httpsUrl.Replace("0.0.0.0", lanIp)}");
}
Console.WriteLine($"  Beenden    : Strg+C\n");

// Browser-URL: lokale HTTPS-Adresse wenn HTTPS aktiv, sonst lokaler HTTP-Fallback
var browserUrl = localHttps ?? localHttp;
try { Process.Start(new ProcessStartInfo(browserUrl) { UseShellExecute = true }); }
catch { Console.WriteLine($"  Bitte {browserUrl} manuell im Browser oeffnen."); }

await app.RunAsync();
