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
    builder.Services.AddReverseProxy();
    builder.Services.AddEsuiteClient(new ContactmomentenClientConfig());
    builder.Services.AddEsuiteClient(new KlantenClientConfig());
    builder.Services.AddEsuiteClient(new ZrcClientConfig());
    builder.Services.AddEsuiteClient(new ZtcClientConfig());

    if (!builder.Environment.IsDevelopment())
    {
        builder.Services.AddAuth(builder.Configuration);
    }

    var app = builder.Build();
    // Configure the HTTP request pipeline.

    app.UseSerilogRequestLogging();

    app.MapHealthChecks("/healthz").AllowAnonymous();
    app.MapReverseProxy();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    logger.Write(LogEventLevel.Fatal, ex, "Application terminated unexpectedly");
}


public partial class Program { }
