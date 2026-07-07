using miSWIYUverifier.Configuration;
using miSWIYUverifier.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace miSWIYUverifier;

/// <summary>
/// Extension methods for easy integration into any ASP.NET Core
/// or generic-host server.
///
/// Usage in your own server:
/// <code>
/// builder.Services.AddMiSWIYUverifier(builder.Configuration);
///
/// // Then resolve via DI:
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
    /// Registers <see cref="VerifierApiService"/> and all dependencies in the DI container.
    /// </summary>
    /// <param name="services">The service container.</param>
    /// <param name="configuration">The application configuration (searched for <c>VerifierSettings</c>).</param>
    /// <returns>The service container for method chaining.</returns>
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
