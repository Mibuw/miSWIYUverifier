using miSWIYUverifier.Configuration;
using miSWIYUverifier.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace miSWIYUverifier;

/// <summary>
/// Extension-Methoden für eine einfache Integration in jeden ASP.NET Core
/// oder Generic-Host-Server.
///
/// Verwendung im eigenen Server:
/// <code>
/// builder.Services.AddMiSWIYUverifier(builder.Configuration);
///
/// // Dann per DI beziehen:
/// var verifier     = app.Services.GetRequiredService&lt;VerifierApiService&gt;();
/// var verification = await verifier.CreateVerificationAsync();
/// var qrPng        = QrCodeService.GeneratePng(verification.VerificationDeepLink!);
///
/// var result   = await verifier.WaitForVerificationAsync(verification.Id);
/// var identity = verifier.ExtractIdentityData(result);
/// // identity.GivenName, identity.FamilyName, identity.BirthDate, ...
/// </code>
///
/// appsettings.json:
/// <code>
/// {
///   "VerifierSettings": {
///     "ManagementUrl": "http://localhost:8083"
///   }
/// }
/// </code>
/// </summary>
public static class MiSWIYUverifierExtensions
{
    /// <summary>
    /// Registriert <see cref="VerifierApiService"/> und alle Abhängigkeiten im DI-Container.
    /// </summary>
    /// <param name="services">Der Service-Container.</param>
    /// <param name="configuration">Die Applikations-Konfiguration (wird nach <c>VerifierSettings</c> durchsucht).</param>
    /// <returns>Den Service-Container für Method-Chaining.</returns>
    public static IServiceCollection AddMiSWIYUverifier(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<VerifierSettings>(
            configuration.GetSection(VerifierSettings.SectionName));

        var settings = configuration
            .GetSection(VerifierSettings.SectionName)
            .Get<VerifierSettings>() ?? new VerifierSettings();

        services.AddHttpClient<VerifierApiService>(client =>
        {
            client.BaseAddress = new Uri(settings.ManagementUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}
