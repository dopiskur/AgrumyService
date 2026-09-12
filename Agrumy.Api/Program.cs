using Agrumy.Api.Startup;
using Agrumy.Shared;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

AgrumySettings settingsForBootCheck = AgrumySettings.Bind(builder.Configuration);
builder.Services.AddSingleton(Options.Create(settingsForBootCheck));

// A first boot with no DB connection string routes to the minimal setup wizard instead of the rest of this file, until an admin supplies one - see Agrumy.Api/Setup/SetupWizard.cs.
if (string.IsNullOrWhiteSpace(settingsForBootCheck.DefaultConnection))
{
    Agrumy.Api.Setup.SetupWizard.ConfigureServices(builder);
    var wizardApp = builder.Build();
    Agrumy.Api.Setup.SetupWizard.LogSetupToken(wizardApp.Services.GetRequiredService<ILogger<Program>>());
    Agrumy.Api.Setup.SetupWizard.MapEndpoints(wizardApp);
    await wizardApp.RunAsync();
    return;
}

// One extension per concern (Agrumy.Api/Startup) - register a new service in the file that owns its domain, not here.
builder
    .AddAgrumyData()
    .AddAgrumyAuth()
    .AddAgrumyIntegrations()
    .AddAgrumyDomainServices()
    .AddAgrumyBackgroundWorkers()
    .AddAgrumyObservability()
    .AddAgrumyWebApi();

var app = builder.Build();

app.UseAgrumyPipeline();
await app.RunStartupDbCheckAsync();

app.Run();

namespace Agrumy.Api
{
    /// Marker type for Agrumy.Api.Tests' WebApplicationFactory - a dedicated type instead of the implicit top-level Program avoids a CS0433 clash with Agrumy.Web's own Program once both assemblies are referenced by the same test project.
    public sealed class ApiHostMarker { }
}
