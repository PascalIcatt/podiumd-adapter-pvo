using Generated.Esuite.ContactmomentenClient;
using Generated.Esuite.KlantenClient;
using PodiumdAdapter.Web.Auth;
using PodiumdAdapter.Web.Endpoints;
using PodiumdAdapter.Web.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateBootstrapLogger();

Log.Information("Starting up");

try
{
    // Add services to the container.
    builder.Host.UseSerilog((ctx, services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext());

    builder.Services.AddHealthChecks();
    builder.Services.AddHttpClient<ESuiteHttpClientRequestAdapter>();
    builder.Services.AddTransient(s => new ContactmomentenClient(s.GetRequiredService<ESuiteHttpClientRequestAdapter>()));
    builder.Services.AddTransient(s => new KlantenClient(s.GetRequiredService<ESuiteHttpClientRequestAdapter>()));
    builder.Services.AddAuth(builder.Configuration);

    var app = builder.Build();
    // Configure the HTTP request pipeline.

    app.UseSerilogRequestLogging();
    app.Use(next => request =>
    {
        return next(request);
    });

    app.Map(Contactmomenten.Api);
    app.Map(Klanten.Api);

    app.MapHealthChecks("/healthz").AllowAnonymous();

    app.Run();
}
catch (Exception ex) when (!ex.GetType().Name.Equals("StopTheHostException", StringComparison.Ordinal))
{
    Log.Fatal(ex, "Unhandled exception");
}
finally
{
    Log.Information("Shut down complete");
    Log.CloseAndFlush();
}

public partial class Program { }
