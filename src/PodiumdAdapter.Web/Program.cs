using Generated.Esuite.ContactmomentenClient;
using Generated.Esuite.KlantenClient;
using PodiumdAdapter.Web.Auth;
using PodiumdAdapter.Web.Endpoints;
using PodiumdAdapter.Web.Infrastructure;
using Serilog;
using Serilog.Events;

using var logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .WriteTo.Console()
    .CreateLogger();

try
{
    logger.Write(LogEventLevel.Information, "Starting web application");

    var builder = WebApplication.CreateBuilder(args);

    // Add services to the container.
    builder.Host.UseSerilog(logger);

    builder.Services.AddHealthChecks();
    builder.Services.AddHttpClient();
    builder.Services.AddTransient<ESuiteRequestAdapter>();
    builder.Services.AddTransient(s => new ContactmomentenClient(s.GetEsuiteRequestAdapter("ESUITE_CONTACTMOMENTEN_BASE_URL")));
    builder.Services.AddTransient(s => new KlantenClient(s.GetEsuiteRequestAdapter("ESUITE_KLANTEN_BASE_URL")));

    if (!builder.Environment.IsDevelopment())
    {
        builder.Services.AddAuth(builder.Configuration);
    }

    var app = builder.Build();
    // Configure the HTTP request pipeline.

    app.UseSerilogRequestLogging();

    app.Map(Contactmomenten.Api);
    app.Map(Klanten.Api);

    app.MapHealthChecks("/healthz").AllowAnonymous();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    logger.Write(LogEventLevel.Fatal, ex, "Application terminated unexpectedly");
}


public partial class Program { }
